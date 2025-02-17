#define USE_TERRAINS

using UnityEngine;
using UnityEngine.Rendering;
using System.Collections;
using System.Collections.Generic;

public class GPUDecalUtils : MonoBehaviour
{
#if UNITY_2017_1_OR_NEWER
	static Material writeMat;
#endif
	static RenderTexture tempRT;
	static ComputeBuffer tempVBuffer, tempArgBuffer;

	const int PASS_WRITEFIRST = 0;
	const int PASS_WRITESECOND = 1;
	const int PASS_WRITESECOND_TRAIL = 2;
	const int PASS_WRITESECOND_NORMAL = 3;
	const int PASS_WRITESECOND_TRAIL_NORMAL = 4;
	const int PASS_CLEAR = 5;
	const int PASS_CLEAR_NORMAL = 6;

	static uint[] emptyArgs;
	static uint[] emptyCount;
	static Vector3 curDecalEdgeA, curDecalEdgeB, matrixRight, matrixUp, matrixForward;
	static Vector4 curDecalEdgePlane;

	static int pGPUDecalVBuffer, pLightmap, pLightmapInd, pShadowmask;

#if UNITY_2017_1_OR_NEWER
	static int pDecalParentMatrix;
	static int pDecalBufferSize;
	static int pDecalID;
	static int pDecalMatrix;
	static int pDecalPlane0;
	static int pDecalPlane1;
	static int pDecalAngleClip;
	static int pDecalLightmapST;
	static int pTempVertexBuffer;
	static int pDecalTrailFP0;
	static int pDecalTrailFP1;
	static int pDecalTrailFP2;
	static int pDecalTrailFT0;
	static int pDecalTrailFT1;
	static int pDecalTrailFT2;
	static int pDecalTrailSP0;
	static int pDecalTrailSP1;
	static int pDecalTrailSP2;
	static int pDecalTrailST0;
	static int pDecalTrailST1;
	static int pDecalTrailST2;
	static int pDecalOpacity;
#endif

