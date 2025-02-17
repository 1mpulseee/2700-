#define USE_TERRAINS

#if UNITY_2018_1_OR_NEWER
#if USE_BURST
#if USE_NEWMATHS
#define USE_BURST_REALLY
#endif
#endif
#endif

using UnityEngine;
using UnityEngine.Rendering;
using System.Collections;
using System.Collections.Generic;
#if UNITY_2018_1_OR_NEWER
using Unity.Collections;
#endif

#if USE_BURST_REALLY
using Unity.Mathematics;
#endif

public class DecalUtils
{
	public struct GroupDesc
	{
		// Material used by all decals in the group
		// Use shaders made specifically for decals (offsetting position to camera to prevent Z-fighting)
		public Material material;

		// Maximum amount of triangles in the group.
		// Decal triangle count depends on receiver geometry detail and decal size.
		// Older decals will disappear when new decals are added above the limit.
		public int maxTrisTotal;

		// Maximum allowed triangle count for one decal.
		// Decal triangle count depends on receiver geometry detail and decal size.
		// While this number can be set to totalTris, using a realistic limit for your game will reduce processing time.
		// If a decal crosses this threshold, some of its triangles may disappear.
		public int maxTrisInDecal;

		// If decals need to be a part of a movable object, assign it here.
		public Renderer parent;

		// Lightmap ID used by decals. Normally obtained from gameObject.GetComponent<Renderer>().lightmapIndex.
		public int lightmapID, realtimeLightmapID;

		// Is this group supposed to be rendered with DrawProceduralIndirect?
		// Such groups don't need VRAM->RAM->VRAM memory transfers, as the generated buffer used directly by the drawing shader.
		// Shader must be aware of this method.
		public bool indirectDraw;

		// Is this decal a trail (e.g. a tire track)?
		// Trails connect edge-to-edge instead of being separated quads.
		// Trails also have a unique continuous UV generation style. 
		public bool isTrail;

		// If isTrail is enabled, controls vertical texture coordinate tiling. 
		public float trailVScale;

        // Should this group have tangents (does it need normal mapping)?
        public bool tangents;

        public Vector4 realtimeLightmapScaleOffset;

        public MaterialPropertyBlock materialPropertyBlock;

		//
		public void SetDefaults()
		{
			lightmapID = -1;
			trailVScale = 1.0f;
		}
	}

	public struct DecalDesc
	{
		// Decal projector origin.
		public Vector3 position;

		// Decal projector rotation.
		public Quaternion rotation;

		// Decal width
		public float sizeX;

		// Decal height
		public float sizeY;

		// Projection distance
		public float distance;

		// At which angle should the decal polygons be removed?
		// Range is from -1 (facing away) to 1 (facing towards)
		public float angleClip;

        // Opacity multiplier for the decal.
        public float opacity;

		// Receiving object's mesh.
		public Mesh mesh;

		// Receivng object's matrix.
		public Matrix4x4 worldMatrix;

		// Receiving object's lightmap scaling/offset. Normally obtained from gameObject.GetComponent<Renderer>().lightmapScaleOffset.
		public Vector4 lightmapScaleOffset;

        //
        public void SetDefaults()
        {
            opacity = 1.0f;
        }
	}

    public class Group
    {
        public Mode mode;
        public ComputeBuffer vbuffer;
        public ComputeBuffer countBuffer, argBuffer;
        public Material material;
        public Renderer parent;
        public Transform parentTform;
        public GameObject go;
        public DecalGroup decalGroup;
        public Mesh mesh;
        public CommandBuffer drawCmd;
        public Bounds bounds, nextBounds;
        public Vector3 prevDecalEdgeA, prevDecalEdgeB;
        public Vector4 prevDecalEdgePlane;
        public int numDecals, maxTrisInDecal, totalTris, boundsCounter, boundsMinDecalCounter;
        public int lightmapID;
        public int decalIDCounter;
        public bool indirectDraw;
        public bool isTrail;
        public float trailV;
        public float trailVScale;
        public bool tangents;
        public bool isSkinned;
        public MaterialPropertyBlock materialPropertyBlock;

        public int fixedOffset;//V, fixedOffsetI;
        public Vector3[] vPos, vNormal;
        public Vector2[] vUV, vUV2;
        public Color[] vColor;
        public BoneWeight[] vSkin;
        public Vector4[] vTangents;
        public byte[] vDecalID;

#if UNITY_2018_1_OR_NEWER
        public NativeArray<Vector3> nvPos;
        public NativeArray<Vector3> nvNormal;
        public NativeArray<Color> nvColor;
        public NativeArray<Vector2> nvUV2;
        public NativeArray<Vector2> nvUV;
        public NativeArray<BoneWeight> nvSkin;
        public NativeArray<byte> nvDecalID;
        public NativeArray<Vector4> nvTangents;
#endif

#if UNITY_2019_3_OR_NEWER
        public GPUDecalUtils.ReadableVertex[] buff;
        public GPUDecalUtils.ReadableVertexTangents[] buffWithTangents;
#endif
    }

