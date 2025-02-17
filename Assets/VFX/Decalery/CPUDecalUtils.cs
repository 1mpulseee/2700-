#define USE_TERRAINS

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
#endif
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

public class CPUDecalUtils : MonoBehaviour
{
#pragma warning disable 0649
    static BoneWeight tmpWeight;
#pragma warning restore 0649
    static Plane decalTriPlane = new Plane();
    static Plane decalCutPlaneAB = new Plane();
    static Plane decalCutPlaneBC = new Plane();
    static Plane decalCutPlaneCA = new Plane();
    static Plane triPlane = new Plane();

    static Vector3[] splitTriPos = new Vector3[3];
    static Vector3[] splitTriNormal = new Vector3[3];
    //static Vector4[] splitTriTangent = new Vector4[3];
    static Vector2[] splitTriUV = new Vector2[3];
    static Vector2[] splitTriUV2 = new Vector2[3];
    static BoneWeight[] splitTriSkin = new BoneWeight[3];
    static Color[] splitTriColor = new Color[3];
    static Color decalColorA = new Color();
    static Color decalColorB = new Color();
    static Color decalColorC = new Color();

    static Matrix4x4 cullingMatrix;
    static Vector4 lightmapScaleOffset; 

#if UNITY_EDITOR
    public static bool checkPrefabs = false;
#endif

    static float saturate(float f)
    {
        return Mathf.Clamp(f, 0.0f, 1.0f);
    }

    static Vector3 triLerp(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 a, Vector3 b, Vector3 c, Vector3 pp)
    {
        /*
        // attrib to interpolate: decal UV
        var p0 = decalUVA;// pickedUvs[pickedIndices[hit.triangleIndex * 3 + 0]];
        var p1 = decalUVB;//pickedUvs[pickedIndices[hit.triangleIndex * 3 + 1]];
        var p2 = decalUVC;//pickedUvs[pickedIndices[hit.triangleIndex * 3 + 2]];

        // world tri coords (decal)
        var a = decalA;//pickedPos[pickedIndices[hit.triangleIndex * 3 + 0]];
        var b = decalB;//pickedPos[pickedIndices[hit.triangleIndex * 3 + 1]];
        var c = decalC;//pickedPos[pickedIndices[hit.triangleIndex * 3 + 2]];

        // world point in tri
        var pp = triA;// statics[k].transform.InverseTransformPoint(p);
        */

        var v0 = b - a;
        var v1 = c - a;
        var v2 = pp - a;
        float d00 = Vector3.Dot(v0, v0);
        float d01 = Vector3.Dot(v0, v1);
        float d11 = Vector3.Dot(v1, v1);
        float d20 = Vector3.Dot(v2, v0);
        float d21 = Vector3.Dot(v2, v1);
        float denom = d00 * d11 - d01 * d01;
        float v = (d11 * d20 - d01 * d21) / denom;
        float w = (d00 * d21 - d01 * d20) / denom;
        float u = 1.0f - v - w;
        return p0*u + p1*v + p2*w;
    }

    static Color triLerp(Color p0, Color p1, Color p2, Vector3 a, Vector3 b, Vector3 c, Vector3 pp)
    {
        var v0 = b - a;
        var v1 = c - a;
        var v2 = pp - a;
        float d00 = Vector3.Dot(v0, v0);
        float d01 = Vector3.Dot(v0, v1);
        float d11 = Vector3.Dot(v1, v1);
        float d20 = Vector3.Dot(v2, v0);
        float d21 = Vector3.Dot(v2, v1);
        float denom = d00 * d11 - d01 * d01;
        float v = (d11 * d20 - d01 * d21) / denom;
        float w = (d00 * d21 - d01 * d20) / denom;
        float u = 1.0f - v - w;
        return p0*u + p1*v + p2*w;
    }

    static BoneWeight LerpBoneWeights(BoneWeight a, BoneWeight b, float c)
    {
        a.weight0 = Mathf.Lerp(a.weight0, b.weight0, c);
        a.weight1 = Mathf.Lerp(a.weight1, b.weight1, c);
        a.weight2 = Mathf.Lerp(a.weight2, b.weight2, c);
        a.weight3 = Mathf.Lerp(a.weight3, b.weight3, c);
        return a;
    }

    struct HashedVertex
    {
        public Vector3 pos, normal;
        public Vector2 uv, uv2;
        public Color color;
    }

#if UNITY_EDITOR
    static void ValidateFileAttribs(string file)
    {
        var attribs = File.GetAttributes(file);
        if ((attribs & FileAttributes.ReadOnly) != 0)
        {
            File.SetAttributes(file, attribs & ~FileAttributes.ReadOnly);
        }
    }
#endif

    static void WeldVerts(List<int> inIndices, List<Vector3> inVerts, List<Vector3> inNormals, List<Vector2> inUV, List<Vector2> inUV2, List<BoneWeight> inSkin, List<Color> inColor,
                          List<int> outIndices, List<Vector3> outVerts, List<Vector3> outNormals, List<Vector2> outUV, List<Vector2> outUV2, List<BoneWeight> outSkin, List<Color> outColor)
    {
        bool hasUV = inUV != null && inUV.Count > 0;
        bool hasColor = inColor != null && inColor.Count > 0;
        bool hasSkin = inSkin != null && inSkin.Count > 0;

        var hashed = new HashedVertex();
        var map = new Dictionary<HashedVertex, int>();

        // Detect and remap similar verts, create new VB
        int numInVerts = inVerts.Count;
        int numOutVerts = 0;
        var vertRemap = new int[numInVerts];
        for(int i=0; i<numInVerts; i++) vertRemap[i] = -1;
        for(int i=0; i<numInVerts; i++)
        {
            hashed.pos = inVerts[i];
            hashed.normal = inNormals[i];
            if (hasUV) hashed.uv = inUV[i];
            hashed.uv2 = inUV2[i];
            if (hasColor) hashed.color = inColor[i];

            int index;
            if (!map.TryGetValue(hashed, out index))
            {
                map[hashed] = index = numOutVerts;
    
                vertRemap[i] = numOutVerts; // make unique

                outVerts.Add(inVerts[i]);
                outNormals.Add(inNormals[i]);
                outUV2.Add(inUV2[i]);
                if (hasUV) outUV.Add(inUV[i]);
                if (hasColor) outColor.Add(inColor[i]);
                if (hasSkin) outSkin.Add(inSkin[i]);

                numOutVerts++;
            }
            else
            {
                vertRemap[i] = index;
            }
        }

        // Remap IB
        int numIndices = inIndices.Count;
        for(int i=0; i<numIndices; i++)
        {
            outIndices.Add(vertRemap[ inIndices[i] ]);
        }
    }

