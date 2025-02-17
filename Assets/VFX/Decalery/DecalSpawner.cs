#define USE_TERRAINS

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class DecalSpawner
{
	[System.Serializable]
	public struct ShaderReplacement
	{
		public Shader src;
		public Shader dest;
	}

	[System.Serializable]
	public class InitData
	{
		// Base material used by all spawned decals
		// Use shaders made specifically for decals (offsetting position to camera to prevent Z-fighting)
		public Material material;

		// Is this decal a trail (e.g. a tire track)?
		// Trails connect edge-to-edge instead of being separated quads.
		// Trails also have a unique continuous UV generation style. 
		public bool isTrail;

		// If isTrail is enabled, controls interval between trail edges (smaller interval = rounder trails).
		public float trailInterval = 0.1f;

		// If isTrail is enabled, controls vertical texture coordinate tiling. 
		public float trailVScale = 1.0f;

		// Should decals have tangents (does it need normal mapping)?
		public bool tangents = false;

		// Should these decals intherit MaterialPropertyBlocks from receivers?
		public bool inheritMaterialPropertyBlock = true;

		// [Advanced]
		// Should the decal always have visible normal mapping?
		// In practice, will disable lightmapping on receivers not having BAKERY_SH keyword enabled in their materials,
		// thus, falling back to the current BakeryVolume (if there is any).
		// I.e. the decal will either be lit by SH lightmaps or volumes.
		// Do not use in projects not using these features.
		public bool _forceBumped = false;

		// Use shader replacement feature? (see shaderReplacement)
		public bool useShaderReplacement = true;

		// Allows optionally overriding decal shader based on the receiver's shader.
		// Can be used for e.g. special surfaces with vertex deformation.
		// If receiver has shader "src", decal will use shader "dest".
		public ShaderReplacement[] shaderReplacement;

		public bool inheritMaterialProperties = false;
		public string[] materialPropertyNamesVector;
	}

	public InitData initData;

	int[] materialPropertyIDsVector;

	// Should the decal geometry be generated on the CPU or the GPU?
	// Note that for the CPU mode to work, "Read/Write enabled" must be turned on on the receiving model assets.
	DecalUtils.Mode mode = DecalUtils.Mode.CPU;

	// Are these decals supposed to be rendered with DrawProceduralIndirect?
	// Such decals don't need VRAM->RAM->VRAM memory transfers, as the generated buffer used directly by the drawing shader.
	// Shader must be aware of this method.
	bool indirectDraw;

	// Shader pass used by indirect-drawing shaders
	int shaderPass = 0;

#if UNITY_EDITOR
#if UNITY_2018_1_OR_NEWER
	static bool eventAdded = false;
#endif
#endif

	struct QueuedDecal
	{
		public Vector3 position;
		public Quaternion rotation;
		public GameObject hitObject;
		public float decalSizeX, decalSizeY, distance, opacity, angleClip;
		public Transform rootObject;

		public QueuedDecal(Vector3 _position, Quaternion _rotation, GameObject _hitObject, float _decalSizeX, float _decalSizeY, float _distance, float _opacity, float _angleClip, Transform _rootObject)
		{
			position = _position;
			rotation = _rotation;
			hitObject = _hitObject;
			decalSizeX = _decalSizeX;
			decalSizeY = _decalSizeY;
			distance = _distance;
			opacity = _opacity;
			angleClip = _angleClip;
			rootObject = _rootObject;
		}
	}

	Queue<QueuedDecal> queue;

	struct HashedShader
	{
		public Shader shader;
		public bool isSkinned, isSH;
		public Material material;
	}

	Dictionary<Shader, Shader> shaderReplacementMap = new Dictionary<Shader, Shader>(); // maps receiver shader to modified decal shader
	Dictionary<HashedShader, Material> shaderReplacementMapExt = new Dictionary<HashedShader, Material>(); // maps a combination of receiver shader and keywords to a material
	HashedShader hashed;

	public Dictionary<int, DecalUtils.Group> staticGroups; // indexed by lightmapID
	public Dictionary<Transform, DecalUtils.Group> movableGroups; // indexed by movable

	DecalUtils.GroupDesc initDesc;
	DecalUtils.DecalDesc desc;

	Vector3 prevPos;//, prevDir; // used for trails
	DecalUtils.Group lastGroup; // needed for trail interruption

	public static List<DecalSpawner> All = new List<DecalSpawner>();
	bool init = false;

#if UNITY_EDITOR
#if UNITY_2018_1_OR_NEWER
    static void OnBeforeAssemblyReload()
    {
    	foreach(var spawner in All)
    	{
	    	if (spawner.staticGroups != null)
	    	{
				foreach(var pair in spawner.staticGroups)
				{
					DecalUtils.ReleaseDecals(pair.Value, false);
				}
			}
			if (spawner.movableGroups != null)
			{
				foreach(var pair in spawner.movableGroups)
				{
					DecalUtils.ReleaseDecals(pair.Value, false);
				}
			}
		}
    }
#endif
#endif

	// Initialize the spawner
	public void Init(int maxTrisTotal, int maxTrisInDecal, DecalUtils.Mode preferredMode, bool preferDrawIndirect, int preferredShaderPass)
	{
		if (init) return;

		All.Add(this);

		// Fill shader replacement map
		if (initData.shaderReplacement != null)
		{
			for(int i=0; i<initData.shaderReplacement.Length; i++)
			{
				shaderReplacementMap[initData.shaderReplacement[i].src] = initData.shaderReplacement[i].dest;
			}
		}
		if (initData.inheritMaterialProperties)
		{
			var numProps = initData.materialPropertyNamesVector.Length;
			materialPropertyIDsVector = new int[numProps];
			for(int i=0; i<numProps; i++)
			{
				materialPropertyIDsVector[i] = Shader.PropertyToID(initData.materialPropertyNamesVector[i]);
			}
		}

		// Fill base group init data
		initDesc.SetDefaults();
		initDesc.material = initData.material;
		initDesc.maxTrisInDecal = maxTrisTotal;
		initDesc.maxTrisTotal = maxTrisTotal;
		initDesc.indirectDraw = preferDrawIndirect;
		initDesc.isTrail = initData.isTrail;
		initDesc.trailVScale = initData.trailVScale;
		initDesc.tangents = initData.tangents;

		mode = preferredMode;
		indirectDraw = preferDrawIndirect;
		shaderPass = preferredShaderPass;

		// Fill base group decal data
		desc.SetDefaults();

		// Init group maps
		if (staticGroups == null) staticGroups = new Dictionary<int, DecalUtils.Group>();
		if (movableGroups == null) movableGroups = new Dictionary<Transform, DecalUtils.Group>();

		init = true;
	}

	// Gets or creates the decal group material based on receiver material
	Material GetMaterial(Material src, bool isSkinned, out bool isSH)
	{
		isSH = false;
		if (!initData.useShaderReplacement)
		{
			isSkinned = false;
			return initData.material;
		}

		hashed.shader = src.shader;
		hashed.isSkinned = isSkinned;
		hashed.material = initData.inheritMaterialProperties ? src : null;
#if BAKERY_INCLUDED
		hashed.isSH = isSH = src.IsKeywordEnabled("BAKERY_SH");
#endif

		Material dest;
		if (shaderReplacementMapExt.TryGetValue(hashed, out dest))
		{
			return dest;
		}
		else
		{
			dest = UnityEngine.Object.Instantiate(initData.material);
			Shader destShader = null;
			if (shaderReplacementMap.TryGetValue(src.shader, out destShader)) dest.shader = destShader;
			if (hashed.isSkinned)
			{
				dest.DisableKeyword("INDIRECT_DRAW");
			}
			else
			{
				if (indirectDraw)
				{
					dest.EnableKeyword("INDIRECT_DRAW");
				}
				else
				{
					dest.DisableKeyword("INDIRECT_DRAW");
				}
			}
			if (initData.inheritMaterialProperties)
			{
				int numProps = materialPropertyIDsVector.Length;
				for(int i=0; i<numProps; i++)
				{
					dest.SetVector(materialPropertyIDsVector[i], src.GetVector(materialPropertyIDsVector[i]));
					Debug.LogError(src.GetVector(materialPropertyIDsVector[i]));
				}
			}
#if BAKERY_INCLUDED
			if (hashed.isSH)
			{
				dest.EnableKeyword("BAKERY_SH");
			}
			else
			{
				dest.DisableKeyword("BAKERY_SH");
			}
#endif
			shaderReplacementMapExt[hashed] = dest;
			return dest;
		}
	}

	MaterialPropertyBlock GetPropertyBlock(Renderer mr)
	{
		var mpb = new MaterialPropertyBlock();
		mr.GetPropertyBlock(mpb);
		return mpb;
	}

#if USE_TERRAINS
	DecalUtils.Group CreateNewGroup(Terrain terrain)
	{
		initDesc.lightmapID = terrain.lightmapIndex;
		initDesc.realtimeLightmapID = terrain.realtimeLightmapIndex;
		initDesc.realtimeLightmapScaleOffset = terrain.realtimeLightmapScaleOffset;
		bool isSH;
		initDesc.material = GetMaterial(terrain.materialTemplate, false, out isSH);
		if (!isSH && initData._forceBumped) initDesc.lightmapID = -1;
		initDesc.materialPropertyBlock = null;

#if UNITY_EDITOR
#if UNITY_2018_1_OR_NEWER
		if (!eventAdded)
		{
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            eventAdded = true;
        }
#endif
#endif
		var grp = DecalUtils.CreateGroup(initDesc, mode);
		if (indirectDraw) GPUDecalUtils.CreateDrawIndirectCommandBuffer(grp, shaderPass);
		return grp;
	}
#endif

	DecalUtils.Group CreateNewGroup(Renderer mr, bool isSkinned = false)
	{
		initDesc.lightmapID = mr.lightmapIndex;
		initDesc.realtimeLightmapID = mr.realtimeLightmapIndex;
		initDesc.realtimeLightmapScaleOffset = mr.realtimeLightmapScaleOffset;
		bool isSH;
		initDesc.material = GetMaterial(mr.sharedMaterial, isSkinned, out isSH);
		if (!isSH && initData._forceBumped) initDesc.lightmapID = -1;
		initDesc.materialPropertyBlock = null;

#if UNITY_2018_1_OR_NEWER
		if (initData.inheritMaterialPropertyBlock && mr.HasPropertyBlock()) initDesc.materialPropertyBlock = GetPropertyBlock(mr);
#else
		if (initData.inheritMaterialPropertyBlock) initDesc.materialPropertyBlock = GetPropertyBlock(mr);
#endif

#if UNITY_EDITOR
#if UNITY_2018_1_OR_NEWER
		if (!eventAdded)
		{
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            eventAdded = true;
        }
#endif
#endif

		if (isSkinned)
		{
			initDesc.indirectDraw = false;
			var grp = DecalUtils.CreateGroup(initDesc, DecalUtils.Mode.CPU);
			initDesc.indirectDraw = indirectDraw;
			return grp;
		}
		else
		{
			var grp = DecalUtils.CreateGroup(initDesc, mode);
			if (indirectDraw) GPUDecalUtils.CreateDrawIndirectCommandBuffer(grp, shaderPass);
			return grp;
		}
	}

#if USE_TERRAINS
	void Process(Vector3 position, Quaternion rotation, float decalSizeX, float decalSizeY, float distance, float opacity, float angleClip, Renderer mr, Terrain terrain, GameObject hitObject, Transform rootObject, bool staticBatch)// GameObject receiver)
#else
	void Process(Vector3 position, Quaternion rotation, float decalSizeX, float decalSizeY, float distance, float opacity, float angleClip, Renderer mr, GameObject hitObject, Transform rootObject, bool staticBatch)// GameObject receiver)
#endif
	{
		// Get mesh
		Mesh mesh = null;
		SkinnedMeshRenderer smr = null;
#if USE_TERRAINS
		bool isTerrain = terrain != null;
		if (!isTerrain)
#endif
		{
			smr = mr as SkinnedMeshRenderer;
			if (smr != null)
			{
				mesh = smr.sharedMesh;
				rootObject = smr.transform;
			}
			else
			{
				var mf = mr.GetComponent<MeshFilter>();
				if (mf == null) return;
				mesh = mf.sharedMesh;
			}
			if (mesh == null) return;
		}

		// Get or create group
		DecalUtils.Group grp = null;
		if (rootObject != null)
		{
			if (!movableGroups.TryGetValue(rootObject, out grp))
			{
#if USE_TERRAINS
				if (isTerrain)
				{
					initDesc.parent = null;
					grp = movableGroups[rootObject] = CreateNewGroup(terrain);
					if (indirectDraw) GPUDecalUtils.UpdateDrawIndirectCommandBuffer(grp, rootObject.localToWorldMatrix, shaderPass);
				}
				else
#endif
				{
					initDesc.parent = mr;
					grp = movableGroups[rootObject] = CreateNewGroup(mr, smr != null);
					if (indirectDraw && smr == null) GPUDecalUtils.UpdateDrawIndirectCommandBuffer(grp, rootObject.localToWorldMatrix, shaderPass);
				}
			}
		}
		else
		{
			int lightmapIndex;
#if USE_TERRAINS
			if (isTerrain)
			{
				lightmapIndex = terrain.lightmapIndex;
			}
			else
#endif
			{
				lightmapIndex = mr.lightmapIndex;
			}

			if (!staticGroups.TryGetValue(lightmapIndex, out grp))
			{
				initDesc.parent = null;
#if USE_TERRAINS
				if (isTerrain)
				{
					grp = staticGroups[lightmapIndex] = CreateNewGroup(terrain);
				}
				else
#endif
				{
					grp = staticGroups[lightmapIndex] = CreateNewGroup(mr);
				}
			}
		}

		// Fill new decal data
		desc.mesh = mesh;
		desc.worldMatrix = (staticBatch || isTerrain) ? Matrix4x4.identity : mr.transform.localToWorldMatrix;
#if USE_TERRAINS
		desc.lightmapScaleOffset = staticBatch ? new Vector4(1,1,0,0) : (isTerrain ? terrain.lightmapScaleOffset : mr.lightmapScaleOffset);
#else
		desc.lightmapScaleOffset = staticBatch ? new Vector4(1,1,0,0) : mr.lightmapScaleOffset;
#endif
		desc.position = position;
		desc.rotation = rotation;
		desc.sizeX = decalSizeX;
		desc.sizeY = decalSizeY;
		desc.distance = distance;
		desc.angleClip = angleClip;
		desc.opacity = opacity;

		// interrupt trail
		if (initData.isTrail && lastGroup != grp)
		{
			desc.opacity = 0;
		}

		// Transform to world-positioned bind pose
		if (smr != null)
		{
			var trs = Matrix4x4.TRS(position, rotation, Vector3.one);
			var bone = hitObject.transform;
			var bones = smr.bones;
			int boneIndex = System.Array.IndexOf(bones, bone);
			if (boneIndex >= 0)
			{
				var localToWorld = smr.transform.localToWorldMatrix;
				var bindPoses = smr.sharedMesh.bindposes;
				var bindPose = bindPoses[boneIndex];
				var worldToLocalBone = bone.worldToLocalMatrix;

				var xform = localToWorld * (bindPose.inverse * worldToLocalBone);
				var newTform = xform * trs;//transform.localToWorldMatrix;
				desc.position = newTform.GetColumn(3);
#if UNITY_2017_1_OR_NEWER
				desc.rotation = newTform.rotation;
#else
				Vector3 fwd = newTform.GetColumn(2);
				Vector3 up = newTform.GetColumn(1);
				desc.rotation = Quaternion.LookRotation(fwd.normalized, up.normalized);
#endif
			}
		}

#if USE_TERRAINS
		if (isTerrain)
		{
			DecalUtils.AddDecal(grp, desc, terrain.gameObject, mode);
		}
		else
#endif
		{
			DecalUtils.AddDecal(grp, desc, mr.gameObject, smr != null ? DecalUtils.Mode.CPU : mode);
		}
		lastGroup = grp;
	}

	// Spawn a decal on hitObject from a projector placed at position in the direction of rotation.
	// If rootObject is set, decals will move together with the object.
	public void AddDecal(Vector3 position, Quaternion rotation, GameObject hitObject, float decalSizeX, float decalSizeY, float distance, float opacity, float angleClip, Transform rootObject)
	{
		if (initData.isTrail)
		{
			var curPos = position;
			if ((prevPos - curPos).sqrMagnitude < initData.trailInterval * initData.trailInterval) return;
			var curDir = (curPos - prevPos).normalized;;
			rotation = Quaternion.LookRotation(rotation * Vector3.forward, curDir);
			//float alphaFactor = Mathf.Clamp(Vector3.Dot(curDir, prevDir), 0.0f, 1.0f);
			//alphaFactor = Mathf.Pow(alphaFactor, 64.0f);
			//opacity *= alphaFactor;
			prevPos = curPos;
			//prevDir = curDir;
		}

		Renderer mr = null;
#if USE_TERRAINS
		Terrain terrain = null;
#endif
		//GameObject receiver = null;

		var meshRef = hitObject.GetComponent<DecalMeshRef>();
		if (meshRef != null)
		{
			var mrs = meshRef.renderers;
			if (mrs == null) return;
			for(int i=0; i<mrs.Length; i++)
			{
				mr = mrs[i];
				//receiver = mr.gameObject;
			}
		}
		else
		{
			mr = hitObject.GetComponent<Renderer>();
			if (mr == null)
			{
				terrain = hitObject.GetComponent<Terrain>();
				if (terrain == null)
				{
					return;
				}
			}
			//receiver = hitObject;
		}

#if USE_TERRAINS
		Process(position, rotation, decalSizeX, decalSizeY, distance, opacity, angleClip, mr, terrain, hitObject, rootObject, mr != null ? mr.isPartOfStaticBatch : false);//hit, mr, receiver);
#else
		Process(position, rotation, decalSizeX, decalSizeY, distance, opacity, angleClip, mr, hitObject, rootObject, mr.isPartOfStaticBatch);//hit, mr, receiver);
#endif

	}

	public void AddDecalToQueue(Vector3 position, Quaternion rotation, GameObject hitObject, float decalSizeX, float decalSizeY, float distance, float opacity, float angleClip, Transform rootObject)
	{
 		if (queue == null) queue = new Queue<QueuedDecal>();
 		queue.Enqueue(new QueuedDecal(position, rotation, hitObject, decalSizeX, decalSizeY, distance, opacity, angleClip, rootObject));
	}

	public void UpdateQueue(int maxDecalsPerFrame)
	{
		if (queue == null) return;
		if (queue.Count == 0) return;
		int limit = System.Math.Min(maxDecalsPerFrame, queue.Count);
		for(int i=0; i<limit; i++)
		{
			var d = queue.Dequeue();
			AddDecal(d.position, d.rotation, d.hitObject, d.decalSizeX, d.decalSizeY, d.distance, d.opacity, d.angleClip, d.rootObject);
		}
	}

	void OnDrawGizmosSelected()
	{

		if (staticGroups == null) return;
		Gizmos.color = Color.green;
		foreach(var pair in staticGroups)
		{
			Gizmos.DrawWireCube(pair.Value.bounds.center, pair.Value.bounds.size);
		}
		foreach(var pair in movableGroups)
		{
			Gizmos.DrawWireCube(pair.Value.parent.bounds.center, pair.Value.parent.bounds.size);
		}
		Gizmos.color = Color.red;
		foreach(var pair in staticGroups)
		{
			Gizmos.DrawLine(pair.Value.prevDecalEdgeA, pair.Value.prevDecalEdgeB);
		}
	}

	public void Release()
	{
		foreach(var pair in staticGroups)
		{
			DecalUtils.ReleaseDecals(pair.Value);
		}
		foreach(var pair in movableGroups)
		{
			DecalUtils.ReleaseDecals(pair.Value);
		}
	}

	public void Clear()
	{
		foreach(var pair in staticGroups)
		{
			DecalUtils.ClearDecals(pair.Value, mode);
		}
		foreach(var pair in movableGroups)
		{
			DecalUtils.ClearDecals(pair.Value, mode);
		}
	}

	public static void Cleanup()
	{
		foreach(var s in All)
		{
			if (s == null) continue;
			s.Release();
		}

		All = new List<DecalSpawner>();
	}
}