	static bool initialized = false;

#if UNITY_2019_3_OR_NEWER
	static VertexAttributeDescriptor[] layout;
	static VertexAttributeDescriptor[] layoutTangents;

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct ReadableVertex
    {
        public float posX, posY, posZ;
        public float normalX, normalY, normalZ;
        public float fade;
        public float uvX, uvY;
        public float uv2X, uv2Y;
        public float pad;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct ReadableVertexTangents
    {
        public float posX, posY, posZ;
        public float normalX, normalY, normalZ;
        public float tangentX, tangentY, tangentZ, tangentW;
        public float fade;
        public float uvX, uvY;
        public float uv2X, uv2Y;
        public float pad;
    }
#endif

#pragma warning disable 0649
	// TODO: compress
	struct Triangle2
	{
		public float worldPosAx, worldPosAy, worldPosAz;
		public float worldNormalAx, worldNormalAy, worldNormalAz;
		public float fadeA;
		public float uvAx, uvAy;
		public float uvA2x, uvA2y;
		public float pad1;

		public float worldPosBx, worldPosBy, worldPosBz;
		public float worldNormalBx, worldNormalBy, worldNormalBz;
		public float pad2;
		public float uvBx, uvBy;
		public float uvB2x, uvB2y;
		public float fadeB;

		public float worldPosCx, worldPosCy, worldPosCz;
		public float worldNormalCx, worldNormalCy, worldNormalCz;
		public float fadeC;
		public float uvCx, uvCy;
		public float uvC2x, uvC2y;
		public float decalID;
	}

	struct Triangle2Tangents
	{
		public float worldPosAx, worldPosAy, worldPosAz;
		public float worldNormalAx, worldNormalAy, worldNormalAz;
		public float tangentAx, tangentAy, tangentAz, tangentAw;
		public float fadeA;
		public float uvAx, uvAy;
		public float uvA2x, uvA2y;
		public float pad1;

		public float worldPosBx, worldPosBy, worldPosBz;
		public float worldNormalBx, worldNormalBy, worldNormalBz;
		public float tangentBx, tangentBy, tangentBz, tangentBw;
		public float pad2;
		public float uvBx, uvBy;
		public float uvB2x, uvB2y;
		public float fadeB;

		public float worldPosCx, worldPosCy, worldPosCz;
		public float worldNormalCx, worldNormalCy, worldNormalCz;
		public float tangentCx, tangentCy, tangentCz, tangentCw;
		public float fadeC;
		public float uvCx, uvCy;
		public float uvC2x, uvC2y;
		public float decalID;
	}
#pragma warning restore 0649

	public static void PrewarmStaticData()
	{
		if (initialized) return;

#if UNITY_2017_1_OR_NEWER
		writeMat = new Material(Shader.Find("Hidden/fGPUDecalWriteShader"));
#endif

		tempRT = new RenderTexture(1, 1, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
		tempRT.Create();

		emptyArgs = new uint[4];
		emptyArgs[0] = 0;
		emptyArgs[1] = 1;
		emptyArgs[2] = 0;
		emptyArgs[3] = 0;

		emptyCount = new uint[4];//1];
		emptyCount[0] = 0;
		emptyCount[1] = 0;
		emptyCount[2] = 0;
		emptyCount[3] = 0;

		pGPUDecalVBuffer = Shader.PropertyToID("fGPUDecalVBuffer");
		pLightmap = Shader.PropertyToID("_Lightmap");
		pLightmapInd = Shader.PropertyToID("_LightmapInd");
		pShadowmask = Shader.PropertyToID("_ShadowMask");

#if UNITY_2017_1_OR_NEWER
		pDecalParentMatrix = Shader.PropertyToID("_DecalParentMatrix");
		pDecalBufferSize = Shader.PropertyToID("_DecalBufferSize");
		pDecalID = Shader.PropertyToID("_DecalID");
		pDecalMatrix = Shader.PropertyToID("_DecalMatrix");
		pDecalPlane0 = Shader.PropertyToID("_DecalPlane0");
		pDecalPlane1 = Shader.PropertyToID("_DecalPlane1");
		pDecalAngleClip = Shader.PropertyToID("_DecalAngleClip");
		pDecalLightmapST = Shader.PropertyToID("_DecalLightmapST");
		pTempVertexBuffer = Shader.PropertyToID("_TempVertexBuffer");
		pDecalTrailFP0 = Shader.PropertyToID("_DecalTrailFP0");
		pDecalTrailFP1 = Shader.PropertyToID("_DecalTrailFP1");
		pDecalTrailFP2 = Shader.PropertyToID("_DecalTrailFP2");
		pDecalTrailFT0 = Shader.PropertyToID("_DecalTrailFT0");
		pDecalTrailFT1 = Shader.PropertyToID("_DecalTrailFT1");
		pDecalTrailFT2 = Shader.PropertyToID("_DecalTrailFT2");
		pDecalTrailSP0 = Shader.PropertyToID("_DecalTrailSP0");
		pDecalTrailSP1 = Shader.PropertyToID("_DecalTrailSP1");
		pDecalTrailSP2 = Shader.PropertyToID("_DecalTrailSP2");
		pDecalTrailST0 = Shader.PropertyToID("_DecalTrailST0");
		pDecalTrailST1 = Shader.PropertyToID("_DecalTrailST1");
		pDecalTrailST2 = Shader.PropertyToID("_DecalTrailST2");
		pDecalOpacity = Shader.PropertyToID("_DecalOpacity");
#endif

#if UNITY_2019_3_OR_NEWER
        layout = new[]
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, 	VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Normal, 		VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Color, 		VertexAttributeFormat.Float32, 1), 
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, 	VertexAttributeFormat.Float32, 2),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord1, 	VertexAttributeFormat.Float32, 2),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord2, 	VertexAttributeFormat.Float32, 1),
        };

        layoutTangents = new[]
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, 	VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Normal, 		VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Tangent, 		VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.Color, 		VertexAttributeFormat.Float32, 1), 
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, 	VertexAttributeFormat.Float32, 2),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord1, 	VertexAttributeFormat.Float32, 2),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord2, 	VertexAttributeFormat.Float32, 1),
        };