    static void UpdateDecalFor(DecalGroup decal, Material sharedMaterial, Vector2[] decalUV, Vector3[] decalPosW, Transform decalTform, int[] decalTris, Color[] decalColor, GameObject parentObject, Material optionalMateralChange, float opacity,
        ref int fixedOffset, byte decalID = 0, Mesh newMesh = null, Vector3[] vPos = null, Vector3[] vNormal = null, Vector2[] vUV = null, Vector2[] vUV2 = null, Color[] vColor = null, BoneWeight[] vSkin = null, byte[] vDecalID = null, Vector4[] vTangents = null, int maxTrisInDecal = 0, bool transformToParent = false, bool earlyCull = false, bool transformUV1 = false)
    {
        //var time1 = new System.Diagnostics.Stopwatch();
        //time1.Start();

        if (decal.roadTransform)
        {
            // Faded-only option (decal uses procedural UVs)
            /*
                float2 xvec = normalize(float2(unity_ObjectToWorld._11, -unity_ObjectToWorld._31));
                float2 zvec = normalize(float2(-unity_ObjectToWorld._12, unity_ObjectToWorld._32));
                OUT.TexCoords = OUT.WorldPos.zx - float2(unity_ObjectToWorld._34, unity_ObjectToWorld._14);
                OUT.TexCoords = float2(dot(OUT.TexCoords,xvec), dot(OUT.TexCoords,zvec));
                OUT.TexCoords = OUT.TexCoords * _MainTex_ST.xy + _MainTex_ST.zw;
            */
            var tform = decal.transform;
            var right = tform.right;
            var forward = tform.up;
            var pos = tform.position;

            var mat = sharedMaterial;
            var scale = mat.mainTextureScale;
            var offset = mat.mainTextureOffset;
            Vector2 xvec = new Vector2(right.x, -right.z);
            Vector2 zvec = new Vector2(-forward.x, forward.z);
            int numDecalVerts = decalUV.Length;
            for(int i=0; i<numDecalVerts; i++)
            {
                float u = decalPosW[i].z - pos.z;
                float v = decalPosW[i].x - pos.x;

                decalUV[i] = new Vector2(u*xvec.x + v*xvec.y,  (u*zvec.x + v*zvec.y));
                decalUV[i] = new Vector2(decalUV[i].x*scale.x+offset.x, decalUV[i].y*scale.y+offset.y);
            }
        }

        if (decal.atlasMinX != 0 || decal.atlasMinY != 0 || decal.atlasMaxX != 1.0f || decal.atlasMaxY != 1.0f)
        {
            int numDecalVerts = decalUV.Length;
            float scaleU = decal.atlasMaxX - decal.atlasMinX;
            float scaleV = decal.atlasMaxY - decal.atlasMinY;
            float offsetU = decal.atlasMinX;
            float offsetV = decal.atlasMinY;
            for(int i=0; i<numDecalVerts; i++)
            {
                decalUV[i] = new Vector2(decalUV[i].x*scaleU+offsetU, 1.0f-((1.0f-decalUV[i].y)*scaleV+offsetV));
            }
        }

        // Reverse decal triangles, if flipped
        bool isDecalFlipped = false;
        if (decalTform != null)
        {
            isDecalFlipped = Mathf.Sign(decalTform.lossyScale.x*decalTform.lossyScale.y*decalTform.lossyScale.z) < 0;
            if (isDecalFlipped)
            {
                var decalTris2 = new int[decalTris.Length];
                for(int t=0; t<decalTris.Length; t += 3)
                {
                    decalTris2[t] = decalTris[t + 2];
                    decalTris2[t + 1] = decalTris[t + 1];
                    decalTris2[t + 2] = decalTris[t];
                }
                decalTris = decalTris2;
            }
        }

        //time1.Stop();

        // Get receiver components and data
#if USE_TERRAINS
        Terrain terrain;
        var staticMesh = DecalUtils.GetSharedMesh(parentObject, out terrain);
        if (staticMesh == null && terrain == null) return;
#else
        var staticMesh = DecalUtils.GetSharedMesh(parentObject);
        if (staticMesh == null) return;
#endif
        var staticTform = parentObject.transform;
        var staticMR = parentObject.GetComponent<Renderer>();
        Vector3[] staticPos, staticNorm;
        Vector2[] staticUV2;
        int[] staticTris;
#if USE_TERRAINS
        if (terrain != null)
        {
            var terrainData = terrain.terrainData;
            //int numDecalVerts = decalPosW.Length;
            int numDecalIndices = decalTris.Length;
            float dminx = float.MaxValue;
            float dminz = float.MaxValue;
            float dmaxx = -float.MaxValue;
            float dmaxz = -float.MaxValue;
            float pf = decal.rayLength;
            float pb = -decal.bias;
            for(int i=0; i<numDecalIndices; i+=3)
            {
                int idA = decalTris[i];
                int idB = decalTris[i+1];
                int idC = decalTris[i+2];
                var posA = decalPosW[idA];
                var posB = decalPosW[idB];
                var posC = decalPosW[idC];
                decalTriPlane.Set3Points(posA, posB, posC);
                var n = decalTriPlane.normal;

                // front
                float x = posA.x + n.x * pf;
                float z = posA.z + n.z * pf;
                if (x < dminx) dminx = x;
                if (z < dminz) dminz = z;
                if (x > dmaxx) dmaxx = x;
                if (z > dmaxz) dmaxz = z;

                x = posB.x + n.x * pf;
                z = posB.z + n.z * pf;
                if (x < dminx) dminx = x;
                if (z < dminz) dminz = z;
                if (x > dmaxx) dmaxx = x;
                if (z > dmaxz) dmaxz = z;

                x = posC.x + n.x * pf;
                z = posC.z + n.z * pf;
                if (x < dminx) dminx = x;
                if (z < dminz) dminz = z;
                if (x > dmaxx) dmaxx = x;
                if (z > dmaxz) dmaxz = z;

                // back
                x = posA.x + n.x * pb;
                z = posA.z + n.z * pb;
                if (x < dminx) dminx = x;
                if (z < dminz) dminz = z;
                if (x > dmaxx) dmaxx = x;
                if (z > dmaxz) dmaxz = z;

                x = posB.x + n.x * pb;
                z = posB.z + n.z * pb;
                if (x < dminx) dminx = x;
                if (z < dminz) dminz = z;
                if (x > dmaxx) dmaxx = x;
                if (z > dmaxz) dmaxz = z;

                x = posC.x + n.x * pb;
                z = posC.z + n.z * pb;
                if (x < dminx) dminx = x;
                if (z < dminz) dminz = z;
                if (x > dmaxx) dmaxx = x;
                if (z > dmaxz) dmaxz = z;
            }

            var posOffset = staticTform.position;
            DecalUtils.CachedTerrain cterrain;
            if (!DecalUtils.cachedTerrains.TryGetValue(terrainData, out cterrain))
            {
                cterrain = DecalUtils.PrepareTerrain(terrainData, posOffset);
            }

            int res = terrainData.heightmapResolution;
            int vertOffset = 0;
            int indexOffset = 0;

            float invScaleX = (res-1) / terrainData.size.x;
            float invScaleZ = (res-1) / terrainData.size.z;

            int tminx = (int)((dminx - posOffset.x) * invScaleX);
            int tminz = (int)((dminz - posOffset.z) * invScaleZ);
            int tmaxx = (int)((dmaxx - posOffset.x) * invScaleX) + 2;
            int tmaxz = (int)((dmaxz - posOffset.z) * invScaleZ) + 2;

            if (tminx >= res) return;
            if (tminz >= res) return;
            if (tmaxx <= 0) return;
            if (tmaxz <= 0) return;

            if (tminx < 0) tminx = 0;
            if (tminz < 0) tminz = 0;
            if (tmaxx > res) tmaxx = res;
            if (tmaxz > res) tmaxz = res;

            int patchResX = tmaxx - tminx;
            int patchResZ = tmaxz - tminz;

            staticPos = new Vector3[patchResX*patchResZ];
            staticNorm = new Vector3[patchResX*patchResZ];
            staticUV2 = new Vector2[patchResX*patchResZ];
            staticTris = new int[(patchResX-1) * (patchResZ-1) * 2 * 3];

            for (int z=0;z<patchResZ;z++)
            {
                int zoff = (tminz + z) * res;
                for (int x=0;x<patchResX;x++)
                {
                    int inIndex =  zoff + (tminx + x);
                    int outIndex = z * patchResX + x;
                    staticPos[outIndex] = cterrain.pos[inIndex];
                    staticNorm[outIndex] = cterrain.norm[inIndex];
                    staticUV2[outIndex] = cterrain.uv[inIndex];

                    if (x < patchResX-1 && z < patchResZ-1)
                    {
                        staticTris[indexOffset] = vertOffset;
                        staticTris[indexOffset + 1] = vertOffset + patchResX;
                        staticTris[indexOffset + 2] = vertOffset + patchResX + 1;

                        staticTris[indexOffset + 3] = vertOffset;
                        staticTris[indexOffset + 4] = vertOffset + patchResX + 1;
                        staticTris[indexOffset + 5] = vertOffset + 1;

                        indexOffset += 6;
                    }

                    vertOffset++;
                }
            }

            /*var mesh = new Mesh();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = staticPos;
            mesh.triangles = staticTris;
            mesh.normals = staticNorm;
            mesh.uv = staticUV2;
            mesh.uv2 = staticUV2;
            var terrGO = new GameObject();
            terrGO.name = "__ExportTerrain";
            GameObjectUtility.SetStaticEditorFlags(terrGO, StaticEditorFlags.LightmapStatic);
            var mf = terrGO.AddComponent<MeshFilter>();
            var mr = terrGO.AddComponent<MeshRenderer>();
            mf.sharedMesh = mesh;
            var unlitTerrainMat = new Material(Shader.Find("Hidden/ftUnlitTerrain"));
            mr.sharedMaterial = unlitTerrainMat;*/
        }
        else
#endif
        {
            staticPos = staticMesh.vertices;
            staticNorm = staticMesh.normals;
            staticTris = staticMesh.triangles;
            staticUV2 = staticMesh.uv2;
        }

        if (staticUV2.Length == 0)
        {
            staticUV2 = staticMesh.uv;
            if (staticUV2.Length == 0)
            {
                staticUV2 = new Vector2[staticNorm.Length];
            }
        }
        var origSMR = staticMR as SkinnedMeshRenderer;// parentObject.GetComponent<SkinnedMeshRenderer>();
        var staticSkin = staticMesh != null ? staticMesh.boneWeights : null;
        bool hasSkin = staticSkin != null && staticSkin.Length > 0 && origSMR != null;

        // Transform receiver to world space
        var numStaticVerts = staticPos.Length;
        var staticPosW = new Vector3[numStaticVerts];
        var staticNormW = new Vector3[numStaticVerts];
        bool isStaticFlipped;
        
        //time1.Start();

#if USE_TERRAINS
        if (terrain != null)
        {
            isStaticFlipped = false;
            staticPosW = staticPos;
        }
        else
#endif
        {
            isStaticFlipped = Mathf.Sign(staticTform.lossyScale.x*staticTform.lossyScale.y*staticTform.lossyScale.z) < 0;
            for(int i=0; i<numStaticVerts; i++)
            {
                staticPosW[i] = staticTform.TransformPoint(staticPos[i]);
            }
        }

        // Init temp data

        bool srcVcolor = decalColor != null && decalColor.Length > 0;
        bool useVColor = srcVcolor || decal.angleFade || opacity != 1.0f;
        Color colorA, colorB, colorC, white;
        colorA = colorB = colorC = white = Color.white;

        bool noUV = (decalUV == null || decalUV.Length == 0);
        var zero2 = Vector2.zero;

        int numDecalTris = decalTris.Length;
        int numStaticTris = staticTris.Length;

        // Matrix cull
        bool[] culled = null;
        if (earlyCull)
        {
            culled = new bool[numStaticTris];
            float minx, miny, minz;
            float maxx, maxy, maxz;
            const float eps = 0.0001f;

            float m00 = cullingMatrix[0,0];
            float m01 = cullingMatrix[0,1];
            float m02 = cullingMatrix[0,2];
            float m03 = cullingMatrix[0,3];

            float m10 = cullingMatrix[1,0];
            float m11 = cullingMatrix[1,1];
            float m12 = cullingMatrix[1,2];
            float m13 = cullingMatrix[1,3];

            float m20 = cullingMatrix[2,0];
            float m21 = cullingMatrix[2,1];
            float m22 = cullingMatrix[2,2];
            float m23 = cullingMatrix[2,3];

            Vector3 triA, triB, triC, point;

            for(int t2=0; t2<numStaticTris; t2 += 3)
            {
                //var triA = cullingMatrix.MultiplyPoint3x4(staticPosW[staticTris[t2]]);
                //var triB = cullingMatrix.MultiplyPoint3x4(staticPosW[staticTris[t2+1]]);
                //var triC = cullingMatrix.MultiplyPoint3x4(staticPosW[staticTris[t2+2]]);

                point = staticPosW[staticTris[t2]];
                triA.x = m00 * point.x + m01 * point.y + m02 * point.z + m03;
                triA.y = m10 * point.x + m11 * point.y + m12 * point.z + m13;
                triA.z = m20 * point.x + m21 * point.y + m22 * point.z + m23;

                point = staticPosW[staticTris[t2+1]];
                triB.x = m00 * point.x + m01 * point.y + m02 * point.z + m03;
                triB.y = m10 * point.x + m11 * point.y + m12 * point.z + m13;
                triB.z = m20 * point.x + m21 * point.y + m22 * point.z + m23;

                point = staticPosW[staticTris[t2+2]];
                triC.x = m00 * point.x + m01 * point.y + m02 * point.z + m03;
                triC.y = m10 * point.x + m11 * point.y + m12 * point.z + m13;
                triC.z = m20 * point.x + m21 * point.y + m22 * point.z + m23;


                minx = triA.x < triB.x ? triA.x : triB.x;
                minx = triC.x < minx ? triC.x : minx;

                miny = triA.y < triB.y ? triA.y : triB.y;
                miny = triC.y < miny ? triC.y : miny;

                minz = triA.z < triB.z ? triA.z : triB.z;
                minz = triC.z < minz ? triC.z : minz;


                maxx = triA.x > triB.x ? triA.x : triB.x;
                maxx = triC.x > maxx ? triC.x : maxx;

                maxy = triA.y > triB.y ? triA.y : triB.y;
                maxy = triC.y > maxy ? triC.y : maxy;

                maxz = triA.z > triB.z ? triA.z : triB.z;
                maxz = triC.z > maxz ? triC.z : maxz;

                minx -= eps;
                miny -= eps;
                minz -= eps;

                maxx += eps;
                maxy += eps;
                maxz += eps;

                bool boundsIntersect = 
                        (minx <= 1.0f) && (maxx >= -1.0f) &&
                        (miny <= 1.0f) && (maxy >= -1.0f) &&
                        (minz <= 1.0f) && (maxz >= -1.0f);

                culled[t2] = !boundsIntersect;
            }
        }

#if USE_TERRAINS
        if (terrain != null)
        {
            staticNormW = staticNorm;
        }
        else
#endif
        {
            for(int i=0; i<numStaticVerts; i++)
            {
                staticNormW[i] = staticTform.TransformDirection(staticNorm[i]);
                //if (staticIsFlipped) staticNormW[i] = -staticNormW[i];
            }
        }

        //time1.Stop();

        bool fixedArray = vPos != null;
        int vPosSize = 0;
        int fixedOffsetStart = fixedOffset;
        List<Vector3> newPos = null;
        List<Vector3> newNormal = null;
        List<Vector2> newUV = null;
        List<Vector2> newUV2 = null;
        List<BoneWeight> newSkin = null;
        List<Color> newColor = null;
        var newIndices = new List<int>();
        if (!fixedArray)
        {
            newPos = new List<Vector3>();
            newNormal = new List<Vector3>();
            //var newTangent = new List<Vector4>();
            newUV = new List<Vector2>();
            newUV2 = new List<Vector2>();
            newSkin = new List<BoneWeight>();
            if (useVColor) newColor = new List<Color>();
            newIndices = new List<int>();
        }
        else
        {
            vPosSize = vPos.Length;
        }

        //time1.Start();

        // Compute angle fading params
        float fadeMul = 0;
        float fadeAdd = 0;
        if (decal.angleFade)
        {
            fadeMul = 1.0f / (1.0f - decal.angleClip);
            fadeAdd = -decal.angleClip * fadeMul;
        }

        decal.avgDir = Vector3.zero;

        // Decal tri loop
        for(int t=0; t<numDecalTris; t += 3)
        {
            // Get decal tri pos and edge dirs
            var decalA = decalPosW[decalTris[t]];
            var decalB = decalPosW[decalTris[t + 1]];
            var decalC = decalPosW[decalTris[t + 2]];
            var decalTriVecAB = (decalB - decalA).normalized;
            var decalTriVecBC = (decalC - decalB).normalized;
            var decalTriVecCA = (decalA - decalC).normalized;

            // Create decal tri plane
            decalTriPlane.Set3Points(decalA, decalB, decalC);

            decal.avgDir += decalTriPlane.normal;

            // Create clipping planes from decal tri edges
            var cutNormalAB = Vector3.Cross(decalTriVecAB, decalTriPlane.normal);
            decalCutPlaneAB.SetNormalAndPosition(cutNormalAB, decalA);

            var cutNormalBC = Vector3.Cross(decalTriVecBC, decalTriPlane.normal);
            decalCutPlaneBC.SetNormalAndPosition(cutNormalBC, decalB);

            var cutNormalCA = Vector3.Cross(decalTriVecCA, decalTriPlane.normal);
            decalCutPlaneCA.SetNormalAndPosition(cutNormalCA, decalA);

            // Get decal tri UVs
            var decalUVA = noUV ? zero2 : decalUV[decalTris[t]];
            var decalUVB = noUV ? zero2 : decalUV[decalTris[t + 1]];
            var decalUVC = noUV ? zero2 : decalUV[decalTris[t + 2]];

            // Get decal tri vcolor
            if (srcVcolor)
            {
                decalColorA = decalColor[decalTris[t]];
                decalColorB = decalColor[decalTris[t + 1]];
                decalColorC = decalColor[decalTris[t + 2]];
            }

            // Receiver tri loop
            for(int t2=0; t2<numStaticTris; t2 += 3)
            {
                if (earlyCull)
                {
                    if (culled[t2]) continue;
                }

                // Get receiver tri positions
                int sa, sb, sc;
                if (isStaticFlipped)
                {
                    sa = t2+2;
                    sb = t2+1;
                    sc = t2;
                }
                else
                {
                    sa = t2;
                    sb = t2+1;
                    sc = t2+2;
                }

                var triA = staticPosW[staticTris[sa]];
                var triB = staticPosW[staticTris[sb]];
                var triC = staticPosW[staticTris[sc]];

                // Angle clip/fade
                if (decal.angleFade)
                {
                    splitTriNormal[0] = staticNormW[staticTris[sa]];
                    splitTriNormal[1] = staticNormW[staticTris[sb]];
                    splitTriNormal[2] = staticNormW[staticTris[sc]];
                    float d0 = Vector3.Dot(decalTriPlane.normal, splitTriNormal[0]);
                    float d1 = Vector3.Dot(decalTriPlane.normal, splitTriNormal[1]);
                    float d2 = Vector3.Dot(decalTriPlane.normal, splitTriNormal[2]);
                    if (d0 < decal.angleClip && d1 < decal.angleClip && d2 < decal.angleClip) continue;
                }
                else
                {
                    // For historical reasons non-fade version is using triangle normal
                    triPlane.Set3Points(triA, triB, triC);
                    if (Vector3.Dot(decalTriPlane.normal, triPlane.normal) < decal.angleClip)// 0.5f)
                    {
                        continue;
                    }
                }

                // Forward distance test (whole tri is beyond distance)
                if (decalTriPlane.GetDistanceToPoint(triA) < -decal.rayLength &&
                    decalTriPlane.GetDistanceToPoint(triB) < -decal.rayLength &&
                    decalTriPlane.GetDistanceToPoint(triC) < -decal.rayLength) continue;

                // Backwards distance test (whole tri is beyond distance)
                if (decalTriPlane.GetDistanceToPoint(triA) > decal.bias &&
                    decalTriPlane.GetDistanceToPoint(triB) > decal.bias &&
                    decalTriPlane.GetDistanceToPoint(triC) > decal.bias) continue;

                // Skip receiver tri if completely outside the decal tri
                if (decalCutPlaneAB.GetSide(triA) && decalCutPlaneAB.GetSide(triB) && decalCutPlaneAB.GetSide(triC)) continue;
                if (decalCutPlaneBC.GetSide(triA) && decalCutPlaneBC.GetSide(triB) && decalCutPlaneBC.GetSide(triC)) continue;
                if (decalCutPlaneCA.GetSide(triA) && decalCutPlaneCA.GetSide(triB) && decalCutPlaneCA.GetSide(triC)) continue;

                // Determine if it's fully inside too
                var inside = (!decalCutPlaneAB.GetSide(triA) && !decalCutPlaneAB.GetSide(triB) && !decalCutPlaneAB.GetSide(triC)) &&
                 (!decalCutPlaneBC.GetSide(triA) && !decalCutPlaneBC.GetSide(triB) && !decalCutPlaneBC.GetSide(triC)) &&
                 (!decalCutPlaneCA.GetSide(triA) && !decalCutPlaneCA.GetSide(triB) && !decalCutPlaneCA.GetSide(triC));

                 // Get receiver tri normal (if it wasn't alrady read)
                if (!decal.angleFade)
                {
                    splitTriNormal[0] = staticNormW[staticTris[sa]];
                    splitTriNormal[1] = staticNormW[staticTris[sb]];
                    splitTriNormal[2] = staticNormW[staticTris[sc]];
                }

                // Get other receiver tri attribs
                splitTriUV2[0] = staticUV2[staticTris[sa]];
                splitTriUV2[1] = staticUV2[staticTris[sb]];
                splitTriUV2[2] = staticUV2[staticTris[sc]];

                if (hasSkin)
                {
                    splitTriSkin[0] = staticSkin[staticTris[sa]];
                    splitTriSkin[1] = staticSkin[staticTris[sb]];
                    splitTriSkin[2] = staticSkin[staticTris[sc]];
                }

                if (inside)
                {
                    // Receiver tri is inside the decal tri - just copy

                    // Interpolate decal UV over receiver tri
                    var uvA = triLerp(decalUVA, decalUVB, decalUVC, decalA, decalB, decalC, triA);
                    var uvB = triLerp(decalUVA, decalUVB, decalUVC, decalA, decalB, decalC, triB);
                    var uvC = triLerp(decalUVA, decalUVB, decalUVC, decalA, decalB, decalC, triC);

                    if (useVColor)
                    {
                        if (srcVcolor)
                        {
                            // Interpolate decal vcolor over receiver tri
                            colorA = triLerp(decalColorA, decalColorB, decalColorC, decalA, decalB, decalC, triA) * opacity;
                            colorB = triLerp(decalColorA, decalColorB, decalColorC, decalA, decalB, decalC, triB) * opacity;
                            colorC = triLerp(decalColorA, decalColorB, decalColorC, decalA, decalB, decalC, triC) * opacity;
                        }
                        else
                        {
                            colorA = colorB = colorC = white * opacity;
                        }

                        if (decal.angleFade)
                        {
                            colorA *= Mathf.Clamp(Vector3.Dot(splitTriNormal[0], decalTriPlane.normal) * fadeMul + fadeAdd, 0.0f, 1.0f);
                            colorB *= Mathf.Clamp(Vector3.Dot(splitTriNormal[1], decalTriPlane.normal) * fadeMul + fadeAdd, 0.0f, 1.0f);
                            colorC *= Mathf.Clamp(Vector3.Dot(splitTriNormal[2], decalTriPlane.normal) * fadeMul + fadeAdd, 0.0f, 1.0f);
                        }
                    }

                    if (fixedArray)
                    {
                        if (fixedOffset + 3 >= vPosSize) fixedOffset = 0;

                        AddTriangle(ref fixedOffset, vPos, vNormal, vColor, vUV2, vUV, vSkin, vDecalID,
                                                                         triA, splitTriNormal[0], colorA, splitTriUV2[0], uvA, splitTriSkin[0],
                                                                         triB, splitTriNormal[1], colorB, splitTriUV2[1], uvB, splitTriSkin[1],
                                                                         triC, splitTriNormal[2], colorC, splitTriUV2[2], uvC, splitTriSkin[2], decalID);
                    }
                    else
                    {
                        newPos.Add(triA);
                        newPos.Add(triB);
                        newPos.Add(triC);

                        newNormal.Add(splitTriNormal[0]);
                        newNormal.Add(splitTriNormal[1]);
                        newNormal.Add(splitTriNormal[2]);

                        newUV2.Add(splitTriUV2[0]);
                        newUV2.Add(splitTriUV2[1]);
                        newUV2.Add(splitTriUV2[2]);

                        if (hasSkin)
                        {
                            newSkin.Add(splitTriSkin[0]);
                            newSkin.Add(splitTriSkin[1]);
                            newSkin.Add(splitTriSkin[2]);
                        }

                        if (useVColor)
                        {
                            newColor.Add(colorA);
                            newColor.Add(colorB);
                            newColor.Add(colorC);
                        }

                        newUV.Add(uvA);
                        newUV.Add(uvB);
                        newUV.Add(uvC);

                        newIndices.Add(newPos.Count - 3);
                        newIndices.Add(newPos.Count - 2);
                        newIndices.Add(newPos.Count - 1);
                    }
                }
                else
                {
                    // Receiver tri is cut by the decal tri

                    splitTriPos[0] = triA;
                    splitTriPos[1] = triB;
                    splitTriPos[2] = triC;

                    // Cut by AB
                    var cutPlane = decalCutPlaneAB;
                    var newPosSplitAB = new List<Vector3>();
                    var newNormalSplitAB = new List<Vector3>();
                    var newUV2SplitAB = new List<Vector2>();
                    List<BoneWeight> newSkinSplitAB = null;
                    if (hasSkin) newSkinSplitAB = new List<BoneWeight>();
                    var newIndicesSplitAB = new List<int>();
                    SplitTriangle(splitTriPos, splitTriNormal, splitTriUV, splitTriColor, splitTriUV2, (hasSkin?splitTriSkin:null), cutPlane, newPosSplitAB, newNormalSplitAB, null, null, newUV2SplitAB, newSkinSplitAB, newIndicesSplitAB);

                    // Cut by BC
                    cutPlane = decalCutPlaneBC;
                    var newPosSplitBC = new List<Vector3>();
                    var newNormalSplitBC = new List<Vector3>();
                    var newUV2SplitBC = new List<Vector2>();
                    List<BoneWeight> newSkinSplitBC = null;
                    if (hasSkin) newSkinSplitBC = new List<BoneWeight>();
                    var newIndicesSplitBC = new List<int>();
                    int newIndicesSplitABCount = newIndicesSplitAB.Count;
                    for(int t3=0; t3<newIndicesSplitABCount; t3 += 3)
                    {
                        splitTriPos[0] = newPosSplitAB[newIndicesSplitAB[t3]];
                        splitTriPos[1] = newPosSplitAB[newIndicesSplitAB[t3 + 1]];
                        splitTriPos[2] = newPosSplitAB[newIndicesSplitAB[t3 + 2]];

                        splitTriNormal[0] = newNormalSplitAB[newIndicesSplitAB[t3]];
                        splitTriNormal[1] = newNormalSplitAB[newIndicesSplitAB[t3 + 1]];
                        splitTriNormal[2] = newNormalSplitAB[newIndicesSplitAB[t3 + 2]];

                        splitTriUV2[0] = newUV2SplitAB[newIndicesSplitAB[t3]];
                        splitTriUV2[1] = newUV2SplitAB[newIndicesSplitAB[t3 + 1]];
                        splitTriUV2[2] = newUV2SplitAB[newIndicesSplitAB[t3 + 2]];

                        if (hasSkin)
                        {
                            splitTriSkin[0] = newSkinSplitAB[newIndicesSplitAB[t3]];
                            splitTriSkin[1] = newSkinSplitAB[newIndicesSplitAB[t3 + 1]];
                            splitTriSkin[2] = newSkinSplitAB[newIndicesSplitAB[t3 + 2]];
                        }

                        SplitTriangle(splitTriPos, splitTriNormal, splitTriUV, splitTriColor, splitTriUV2, (hasSkin?splitTriSkin:null), cutPlane, newPosSplitBC, newNormalSplitBC, null, null, newUV2SplitBC, newSkinSplitBC, newIndicesSplitBC);
                    }

                    // Cut by CA
                    cutPlane = decalCutPlaneCA;
                    var newUVStart = fixedArray ? fixedOffset : newUV.Count;
                    int newIndicesSplitBCCount = newIndicesSplitBC.Count;
                    for(int t3=0; t3<newIndicesSplitBCCount; t3 += 3)
                    {
                        splitTriPos[0] = newPosSplitBC[newIndicesSplitBC[t3]];
                        splitTriPos[1] = newPosSplitBC[newIndicesSplitBC[t3 + 1]];
                        splitTriPos[2] = newPosSplitBC[newIndicesSplitBC[t3 + 2]];

                        splitTriNormal[0] = newNormalSplitBC[newIndicesSplitBC[t3]];
                        splitTriNormal[1] = newNormalSplitBC[newIndicesSplitBC[t3 + 1]];
                        splitTriNormal[2] = newNormalSplitBC[newIndicesSplitBC[t3 + 2]];

                        splitTriUV2[0] = newUV2SplitBC[newIndicesSplitBC[t3]];
                        splitTriUV2[1] = newUV2SplitBC[newIndicesSplitBC[t3 + 1]];
                        splitTriUV2[2] = newUV2SplitBC[newIndicesSplitBC[t3 + 2]];

                        if (hasSkin)
                        {
                            splitTriSkin[0] = newSkinSplitBC[newIndicesSplitBC[t3]];
                            splitTriSkin[1] = newSkinSplitBC[newIndicesSplitBC[t3 + 1]];
                            splitTriSkin[2] = newSkinSplitBC[newIndicesSplitBC[t3 + 2]];
                        }

                        if (fixedArray)
                        {
                            SplitTriangle2(splitTriPos, splitTriNormal, splitTriUV, splitTriColor, splitTriUV2, (hasSkin?splitTriSkin:null), cutPlane, decalID, ref fixedOffset, vPos, vNormal, vUV, vColor, vUV2, vSkin, vDecalID);
                        }
                        else
                        {
                            SplitTriangle(splitTriPos, splitTriNormal, splitTriUV, splitTriColor, splitTriUV2, (hasSkin?splitTriSkin:null), cutPlane, newPos, newNormal, newUV, newColor, newUV2, newSkin, newIndices);
                        }
                    }

                    // Interpolate decal attribs over new tris
                    int newUVCount = fixedArray ? fixedOffset : newUV.Count;
                    for(int u=newUVStart; u<newUVCount; u++)
                    {
                        var wpos = fixedArray ? vPos[u] : newPos[u];
                        var lerpUV = triLerp(decalUVA, decalUVB, decalUVC, decalA, decalB, decalC, wpos);
                        if (fixedArray)
                        {
                            vUV[u] = lerpUV;
                        }
                        else
                        {
                            newUV[u] = lerpUV;
                        }
                        if (useVColor)
                        {
                            Color lerpColor;
                            if (srcVcolor)
                            {
                                lerpColor = triLerp(decalColorA, decalColorB, decalColorC, decalA, decalB, decalC, wpos) * opacity;
                            }
                            else
                            {
                                lerpColor = white;
                            }

                            if (decal.angleFade)
                            {
                                lerpColor *= Mathf.Clamp(Vector3.Dot(fixedArray ? vNormal[u] : newNormal[u], decalTriPlane.normal) * fadeMul + fadeAdd, 0.0f, 1.0f) * opacity;
                            }

                            if (fixedArray)
                            {
                                vColor[u] = lerpColor;
                            }
                            else
                            {
                                newColor[u] = lerpColor;
                            }
                        }
                    }
                }
            }
        }

        decal.avgDir = decal.avgDir.normalized;

        //Debug.LogError("Algorithm time: " + time1.Elapsed.TotalMilliseconds);

        // Create decal mesh
        if (newMesh == null)
        {
            if (newPos.Count == 0)
            {
                Debug.Log("No mesh was created for " + decal.name + " / " + parentObject);
                
                return;
            }

            newMesh = new Mesh();
            if (hasSkin)
            {
                var worldToRoot = staticTform;//origSMR.rootBone;
                int numVerts = newPos.Count;
                for(int v=0; v<numVerts; v++)
                {
                    newPos[v] = worldToRoot.InverseTransformPoint(newPos[v]);
                    newNormal[v] = worldToRoot.InverseTransformDirection(newNormal[v]);
                }
            }
    #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                if (decal.optimize)
                {
                    var newPos2 = new List<Vector3>();
                    var newNormal2 = new List<Vector3>();
                    var newUV_2 = new List<Vector2>();
                    var newUV2_2 = new List<Vector2>();
                    var newSkin2 = new List<BoneWeight>();
                    var newColor2 = new List<Color>();
                    var newIndices2 = new List<int>();
                    WeldVerts(newIndices, newPos, newNormal, newUV, newUV2, newSkin, newColor,
                              newIndices2, newPos2, newNormal2, newUV_2, newUV2_2, newSkin2, newColor2);
                    newPos = newPos2;
                    newNormal = newNormal2;
                    newUV = newUV_2;
                    newUV2 = newUV2_2;
                    newSkin = newSkin2;
                    newColor = newColor2;
                    newIndices = newIndices2;
                }
            }
    #endif
            newMesh.vertices = newPos.ToArray();
            newMesh.normals = newNormal.ToArray();
            newMesh.uv = newUV.ToArray();
            newMesh.uv2 = newUV2.ToArray();
            if (hasSkin) newMesh.boneWeights = newSkin.ToArray();
            if (useVColor) newMesh.colors = newColor.ToArray();
            newMesh.triangles = newIndices.ToArray();

            if (decal.tangents) newMesh.RecalculateTangents();

    #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                if (decal.optimize)
                {
                    MeshUtility.Optimize(newMesh);
                }
            }
    #endif