    public enum Mode
    {
        CPU,
        GPU,
        CPUBurst
    }

    public static DecalUtils.Group CreateGroup(DecalUtils.GroupDesc desc, Mode mode)
    {
        if (mode == Mode.GPU)
        {
            return GPUDecalUtils.CreateGroup(desc);
        }
        else if (mode == Mode.CPUBurst)
        {
            return CPUBurstDecalUtils.CreateGroup(desc);
        }
        else
        {
            return CPUDecalUtils.CreateGroup(desc);
        }
    }

    public static void AddDecal(DecalUtils.Group group, DecalUtils.DecalDesc desc, GameObject receiver, Mode mode)
    {
        if (mode == Mode.GPU)
        {
            GPUDecalUtils.AddDecal(group, desc, receiver);
        }
        else if (mode == Mode.CPUBurst)
        {
            CPUBurstDecalUtils.AddDecal(group, desc, receiver);
        }
        else
        {
            CPUDecalUtils.AddDecal(group, desc, receiver);
        }
    }

    public static void ClearDecals(DecalUtils.Group group, Mode mode)
    {
        if (mode == Mode.GPU)
        {
            GPUDecalUtils.ClearDecals(group);
        }
        else if (mode == Mode.CPUBurst)
        {
            CPUBurstDecalUtils.ClearDecals(group);
        }
        else
        {
            CPUDecalUtils.ClearDecals(group);
        }
    }

    static Vector3 Vector3Abs(Vector3 v)
    {
        return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }

    internal static void ExpandBounds(Group group, Matrix4x4 decalMatrix, DecalDesc desc)
    {
        Vector3 row0 = decalMatrix.GetColumn(0);
        Vector3 row1 = decalMatrix.GetColumn(1);
        Vector3 row2 = decalMatrix.GetColumn(2);
        var center = desc.position + row2 * desc.distance * 0.5f;
        row0 = Vector3Abs(row0);
        row1 = Vector3Abs(row1);
        row2 = Vector3Abs(row2);
        var minPoint = center - row0 - row1 - row2;
        var maxPoint = center + row0 + row1 + row2;

        var bounds = group.bounds;
        bounds.SetMinMax(minPoint, maxPoint);

        if (group.numDecals == 0)
        {
            group.bounds = bounds;
        }
        else
        {
            group.bounds.Encapsulate(bounds);
        }

        if(group.boundsMinDecalCounter > group.totalTris) // each decal is minimum 1 tri (actual count is unknown / on the GPU) ..... can be 0 actually .... but let's keep it for now
        {
            if (group.boundsCounter == 0)
            {
                group.nextBounds = bounds;
                group.boundsCounter++;
            }
            else
            {
                group.nextBounds.Encapsulate(bounds);
            }

            if (group.boundsMinDecalCounter > group.totalTris * 2)
            {
                group.bounds = group.nextBounds;
                group.boundsCounter = 0;
                group.boundsMinDecalCounter = 0;
            }
        }
        group.boundsMinDecalCounter++;
    }

    public static void ReleaseDecals(DecalUtils.Group group, bool affectScene = true)
    {
        if (group == null) return;
        if (group.vbuffer != null)
        {
            group.vbuffer.Release();
            group.vbuffer = null;
        }
        group.totalTris = 0;
        group.maxTrisInDecal = 0;
        if (group.argBuffer != null)
        {
            group.argBuffer.Release();
            group.argBuffer = null;
        }
        if (group.countBuffer != null)
        {
            group.countBuffer.Release();
            group.countBuffer = null;
        }
        group.parent = null;
        group.parentTform = null;
        if (affectScene)
        {
            if (group.go != null)
            {
                UnityEngine.Object.Destroy(group.go);
                group.go = null;
            }
            if (group.mesh != null)
            {
                UnityEngine.Object.Destroy(group.mesh);
                group.mesh = null;
            }
        }
        group.lightmapID = -1;

#if UNITY_2018_1_OR_NEWER
        if (group.nvPos.IsCreated) group.nvPos.Dispose();
        if (group.nvNormal.IsCreated) group.nvNormal.Dispose();
        if (group.nvColor.IsCreated) group.nvColor.Dispose();
        if (group.nvUV2.IsCreated) group.nvUV2.Dispose();
        if (group.nvUV.IsCreated) group.nvUV.Dispose();
        if (group.nvSkin.IsCreated) group.nvSkin.Dispose();
        if (group.nvDecalID.IsCreated) group.nvDecalID.Dispose();
        if (group.nvTangents.IsCreated) group.nvTangents.Dispose();
#endif
    }

#if USE_TERRAINS
    public class CachedTerrain
    {
        public Vector3[] pos, norm;
        public Vector2[] uv;
        public int[] indices;

#if USE_BURST_REALLY
        public NativeArray<float3> nStaticPos, nStaticNorm;
        public NativeArray<float2> nStaticUV2;
        public NativeArray<int> nStaticTris;
#endif