#endif

		initialized = true;
	}

	static void SetTris(Mesh mesh, int count)
	{
		var tris = new int[count*3];
		for(int i=0; i<count; i++)
		{
			tris[i*3] = i*3;
			tris[i*3+1] = i*3+1;
			tris[i*3+2] = i*3+2;
		}
		mesh.triangles = tris;
	}

	// Creates a group of decals using the same material, lightmap and settings.
	public static DecalUtils.Group CreateGroup(DecalUtils.GroupDesc desc)
	{
		const int triSize  = (3*3 + 3*3 + 3*2 + 1) * sizeof(float);// sizeOf(Triangle);
		//int triSize2 = (3*3 + 3*3 + 3*2 + 3*2 + 3*1 + 1) * sizeof(float);// sizeOf(Triangle);
		const int triSize2 = (3*3 + 3*3 + 3*2 + 3*2 + 3*1 + 3) * sizeof(float);// sizeOf(Triangle);
		//triSize2 = (3*3+1)*sizeof(float) + (3*3 + 3*2 + 3*2) *sizeof(short) + sizeof(uint);
		const int triSize2t = (3*3 + 3*3 + 3*2 + 3*2 + 3*1 + 3*4 + 3) * sizeof(float);// sizeOf(Triangle);
		int totalTris = desc.maxTrisTotal;
		var vbuffer = new ComputeBuffer(totalTris, desc.tangents ? triSize2t : triSize2);
		if (desc.tangents)
		{
			vbuffer.SetData(new Triangle2Tangents[totalTris]);
		}
		else
		{
			vbuffer.SetData(new Triangle2[totalTris]);
		}

		if (tempVBuffer == null || tempVBuffer.count < desc.maxTrisInDecal)
		{
			if (tempVBuffer != null) tempVBuffer.Release();
			tempVBuffer = new ComputeBuffer(desc.maxTrisInDecal, triSize);
		}

		PrewarmStaticData();

		int argStride = 4;
		var argBuffer = new ComputeBuffer(1, argStride * sizeof(uint), ComputeBufferType.IndirectArguments);
		argBuffer.SetData(emptyArgs);

		var countBuffer = new ComputeBuffer(1, argStride * sizeof(uint), ComputeBufferType.IndirectArguments); // this buffer needs to be just 1 uint long, but we create it as IndirectArguments because we reuse the same RWBuffer in the shader
		countBuffer.SetData(emptyCount);

		if (tempArgBuffer == null)
		{
			tempArgBuffer = new ComputeBuffer(1, argStride * sizeof(uint), ComputeBufferType.IndirectArguments);
			tempArgBuffer.SetData(emptyArgs);
		}

		var parentTform = desc.parent != null ? desc.parent.transform : null;

		GameObject go = null;
		Mesh newMesh = null;
		if (!desc.indirectDraw)
		{
			go = new GameObject();
			go.name = "Decals";
			var tform = go.transform;
			tform.parent = parentTform;
			tform.localPosition = Vector3.zero;
			tform.localRotation = Quaternion.identity;
			tform.localScale = Vector3.one;

			newMesh = new Mesh();
#if UNITY_2019_3_OR_NEWER
			newMesh.SetVertexBufferParams(totalTris*3, desc.tangents ? layoutTangents : layout);
			SetTris(newMesh, totalTris);
#endif
			if (desc.parent != null)
			{
				var mff = desc.parent.GetComponent<MeshFilter>();
				if (mff != null)
				{
					var pmesh = mff.sharedMesh;
					if (pmesh != null)
					{
						newMesh.bounds = pmesh.bounds;
					}
				}
			}
			var mf = go.AddComponent<MeshFilter>();
			mf.sharedMesh = newMesh;
			var mr = go.AddComponent<MeshRenderer>();
			mr.sharedMaterial = desc.material;
			mr.lightmapIndex = desc.lightmapID;
			mr.realtimeLightmapIndex = desc.realtimeLightmapID;
			mr.realtimeLightmapScaleOffset = desc.realtimeLightmapScaleOffset;
			if (desc.materialPropertyBlock != null) mr.SetPropertyBlock(desc.materialPropertyBlock);
		}

		var group = new DecalUtils.Group();
		group.mode = DecalUtils.Mode.GPU;
		group.material = desc.material;
		group.vbuffer = vbuffer;
		group.totalTris = totalTris;
		group.maxTrisInDecal = desc.maxTrisInDecal;
		group.argBuffer = argBuffer;
		group.countBuffer = countBuffer;
		group.parent = desc.parent;
		group.parentTform = parentTform;
		group.go = go;
		group.mesh = newMesh;
		group.lightmapID = desc.lightmapID >= 0xFFFE ? -1 : desc.lightmapID;
		group.bounds = group.nextBounds = new Bounds(new Vector3(-99999, -99999, -99999), Vector3.zero);
		group.indirectDraw = desc.indirectDraw;
		group.isTrail = desc.isTrail;
		group.trailVScale = desc.trailVScale;
		group.tangents = desc.tangents;
		group.materialPropertyBlock = desc.materialPropertyBlock;
#if UNITY_2019_3_OR_NEWER
		if (desc.tangents)
		{
			group.buffWithTangents = new ReadableVertexTangents[group.totalTris*3];
		}
		else
		{
			group.buff = new ReadableVertex[group.totalTris*3];
		}
#endif

		return group;
	}

	// Creates a CommandBuffer drawing the decals directly from generated data on the GPU.
	// Decal group must be created witn indirectDraw enabled.
	public static CommandBuffer CreateDrawIndirectCommandBuffer(DecalUtils.Group group, int pass)
	{
		if (group.mode != DecalUtils.Mode.GPU)
		{
			Debug.LogError("Decal mode is not GPU.");
			return null;
		}
		var cmd = new CommandBuffer();
		cmd.name = "DrawDecals_" + (group.parentTform != null ? group.parentTform.name : "") + "_" + group.lightmapID;
		group.drawCmd = cmd;
		UpdateDrawIndirectCommandBuffer(group, Matrix4x4.identity, pass);
		return cmd;
	}

	// Updates the CommandBuffer with new recevier object's matrix.
	// Only useful for decals attached to movable objects.
	public static void UpdateDrawIndirectCommandBuffer(DecalUtils.Group group, Matrix4x4 mtx, int pass)
	{
		if (group.material == null) return;
		if (group.material.shader == null) return;
		if (group.mode != DecalUtils.Mode.GPU)
		{
			Debug.LogError("Decal mode is not GPU.");
			return;
		}
		var passCount = group.material.passCount;
		if (pass >= passCount) pass = passCount - 1;
		var cmd = group.drawCmd;
		cmd.Clear();
		int shaderPass = pass;
		cmd.SetGlobalBuffer(pGPUDecalVBuffer, group.vbuffer); // same material can be used on different groups
		//group.material.SetBuffer(pGPUDecalVBuffer, group.vbuffer);
		if (group.lightmapID >= 0)
		{
			cmd.EnableShaderKeyword("LIGHTMAP_ON");
			var lms = LightmapSettings.lightmaps[group.lightmapID];
			cmd.SetGlobalTexture(pLightmap, lms.lightmapColor);
			if (lms.lightmapDir != null)
			{
				cmd.EnableShaderKeyword("DIRLIGHTMAP_COMBINED");
				cmd.SetGlobalTexture(pLightmapInd, lms.lightmapDir);
			}
			else
			{
				cmd.DisableShaderKeyword("DIRLIGHTMAP_COMBINED");
			}
			if (lms.shadowMask != null)
			{
				cmd.EnableShaderKeyword("SHADOWS_SHADOWMASK");
				cmd.SetGlobalTexture(pShadowmask, lms.shadowMask);
			}
			else
			{
				cmd.DisableShaderKeyword("SHADOWS_SHADOWMASK");
			}
		}
		else
		{
			cmd.DisableShaderKeyword("LIGHTMAP_ON");
		}
		if (group.materialPropertyBlock == null)
		{
			cmd.DrawProceduralIndirect(mtx, group.material, shaderPass, MeshTopology.Triangles, group.argBuffer);
		}
		else
		{
			cmd.DrawProceduralIndirect(mtx, group.material, shaderPass, MeshTopology.Triangles, group.argBuffer, 0, group.materialPropertyBlock);
		}
	}

	static Vector4 GetPlane(ref Matrix4x4 mtx, int a, int b, float sign)
	{
		var c = mtx.GetRow(a) + mtx.GetRow(b) * sign;
		float invLength = -1.0f / (new Vector3(c.x, c.y, c.z)).magnitude;
		c *= invLength;
		return c;
	}

	// Adds a decal to an existing Group.
	public static void AddDecal(DecalUtils.Group group, DecalUtils.DecalDesc desc, GameObject receiver)
	{
#if UNITY_2017_1_OR_NEWER
		if (group.mode != DecalUtils.Mode.GPU)
		{
			Debug.LogError("Decal mode is not GPU.");
			return;
		}
		var decalMatrix = Matrix4x4.TRS(desc.position, desc.rotation, new Vector3(desc.sizeX, desc.sizeY, desc.distance));
		bool firstDecal = group.numDecals == 0;

		if (group.parentTform == null)
		{
			DecalUtils.ExpandBounds(group, decalMatrix, desc);
		}

		decalMatrix.SetColumn(3, decalMatrix.GetColumn(3) + decalMatrix.GetColumn(2) * desc.distance * 0.5f);
		decalMatrix = decalMatrix.inverse;

		if (group.isTrail)
		{
			matrixRight =   decalMatrix.GetRow(0);
			matrixUp = 	    decalMatrix.GetRow(1);
			matrixForward = decalMatrix.GetRow(2);
			matrixRight = matrixRight.normalized;
			matrixUp = matrixUp.normalized;
			matrixForward = matrixForward.normalized;
			var edgeCenter = desc.position + matrixUp * desc.sizeY;
			var rightShift = matrixRight * desc.sizeX;
			curDecalEdgeA = edgeCenter - rightShift;
			curDecalEdgeB = edgeCenter + rightShift;
			curDecalEdgePlane = new Vector4(matrixUp.x, matrixUp.y, matrixUp.z, -Vector3.Dot(curDecalEdgeA, matrixUp));
		}

		var cmd = new CommandBuffer();
		cmd.name = "UpdateDecal";
		cmd.ClearRandomWriteTargets();
		cmd.SetRenderTarget(tempRT);
		cmd.SetRandomWriteTarget(1, tempVBuffer);// group.vbuffer);
		cmd.SetRandomWriteTarget(2, tempArgBuffer);
		cmd.SetGlobalMatrix(pDecalParentMatrix, group.parentTform == null ? Matrix4x4.identity : group.parentTform.worldToLocalMatrix);
		cmd.SetGlobalFloat(pDecalBufferSize, group.totalTris);
		cmd.SetGlobalFloat(pDecalID, group.decalIDCounter+1);
		float prevTrailV = group.trailV;
		if (group.isTrail && group.numDecals > 0)
		{
			/*var tcenter = (group.prevDecalEdgeA + group.prevDecalEdgeB + curDecalEdgeA + curDecalEdgeB) * 0.25f;
			float tminX = Mathf.Min(Mathf.Min(group.prevDecalEdgeA.x, group.prevDecalEdgeB.x), Mathf.Min(curDecalEdgeA.x, curDecalEdgeB.x));
			float tminY = Mathf.Min(Mathf.Min(group.prevDecalEdgeA.y, group.prevDecalEdgeB.y), Mathf.Min(curDecalEdgeA.y, curDecalEdgeB.y));
			float tminZ = Mathf.Min(Mathf.Min(group.prevDecalEdgeA.z, group.prevDecalEdgeB.z), Mathf.Min(curDecalEdgeA.z, curDecalEdgeB.z));
			float tmaxX = Mathf.Max(Mathf.Max(group.prevDecalEdgeA.x, group.prevDecalEdgeB.x), Mathf.Max(curDecalEdgeA.x, curDecalEdgeB.x));
			float tmaxY = Mathf.Max(Mathf.Max(group.prevDecalEdgeA.y, group.prevDecalEdgeB.y), Mathf.Max(curDecalEdgeA.y, curDecalEdgeB.y));
			float tmaxZ = Mathf.Max(Mathf.Max(group.prevDecalEdgeA.z, group.prevDecalEdgeB.z), Mathf.Max(curDecalEdgeA.z, curDecalEdgeB.z));
			float tsizeX = tmaxX - tminX;
			float tsizeY = tmaxY - tminY;
			float tsizeZ = tmaxZ - tminZ;
			var tsize = new Vector3(tsizeX, tsizeY, tsizeZ) + Vector3Abs(matrixForward) * distance * 2;
			Debug.LogError(tsize);
			decalMatrix.SetTRS(tcenter, Quaternion.identity, tsize);
			decalMatrix = decalMatrix.inverse;*/
			float moveDist = Mathf.Max(Vector3.Distance(curDecalEdgeA, group.prevDecalEdgeA), Vector3.Distance(curDecalEdgeB, group.prevDecalEdgeB));
			float mul = Mathf.Min(desc.sizeX, desc.sizeY) * moveDist;
			decalMatrix.SetRow(0, decalMatrix.GetRow(0) * mul);
			decalMatrix.SetRow(1, decalMatrix.GetRow(1) * mul);
			decalMatrix.SetRow(2, decalMatrix.GetRow(2) * mul);
			cmd.SetGlobalMatrix(pDecalMatrix, decalMatrix);
			var left =   -Vector3.Cross((curDecalEdgeA - group.prevDecalEdgeA).normalized, matrixForward).normalized;
			var right = Vector3.Cross((curDecalEdgeB - group.prevDecalEdgeB).normalized, matrixForward).normalized;
			cmd.SetGlobalVector(pDecalPlane0, new Vector4(left.x, left.y, left.z, -Vector3.Dot(left, curDecalEdgeA)));
			cmd.SetGlobalVector(pDecalPlane1, new Vector4(right.x, right.y, right.z, -Vector3.Dot(right, curDecalEdgeB)));
			group.trailV += moveDist * group.trailVScale;
		}
		else
		{
			cmd.SetGlobalMatrix(pDecalMatrix, decalMatrix);
			cmd.SetGlobalVector(pDecalPlane0, GetPlane(ref decalMatrix, 3,0, -1));// new Vector4(1, 0, 0, -(position.x+size*0.5f)));
			cmd.SetGlobalVector(pDecalPlane1, GetPlane(ref decalMatrix, 3,0, 1));// new Vector4(-1, 0, 0, (position.x-size*0.5f)));
		}
		cmd.SetGlobalFloat(pDecalAngleClip, desc.angleClip);
		cmd.SetGlobalVector(pDecalLightmapST, desc.lightmapScaleOffset);
		group.decalIDCounter++;

#if USE_TERRAINS
		if (desc.mesh == null)
		{
			var terrain = receiver.GetComponent<Terrain>();
			if (terrain != null)
			{
	            var posOffset = receiver.transform.position;
	            var terrainData = terrain.terrainData;
	            DecalUtils.CachedTerrain cterrain;
	            if (!DecalUtils.cachedTerrains.TryGetValue(terrainData, out cterrain))
	            {
	                cterrain = DecalUtils.PrepareTerrain(terrainData, posOffset, false, true);
	            }
	            desc.mesh = cterrain.tempMesh;
	            desc.worldMatrix = Matrix4x4.identity;
	        }
	    }
#endif

		int numSubs = desc.mesh.subMeshCount;
		//int totalTris = 0;
		for(int subMesh=0; subMesh<numSubs; subMesh++)
		{
			//totalTris += mesh.GetSubMesh(subMesh).indexCount;
			cmd.DrawMesh(desc.mesh, desc.worldMatrix, writeMat, subMesh, PASS_WRITEFIRST);
		}
		cmd.SetRandomWriteTarget(1, group.vbuffer);
		cmd.SetRandomWriteTarget(2, group.countBuffer);
		cmd.SetGlobalBuffer(pTempVertexBuffer, tempVBuffer);
		//cmd.SetGlobalBuffer("_TempDecalArgBuffer", group.tempArgBuffer);
		cmd.SetRandomWriteTarget(3, tempArgBuffer);
		if (group.isTrail && group.numDecals > 0)
		{
			cmd.SetGlobalVector(pDecalPlane0, curDecalEdgePlane);
			cmd.SetGlobalVector(pDecalPlane1, new Vector4(-group.prevDecalEdgePlane.x, -group.prevDecalEdgePlane.y, -group.prevDecalEdgePlane.z, -group.prevDecalEdgePlane.w));

			cmd.SetGlobalVector(pDecalTrailFP0, group.prevDecalEdgeA);
			cmd.SetGlobalVector(pDecalTrailFP1, curDecalEdgeA);
			cmd.SetGlobalVector(pDecalTrailFP2, curDecalEdgeB);
			
			cmd.SetGlobalVector(pDecalTrailFT0, new Vector2(0,prevTrailV));
			cmd.SetGlobalVector(pDecalTrailFT1, new Vector2(0,group.trailV));
			cmd.SetGlobalVector(pDecalTrailFT2, new Vector2(1,group.trailV));

			cmd.SetGlobalVector(pDecalTrailSP0, curDecalEdgeB);
			cmd.SetGlobalVector(pDecalTrailSP1, group.prevDecalEdgeB);
			cmd.SetGlobalVector(pDecalTrailSP2, group.prevDecalEdgeA);

			cmd.SetGlobalVector(pDecalTrailST0, new Vector2(1,group.trailV));
			cmd.SetGlobalVector(pDecalTrailST1, new Vector2(1,prevTrailV));
			cmd.SetGlobalVector(pDecalTrailST2, new Vector2(0,prevTrailV));
		}
		else
		{
			cmd.SetGlobalVector(pDecalPlane0, GetPlane(ref decalMatrix, 3,1, -1));// new Vector4(0, 0, 1, -(position.z+size*0.5f)));
			cmd.SetGlobalVector(pDecalPlane1, GetPlane(ref decalMatrix, 3,1, 1));//new Vector4(0, 0, -1, (position.z-size*0.5f)));
		}
		cmd.SetGlobalFloat(pDecalOpacity, desc.opacity);
		//for(int subMesh=0; subMesh<numSubs; subMesh++)
		//{
			if (group.isTrail && group.numDecals > 0)
			{
				cmd.DrawProceduralIndirect(Matrix4x4.identity, writeMat, group.tangents ? PASS_WRITESECOND_TRAIL_NORMAL : PASS_WRITESECOND_TRAIL, MeshTopology.Triangles, tempArgBuffer);
			}
			else
			{
				cmd.DrawProceduralIndirect(Matrix4x4.identity, writeMat, group.tangents ? PASS_WRITESECOND_NORMAL : PASS_WRITESECOND, MeshTopology.Triangles, tempArgBuffer);
			}
		//}
		//totalTris /= 3;

		cmd.SetRandomWriteTarget(4, group.argBuffer);
		cmd.DrawProcedural(Matrix4x4.identity, writeMat, group.tangents ? PASS_CLEAR_NORMAL : PASS_CLEAR, MeshTopology.Triangles, 3 * group.maxTrisInDecal);

		cmd.ClearRandomWriteTargets();
		Graphics.ExecuteCommandBuffer(cmd);

		if (!group.indirectDraw)
		{

#if UNITY_2019_3_OR_NEWER
			if (group.tangents)
			{
				//var buff = new ReadableVertexTangents[group.totalTris*3];
				group.vbuffer.GetData(group.buffWithTangents);
				group.mesh.SetVertexBufferData(group.buffWithTangents, 0, 0, group.totalTris*3, 0, MeshUpdateFlags.DontRecalculateBounds);
			}
			else
			{
				//var buff = new ReadableVertex[group.totalTris*3];
				group.vbuffer.GetData(group.buff);
				group.mesh.SetVertexBufferData(group.buff, 0, 0, group.totalTris*3, 0, MeshUpdateFlags.DontRecalculateBounds);
			}
#else
			var verts = new Vector3[group.totalTris*3];
			var uvs = new Vector2[group.totalTris*3];
			var uvs2 = new Vector2[group.totalTris*3];
			var colors = new Color[group.totalTris*3];
			var origBounds = group.mesh.bounds;

			if (group.tangents)
			{
				var buff = new Triangle2Tangents[group.totalTris];
				group.vbuffer.GetData(buff);
				for(int i=0; i<group.totalTris; i++)
				{
					verts[i*3]   = new Vector3(buff[i].worldPosAx, buff[i].worldPosAy, buff[i].worldPosAz);
					verts[i*3+1] = new Vector3(buff[i].worldPosBx, buff[i].worldPosBy, buff[i].worldPosBz);
					verts[i*3+2] = new Vector3(buff[i].worldPosCx, buff[i].worldPosCy, buff[i].worldPosCz);

					uvs[i*3]     = new Vector2(buff[i].uvAx, buff[i].uvAy);
					uvs[i*3+1]   = new Vector2(buff[i].uvBx, buff[i].uvBy);
					uvs[i*3+2]   = new Vector2(buff[i].uvCx, buff[i].uvCy);

					uvs2[i*3]     = new Vector2(buff[i].uvA2x, buff[i].uvA2y);
					uvs2[i*3+1]   = new Vector2(buff[i].uvB2x, buff[i].uvB2y);
					uvs2[i*3+2]   = new Vector2(buff[i].uvC2x, buff[i].uvC2y);

					colors[i*3] =   new Color(buff[i].fadeA, buff[i].fadeA, buff[i].fadeA, buff[i].fadeA);
					colors[i*3+1] = new Color(buff[i].fadeB, buff[i].fadeB, buff[i].fadeB, buff[i].fadeB);
					colors[i*3+2] = new Color(buff[i].fadeC, buff[i].fadeC, buff[i].fadeC, buff[i].fadeC);
				}
			}
			else
			{
				var buff = new Triangle2[group.totalTris];
				group.vbuffer.GetData(buff);
				for(int i=0; i<group.totalTris; i++)
				{
					verts[i*3]   = new Vector3(buff[i].worldPosAx, buff[i].worldPosAy, buff[i].worldPosAz);
					verts[i*3+1] = new Vector3(buff[i].worldPosBx, buff[i].worldPosBy, buff[i].worldPosBz);
					verts[i*3+2] = new Vector3(buff[i].worldPosCx, buff[i].worldPosCy, buff[i].worldPosCz);

					uvs[i*3]     = new Vector2(buff[i].uvAx, buff[i].uvAy);
					uvs[i*3+1]   = new Vector2(buff[i].uvBx, buff[i].uvBy);
					uvs[i*3+2]   = new Vector2(buff[i].uvCx, buff[i].uvCy);

					uvs2[i*3]     = new Vector2(buff[i].uvA2x, buff[i].uvA2y);
					uvs2[i*3+1]   = new Vector2(buff[i].uvB2x, buff[i].uvB2y);
					uvs2[i*3+2]   = new Vector2(buff[i].uvC2x, buff[i].uvC2y);

					colors[i*3] =   new Color(buff[i].fadeA, buff[i].fadeA, buff[i].fadeA, buff[i].fadeA);
					colors[i*3+1] = new Color(buff[i].fadeB, buff[i].fadeB, buff[i].fadeB, buff[i].fadeB);
					colors[i*3+2] = new Color(buff[i].fadeC, buff[i].fadeC, buff[i].fadeC, buff[i].fadeC);
				}
			}

			group.mesh.vertices = verts;
			group.mesh.uv = uvs;
			group.mesh.uv2 = uvs2;
			group.mesh.colors = colors;
			if (firstDecal) SetTris(group.mesh, group.totalTris);
			group.mesh.bounds = origBounds;
#endif
			if (group.parentTform == null) group.mesh.bounds = group.bounds;
		}

		if (group.isTrail)
		{
			group.prevDecalEdgeA = curDecalEdgeA;
			group.prevDecalEdgeB = curDecalEdgeB;
			group.prevDecalEdgePlane = curDecalEdgePlane;
		}

		group.numDecals++;
#else
		Debug.LogError("GPU decals are not supported on < 2017.1.");
#endif
	}

	// Removes all decals from the group.
	public static void ClearDecals(DecalUtils.Group group)
	{
		if (group.mode != DecalUtils.Mode.GPU)
		{
			Debug.LogError("Decal mode is not GPU.");
			return;
		}
		group.argBuffer.SetData(emptyArgs);
		group.countBuffer.SetData(emptyCount);
		group.bounds = group.nextBounds = new Bounds(new Vector3(-99999, -99999, -99999), Vector3.zero);
		group.numDecals = group.boundsCounter = group.boundsMinDecalCounter = 0;
		if (group.mesh != null)
		{
			group.mesh.bounds = group.bounds;
			if (group.tangents)
			{
				group.vbuffer.SetData(new Triangle2Tangents[group.totalTris]);
			}
			else
			{
				group.vbuffer.SetData(new Triangle2[group.totalTris]);
			}
		}
	}

	public static void Cleanup()
	{
		initialized = false;
	}
}