            var newGO = new GameObject();
            newGO.tag = decal.gameObject.tag;// "Optimizable";
    #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                if (newGO.scene != decal.gameObject.scene) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(newGO, decal.gameObject.scene);
                decal.sceneObjects.Add(newGO);
                EditorUtility.SetDirty(decal);
                if (!decal.linkToParent) GameObjectUtility.SetStaticEditorFlags(newGO, StaticEditorFlags.BatchingStatic);
            }
    #endif

            newGO.name = "NEW_DECAL#" + decal.name+"#"+parentObject.name;
            GameObject existingPrefab = null;

            if (decal.linkToParent)
            {
    #if UNITY_EDITOR
                if (checkPrefabs)
                {
                    var existingT = parentObject.transform.Find(newGO.name);
                    if (existingT != null) existingPrefab = existingT.gameObject;
                }
    #endif
                newGO.transform.parent = parentObject.transform;
            }
            else if (decal.linkToDecalGroup)
            {
                var temp = new GameObject();
                temp.name = "NEW_DECAL#parent";
                var decalTform2 = decal.transform;
                var tempTform = temp.transform;
                tempTform.parent = decalTform2;
                tempTform.localPosition = Vector3.zero;
                tempTform.localRotation = Quaternion.identity;
                var scl = decalTform2.localScale;
                tempTform.localScale = new Vector3(1.0f/scl.x, 1.0f/scl.y, 1.0f/scl.z);
                newGO.transform.parent = tempTform;
            }

            Renderer newMR = null;
            newGO.layer = decal.gameObject.layer;

            if (hasSkin)
            {
                var smr = newGO.AddComponent<SkinnedMeshRenderer>();
                smr.sharedMaterial = optionalMateralChange != null ? optionalMateralChange : sharedMaterial;
                smr.bones = origSMR.bones;
                newMesh.bindposes = origSMR.sharedMesh.bindposes;
                smr.sharedMesh = newMesh;
                smr.rootBone = origSMR.rootBone;
                newMR = smr;
            }
            else
            {
                var newMF = newGO.AddComponent<MeshFilter>();
                newMF.sharedMesh = newMesh;
                newMR = newGO.AddComponent<MeshRenderer>();
                newMR.sharedMaterial = optionalMateralChange != null ? optionalMateralChange : sharedMaterial;
            }