        public Mesh tempMesh;
    }

    public static Dictionary<TerrainData, CachedTerrain> cachedTerrains = new Dictionary<TerrainData, CachedTerrain>();

    public static CachedTerrain PrepareTerrain(TerrainData terrainData, Vector3 posOffset, bool useBurst = false, bool createMesh = false)
    {
        var cterrain = new DecalUtils.CachedTerrain();
        int res = terrainData.heightmapResolution;
        var heightmap = terrainData.GetHeights(0, 0, res, res);

        float scaleX = terrainData.size.x / (res-1);
        float scaleY = terrainData.size.y;
        float scaleZ = terrainData.size.z / (res-1);
        var uvscale = new Vector2(1,1) / (res-1);

        int vertOffset = 0;
        int indexOffset = 0;

        var staticPos = new Vector3[res*res];
        var staticNorm = new Vector3[res*res];
        var staticUV2 = new Vector2[res*res];
        var staticTris = new int[(res-1) * (res-1) * 2 * 3];
        for (int y=0;y<res;y++)
        {
            for (int x=0;x<res;x++)
            {
                //int index = x * patchResY + y;
                int index = y * res + x;
                float height = heightmap[y,x];

                staticPos[index] = new Vector3(x * scaleX, height * scaleY, y * scaleZ) + posOffset;
                staticUV2[index] = new Vector2(x * uvscale.x, y * uvscale.y);

                staticNorm[index] = terrainData.GetInterpolatedNormal(x / (float)res, y / (float)res);

                if (x < res-1 && y < res-1)
                {
                    staticTris[indexOffset] = vertOffset;
                    staticTris[indexOffset + 1] = vertOffset + res;
                    staticTris[indexOffset + 2] = vertOffset + res + 1;

                    staticTris[indexOffset + 3] = vertOffset;
                    staticTris[indexOffset + 4] = vertOffset + res + 1;
                    staticTris[indexOffset + 5] = vertOffset + 1;

                    indexOffset += 6;
                }

                vertOffset++;
            }
        }

        cterrain.pos = staticPos;
        cterrain.norm = staticNorm;
        cterrain.uv = staticUV2;
        cterrain.indices = staticTris;

#if USE_BURST_REALLY
        if (useBurst)
        {
            int vcount = staticPos.Length;
            var nStaticPosV3  = new NativeArray<Vector3>(vcount*2, Allocator.Persistent); // second half is a temporary scratch buffer
            var nStaticNormV3 = new NativeArray<Vector3>(vcount*2, Allocator.Persistent);
            var nStaticUV2V2  = new NativeArray<Vector2>(vcount*2, Allocator.Persistent);
            NativeArray<Vector3>.Copy(staticPos,  0, nStaticPosV3,  vcount, vcount);
            NativeArray<Vector3>.Copy(staticNorm, 0, nStaticNormV3, vcount, vcount);
            NativeArray<Vector2>.Copy(staticUV2,  0, nStaticUV2V2,  vcount, vcount);
            cterrain.nStaticPos =  nStaticPosV3.Reinterpret<float3>();
            cterrain.nStaticNorm = nStaticNormV3.Reinterpret<float3>();
            cterrain.nStaticUV2 =  nStaticUV2V2.Reinterpret<float2>();

            int icount = staticTris.Length;
            cterrain.nStaticTris  = new NativeArray<int>(icount*2, Allocator.Persistent);
            NativeArray<int>.Copy(staticTris, cterrain.nStaticTris, icount);
        }
#endif

        if (createMesh)
        {
            var mesh = new Mesh();
#if UNITY_2017_3_OR_NEWER
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
#endif
            mesh.vertices = staticPos;
            mesh.triangles = staticTris;
            mesh.normals = staticNorm;
            mesh.uv = staticUV2;
            mesh.uv2 = staticUV2;
            cterrain.tempMesh = mesh;
        }

        cachedTerrains[terrainData] = cterrain;
        return cterrain;
    }
#endif

#if USE_TERRAINS
    public static Mesh GetSharedMesh(GameObject obj, out Terrain terrain)
    {
        terrain = null;
        var mf = obj.GetComponent<MeshFilter>();
        if (mf != null)
        {
            return mf.sharedMesh;
        }
        var mrSkin = obj.GetComponent<SkinnedMeshRenderer>();
        if (mrSkin != null)
        {
            return mrSkin.sharedMesh;
        }
        terrain = obj.GetComponent<Terrain>();
        return null;
    }
#else
    public static Mesh GetSharedMesh(GameObject obj)
    {
        var mf = obj.GetComponent<MeshFilter>();
        if (mf != null)
        {
            return mf.sharedMesh;
        }
        var mrSkin = obj.GetComponent<SkinnedMeshRenderer>();
        if (mrSkin != null)
        {
            return mrSkin.sharedMesh;
        }
        return null;
    }
#endif
}