#if UNITY_EDITOR
            if (decal.makeAsset)
            {
                var activeScene = EditorSceneManager.GetActiveScene();
                newMesh.name = activeScene.name + "_" + newGO.name;

                var outDir = Application.dataPath + "/DecaleryMeshes";
                if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

                var outPath = "Assets/DecaleryMeshes/" + newMesh.name + ".asset";
                if (File.Exists(outPath)) ValidateFileAttribs(outPath);
                AssetDatabase.CreateAsset(newMesh, outPath);
                AssetDatabase.SaveAssets();
            }
#endif

#if USE_TERRAINS
            if (terrain != null)
            {
                newMR.lightmapIndex = terrain.lightmapIndex;
                newMR.lightmapScaleOffset = terrain.lightmapScaleOffset;
                newMR.realtimeLightmapIndex = terrain.realtimeLightmapIndex;
                newMR.realtimeLightmapScaleOffset = terrain.realtimeLightmapScaleOffset;
            }
            else
#endif
            {
                // Legacy
                if (decal.surfaceReplacement != null)
                {
                    for(int s=0; s<decal.surfaceReplacement.Length; s++)
                    {
                        if (decal.surfaceReplacement[s] == staticMR.sharedMaterial)
                        {
                            newMR.sharedMaterial = decal.surfaceReplacementMaterial[s];
                            break;
                        }
                    }
                }

                newMR.lightmapIndex = staticMR.lightmapIndex;
                newMR.lightmapScaleOffset = staticMR.lightmapScaleOffset;
                newMR.realtimeLightmapIndex = staticMR.realtimeLightmapIndex;
                newMR.realtimeLightmapScaleOffset = staticMR.realtimeLightmapScaleOffset;

    #if UNITY_2018_1_OR_NEWER
                if (staticMR.HasPropertyBlock())
                {
                    var mb = new MaterialPropertyBlock();
                    staticMR.GetPropertyBlock(mb);
                    newMR.SetPropertyBlock(mb);
                }
    #else
                var mb = new MaterialPropertyBlock();
                staticMR.GetPropertyBlock(mb);
                newMR.SetPropertyBlock(mb);
    #endif
            }

#if BAKERY_INCLUDED
            // Bakery data patching
            var storageGOUnused = GameObject.Find("!decalLightmaps");
            if (storageGOUnused != null)
            {
                DestroyImmediate(storageGOUnused);
            }

            ftLightmapsStorage storage = null;

    #if UNITY_EDITOR
            GameObject prefabRoot = null;
            if (checkPrefabs && decal.linkToParent)
            {
#if USE_TERRAINS
                GameObject staticGO = terrain != null ? terrain.gameObject : staticMR.gameObject;
#else
                GameObject staticGO = staticMR.gameObject;
#endif
                var ptype = PrefabUtility.GetPrefabType(staticGO);
                if (ptype == PrefabType.PrefabInstance)
                {
                    var proot = staticGO.GetComponentInParent<BakeryLightmappedPrefab>();
                    if (proot != null)
                    {
                        var pstorageGO = proot.transform.Find("BakeryPrefabLightmapData");
                        if (pstorageGO != null)
                        {
                            storage = pstorageGO.GetComponent<ftLightmapsStorage>();
                            if (storage != null)
                            {
                                if (EditorUtility.DisplayDialog("Decalery", "Are you sure you want to apply this decal to a Lightmapped Prefab? All overrides of this prefab instance will be applied.", "Apply to prefab", "Apply to scene"))
                                {
                                    prefabRoot = proot.gameObject;

                                    if (existingPrefab != null)
                                    {
                                        var exMR = existingPrefab.GetComponent<Renderer>();
                                        var exMF = existingPrefab.GetComponent<MeshFilter>();

                                        if (exMR != null) exMR.sharedMaterial = newMR.sharedMaterial;
                                        if (exMF != null) exMF.sharedMesh = newMesh;

                                        DestroyImmediate(newMR.gameObject);

                                        newMR = exMR;
                                    }
                                }
                            }
                        }
                    }
                }
            }
    #endif

            if (storage == null)
            {
                var storageGO = GameObject.Find("!ftraceLightmaps");
                if (storageGO == null)
                {
                    storageGO = new GameObject();
                    storageGO.name = "!ftraceLightmaps";
                    storageGO.hideFlags = HideFlags.HideInHierarchy;
                }
                storage = storageGO.GetComponent<ftLightmapsStorage>();
                if (storage == null)
                {
                    storage = storageGO.AddComponent<ftLightmapsStorage>();
                }
            }

            if (storage != null && storage.bakedRenderers != null && storage.bakedIDs != null && storage.bakedRenderers.Count > 0)
            {
                int lightmapIndex = newMR.lightmapIndex;
                var staticIndex = storage.bakedRenderers.LastIndexOf(staticMR);
                if (staticIndex >= 0) lightmapIndex = storage.bakedIDs[staticIndex];

                storage.bakedRenderers.Add(newMR);
                storage.bakedIDs.Add(lightmapIndex);
                storage.bakedScaleOffset.Add(newMR.lightmapScaleOffset);
        #if UNITY_EDITOR
                storage.bakedVertexOffset.Add(-1);
                storage.bakedVertexColorMesh.Add(null);

        #if UNITY_EDITOR
                if (prefabRoot != null) PrefabUtility.ReplacePrefab(prefabRoot, PrefabUtility.GetPrefabParent(prefabRoot), ReplacePrefabOptions.ConnectToPrefab);
        #endif

                if (!Application.isPlaying)
                {
                    EditorUtility.SetDirty(storage);
                    EditorSceneManager.MarkAllScenesDirty();
                }
        #endif
            }
#else
            // Stripped-down version of Bakery's lightmaps storage
            var storageGO = GameObject.Find("!decalLightmaps");
            if (storageGO == null)
            {
                storageGO = new GameObject();
                storageGO.name = "!decalLightmaps";
                storageGO.hideFlags = HideFlags.HideInHierarchy;
            }
            var storage = storageGO.GetComponent<DecalLightmapsStorage>();
            if (storage == null)
            {
                storage = storageGO.AddComponent<DecalLightmapsStorage>();
            }

            int lightmapIndex = newMR.lightmapIndex;
            if (lightmapIndex >= 0)
            {
                var lightmaps = LightmapSettings.lightmaps;
                if (lightmaps != null && lightmaps.Length > lightmapIndex)
                {
                    var ldata = lightmaps[lightmapIndex];
                    if (ldata.lightmapColor != null)
                    {
                        if (storage.maps == null) storage.maps = new List<Texture2D>();
                        if (storage.dirMaps == null) storage.dirMaps = new List<Texture2D>();
                        if (storage.masks == null) storage.masks = new List<Texture2D>();
                        int existingIndex = storage.maps.IndexOf(ldata.lightmapColor);
                        if (existingIndex < 0)
                        {
                            existingIndex = storage.maps.Count;
                            storage.maps.Add(ldata.lightmapColor);
                            storage.dirMaps.Add(ldata.lightmapDir);
                            storage.masks.Add(ldata.shadowMask);
                        }
                        storage.bakedRenderers.Add(newMR);
                        storage.bakedIDs.Add(existingIndex);
                        storage.bakedScaleOffset.Add(newMR.lightmapScaleOffset);
                    }
                }
            }

        #if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    EditorUtility.SetDirty(storage);
                    EditorSceneManager.MarkAllScenesDirty();
                }
        #endif

#endif
    
        }
        else
        {
            // Existing mesh
            int start, end;
            byte refValue = vDecalID[fixedOffset];
            if (refValue != 0)
            {
                // Clear tail
                start = fixedOffset;
                end = fixedOffset + maxTrisInDecal;
                float nan = 1.0f / 0.0f;
                var nan3 = new Vector3(nan, nan, nan);
                for(int i=start; i<end; i++)
                {
                    int index = i;
                    if (index >= vPosSize) index = i % vPosSize;
                    if (vDecalID[index] == refValue)
                    {
                        vPos[index] = nan3;
                    }
                    else
                    {
                        break;
                    }
                }
            }
            start = fixedOffsetStart;
            end = fixedOffset >= fixedOffsetStart ?  fixedOffset : (vPosSize + fixedOffset); // overshoot because we wrap in the loop
            if (transformToParent)
            {
                for(int i=start; i<end; i++)
                {
                    int index = i;
                    if (index >= vPosSize) index = i % vPosSize;
                    vPos[index] = staticTform.InverseTransformPoint(vPos[index]);
                    vNormal[index] = staticTform.InverseTransformDirection(vNormal[index]);
                }
            }
            if (transformUV1)
            {
                for(int i=start; i<end; i++)
                {
                    int index = i;
                    if (index >= vPosSize) index = i % vPosSize;
                    vUV2[index] = new Vector2(vUV2[index].x * lightmapScaleOffset.x + lightmapScaleOffset.z, vUV2[index].y * lightmapScaleOffset.y + lightmapScaleOffset.w);
                }
            }
            newMesh.vertices = vPos;
            newMesh.normals = vNormal;
            newMesh.uv = vUV;
            newMesh.uv2 = vUV2;
            if (hasSkin) newMesh.boneWeights = vSkin;
            if (useVColor) newMesh.colors = vColor;

            if (decal.tangents)
            {
                Vector3 a, b, c, edge1, edge2, tangent, binormal;
                Vector2 ta, tb, tc, tedge1, tedge2;
                float mul, w;
                for(int i=start; i<end; i+=3)
                {
                    int index = i;
                    if (index >= vPosSize) index = i % vPosSize;
                    a = vPos[index];
                    if (float.IsNaN(a.x)) continue;
                    b = vPos[index+1];
                    c = vPos[index+2];
                    ta = vUV[index];
                    tb = vUV[index+1];
                    tc = vUV[index+2];
                    edge1 = b - a;
                    edge2 = c - a;
                    tedge1 = tb - ta;
                    tedge2 = tc - ta;
                    mul = 1.0f / (tedge1.x * tedge2.y - tedge2.x * tedge1.y);
                    tangent =  ((tedge2.y * edge1 - tedge1.y * edge2) * mul).normalized;
                    binormal = ((tedge1.x * edge2 - tedge2.x * edge1) * mul).normalized;
                    w = (Vector3.Dot(Vector3.Cross(vNormal[index], tangent), binormal) < 0.0f) ? -1.0f : 1.0f;
                    vTangents[index] = vTangents[index+1] = vTangents[index+2] = new Vector4(tangent.x, tangent.y, tangent.z, w);
                }
                newMesh.tangents = vTangents;
            }
        }

    }

    // Update decal geometry from DecalGroup component.
    public static void UpdateDecal(DecalGroup decal)
    {

#if UNITY_EDITOR
        // Used for clearing decals later
        if (decal.sceneObjects == null) decal.sceneObjects = new List<GameObject>();
        for(int i=0; i<decal.sceneObjects.Count; i++)
        {
            if (decal.sceneObjects[i] == null)
            {
                decal.sceneObjects.RemoveAt(i);
                i--;
            }
        }
        decal.originalName = decal.name;
#endif

        // Get decal components
        var go = decal.gameObject;

        var decalMf = go.GetComponent<MeshFilter>();
        if (decalMf == null)
        {
            Debug.LogError("No mesh on " + decal.name);
            return;
        }

        if (go.GetComponent<MeshFilter>() == null)
        {
            go.AddComponent<MeshFilter>();
        }
        var mr = go.GetComponent<MeshRenderer>();
        if (mr == null)
        {
            mr = go.AddComponent<MeshRenderer>();
        }

        RaycastHit hit;

        var decalMesh = decalMf.sharedMesh;
        var decalTform = go.transform;
        if (decalMesh == null) Debug.LogError("No decalmesh in " + decalMf.name);
        var decalTris = decalMesh.triangles;
        var decalPos = decalMesh.vertices;
        var decalPosW = new Vector3[decalPos.Length];
        var decalUV = decalMesh.uv;
        var decalColor = decalMesh.colors;

        // Transform decal to world space
        for(int i=0; i<decalPos.Length; i++)
        {
            decalPosW[i] = decalTform.TransformPoint(decalPos[i]);
        }

        // Collect receivers
        var parents = new List<GameObject>();
        List<Material> materialOverrides = null;
        int layersUsed = 0;
        if (decal.pickParentWithRaycast)
        {
            var decalNorm = decalMesh.normals;
            for(int i=0; i<decalPos.Length; i++)
            {
                if (Physics.Raycast(decalPosW[i], -decalTform.TransformDirection(decalNorm[i]), out hit, decal.rayLength))
                {
                    var prt = hit.collider.gameObject;
                    if (!parents.Contains(prt))
                    {
                        layersUsed |= 1 << prt.layer;
                        parents.Add(prt);
                    }
                }
            }
        }
        else if (decal.pickParentWithBox)
        {
            var size = mr.bounds.extents * 2 * decal.boxScale;//*0.5f;
            size = new Vector3(Mathf.Max(size.x, decal.rayLength), Mathf.Max(size.y, decal.rayLength), Mathf.Max(size.z, decal.rayLength));
            var colliders = Physics.OverlapBox(mr.bounds.center, size);
            for(int i=0; i<colliders.Length; i++)
            {
                var prt = colliders[i].gameObject;
                if (!parents.Contains(prt))
                {
                    layersUsed |= 1 << prt.layer;
                    parents.Add(prt);
                }
            }
        }
        else if (decal.parentObject != null)
        {
            layersUsed |= 1 << decal.parentObject.layer;
            parents.Add(decal.parentObject);
            if (decal.parentObjectsAdditional != null)
            {
                materialOverrides = new List<Material>();
                materialOverrides.Add(decal.optionalMateralChange);
                for(int di=0; di<decal.parentObjectsAdditional.Count; di++)
                {
                    if (decal.parentObjectsAdditional[di] == null) continue;
                    layersUsed |= 1 << decal.parentObjectsAdditional[di].layer;
                    parents.Add(decal.parentObjectsAdditional[di]);
                    materialOverrides.Add(decal.optionalMateralChangeAdditional[di]);
                }
            }
        }

        // Filter receivers
        if (parents.Count > 0 && (decal.layerMask & layersUsed) != layersUsed)
        {
            var filtered = new List<GameObject>();
            for(int i=0; i<parents.Count; i++)
            {
                if (((1 << parents[i].layer) & decal.layerMask.value) != 0)
                {
                    filtered.Add(parents[i]);
                }
            }
            parents = filtered;
        }

        if (parents.Count == 0)
        {
            Debug.LogError("Parent object is not set for decal " + decal.name);
            return;
        }

        int offset = 0;
        for(int i=0; i<parents.Count; i++)
        {
            //var time1 = new System.Diagnostics.Stopwatch();
            //time1.Start();
            UpdateDecalFor(decal, mr.sharedMaterial, decalUV, decalPosW, decalTform, decalTris, decalColor, parents[i], materialOverrides == null ? decal.optionalMateralChange : materialOverrides[i], 1.0f, ref offset);
            //Debug.LogError("Generation time (old): " + time1.Elapsed.TotalMilliseconds);
        }
    }

    // Old list function
    static void SplitTriangle(Vector3[] triPos, Vector3[] triNormal, Vector2[] triUV, Color[] triColor, Vector2[] triUV2, BoneWeight[] triSkin, Plane plane, List<Vector3> newPos, List<Vector3> newNormal, List<Vector2> newUV, List<Color> newColor, List<Vector2> newUV2, List<BoneWeight> newSkin, List<int> newIndices)
    {
        bool useVColor = newColor != null;
        bool useUV = newUV != null;

        var Ap = triPos[0];
        var Bp = triPos[1];
        var Cp = triPos[2];

        var An = triNormal[0];
        var Bn = triNormal[1];
        var Cn = triNormal[2];

        //var AT = triTangent[0];
        //var BT = triTangent[1];
        //var CT = triTangent[2];

        var At = triUV[0];
        var Bt = triUV[1];
        var Ct = triUV[2];

        var At2 = triUV2[0];
        var Bt2 = triUV2[1];
        var Ct2 = triUV2[2];

        BoneWeight Ab = tmpWeight;
        BoneWeight Bb = tmpWeight;
        BoneWeight Cb = tmpWeight;
        bool hasSkin = triSkin != null;
        if (hasSkin)
        {
            Ab = triSkin[0];
            Bb = triSkin[1];
            Cb = triSkin[2];
        }

        var Ac = triColor[0];
        var Bc = triColor[1];
        var Cc = triColor[2];

        float pa = plane.GetDistanceToPoint(Ap);
        float pb = plane.GetDistanceToPoint(Bp);
        float pc = plane.GetDistanceToPoint(Cp);

        int A,B,C,D,E;
        Vector3 dp,ep;

        float signA = Mathf.Sign(pa);
        float signB = Mathf.Sign(pb);
        float signC = Mathf.Sign(pc);

        bool arg1 = ( (signA == signB) && (signB == signC) );
        bool arg2 = ( (signB == signC) && (pa == 0) );
        bool arg3 = ( (signB == signA) && (pc == 0) );
        bool arg4 = ( (signA == signC) && (pb == 0) );
        if (arg1||arg2||arg3||arg4)
        {
            if (pa>0.0f)
            {
                // surf a
            }
            else
            {
                // surf b
                A = newPos.Count;
                newPos.Add(Ap);
                newNormal.Add(An);
                //newTangent.Add(AT);
                if (useVColor) newColor.Add(Ac);
                newUV2.Add(At2);
                if (useUV) newUV.Add((At));// - surfbUVoffset);

                B = newPos.Count;
                newPos.Add(Bp);
                newNormal.Add(Bn);
                //newTangent.Add(BT);
                if (useVColor) newColor.Add(Bc);
                newUV2.Add(Bt2);
                if (useUV) newUV.Add((Bt));// - surfbUVoffset);

                C = newPos.Count;
                newPos.Add(Cp);
                newNormal.Add(Cn);
                //newTangent.Add(CT);
                if (useVColor) newColor.Add(Cc);
                newUV2.Add(Ct2);
                if (useUV) newUV.Add((Ct));// - surfbUVoffset);

                if (hasSkin)
                {
                    newSkin.Add(Ab);
                    newSkin.Add(Bb);
                    newSkin.Add(Cb);
                }

                newIndices.Add(A);
                newIndices.Add(B);
                newIndices.Add(C);
            }
            return;
        }

        if (signA == signB) //AB|C
        {
            float dist;
            var rayAC = new Ray(Ap, (Cp-Ap).normalized);
            plane.Raycast(rayAC, out dist);
            dp = rayAC.origin + rayAC.direction * dist;
            var di = saturate(dist / (Cp-Ap).magnitude);
            dp = Vector3.Lerp(Ap, Cp, di);
            var dn = Vector3.Lerp(An, Cn, di);
            //var dT = Vector3.Lerp(AT, CT, di);
            var dt = Vector2.Lerp(At, Ct, di);
            var dt2 = Vector2.Lerp(At2, Ct2, di);
            BoneWeight db = tmpWeight;
            if (hasSkin) db = LerpBoneWeights(Ab, Cb, di);
            var dc = Color.Lerp(Ac, Cc, di);

            var rayBC = new Ray(Bp, (Cp-Bp).normalized);
            plane.Raycast(rayBC, out dist);
            ep = rayBC.origin + rayBC.direction * dist;
            var ei = saturate(dist / (Cp-Bp).magnitude);
            ep = Vector3.Lerp(Bp, Cp, ei);
            var en = Vector3.Lerp(Bn, Cn, ei);
            //var eT = Vector3.Lerp(BT, CT, ei);
            var et = Vector2.Lerp(Bt, Ct, ei);
            var et2 = Vector2.Lerp(Bt2, Ct2, ei);
            BoneWeight eb = tmpWeight;
            if (hasSkin) eb = LerpBoneWeights(Bb, Cb, ei);
            var ec = Color.Lerp(Bc, Cc, ei);


            // surf a
            A = newPos.Count;
            newPos.Add(Ap);
            newNormal.Add(An);
            //newTangent.Add(AT);
            if (useVColor) newColor.Add(Ac);
            newUV2.Add(At2);
            if (useUV) newUV.Add((At));// - surfUVoffset[surfa]);

            B = newPos.Count;
            newPos.Add(Bp);
            newNormal.Add(Bn);
            //newTangent.Add(BT);
            if (useVColor) newColor.Add(Bc);
            newUV2.Add(Bt2);
            if (useUV) newUV.Add((Bt));// - surfUVoffset[surfa]);

            // surf b
            C = newPos.Count;
            newPos.Add(Cp);
            newNormal.Add(Cn);
            //newTangent.Add(CT);
            if (useVColor) newColor.Add(Cc);
            newUV2.Add(Ct2);
            if (useUV) newUV.Add((Ct));// - surfUVoffset[surfb]);

            // surf a
            D = newPos.Count;
            newPos.Add(dp);
            newNormal.Add(dn);
            //newTangent.Add(dT);
            if (useVColor) newColor.Add(dc);
            newUV2.Add(dt2);
            if (useUV) newUV.Add((dt));// - surfUVoffset[surfa]);

            E = newPos.Count;
            newPos.Add(ep);
            newNormal.Add(en);
            //newTangent.Add(eT);
            if (useVColor) newColor.Add(ec);
            newUV2.Add(et2);
            if (useUV) newUV.Add((et));// - surfUVoffset[surfa]);

            if (hasSkin)
            {
                newSkin.Add(Ab);
                newSkin.Add(Bb);
                newSkin.Add(Cb);
                newSkin.Add(db);
                newSkin.Add(eb);
            }

            if (pa<0.0f)
            {
                newIndices.Add(D);
                newIndices.Add(A);
                newIndices.Add(B);

                newIndices.Add(D);
                newIndices.Add(B);
                newIndices.Add(E);
            }

            // surf b
            D = newPos.Count;
            newPos.Add(dp);
            newNormal.Add(dn);
            //newTangent.Add(dT);
            if (useVColor) newColor.Add(dc);
            newUV2.Add(dt2);
            if (useUV) newUV.Add((dt));// - surfUVoffset[surfb]);

            E = newPos.Count;
            newPos.Add(ep);
            newNormal.Add(en);
            //newTangent.Add(eT);
            if (useVColor) newColor.Add(ec);
            newUV2.Add(et2);
            if (useUV) newUV.Add((et));// - surfUVoffset[surfb]);

            if (hasSkin)
            {
                newSkin.Add(db);
                newSkin.Add(eb);
            }

            if (pa>0.0f)
            {
                newIndices.Add(C);
                newIndices.Add(D);
                newIndices.Add(E);
            }

            return;
        }

        if (signA == signC) //AC|B
        {
            float dist;
            var rayAB = new Ray(Ap, (Bp-Ap).normalized);
            plane.Raycast(rayAB, out dist);
            dp = rayAB.origin + rayAB.direction * dist;
            var di = saturate(dist / (Bp-Ap).magnitude);
            dp = Vector3.Lerp(Ap, Bp, di);
            var dn = Vector3.Lerp(An, Bn, di);
            //var dT = Vector3.Lerp(AT, BT, di);
            var dt = Vector2.Lerp(At, Bt, di);
            var dt2 = Vector2.Lerp(At2, Bt2, di);
            BoneWeight db = tmpWeight;
            if (hasSkin) db = LerpBoneWeights(Ab, Bb, di);
            var dc = Color.Lerp(Ac, Bc, di);

            var rayBC = new Ray(Bp, (Cp-Bp).normalized);
            plane.Raycast(rayBC, out dist);
            ep = rayBC.origin + rayBC.direction * dist;
            var ei = saturate(dist / (Cp-Bp).magnitude);
            ep = Vector3.Lerp(Bp, Cp, ei);
            var en = Vector3.Lerp(Bn, Cn, ei);
            //var eT = Vector3.Lerp(BT, CT, ei);
            var et = Vector2.Lerp(Bt, Ct, ei);
            var et2 = Vector2.Lerp(Bt2, Ct2, ei);
            BoneWeight eb = tmpWeight;
            if (hasSkin) eb = LerpBoneWeights(Bb, Cb, ei);
            var ec = Color.Lerp(Bc, Cc, ei);

            // surf a
            A = newPos.Count;
            newPos.Add(Ap);
            newNormal.Add(An);
            //newTangent.Add(AT);
            if (useVColor) newColor.Add(Ac);
            newUV2.Add(At2);
            if (useUV) newUV.Add((At));// - surfUVoffset[surfa]);

            // surf b
            B = newPos.Count;
            newPos.Add(Bp);
            newNormal.Add(Bn);
            //newTangent.Add(BT);
            if (useVColor) newColor.Add(Bc);
            newUV2.Add(Bt2);
            if (useUV) newUV.Add((Bt));// - surfUVoffset[surfb]);

            // surf a
            C = newPos.Count;
            newPos.Add(Cp);
            newNormal.Add(Cn);
            //newTangent.Add(CT);
            if (useVColor) newColor.Add(Cc);
            newUV2.Add(Ct2);
            if (useUV) newUV.Add((Ct));// - surfUVoffset[surfa]);

            D = newPos.Count;
            newPos.Add(dp);
            newNormal.Add(dn);
            //newTangent.Add(dT);
            if (useVColor) newColor.Add(dc);
            newUV2.Add(dt2);
            if (useUV) newUV.Add((dt));// - surfUVoffset[surfa]);

            E = newPos.Count;
            newPos.Add(ep);
            newNormal.Add(en);
            //newTangent.Add(eT);
            if (useVColor) newColor.Add(ec);
            newUV2.Add(et2);
            if (useUV) newUV.Add((et));// - surfUVoffset[surfa]);

            if (hasSkin)
            {
                newSkin.Add(Ab);
                newSkin.Add(Bb);
                newSkin.Add(Cb);
                newSkin.Add(db);
                newSkin.Add(eb);
            }

            if (pa<0.0f)
            {
                newIndices.Add(A);
                newIndices.Add(D);
                newIndices.Add(C);

                newIndices.Add(D);
                newIndices.Add(E);
                newIndices.Add(C);
            }

            // surf b
            D = newPos.Count;
            newPos.Add(dp);
            newNormal.Add(dn);
            //newTangent.Add(dT);
            if (useVColor) newColor.Add(dc);
            newUV2.Add(dt2);
            if (useUV) newUV.Add((dt));// - surfUVoffset[surfb]);

            E = newPos.Count;
            newPos.Add(ep);
            newNormal.Add(en);
            //newTangent.Add(eT);
            if (useVColor) newColor.Add(ec);
            newUV2.Add(et2);
            if (useUV) newUV.Add((et));// - surfUVoffset[surfb]);

            if (hasSkin)
            {
                newSkin.Add(db);
                newSkin.Add(eb);
            }

            if (pa>0.0f)
            {
                newIndices.Add(B);
                newIndices.Add(E);
                newIndices.Add(D);
            }

            return;
        }

        if (signB == signC) //BC|A
        {
            float dist;
            var rayAC = new Ray(Ap, (Cp-Ap).normalized);
            plane.Raycast(rayAC, out dist);
            dp = rayAC.origin + rayAC.direction * dist;
            var di = saturate(dist / (Cp-Ap).magnitude);
            dp = Vector3.Lerp(Ap, Cp, di);
            var dn = Vector3.Lerp(An, Cn, di);
            //var dT = Vector3.Lerp(AT, CT, di);
            var dt = Vector2.Lerp(At, Ct, di);
            var dt2 = Vector2.Lerp(At2, Ct2, di);
            BoneWeight db = tmpWeight;
            if (hasSkin) db = LerpBoneWeights(Ab, Cb, di);
            var dc = Color.Lerp(Ac, Cc, di);

            var rayBA = new Ray(Bp, (Ap-Bp).normalized);
            plane.Raycast(rayBA, out dist);
            ep = rayBA.origin + rayBA.direction * dist;
            var ei = saturate(dist / (Ap-Bp).magnitude);
            ep = Vector3.Lerp(Bp, Ap, ei);
            var en = Vector3.Lerp(Bn, An, ei);
            //var eT = Vector3.Lerp(BT, AT, ei);
            var et = Vector2.Lerp(Bt, At, ei);
            var et2 = Vector2.Lerp(Bt2, At2, ei);
            BoneWeight eb = tmpWeight;
            if (hasSkin) eb = LerpBoneWeights(Bb, Ab, ei);
            var ec = Color.Lerp(Bc, Cc, ei);

            // surf b
            A = newPos.Count;
            newPos.Add(Ap);
            newNormal.Add(An);
            //newTangent.Add(AT);
            if (useVColor) newColor.Add(Ac);
            newUV2.Add(At2);
            if (useUV) newUV.Add((At));// - surfUVoffset[surfb]);

            // surf a
            B = newPos.Count;
            newPos.Add(Bp);
            newNormal.Add(Bn);
            //newTangent.Add(BT);
            if (useVColor) newColor.Add(Bc);
            newUV2.Add(Bt2);
            if (useUV) newUV.Add((Bt));// - surfUVoffset[surfa]);

            C = newPos.Count;
            newPos.Add(Cp);
            newNormal.Add(Cn);
            //newTangent.Add(CT);
            if (useVColor) newColor.Add(Cc);
            newUV2.Add(Ct2);
            if (useUV) newUV.Add((Ct));// - surfUVoffset[surfa]);

            D = newPos.Count;
            newPos.Add(dp);
            newNormal.Add(dn);
            //newTangent.Add(dT);
            if (useVColor) newColor.Add(dc);
            newUV2.Add(dt2);
            if (useUV) newUV.Add((dt));// - surfUVoffset[surfa]);

            E = newPos.Count;
            newPos.Add(ep);
            newNormal.Add(en);
            //newTangent.Add(eT);
            if (useVColor) newColor.Add(ec);
            newUV2.Add(et2);
            if (useUV) newUV.Add((et));// - surfUVoffset[surfa]);

            if (hasSkin)
            {
                newSkin.Add(Ab);
                newSkin.Add(Bb);
                newSkin.Add(Cb);
                newSkin.Add(db);
                newSkin.Add(eb);
            }

            if (pa>0.0f)
            {
                newIndices.Add(B);
                newIndices.Add(C);
                newIndices.Add(D);

                newIndices.Add(B);
                newIndices.Add(D);
                newIndices.Add(E);
            }

            // surf b
            D = newPos.Count;
            newPos.Add(dp);
            newNormal.Add(dn);
            //newTangent.Add(dT);
            if (useVColor) newColor.Add(dc);
            newUV2.Add(dt2);
            if (useUV) newUV.Add((dt));// - surfUVoffset[surfb]);

            E = newPos.Count;
            newPos.Add(ep);
            newNormal.Add(en);
            //newTangent.Add(eT);
            if (useVColor) newColor.Add(ec);
            newUV2.Add(et2);
            if (useUV) newUV.Add((et));// - surfUVoffset[surfb]);

            if (hasSkin)
            {
                newSkin.Add(db);
                newSkin.Add(eb);
            }

            if (pa<=0.0f)
            {
                newIndices.Add(A);
                newIndices.Add(E);
                newIndices.Add(D);
            }

            return;
        }
    }

#if UNITY_2018_1_OR_NEWER
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    static void AddTriangle(ref int fixedOffset, Vector3[] vPos, Vector3[] vNormal, Color[] vColor, Vector2[] vUV2, Vector2[] vUV, BoneWeight[] vSkin, byte[] vDecalID,
                                 Vector3 Ap, Vector3 An, Color Ac, Vector2 At2, Vector2 At, BoneWeight Ab,
                                 Vector3 Bp, Vector3 Bn, Color Bc, Vector2 Bt2, Vector2 Bt, BoneWeight Bb,
                                 Vector3 Cp, Vector3 Cn, Color Cc, Vector2 Ct2, Vector2 Ct, BoneWeight Cb, byte decalID)
    {
        int A = fixedOffset;
        int B = fixedOffset + 1;
        int C = fixedOffset + 2;
        vPos[A] = Ap;
        vPos[B] = Bp;
        vPos[C] = Cp;
        vNormal[A] = An;
        vNormal[B] = Bn;
        vNormal[C] = Cn;
        if (vColor != null)
        {
            vColor[A] = Ac;
            vColor[B] = Bc;
            vColor[C] = Cc;
        }
        vUV2[A] = At2;
        vUV2[B] = Bt2;
        vUV2[C] = Ct2;
        if (vUV != null)
        {
            vUV[A] = At;
            vUV[B] = Bt;
            vUV[C] = Ct;
        }
        if (vSkin != null)
        {
            vSkin[A] = Ab;
            vSkin[B] = Bb;
            vSkin[C] = Cb;
        }
        vDecalID[A] = vDecalID[B] = vDecalID[C] = decalID;
        fixedOffset += 3;
    }

    // New fixed array function
    static void SplitTriangle2(Vector3[] triPos, Vector3[] triNormal, Vector2[] triUV, Color[] triColor, Vector2[] triUV2, BoneWeight[] triSkin, Plane plane, byte decalID, ref int fixedOffset, Vector3[] vPos, Vector3[] vNormal, Vector2[] vUV, Color[] vColor, Vector2[] vUV2, BoneWeight[] vSkin, byte[] vDecalID)
    {
        //bool useVColor = vColor != null;
        //bool useUV = vUV != null;
        int vPosSize = vPos.Length;

        var Ap = triPos[0];
        var Bp = triPos[1];
        var Cp = triPos[2];

        var An = triNormal[0];
        var Bn = triNormal[1];
        var Cn = triNormal[2];

        var At = triUV[0];
        var Bt = triUV[1];
        var Ct = triUV[2];

        var At2 = triUV2[0];
        var Bt2 = triUV2[1];
        var Ct2 = triUV2[2];

        BoneWeight Ab = tmpWeight;
        BoneWeight Bb = tmpWeight;
        BoneWeight Cb = tmpWeight;
        bool hasSkin = triSkin != null;
        if (hasSkin)
        {
            Ab = triSkin[0];
            Bb = triSkin[1];
            Cb = triSkin[2];
        }

        var Ac = triColor[0];
        var Bc = triColor[1];
        var Cc = triColor[2];

        float pa = plane.GetDistanceToPoint(Ap);
        float pb = plane.GetDistanceToPoint(Bp);
        float pc = plane.GetDistanceToPoint(Cp);

        Vector3 dp,ep;

        float signA = Mathf.Sign(pa);
        float signB = Mathf.Sign(pb);
        float signC = Mathf.Sign(pc);

        bool arg1 = ( (signA == signB) && (signB == signC) );
        bool arg2 = ( (signB == signC) && (pa == 0) );
        bool arg3 = ( (signB == signA) && (pc == 0) );
        bool arg4 = ( (signA == signC) && (pb == 0) );
        if (arg1||arg2||arg3||arg4)
        {
            if (pa <= 0.0f)
            {
                if (fixedOffset + 3 >= vPosSize) fixedOffset = 0;

                // surf b
                AddTriangle(ref fixedOffset, vPos, vNormal, vColor, vUV2, vUV, vSkin, vDecalID,
                                             Ap, An, Ac, At2, At, Ab,
                                             Bp, Bn, Bc, Bt2, Bt, Bb,
                                             Cp, Cn, Cc, Ct2, Ct, Cb, decalID);
            }

            return;
        }

        if (signA == signB) //AB|C
        {
            float dist;
            var rayAC = new Ray(Ap, (Cp-Ap).normalized);
            plane.Raycast(rayAC, out dist);
            dp = rayAC.origin + rayAC.direction * dist;
            var di = saturate(dist / (Cp-Ap).magnitude);
            dp = Vector3.Lerp(Ap, Cp, di);
            var dn = Vector3.Lerp(An, Cn, di);
            //var dT = Vector3.Lerp(AT, CT, di);
            var dt = Vector2.Lerp(At, Ct, di);
            var dt2 = Vector2.Lerp(At2, Ct2, di);
            BoneWeight db = tmpWeight;
            if (hasSkin) db = LerpBoneWeights(Ab, Cb, di);
            var dc = Color.Lerp(Ac, Cc, di);

            var rayBC = new Ray(Bp, (Cp-Bp).normalized);
            plane.Raycast(rayBC, out dist);
            ep = rayBC.origin + rayBC.direction * dist;
            var ei = saturate(dist / (Cp-Bp).magnitude);
            ep = Vector3.Lerp(Bp, Cp, ei);
            var en = Vector3.Lerp(Bn, Cn, ei);
            //var eT = Vector3.Lerp(BT, CT, ei);
            var et = Vector2.Lerp(Bt, Ct, ei);
            var et2 = Vector2.Lerp(Bt2, Ct2, ei);
            BoneWeight eb = tmpWeight;
            if (hasSkin) eb = LerpBoneWeights(Bb, Cb, ei);
            var ec = Color.Lerp(Bc, Cc, ei);

            if (fixedOffset + 9 >= vPosSize) fixedOffset = 0;

            // surf a
            if (pa < 0.0f)
            {
                AddTriangle(ref fixedOffset, vPos, vNormal, vColor, vUV2, vUV, vSkin, vDecalID,
                                             dp, dn, dc, dt2, dt, db,
                                             Ap, An, Ac, At2, At, Ab,
                                             Bp, Bn, Bc, Bt2, Bt, Bb, decalID);

                AddTriangle(ref fixedOffset, vPos, vNormal, vColor, vUV2, vUV, vSkin, vDecalID,
                                             dp, dn, dc, dt2, dt, db,
                                             Bp, Bn, Bc, Bt2, Bt, Bb,
                                             ep, en, ec, et2, et, eb, decalID);
            }

            // surf b
            if (pa >= 0.0f)
            {
                AddTriangle(ref fixedOffset, vPos, vNormal, vColor, vUV2, vUV, vSkin, vDecalID,
                                             Cp, Cn, Cc, Ct2, Ct, Cb,
                                             dp, dn, dc, dt2, dt, db,
                                             ep, en, ec, et2, et, eb, decalID);
            }

            return;
        }

        if (signA == signC) //AC|B
        {
            float dist;
            var rayAB = new Ray(Ap, (Bp-Ap).normalized);
            plane.Raycast(rayAB, out dist);
            dp = rayAB.origin + rayAB.direction * dist;
            var di = saturate(dist / (Bp-Ap).magnitude);
            dp = Vector3.Lerp(Ap, Bp, di);
            var dn = Vector3.Lerp(An, Bn, di);
            //var dT = Vector3.Lerp(AT, BT, di);
            var dt = Vector2.Lerp(At, Bt, di);
            var dt2 = Vector2.Lerp(At2, Bt2, di);
            BoneWeight db = tmpWeight;
            if (hasSkin) db = LerpBoneWeights(Ab, Bb, di);
            var dc = Color.Lerp(Ac, Bc, di);

            var rayBC = new Ray(Bp, (Cp-Bp).normalized);
            plane.Raycast(rayBC, out dist);
            ep = rayBC.origin + rayBC.direction * dist;
            var ei = saturate(dist / (Cp-Bp).magnitude);
            ep = Vector3.Lerp(Bp, Cp, ei);
            var en = Vector3.Lerp(Bn, Cn, ei);
            //var eT = Vector3.Lerp(BT, CT, ei);
            var et = Vector2.Lerp(Bt, Ct, ei);
            var et2 = Vector2.Lerp(Bt2, Ct2, ei);
            BoneWeight eb = tmpWeight;
            if (hasSkin) eb = LerpBoneWeights(Bb, Cb, ei);
            var ec = Color.Lerp(Bc, Cc, ei);

            if (fixedOffset + 9 >= vPosSize) fixedOffset = 0;

            // surf a
            if (pa < 0.0f)
            {
                AddTriangle(ref fixedOffset, vPos, vNormal, vColor, vUV2, vUV, vSkin, vDecalID,
                                             Ap, An, Ac, At2, At, Ab,
                                             dp, dn, dc, dt2, dt, db,
                                             Cp, Cn, Cc, Ct2, Ct, Cb, decalID);

                AddTriangle(ref fixedOffset, vPos, vNormal, vColor, vUV2, vUV, vSkin, vDecalID,
                                             dp, dn, dc, dt2, dt, db,
                                             ep, en, ec, et2, et, eb,
                                             Cp, Cn, Cc, Ct2, Ct, Cb, decalID);
            }

            // surf b
            if (pa >= 0.0f)
            {
                AddTriangle(ref fixedOffset, vPos, vNormal, vColor, vUV2, vUV, vSkin, vDecalID,
                                             Bp, Bn, Bc, Bt2, Bt, Bb,
                                             ep, en, ec, et2, et, eb,
                                             dp, dn, dc, dt2, dt, db, decalID);
            }

            return;
        }

        if (signB == signC) //BC|A
        {
            float dist;
            var rayAC = new Ray(Ap, (Cp-Ap).normalized);
            plane.Raycast(rayAC, out dist);
            dp = rayAC.origin + rayAC.direction * dist;
            var di = saturate(dist / (Cp-Ap).magnitude);
            dp = Vector3.Lerp(Ap, Cp, di);
            var dn = Vector3.Lerp(An, Cn, di);
            //var dT = Vector3.Lerp(AT, CT, di);
            var dt = Vector2.Lerp(At, Ct, di);
            var dt2 = Vector2.Lerp(At2, Ct2, di);
            BoneWeight db = tmpWeight;
            if (hasSkin) db = LerpBoneWeights(Ab, Cb, di);
            var dc = Color.Lerp(Ac, Cc, di);

            var rayBA = new Ray(Bp, (Ap-Bp).normalized);
            plane.Raycast(rayBA, out dist);
            ep = rayBA.origin + rayBA.direction * dist;
            var ei = saturate(dist / (Ap-Bp).magnitude);
            ep = Vector3.Lerp(Bp, Ap, ei);
            var en = Vector3.Lerp(Bn, An, ei);
            //var eT = Vector3.Lerp(BT, AT, ei);
            var et = Vector2.Lerp(Bt, At, ei);
            var et2 = Vector2.Lerp(Bt2, At2, ei);
            BoneWeight eb = tmpWeight;
            if (hasSkin) eb = LerpBoneWeights(Bb, Ab, ei);
            var ec = Color.Lerp(Bc, Cc, ei);

            if (fixedOffset + 9 >= vPosSize) fixedOffset = 0;

            // surf a
            if (pa > 0.0f)
            {
                AddTriangle(ref fixedOffset, vPos, vNormal, vColor, vUV2, vUV, vSkin, vDecalID,
                                             Bp, Bn, Bc, Bt2, Bt, Bb,
                                             Cp, Cn, Cc, Ct2, Ct, Cb,
                                             dp, dn, dc, dt2, dt, db, decalID);

                AddTriangle(ref fixedOffset, vPos, vNormal, vColor, vUV2, vUV, vSkin, vDecalID,
                                             Bp, Bn, Bc, Bt2, Bt, Bb,
                                             dp, dn, dc, dt2, dt, db,
                                             ep, en, ec, et2, et, eb, decalID);
            }

            // surf b
            if (pa <= 0.0f)
            {
                AddTriangle(ref fixedOffset, vPos, vNormal, vColor, vUV2, vUV, vSkin, vDecalID,
                                             Ap, An, Ac, At2, At, Ab,
                                             ep, en, ec, et2, et, eb,
                                             dp, dn, dc, dt2, dt, db, decalID);
            }

            return;
        }
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

    public static DecalUtils.Group CreateGroup(DecalUtils.GroupDesc desc)
    {
        var parentTform = desc.parent != null ? desc.parent.transform : null;

        var go = new GameObject();
        go.name = "Decals";
        var tform = go.transform;
        tform.parent = parentTform;
        tform.localPosition = Vector3.zero;
        tform.localRotation = Quaternion.identity;
        tform.localScale = Vector3.one;

        var newMesh = new Mesh();
        bool isSkinned = false;
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
            var origSMR = desc.parent.GetComponent<SkinnedMeshRenderer>();
            if (origSMR != null)
            {
                var smr = go.AddComponent<SkinnedMeshRenderer>();
                smr.sharedMaterial = desc.material;
                smr.lightmapIndex = desc.lightmapID;
                smr.realtimeLightmapIndex = desc.realtimeLightmapID;
                smr.realtimeLightmapScaleOffset = desc.realtimeLightmapScaleOffset;
                smr.bones = origSMR.bones;
                newMesh.bindposes = origSMR.sharedMesh.bindposes;
                newMesh.bounds = origSMR.sharedMesh.bounds;
                smr.localBounds = origSMR.localBounds;
                smr.sharedMesh = newMesh;
                smr.rootBone = origSMR.rootBone;
                if (desc.materialPropertyBlock != null) smr.SetPropertyBlock(desc.materialPropertyBlock);
                isSkinned = true;
            }
        }
        if (!isSkinned)
        {
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
        group.mode = DecalUtils.Mode.CPU;
        group.material = desc.material;
        group.totalTris = desc.maxTrisTotal;
        group.maxTrisInDecal = desc.maxTrisInDecal;
        group.parent = desc.parent;
        group.parentTform = parentTform;
        group.go = go;
        group.mesh = newMesh;
        group.lightmapID = desc.lightmapID;
        group.bounds = group.nextBounds = new Bounds(new Vector3(-99999, -99999, -99999), Vector3.zero);
        group.isTrail = desc.isTrail;
        group.trailVScale = desc.trailVScale;
        group.tangents = desc.tangents;
        group.isSkinned = isSkinned;

        group.decalGroup = go.AddComponent<DecalGroup>();
        group.decalGroup.linkToParent = group.parentTform != null;

        group.vPos = new Vector3[desc.maxTrisTotal * 3];
        group.vNormal = new Vector3[desc.maxTrisTotal * 3];
        group.vUV = new Vector2[desc.maxTrisTotal * 3];
        group.vUV2 = new Vector2[desc.maxTrisTotal * 3];
        group.vColor = new Color[desc.maxTrisTotal * 3];
        group.vDecalID = new byte[desc.maxTrisTotal * 3];
        if (desc.tangents) group.vTangents = new Vector4[desc.maxTrisTotal * 3];
        if (isSkinned) group.vSkin = new BoneWeight[desc.maxTrisTotal * 3];

        return group;
    }

    public static void AddDecal(DecalUtils.Group group, DecalUtils.DecalDesc desc, GameObject receiver)
    {
        //var time0 = new System.Diagnostics.Stopwatch();
        //time0.Start();

        var decal = group.decalGroup;

        decal.parentObject = receiver;
        decal.rayLength = desc.distance;
        decal.angleClip = desc.angleClip;
        decal.angleFade = true;
        decal.tangents = group.tangents;

        var right = desc.rotation * Vector3.right;
        var up = desc.rotation * Vector3.up;
        right *= desc.sizeX;
        up *= desc.sizeY;
        var decalPosW = new Vector3[4];
        if (group.isTrail && group.numDecals > 0)
        {
            decalPosW[0] = group.prevDecalEdgeA;
            decalPosW[1] = group.prevDecalEdgeB;
            decalPosW[2] = desc.position + right + up;
            decalPosW[3] = desc.position - right + up;
        }
        else
        {
            decalPosW[0] = desc.position - right - up;
            decalPosW[1] = desc.position + right - up;
            decalPosW[2] = desc.position + right + up;
            decalPosW[3] = desc.position - right + up;
        }

        var decalUV = new Vector2[4];
        if (group.isTrail && group.numDecals > 0)
        {
            float prevV = group.trailV;
            float moveDist = Mathf.Max(Vector3.Distance(decalPosW[3], group.prevDecalEdgeA), Vector3.Distance(decalPosW[2], group.prevDecalEdgeB));
            group.trailV += moveDist * group.trailVScale;
            float nextV = group.trailV;
            decalUV[0] = new Vector2(0,prevV);
            decalUV[1] = new Vector2(1,prevV);
            decalUV[2] = new Vector2(1,nextV);
            decalUV[3] = new Vector2(0,nextV);
        }
        else
        {
            decalUV[0] = new Vector2(0,0);
            decalUV[1] = new Vector2(1,0);
            decalUV[2] = new Vector2(1,1);
            decalUV[3] = new Vector2(0,1);
        }

        var decalTris = new int[6];
        decalTris[2] = 0;
        decalTris[1] = 1;
        decalTris[0] = 2;
        decalTris[5] = 2;
        decalTris[4] = 3;
        decalTris[3] = 0;

        bool transformToParent = group.parentTform != null;// && !group.isSkinned;

        var decalMatrix = Matrix4x4.TRS(desc.position, desc.rotation, new Vector3(desc.sizeX, desc.sizeY, desc.distance));
        cullingMatrix = decalMatrix;
        cullingMatrix.SetColumn(3, decalMatrix.GetColumn(3) + decalMatrix.GetColumn(2) * desc.distance * 0.5f);
        cullingMatrix = cullingMatrix.inverse;
        bool cull = true;

        lightmapScaleOffset = desc.lightmapScaleOffset;
        bool transformUV1 = group.lightmapID >= 0;

        UpdateDecalFor(decal, group.material, decalUV, decalPosW, null, decalTris, null, receiver.gameObject, null, desc.opacity, ref group.fixedOffset, (byte)((group.numDecals % 255) + 1), group.mesh, group.vPos, group.vNormal, group.vUV, group.vUV2, group.vColor, group.vSkin, group.vDecalID, group.vTangents, group.maxTrisInDecal, transformToParent, cull, transformUV1);

        if (group.parentTform == null)
        {
            DecalUtils.ExpandBounds(group, decalMatrix, desc);
            group.mesh.bounds = group.bounds;
        }

        if (group.isTrail)
        {
            group.prevDecalEdgeA = decalPosW[3];
            group.prevDecalEdgeB = decalPosW[2];
        }

        if (group.fixedOffset > 0)
        {
            if (group.numDecals == 0)
            {
                SetTris(group.mesh, group.totalTris);
            }

            group.numDecals++;
        }

        //Debug.LogError("AddDecal: " + time0.Elapsed.TotalMilliseconds);
    }

    public static void ClearDecals(DecalUtils.Group group)
    {
        if (group.mode != DecalUtils.Mode.CPU)
        {
            Debug.LogError("Decal mode is not CPU.");
            return;
        }
        group.bounds = group.nextBounds = new Bounds(new Vector3(-99999, -99999, -99999), Vector3.zero);
        group.numDecals = group.boundsCounter = group.boundsMinDecalCounter = 0;
        group.fixedOffset = 0;
        float nan = 1.0f / 0.0f;
        var nan3 = new Vector3(nan, nan, nan);
        for(int i=0; i<group.totalTris*3; i++)
        {
            group.vPos[i] = nan3;
        }
        group.mesh.vertices = group.vPos;
    }
}

