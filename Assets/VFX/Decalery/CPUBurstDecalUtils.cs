#define USE_TERRAINS

#if UNITY_2018_1_OR_NEWER
#if USE_BURST
#if USE_NEWMATHS
#define USE_BURST_REALLY
#endif
#endif
#endif

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
#endif
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;


#if USE_BURST_REALLY
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Jobs;
#endif


#if USE_BURST_REALLY

public class CPUBurstDecalUtils : MonoBehaviour
{
    static NativeArray<float3> _splitTriPos;
    static NativeArray<float3> _splitTriNormal;
    static NativeArray<float2> _splitTriUV;
    static NativeArray<float2> _splitTriUV2;
    static NativeArray<float4> _splitTriColor;
    static NativeArray<BoneWeight> _splitTriSkin;

    static NativeArray<float3> nAvgDir;
    static NativeArray<int> nOutputs;

    static NativeArray<float3> nvTempSplitPos, nvTempSplitNormal;
    static NativeArray<float4> nvTempSplitColor;
    static NativeArray<float2> nvTempSplitUV2, nvTempSplitUV;
    static NativeArray<BoneWeight> nvTempSplitSkin;
    static NativeArray<int> nvTempSplitIndices;

    static NativeArray<float3> nvPos, nvNormal;
    static NativeArray<float4> nvColor, vEmptyColor, vEmptyTangent;
    static NativeArray<float2> nvUV2, nvUV;
    static NativeArray<BoneWeight> nvSkin, vEmptySkin;
    static NativeArray<float3> vEmptyPos, vEmptyNorm;
    static NativeArray<byte> nvDecalID;
    static NativeArray<int> nvIndices, vEmptyIndices;
    static NativeArray<bool> nvCulled;

    static bool staticNativeArraysInit = false;

    const int maxOutputVerts = 256000;
    const int maxOutputIndices = 256000*3;

    const int maxTempSplitVerts = 256;
    const int maxTempSplitIndices = 256*3;

#pragma warning disable 0649
    static BoneWeight _tmpWeight;
#pragma warning restore 0649

#if UNITY_EDITOR
    public static bool checkPrefabs = false;
#endif

    static Matrix4x4 cullingMatrix;
    static Vector4 lightmapScaleOffset;

    const int FLAG_UV = 1;
    const int FLAG_VC = 2;
    const int FLAG_SKIN = 4;

    static float saturate(float f)
    {
        return Mathf.Clamp(f, 0.0f, 1.0f);
    }

    static float2 triLerp(float2 p0, float2 p1, float2 p2, float3 a, float3 b, float3 c, float3 pp)
    {
        var v0 = b - a;
        var v1 = c - a;
        var v2 = pp - a;
        float d00 = math.dot(v0, v0);
        float d01 = math.dot(v0, v1);
        float d11 = math.dot(v1, v1);
        float d20 = math.dot(v2, v0);
        float d21 = math.dot(v2, v1);
        float denom = d00 * d11 - d01 * d01;
        float v = (d11 * d20 - d01 * d21) / denom;
        float w = (d00 * d21 - d01 * d20) / denom;
        float u = 1.0f - v - w;
        return p0*u + p1*v + p2*w;
    }

    static float3 triLerp(float3 p0, float3 p1, float3 p2, float3 a, float3 b, float3 c, float3 pp)
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
        float d00 = math.dot(v0, v0);
        float d01 = math.dot(v0, v1);
        float d11 = math.dot(v1, v1);
        float d20 = math.dot(v2, v0);
        float d21 = math.dot(v2, v1);
        float denom = d00 * d11 - d01 * d01;
        float v = (d11 * d20 - d01 * d21) / denom;
        float w = (d00 * d21 - d01 * d20) / denom;
        float u = 1.0f - v - w;
        return p0*u + p1*v + p2*w;
    }

    static float4 triLerp(float4 p0, float4 p1, float4 p2, float3 a, float3 b, float3 c, float3 pp)
    {
        var v0 = b - a;
        var v1 = c - a;
        var v2 = pp - a;
        float d00 = math.dot(v0, v0);
        float d01 = math.dot(v0, v1);
        float d11 = math.dot(v1, v1);
        float d20 = math.dot(v2, v0);
        float d21 = math.dot(v2, v1);
        float denom = d00 * d11 - d01 * d01;
        float v = (d11 * d20 - d01 * d21) / denom;
        float w = (d00 * d21 - d01 * d20) / denom;
        float u = 1.0f - v - w;
        return p0*u + p1*v + p2*w;
    }

    static BoneWeight LerpBoneWeights(BoneWeight a, BoneWeight b, float c)
    {
        a.weight0 = math.lerp(a.weight0, b.weight0, c);
        a.weight1 = math.lerp(a.weight1, b.weight1, c);
        a.weight2 = math.lerp(a.weight2, b.weight2, c);
        a.weight3 = math.lerp(a.weight3, b.weight3, c);
        return a;
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

    struct HashedVertex
    {
        public float3 pos, normal;
        public float2 uv, uv2;
        public float4 color;
    }

    static void WeldVerts(int numInVerts, int numInIndices, int inputFlags, ref int numOutVerts, ref int numOutIndices,
                            NativeArray<int> inIndices,     NativeArray<float3> inVerts,    NativeArray<float3> inNormals,  NativeArray<float2> inUV,   NativeArray<float2> inUV2,  NativeArray<BoneWeight> inSkin,     NativeArray<float4> inColor,
                            NativeArray<int> outIndices,    NativeArray<float3> outVerts,   NativeArray<float3> outNormals, NativeArray<float2> outUV,  NativeArray<float2> outUV2, NativeArray<BoneWeight> outSkin,    NativeArray<float4> outColor)
    {
        var hashed = new HashedVertex();
        var map = new Dictionary<HashedVertex, int>();

        bool hasUV =    (inputFlags & FLAG_UV) != 0;
        bool hasColor = (inputFlags & FLAG_VC) != 0;
        bool hasSkin =  (inputFlags & FLAG_SKIN) != 0;

        // Detect and remap similar verts, create new VB
        numOutVerts = 0;
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

                outVerts[numOutVerts] = inVerts[i];
                outNormals[numOutVerts] = inNormals[i];
                outUV2[numOutVerts] = inUV2[i];
                if (hasUV) outUV[numOutVerts] = inUV[i];
                if (hasColor) outColor[numOutVerts] = inColor[i];
                if (hasSkin) outSkin[numOutVerts] = inSkin[i];

                numOutVerts++;
            }
            else
            {
                vertRemap[i] = index;
            }
        }

        // Remap IB
        numOutIndices = 0;
        for(int i=0; i<numInIndices; i++)
        {
            outIndices[numOutIndices] = vertRemap[ inIndices[i] ];
            numOutIndices++;
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    private struct UpdateDecalFor_Job : IJob
    {
        // Decal mesh data
        public NativeArray<float2> decalUV;
        public NativeArray<float3> decalPosW;
        public NativeArray<int> decalTris;
        public NativeArray<float4> decalColor;

        // Decal constants
        public float opacity;
        
        // UV transform data
        public bool roadTransform;
        public float2 mainTextureScale, mainTextureOffset;
        public float atlasMinX, atlasMaxX, atlasMinY, atlasMaxY;

        // Decal transform data
        public Vector3 tformRight;
        public Vector3 tformUp;
        public Vector3 tformForward;
        public Vector3 tformPosition;
        public bool isDecalFlipped;

        // Receiver mesh data
        public NativeArray<float3> staticPos, staticNorm;
        public NativeArray<float2> staticUV2;
        public NativeArray<int> staticTris;

        // Receiver transformed mesh data
        public NativeArray<float3> staticPosW, staticNormW;
        
        // Receiver optional mesh data
        public NativeArray<bool> staticTriCulled;
        public NativeArray<BoneWeight> staticSkin;

        // Receiver transform data
        public float4x4 staticTformMatrix, staticTformInvMatrix;
        public bool isStaticFlipped;

#if USE_TERRAINS
        public int heightmapRes;
        public float terrainInvScaleX, terrainInvScaleZ;
#endif

        // Early cull params
        public bool earlyCull;
        public float4x4 cullingMtx;

        // Angle clip/fade params
        public bool angleFade;
        public float angleClip;

        // Projection distances
        public float rayLength, bias;

        // Temp split arrays (3)
        public NativeArray<float3> splitTriPos;
        public NativeArray<float3> splitTriNormal;
        public NativeArray<float2> splitTriUV;
        public NativeArray<float2> splitTriUV2;
        public NativeArray<float4> splitTriColor;
        public NativeArray<BoneWeight> splitTriSkin;

        // Output data
        public bool isRuntimeFixedArray;
        public int fixedOffset, fixedTriOffset;
        public NativeArray<float3> vPos, vNormal;
        public NativeArray<float4> vColor, vTangents;
        public NativeArray<float2> vUV2, vUV;
        public NativeArray<BoneWeight> vSkin;
        public NativeArray<byte> vDecalID;
        public NativeArray<int> vIndices;

        // Larger temp split arrays
        public NativeArray<float3> vTempSplitPos;
        public NativeArray<float3> vTempSplitNormal;
        public NativeArray<float4> vTempSplitColor;
        public NativeArray<float2> vTempSplitUV;
        public NativeArray<float2> vTempSplitUV2;
        public NativeArray<BoneWeight> vTempSplitSkin;
        public NativeArray<int> vTempSplitIndices;

        // Current decal ID
        public byte decalID;

        // Flags
        public int decalFlags, staticFlags;

        // Temp default weight
        public BoneWeight tmpWeight;

        public int maxTrisInDecal;
        public bool transformToParent, transformUV1;
        public float4 _lightmapScaleOffset;

        public bool tangents;

        // Output values
        public NativeArray<int> outputsInt; // numOutVerts, numOutIndices
        public NativeArray<float3> avgDir;

        public void Execute()
        {
            int numDecalUVs = decalUV.Length;
            int numDecalTris = decalTris.Length;
            bool noUV = (decalFlags & FLAG_UV) == 0;// numDecalUVs == 0;
            float2 zero2 = float2.zero;
            bool srcVcolor = (decalFlags & FLAG_VC) != 0;
            bool genVColor = srcVcolor || angleFade || opacity != 1.0f;
            float4 decalColorA = new float4(1,1,1,1);
            float4 decalColorB = new float4(1,1,1,1);
            float4 decalColorC = new float4(1,1,1,1);
            float4 colorA = new float4(1,1,1,1);
            float4 colorB = new float4(1,1,1,1);
            float4 colorC = new float4(1,1,1,1);
            bool hasSkin = (staticFlags & FLAG_SKIN) != 0;// staticSkin.Length > 0;
            bool hasVUV = (decalFlags & FLAG_UV) != 0;
            int vPosSize = vPos.Length;
            int fixedOffsetStart = fixedOffset;

            int numStaticVerts = staticPosW.Length;
            int numStaticTris = staticTris.Length;

#if USE_TERRAINS
            bool isTerrain = heightmapRes > 0;
            if (isTerrain)
            {
                float dminx = float.MaxValue;
                float dminz = float.MaxValue;
                float dmaxx = -float.MaxValue;
                float dmaxz = -float.MaxValue;
                float pf = rayLength;
                float pb = -bias;
                for(int i=0; i<numDecalTris; i+=3)
                {
                    int idA = decalTris[i];
                    int idB = decalTris[i+1];
                    int idC = decalTris[i+2];
                    var posA = decalPosW[idA];
                    var posB = decalPosW[idB];
                    var posC = decalPosW[idC];

                    var ba = posB - posA;
                    var ac = posA - posC;
                    var n = math.normalize(math.cross(ba, -ac));

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

                int res = heightmapRes;
                int vertOffset = 0;
                int indexOffset = 0;

                var staticTPos = staticTformMatrix.c3;

                int tminx = (int)((dminx - staticTPos.x) * terrainInvScaleX);
                int tminz = (int)((dminz - staticTPos.z) * terrainInvScaleZ);
                int tmaxx = (int)((dmaxx - staticTPos.x) * terrainInvScaleX) + 2;
                int tmaxz = (int)((dmaxz - staticTPos.z) * terrainInvScaleZ) + 2;

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

                //int newNumVerts = patchResX*patchResZ;
                //int newNumIndices = (patchResX-1) * (patchResZ-1) * 2 * 3;

                int vcount = staticPos.Length / 2; // readable data starts from vcount; writeable from 0

                for (int z=0;z<patchResZ;z++)
                {
                    int zoff = (tminz + z) * res;
                    for (int x=0;x<patchResX;x++)
                    {
                        int inIndex =  zoff + (tminx + x);
                        int outIndex = z * patchResX + x;

                        // shouldn't overlap...
                        staticPos[outIndex] = staticPos[inIndex + vcount];
                        staticNorm[outIndex] = staticNorm[inIndex + vcount];
                        staticUV2[outIndex] = staticUV2[inIndex + vcount];

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

                numStaticVerts = vertOffset;
                numStaticTris = indexOffset;
            }
#endif

            if (roadTransform)
            {
                Vector2 xvec = new Vector2(tformRight.x, -tformRight.z);
                Vector2 zvec = new Vector2(-tformForward.x, tformForward.z);
                for(int i=0; i<numDecalUVs; i++)
                {
                    float u = decalPosW[i].z - tformPosition.z;
                    float v = decalPosW[i].x - tformPosition.x;

                    decalUV[i] = new Vector2(u*xvec.x + v*xvec.y,  (u*zvec.x + v*zvec.y));
                    decalUV[i] = new Vector2(decalUV[i].x*mainTextureScale.x+mainTextureOffset.x, decalUV[i].y*mainTextureScale.y+mainTextureOffset.y);
                }
            }

            if (atlasMinX != 0 || atlasMinY != 0 || atlasMaxX != 1.0f || atlasMaxY != 1.0f)
            {
                float scaleU = atlasMaxX - atlasMinX;
                float scaleV = atlasMaxY - atlasMinY;
                float offsetU = atlasMinX;
                float offsetV = atlasMinY;
                for(int i=0; i<numDecalUVs; i++)
                {
                    decalUV[i] = new Vector2(decalUV[i].x*scaleU+offsetU, 1.0f-((1.0f-decalUV[i].y)*scaleV+offsetV));
                }
            }

            // Reverse decal triangles, if flipped
            if (isDecalFlipped)
            {
                for(int t=0; t<numDecalTris; t += 3) // non-Burst version could use this optimization
                {
                    var a = decalTris[t];
                    var b = decalTris[t + 1];
                    var c = decalTris[t + 2];
                    decalTris[t] = c;
                    decalTris[t + 1] = b;
                    decalTris[t + 2] = a;
                }
            }

            // Transform receiver to world space
            float3x3 rotMatrix;
#if USE_TERRAINS
            if (isTerrain)
            {
                staticPosW = staticPos;
                staticNormW = staticNorm;
            }
            else
#endif
            {
                for(int i=0; i<numStaticVerts; i++)
                {

                    var pos4 = math.mul(staticTformMatrix, new float4(staticPos[i], 1.0f));
                    staticPosW[i] = new float3(pos4.x, pos4.y, pos4.z);
                }
                rotMatrix = new float3x3(math.normalize(staticTformMatrix.c0.xyz), math.normalize(staticTformMatrix.c1.xyz), math.normalize(staticTformMatrix.c2.xyz));
                for(int i=0; i<numStaticVerts; i++)
                {
                    staticNormW[i] = math.mul(rotMatrix, staticNorm[i]);
                }
            }

            // Matrix cull
            if (earlyCull)
            {
                float minx, miny, minz;
                float maxx, maxy, maxz;
                const float eps = 0.0001f;

                float4 triA, triB, triC;
                float3 point;

                for(int t2=0; t2<numStaticTris; t2 += 3)
                {
                    point = staticPosW[staticTris[t2]];
                    triA = math.mul(cullingMtx, new float4(point, 1.0f));

                    point = staticPosW[staticTris[t2+1]];
                    triB = math.mul(cullingMtx, new float4(point, 1.0f));

                    point = staticPosW[staticTris[t2+2]];
                    triC = math.mul(cullingMtx, new float4(point, 1.0f));

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

                    staticTriCulled[t2] = !boundsIntersect;
                }
            }

            // Compute angle fading params
            float fadeMul = 0;
            float fadeAdd = 0;
            if (angleFade)
            {
                fadeMul = 1.0f / (1.0f - angleClip);
                fadeAdd = -angleClip * fadeMul;
            }

            avgDir[0] = float3.zero;


            // Decal tri loop
            for(int t=0; t<numDecalTris; t += 3)
            {
                // Get decal tri pos and edge dirs
                var decalA = decalPosW[decalTris[t]];
                var decalB = decalPosW[decalTris[t + 1]];
                var decalC = decalPosW[decalTris[t + 2]];
                var ba = decalB - decalA;
                var cb = decalC - decalB;
                var ac = decalA - decalC;
                var decalTriVecAB = math.normalize(ba);
                var decalTriVecBC = math.normalize(cb);
                var decalTriVecCA = math.normalize(ac);

                // Create decal tri plane
                var decalTriPlaneN = math.normalize(math.cross(ba, -ac));
                var decalTriPlaneD = -math.dot(decalTriPlaneN, decalA);

                avgDir[0] += decalTriPlaneN;

                // Create clipping planes from decal tri edges
                var cutNormalAB = math.cross(decalTriVecAB, decalTriPlaneN);
                var decalCutPlaneAB_N = math.normalize(cutNormalAB);
                var decalCutPlaneAB_D = -math.dot(decalCutPlaneAB_N, decalA);

                var cutNormalBC = math.cross(decalTriVecBC, decalTriPlaneN);
                var decalCutPlaneBC_N = math.normalize(cutNormalBC);
                var decalCutPlaneBC_D = -math.dot(decalCutPlaneBC_N, decalB);

                var cutNormalCA = math.cross(decalTriVecCA, decalTriPlaneN);
                var decalCutPlaneCA_N = math.normalize(cutNormalCA);
                var decalCutPlaneCA_D = -math.dot(decalCutPlaneCA_N, decalA);

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
                        if (staticTriCulled[t2]) continue;
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
                    if (angleFade)
                    {
                        splitTriNormal[0] = staticNormW[staticTris[sa]];
                        splitTriNormal[1] = staticNormW[staticTris[sb]];
                        splitTriNormal[2] = staticNormW[staticTris[sc]];
                        var d0 = math.dot(decalTriPlaneN, splitTriNormal[0]);
                        var d1 = math.dot(decalTriPlaneN, splitTriNormal[1]);
                        var d2 = math.dot(decalTriPlaneN, splitTriNormal[2]);
                        if (d0 < angleClip && d1 < angleClip && d2 < angleClip) continue;
                    }
                    else
                    {
                        // For historical reasons non-fade version is using triangle normal
                        var triPlaneN = math.normalize(math.cross(triB - triA, triC - triA));
                        var triPlaneD = -math.dot(triPlaneN, triA);
                        if (math.dot(decalTriPlaneN, triPlaneN) < angleClip)// 0.5f)
                        {
                            continue;
                        }
                    }

                    // Forward/backwards distance test (whole tri is beyond distance)
                    if (math.dot(decalTriPlaneN, triA) + decalTriPlaneD < -rayLength &&
                        math.dot(decalTriPlaneN, triB) + decalTriPlaneD < -rayLength &&
                        math.dot(decalTriPlaneN, triC) + decalTriPlaneD < -rayLength) continue;

                    // Backwards distance test (whole tri is beyond distance)
                    if (math.dot(decalTriPlaneN, triA) + decalTriPlaneD > bias &&
                        math.dot(decalTriPlaneN, triB) + decalTriPlaneD > bias &&
                        math.dot(decalTriPlaneN, triC) + decalTriPlaneD > bias) continue;

                    // Skip receiver tri if completely outside the decal tri
                    if ((math.dot(decalCutPlaneAB_N, triA) + decalCutPlaneAB_D > 0.0f) && (math.dot(decalCutPlaneAB_N, triB) + decalCutPlaneAB_D > 0.0f) && (math.dot(decalCutPlaneAB_N, triC) + decalCutPlaneAB_D > 0.0f)) continue;
                    if ((math.dot(decalCutPlaneBC_N, triA) + decalCutPlaneBC_D > 0.0f) && (math.dot(decalCutPlaneBC_N, triB) + decalCutPlaneBC_D > 0.0f) && (math.dot(decalCutPlaneBC_N, triC) + decalCutPlaneBC_D > 0.0f)) continue;
                    if ((math.dot(decalCutPlaneCA_N, triA) + decalCutPlaneCA_D > 0.0f) && (math.dot(decalCutPlaneCA_N, triB) + decalCutPlaneCA_D > 0.0f) && (math.dot(decalCutPlaneCA_N, triC) + decalCutPlaneCA_D > 0.0f)) continue;

                    // Determine if it's fully inside too
                    var inside = (!(math.dot(decalCutPlaneAB_N, triA) + decalCutPlaneAB_D > 0.0f) && !(math.dot(decalCutPlaneAB_N, triB) + decalCutPlaneAB_D > 0.0f) && !(math.dot(decalCutPlaneAB_N, triC) + decalCutPlaneAB_D > 0.0f)) &&
                                 (!(math.dot(decalCutPlaneBC_N, triA) + decalCutPlaneBC_D > 0.0f) && !(math.dot(decalCutPlaneBC_N, triB) + decalCutPlaneBC_D > 0.0f) && !(math.dot(decalCutPlaneBC_N, triC) + decalCutPlaneBC_D > 0.0f)) &&
                                 (!(math.dot(decalCutPlaneCA_N, triA) + decalCutPlaneCA_D > 0.0f) && !(math.dot(decalCutPlaneCA_N, triB) + decalCutPlaneCA_D > 0.0f) && !(math.dot(decalCutPlaneCA_N, triC) + decalCutPlaneCA_D > 0.0f));

                     // Get receiver tri normal (if it wasn't alrady read)
                    if (!angleFade)
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

                        if (genVColor)
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
                                colorA = colorB = colorC = new float4(opacity, opacity, opacity, opacity);
                            }

                            if (angleFade)
                            {
                                colorA *= math.clamp(math.dot(splitTriNormal[0], decalTriPlaneN) * fadeMul + fadeAdd, 0.0f, 1.0f);
                                colorB *= math.clamp(math.dot(splitTriNormal[1], decalTriPlaneN) * fadeMul + fadeAdd, 0.0f, 1.0f);
                                colorC *= math.clamp(math.dot(splitTriNormal[2], decalTriPlaneN) * fadeMul + fadeAdd, 0.0f, 1.0f);
                            }
                        }

                        if (isRuntimeFixedArray)
                        {
                            if (fixedOffset + 3 >= vPosSize) fixedOffset = 0;

                            AddTriangle(ref fixedOffset, vPos, vNormal, genVColor, vColor, vUV2, hasVUV, vUV, hasSkin, vSkin, vDecalID,
                                                                             triA, splitTriNormal[0], colorA, splitTriUV2[0], uvA, splitTriSkin[0],
                                                                             triB, splitTriNormal[1], colorB, splitTriUV2[1], uvB, splitTriSkin[1],
                                                                             triC, splitTriNormal[2], colorC, splitTriUV2[2], uvC, splitTriSkin[2], decalID);
                        }
                        else
                        {
                            if (fixedOffset + 3 >= vPosSize) fixedOffset = 0;

                            vPos[fixedOffset] = triA;
                            vPos[fixedOffset+1] = triB;
                            vPos[fixedOffset+2] = triC;

                            vNormal[fixedOffset] = splitTriNormal[0];
                            vNormal[fixedOffset+1] = splitTriNormal[1];
                            vNormal[fixedOffset+2] = splitTriNormal[2];

                            vUV2[fixedOffset] = splitTriUV2[0];
                            vUV2[fixedOffset+1] = splitTriUV2[1];
                            vUV2[fixedOffset+2] = splitTriUV2[2];

                            if (hasSkin)
                            {
                                vSkin[fixedOffset] = splitTriSkin[0];
                                vSkin[fixedOffset+1] = splitTriSkin[1];
                                vSkin[fixedOffset+2] = splitTriSkin[2];
                            }

                            if (genVColor)
                            {
                                vColor[fixedOffset] = colorA;
                                vColor[fixedOffset+1] = colorB;
                                vColor[fixedOffset+2] = colorC;
                            }

                            vUV[fixedOffset] = uvA;
                            vUV[fixedOffset+1] = uvB;
                            vUV[fixedOffset+2] = uvC;

                            vIndices[fixedTriOffset] = fixedOffset;
                            vIndices[fixedTriOffset+1] = fixedOffset+1;
                            vIndices[fixedTriOffset+2] = fixedOffset+2;

                            fixedOffset += 3;
                            fixedTriOffset += 3;
                        }
                    }
                    else
                    {
                        // Receiver tri is cut by the decal tri

                        splitTriPos[0] = triA;
                        splitTriPos[1] = triB;
                        splitTriPos[2] = triC;

                        // Cut by AB
                        var cutPlane_N = decalCutPlaneAB_N;
                        var cutPlane_D = decalCutPlaneAB_D;

                        int fixedOffsetLocal = 0;
                        int fixedTriOffsetLocal = 0;
                        SplitTriangle(splitTriPos, splitTriNormal, splitTriUV, splitTriColor, splitTriUV2, splitTriSkin, cutPlane_N, cutPlane_D, vTempSplitPos, vTempSplitNormal, vTempSplitUV, vTempSplitColor, vTempSplitUV2, vTempSplitSkin, vTempSplitIndices, ref fixedOffsetLocal, ref fixedTriOffsetLocal, genVColor, false, hasSkin, tmpWeight);

                        // Cut by BC
                        cutPlane_N = decalCutPlaneBC_N;
                        cutPlane_D = decalCutPlaneBC_D;
                        int newIndicesSplitABCount = fixedTriOffsetLocal;
                        for(int t3=0; t3<newIndicesSplitABCount; t3 += 3)
                        {
                            splitTriPos[0] = vTempSplitPos[vTempSplitIndices[t3]];
                            splitTriPos[1] = vTempSplitPos[vTempSplitIndices[t3 + 1]];
                            splitTriPos[2] = vTempSplitPos[vTempSplitIndices[t3 + 2]];

                            splitTriNormal[0] = vTempSplitNormal[vTempSplitIndices[t3]];
                            splitTriNormal[1] = vTempSplitNormal[vTempSplitIndices[t3 + 1]];
                            splitTriNormal[2] = vTempSplitNormal[vTempSplitIndices[t3 + 2]];

                            splitTriUV2[0] = vTempSplitUV2[vTempSplitIndices[t3]];
                            splitTriUV2[1] = vTempSplitUV2[vTempSplitIndices[t3 + 1]];
                            splitTriUV2[2] = vTempSplitUV2[vTempSplitIndices[t3 + 2]];

                            if (hasSkin)
                            {
                                splitTriSkin[0] = vTempSplitSkin[vTempSplitIndices[t3]];
                                splitTriSkin[1] = vTempSplitSkin[vTempSplitIndices[t3 + 1]];
                                splitTriSkin[2] = vTempSplitSkin[vTempSplitIndices[t3 + 2]];
                            }

                            SplitTriangle(splitTriPos, splitTriNormal, splitTriUV, splitTriColor, splitTriUV2, splitTriSkin, cutPlane_N, cutPlane_D, vTempSplitPos, vTempSplitNormal, vTempSplitUV, vTempSplitColor, vTempSplitUV2, vTempSplitSkin, vTempSplitIndices, ref fixedOffsetLocal, ref fixedTriOffsetLocal, genVColor, false, hasSkin, tmpWeight);
                        }

                        // Cut by CA
                        cutPlane_N = decalCutPlaneCA_N;
                        cutPlane_D = decalCutPlaneCA_D;
                        var newUVStart = fixedOffset;//isRuntimeFixedArray ? fixedOffset : fixedOffset;
                        int newIndicesSplitBCCount = fixedTriOffsetLocal;//newIndicesSplitBC.Count;
                        for(int t3=newIndicesSplitABCount; t3<newIndicesSplitBCCount; t3 += 3)
                        {
                            splitTriPos[0] = vTempSplitPos[vTempSplitIndices[t3]];
                            splitTriPos[1] = vTempSplitPos[vTempSplitIndices[t3 + 1]];
                            splitTriPos[2] = vTempSplitPos[vTempSplitIndices[t3 + 2]];

                            splitTriNormal[0] = vTempSplitNormal[vTempSplitIndices[t3]];
                            splitTriNormal[1] = vTempSplitNormal[vTempSplitIndices[t3 + 1]];
                            splitTriNormal[2] = vTempSplitNormal[vTempSplitIndices[t3 + 2]];

                            splitTriUV2[0] = vTempSplitUV2[vTempSplitIndices[t3]];
                            splitTriUV2[1] = vTempSplitUV2[vTempSplitIndices[t3 + 1]];
                            splitTriUV2[2] = vTempSplitUV2[vTempSplitIndices[t3 + 2]];

                            if (hasSkin)
                            {
                                splitTriSkin[0] = vTempSplitSkin[vTempSplitIndices[t3]];
                                splitTriSkin[1] = vTempSplitSkin[vTempSplitIndices[t3 + 1]];
                                splitTriSkin[2] = vTempSplitSkin[vTempSplitIndices[t3 + 2]];
                            }

                            if (isRuntimeFixedArray)
                            {
                                SplitTriangle2(splitTriPos, splitTriNormal, splitTriUV, splitTriColor, splitTriUV2, splitTriSkin, cutPlane_N, cutPlane_D, decalID, ref fixedOffset, vPos, vNormal, vUV, vColor, vUV2, vSkin, vDecalID, genVColor, hasVUV, hasSkin, tmpWeight);
                            }
                            else
                            {
                                SplitTriangle(splitTriPos, splitTriNormal, splitTriUV, splitTriColor, splitTriUV2, splitTriSkin, cutPlane_N, cutPlane_D, vPos, vNormal, vUV, vColor, vUV2, vSkin, vIndices, ref fixedOffset, ref fixedTriOffset, genVColor, hasVUV, hasSkin, tmpWeight);
                            }
                        }

                        // Interpolate decal attribs over new tris
                        int newUVCount = fixedOffset;
                        for(int u=newUVStart; u<newUVCount; u++)
                        {
                            var wpos = vPos[u];
                            var lerpUV = triLerp(decalUVA, decalUVB, decalUVC, decalA, decalB, decalC, wpos);
                            vUV[u] = lerpUV;
                            if (genVColor)
                            {
                                float4 lerpColor;
                                if (srcVcolor)
                                {
                                    lerpColor = triLerp(decalColorA, decalColorB, decalColorC, decalA, decalB, decalC, wpos) * opacity;
                                }
                                else
                                {
                                    lerpColor = new float4(1,1,1,1);
                                }

                                if (angleFade)
                                {
                                    lerpColor *= math.clamp(math.dot(vNormal[u], decalTriPlaneN) * fadeMul + fadeAdd, 0.0f, 1.0f) * opacity;
                                }

                                vColor[u] = lerpColor;
                            }
                        }
                    }
                }
            }

            avgDir[0] = math.normalize(avgDir[0]);

            if (hasSkin)
            {
                rotMatrix = new float3x3(math.normalize(staticTformInvMatrix.c0.xyz), math.normalize(staticTformInvMatrix.c1.xyz), math.normalize(staticTformInvMatrix.c2.xyz));
                for(int v=0; v<fixedOffset; v++)
                {
                    var pos4 = math.mul(staticTformInvMatrix, new float4(vPos[v], 1.0f));
                    vPos[v] = new float3(pos4.x, pos4.y, pos4.z);
                    vNormal[v] = math.mul(rotMatrix, vNormal[v]);
                }
            }

            if (isRuntimeFixedArray)
            {
                int start, end;
                byte refValue = vDecalID[fixedOffset];
                if (refValue != 0)
                {
                    // Clear tail
                    start = fixedOffset;
                    end = fixedOffset + maxTrisInDecal;
                    float nan = 1.0f / 0.0f;
                    var nan3 = new float3(nan, nan, nan);
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
                    rotMatrix = new float3x3(math.normalize(staticTformInvMatrix.c0.xyz), math.normalize(staticTformInvMatrix.c1.xyz), math.normalize(staticTformInvMatrix.c2.xyz));
                    for(int i=start; i<end; i++)
                    {
                        int index = i;
                        if (index >= vPosSize) index = i % vPosSize;
                        
                        var pos4 = math.mul(staticTformInvMatrix, new float4(vPos[index], 1.0f));
                        vPos[index] = new float3(pos4.x, pos4.y, pos4.z);
                        vNormal[index] = math.mul(rotMatrix, vNormal[index]);
                    }
                }
                if (transformUV1)
                {
                    for(int i=start; i<end; i++)
                    {
                        int index = i;
                        if (index >= vPosSize) index = i % vPosSize;
                        vUV2[index] = new float2(vUV2[index].x * _lightmapScaleOffset.x + _lightmapScaleOffset.z, vUV2[index].y * _lightmapScaleOffset.y + _lightmapScaleOffset.w);
                    }
                }
                if (tangents)
                {
                    float3 a, b, c, edge1, edge2, tangent, binormal;
                    float2 ta, tb, tc, tedge1, tedge2;
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
                        tangent =  math.normalize((tedge2.y * edge1 - tedge1.y * edge2) * mul);
                        binormal = math.normalize((tedge1.x * edge2 - tedge2.x * edge1) * mul);
                        w = (math.dot(math.cross(vNormal[index], tangent), binormal) < 0.0f) ? -1.0f : 1.0f;
                        vTangents[index] = vTangents[index+1] = vTangents[index+2] = new float4(tangent.x, tangent.y, tangent.z, w);
                    }
                }
            }

            outputsInt[0] = fixedOffset;
            outputsInt[1] = fixedTriOffset;
        }
    }

#if UNITY_EDITOR
    static void OnBeforeAssemblyReload()
    {
        if (!staticNativeArraysInit) return;

        _splitTriPos.Dispose();
        _splitTriNormal.Dispose();
        _splitTriUV.Dispose();
        _splitTriUV2.Dispose();
        _splitTriColor.Dispose();
        _splitTriSkin.Dispose();
        nOutputs.Dispose();
        nAvgDir.Dispose();
        nvTempSplitPos.Dispose();
        nvTempSplitNormal.Dispose();
        nvTempSplitColor.Dispose();
        nvTempSplitUV.Dispose();
        nvTempSplitUV2.Dispose();
        nvTempSplitSkin.Dispose();
        nvTempSplitIndices.Dispose();
        nvPos.Dispose();
        nvNormal.Dispose();
        nvColor.Dispose();
        nvUV2.Dispose();
        nvUV.Dispose();
        nvSkin.Dispose();
        nvDecalID.Dispose();
        nvIndices.Dispose();
        nvCulled.Dispose();
        vEmptySkin.Dispose();
        vEmptyColor.Dispose();
        vEmptyTangent.Dispose();
        vEmptyIndices.Dispose();

        staticNativeArraysInit = false;
    }
#endif

    static void UpdateDecalFor(DecalGroup decal, Material sharedMaterial, Vector2[] _decalUV, Vector3[] _decalPosW, Transform decalTform, int[] _decalTris, Color[] _decalColor, GameObject parentObject, Material optionalMateralChange, float _opacity,
        ref int _fixedOffset, byte _decalID = 0, Mesh newMesh = null, DecalUtils.Group groupData = null, int _maxTrisInDecal = 0, bool _transformToParent = false, bool _earlyCull = false, bool _transformUV1 = false)
    {
        //var data = Mesh.AcquireReadOnlyMeshData(decalTform.GetComponent<MeshFilter>().sharedMesh);

        //var time0 = new System.Diagnostics.Stopwatch();
        //time0.Start();

        int _decalFlags = ((_decalUV != null && _decalUV.Length > 0) ? FLAG_UV : 0) |
                          ((_decalColor != null && _decalColor.Length > 0) ? FLAG_VC : 0);

        bool hasVColor = (_decalFlags & FLAG_VC) != 0;

        //time0.Stop();

        var v2zero = Vector2.zero;
        var v3zero = Vector3.zero;
        bool hasTform = decalTform != null;

        //var time2 = new System.Diagnostics.Stopwatch();
        //time2.Start();

        // Get receiver components and data

#if USE_TERRAINS
        Terrain terrain;
        var staticMesh = DecalUtils.GetSharedMesh(parentObject, out terrain);
        bool isTerrain = terrain != null;
        if (staticMesh == null && !isTerrain) return;
#else
        var staticMesh = DecalUtils.GetSharedMesh(parentObject);
        if (staticMesh == null) return;
#endif
        var staticTform = parentObject.transform;
        Renderer staticMR;
        Vector3[] _staticPos, _staticNorm;
        Vector2[] _staticUV2 = null;
        BoneWeight[] _staticSkin = null;
        int[] _staticTris;
        bool hasSkin = false;
        bool _isStaticFlipped = false;
        SkinnedMeshRenderer origSMR = null;
        int heightmapRes = 0;
        float terrainInvScaleX = 0;
        float terrainInvScaleZ = 0;

#if USE_TERRAINS
        DecalUtils.CachedTerrain cterrain = null;
        if (isTerrain)
        {
            var terrainData = terrain.terrainData;
            staticMR = null;
            var posOffset = staticTform.position;
            if (!DecalUtils.cachedTerrains.TryGetValue(terrainData, out cterrain))
            {
                cterrain = DecalUtils.PrepareTerrain(terrainData, posOffset, true);
            }
            if (cterrain.nStaticPos.Length == 0)
            {
                cterrain = DecalUtils.PrepareTerrain(terrainData, posOffset, true); // sometimes static vars are alive, but NativeArrays are reset
            }
            _staticPos = cterrain.pos;
            _staticNorm = cterrain.norm;
            _staticUV2 = cterrain.uv;
            _staticTris = cterrain.indices;

            heightmapRes = terrainData.heightmapResolution;
            terrainInvScaleX = (heightmapRes-1) / terrainData.size.x;
            terrainInvScaleZ = (heightmapRes-1) / terrainData.size.z;
        }
        else
#endif
        {
            staticMR = parentObject.GetComponent<Renderer>();
            _staticPos = staticMesh.vertices;
            _staticNorm = staticMesh.normals;
            _staticTris = staticMesh.triangles;
        }

        if (_staticNorm == null || _staticNorm.Length == 0)
        {
            if (decalTform == null) return;
            Debug.LogError("Receiver " + decalTform.name + " has no normals");
            return;
        }
        
#if USE_TERRAINS
        if (terrain == null)
#endif
        {
            _staticUV2 = staticMesh.uv2;
            if (_staticUV2.Length == 0)
            {
                _staticUV2 = staticMesh.uv;
            }

            origSMR = staticMR as SkinnedMeshRenderer;
            _staticSkin = staticMesh.boneWeights;
            hasSkin = _staticSkin != null && _staticSkin.Length > 0 && origSMR != null;

            _isStaticFlipped = Mathf.Sign(staticTform.lossyScale.x*staticTform.lossyScale.y*staticTform.lossyScale.z) < 0;
        }

        /*var _mesh = new Mesh();
        _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        _mesh.vertices = _staticPos;
        _mesh.triangles = _staticTris;
        _mesh.normals = _staticNorm;
        _mesh.uv = _staticUV2;
        _mesh.uv2 = _staticUV2;
        var terrGO = new GameObject();
        terrGO.name = "__ExportTerrain";
        GameObjectUtility.SetStaticEditorFlags(terrGO, StaticEditorFlags.LightmapStatic);
        var mf = terrGO.AddComponent<MeshFilter>();
        var mr = terrGO.AddComponent<MeshRenderer>();
        mf.sharedMesh = _mesh;
        var unlitTerrainMat = new Material(Shader.Find("Hidden/ftUnlitTerrain"));
        mr.sharedMaterial = unlitTerrainMat;*/

        int _staticFlags = hasSkin ? FLAG_SKIN : 0;

        var numStaticVerts = _staticPos.Length;

        //Debug.LogError("Getting mesh data time: " + time2.Elapsed.TotalMilliseconds);

        //time0.Start();

        NativeArray<float3> _nvPos, _nvNormal;
        NativeArray<float4> _nvColor, _nvTangents;
        NativeArray<float2> _nvUV2, _nvUV;
        NativeArray<BoneWeight> _nvSkin;
        NativeArray<byte> _nvDecalID;
        NativeArray<int> _nvIndices;

        if (!staticNativeArraysInit)
        {
            _splitTriPos = new NativeArray<float3>(3, Allocator.Persistent);
            _splitTriNormal = new NativeArray<float3>(3, Allocator.Persistent);
            _splitTriUV = new NativeArray<float2>(3, Allocator.Persistent);
            _splitTriUV2 = new NativeArray<float2>(3, Allocator.Persistent);
            _splitTriColor = new NativeArray<float4>(3, Allocator.Persistent);
            _splitTriSkin = new NativeArray<BoneWeight>(3, Allocator.Persistent);

            nOutputs = new   NativeArray<int>(2, Allocator.Persistent);
            nAvgDir = new    NativeArray<float3>(1, Allocator.Persistent);

            nvTempSplitPos = new    NativeArray<float3>(maxTempSplitVerts, Allocator.Persistent);
            nvTempSplitNormal = new NativeArray<float3>(maxTempSplitVerts, Allocator.Persistent);
            nvTempSplitColor = new  NativeArray<float4>(maxTempSplitVerts, Allocator.Persistent);
            nvTempSplitUV = new     NativeArray<float2>(maxTempSplitVerts, Allocator.Persistent);
            nvTempSplitUV2 = new    NativeArray<float2>(maxTempSplitVerts, Allocator.Persistent);
            nvTempSplitSkin = new   NativeArray<BoneWeight>(maxTempSplitVerts, Allocator.Persistent);
            nvTempSplitIndices = new   NativeArray<int>(maxTempSplitIndices, Allocator.Persistent);

            nvPos = new     NativeArray<float3>(maxOutputVerts, Allocator.Persistent);
            nvNormal = new  NativeArray<float3>(maxOutputVerts, Allocator.Persistent);
            nvColor = new   NativeArray<float4>(maxOutputVerts, Allocator.Persistent);
            nvUV2 = new     NativeArray<float2>(maxOutputVerts, Allocator.Persistent);
            nvUV = new      NativeArray<float2>(maxOutputVerts, Allocator.Persistent);
            nvSkin = new    NativeArray<BoneWeight>(maxOutputVerts, Allocator.Persistent);
            nvDecalID = new NativeArray<byte>(0, Allocator.Persistent);
            nvIndices = new NativeArray<int>(maxOutputIndices, Allocator.Persistent);
            nvCulled = new  NativeArray<bool>(maxOutputIndices, Allocator.Persistent);

            vEmptySkin = new NativeArray<BoneWeight>(0, Allocator.Persistent);
            vEmptyPos = new NativeArray<float3>(0, Allocator.Persistent);
            vEmptyNorm = new NativeArray<float3>(0, Allocator.Persistent);
            vEmptyColor = new NativeArray<float4>(0, Allocator.Persistent);
            vEmptyTangent = new NativeArray<float4>(0, Allocator.Persistent);
            vEmptyIndices = new NativeArray<int>(0, Allocator.Persistent);

            staticNativeArraysInit = true;

#if UNITY_EDITOR
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
#endif
        }

        // Allocation based on decal buffers
        var nDecalUV = new NativeArray<Vector2>(_decalUV, Allocator.TempJob).Reinterpret<float2>();
        var nDecalPosW = new NativeArray<Vector3>(_decalPosW, Allocator.TempJob).Reinterpret<float3>();
        var nDecalTris = new NativeArray<int>(_decalTris, Allocator.TempJob);
        var nDecalColor = hasVColor ? new NativeArray<Color>(_decalColor, Allocator.TempJob).Reinterpret<float4>() : vEmptyColor;

        // Allocation based on static buffers
        NativeArray<bool> nStaticTriCulled = nvCulled;
        NativeArray<float3> nStaticPos, nStaticPosW, nStaticNorm, nStaticNormW;
        NativeArray<float2> nStaticUV, nStaticUV2;
        NativeArray<BoneWeight> nStaticSkin;
        NativeArray<int> nStaticTris;
#if USE_TERRAINS
        if (isTerrain)
        {
            nStaticPos =    cterrain.nStaticPos;
            nStaticPosW =   vEmptyPos;
            nStaticNorm =   cterrain.nStaticNorm;
            nStaticNormW =  vEmptyNorm;
            nStaticUV =     nStaticUV2 =    cterrain.nStaticUV2;
            nStaticTris = cterrain.nStaticTris;
            nStaticSkin = vEmptySkin;
        }
        else
#endif
        {
            nStaticPos =  new NativeArray<Vector3>(_staticPos, Allocator.TempJob).Reinterpret<float3>();
            nStaticPosW = new NativeArray<float3>(numStaticVerts, Allocator.TempJob);
            nStaticNorm =  new NativeArray<Vector3>(_staticNorm, Allocator.TempJob).Reinterpret<float3>();
            nStaticNormW = new NativeArray<float3>(numStaticVerts, Allocator.TempJob);
            nStaticTris = new NativeArray<int>(_staticTris, Allocator.TempJob);
            if (_staticUV2.Length == 0)
            {
                nStaticUV2 = new NativeArray<float2>(_staticPos.Length, Allocator.TempJob);
            }
            else
            {
                nStaticUV2 = new NativeArray<Vector2>(_staticUV2, Allocator.TempJob).Reinterpret<float2>();
            }
            if (hasSkin)
            {
                nStaticSkin = new NativeArray<BoneWeight>(_staticSkin, Allocator.TempJob);
            }
            else
            {
                nStaticSkin = vEmptySkin;// new NativeArray<BoneWeight>(0, Allocator.TempJob);
            }
        }

        bool fixedArray = groupData != null;// _vPos != null;
        int vPosSize = 0;
        int fixedOffsetStart = _fixedOffset;

        if (fixedArray)
        {
            _nvPos      = groupData.nvPos.Reinterpret<float3>();//  = new     NativeArray<Vector3>(_vPos, Allocator.TempJob).Reinterpret<float3>();
            _nvNormal   = groupData.nvNormal.Reinterpret<float3>();//  = new  NativeArray<Vector3>(_vNormal, Allocator.TempJob).Reinterpret<float3>();
            _nvColor    = groupData.nvColor.Reinterpret<float4>();//  = new   NativeArray<Color>(_vColor, Allocator.TempJob).Reinterpret<float4>();
            _nvUV2      = groupData.nvUV2.Reinterpret<float2>();//  = new     NativeArray<Vector2>(_vUV2, Allocator.TempJob).Reinterpret<float2>();
            _nvUV       = groupData.nvUV.Reinterpret<float2>();//  = new      NativeArray<Vector2>(_vUV, Allocator.TempJob).Reinterpret<float2>();
            _nvSkin     = hasSkin ? groupData.nvSkin : nvSkin;//  = hasSkin ? new    NativeArray<BoneWeight>(_vSkin, Allocator.TempJob) : nvSkin;
            _nvDecalID  = groupData.nvDecalID;//  = new NativeArray<byte>(_vDecalID, Allocator.TempJob);
            _nvTangents = decal.tangents ? groupData.nvTangents.Reinterpret<float4>() : vEmptyTangent;

            _nvIndices = vEmptyIndices;
        }
        else
        {
            _nvPos =        nvPos;
            _nvNormal =     nvNormal;
            _nvColor =      nvColor;
            _nvTangents =   vEmptyTangent;
            _nvUV2 =        nvUV2;
            _nvUV =         nvUV;
            _nvSkin =       nvSkin;
            _nvDecalID =    nvDecalID;

            _nvIndices = nvIndices;

            _fixedOffset = 0;
        }
        int _fixedTriOffset = 0;

        //Debug.LogError("Array creation time: " + time0.Elapsed.TotalMilliseconds);

        //var time1 = new System.Diagnostics.Stopwatch();
        //time1.Start();
        var job = new UpdateDecalFor_Job
        {
            decalUV = nDecalUV,
            decalPosW = nDecalPosW,
            decalTris = nDecalTris,
            decalColor = nDecalColor,

            opacity = _opacity,
            
            roadTransform = decal.roadTransform,
            mainTextureScale =  (decal.roadTransform ? sharedMaterial.mainTextureScale : v2zero),
            mainTextureOffset = (decal.roadTransform ? sharedMaterial.mainTextureOffset : v2zero),

            atlasMinX = decal.atlasMinX,
            atlasMaxX = decal.atlasMaxX,
            atlasMinY = decal.atlasMinY,
            atlasMaxY = decal.atlasMaxY,

            tformRight =    hasTform ? decalTform.right : v3zero,
            tformUp =       hasTform ? decalTform.up : v3zero,
            tformForward =  hasTform ? decalTform.forward : v3zero,
            tformPosition = hasTform ? decalTform.position : v3zero,
            isDecalFlipped = hasTform ? (Mathf.Sign(decalTform.lossyScale.x*decalTform.lossyScale.y*decalTform.lossyScale.z) < 0) : false,

            staticPos = nStaticPos,
            staticPosW = nStaticPosW,
            staticNorm = nStaticNorm,
            staticNormW = nStaticNormW,
            staticUV2 = nStaticUV2,
            staticTris = nStaticTris,
            staticSkin = nStaticSkin,
            staticTriCulled = nStaticTriCulled,
            staticTformMatrix = staticTform.localToWorldMatrix,
            staticTformInvMatrix = staticTform.worldToLocalMatrix,
            isStaticFlipped = _isStaticFlipped,
#if USE_TERRAINS
            heightmapRes = heightmapRes,
            terrainInvScaleX = terrainInvScaleX,
            terrainInvScaleZ = terrainInvScaleZ,
#endif

            earlyCull = _earlyCull,
            cullingMtx = cullingMatrix,

            angleFade = decal.angleFade,
            angleClip = decal.angleClip,

            rayLength = decal.rayLength,
            bias = decal.bias,

            splitTriPos = _splitTriPos,
            splitTriNormal = _splitTriNormal,
            splitTriUV = _splitTriUV,
            splitTriUV2 = _splitTriUV2,
            splitTriColor = _splitTriColor,
            splitTriSkin = _splitTriSkin,

            isRuntimeFixedArray = fixedArray,
            fixedOffset = _fixedOffset,
            fixedTriOffset = _fixedTriOffset,
            vPos =      _nvPos,
            vNormal =   _nvNormal,
            vColor =    _nvColor,
            vTangents = _nvTangents,
            vUV2 =      _nvUV2,
            vUV =       _nvUV,
            vSkin =     _nvSkin,
            vDecalID =  _nvDecalID,
            vIndices =  _nvIndices,

            vTempSplitPos = nvTempSplitPos,
            vTempSplitNormal = nvTempSplitNormal,
            vTempSplitColor = nvTempSplitColor,
            vTempSplitUV = nvTempSplitUV,
            vTempSplitUV2 = nvTempSplitUV2,
            vTempSplitSkin = nvTempSplitSkin,
            vTempSplitIndices = nvTempSplitIndices,

            decalID = _decalID,

            decalFlags = _decalFlags,
            staticFlags = _staticFlags,

            tmpWeight = _tmpWeight,
            maxTrisInDecal = _maxTrisInDecal,
            transformToParent = _transformToParent,
            transformUV1 = _transformUV1,
            _lightmapScaleOffset = lightmapScaleOffset,

            tangents = decal.tangents,

            outputsInt = nOutputs,
            avgDir = nAvgDir

        };
        job.Schedule().Complete();
        //Debug.LogError("Job time: " + time1.Elapsed.TotalMilliseconds);

        decal.avgDir = job.avgDir[0];
        int fixedOffset = nOutputs[0];
        int fixedTriOffset = nOutputs[1];

        // Create decal mesh
        bool noMesh = false;
        bool srcVcolor = nDecalColor.Length > 0;
        bool useVColor = srcVcolor || decal.angleFade || _opacity != 1.0f;
        vPosSize = job.vPos.Length;
        if (newMesh == null)
        {
            if (fixedOffset == 0)
            {
                Debug.Log("No mesh was created for " + decal.name);
                noMesh = true;
            }

            if (!noMesh)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    if (decal.optimize)
                    {

                        int numOutVerts = 0;
                        int numOutIndices = 0;

                        WeldVerts(fixedOffset, fixedTriOffset, _decalFlags | _staticFlags | (useVColor ? FLAG_VC : 0), ref numOutVerts, ref numOutIndices,
                            nvIndices,   nvPos,   nvNormal,   nvUV,    nvUV2,   nvSkin,  nvColor,
                            nvIndices,   nvPos,   nvNormal,   nvUV,    nvUV2,   nvSkin,  nvColor);  // non-Burst version could use this optimization

                        fixedOffset = numOutVerts;
                        fixedTriOffset = numOutIndices;
                    }
                }
#endif

                newMesh = new Mesh();

                //time2 = new System.Diagnostics.Stopwatch();
                //time2.Start();

                newMesh.vertices =  new NativeSlice<Vector3>(job.vPos.Reinterpret<Vector3>(), 0, fixedOffset).ToArray();
                newMesh.normals =   new NativeSlice<Vector3>(job.vNormal.Reinterpret<Vector3>(), 0, fixedOffset).ToArray();
                newMesh.uv =        new NativeSlice<Vector2>(job.vUV.Reinterpret<Vector2>(), 0, fixedOffset).ToArray();
                newMesh.uv2 =       new NativeSlice<Vector2>(job.vUV2.Reinterpret<Vector2>(), 0, fixedOffset).ToArray();
                if (hasSkin)
                {
                    newMesh.boneWeights = new NativeSlice<BoneWeight>(job.vSkin, 0, fixedOffset).ToArray();
                    //var arr = newMesh.boneWeights;
                    //for(int i=0; i<arr.Length; i++)
                    //{
                        //Debug.LogError(arr[i].weight0);
                    //}
                }
                if (useVColor)  newMesh.colors = new NativeSlice<Color>(job.vColor.Reinterpret<Color>(), 0, fixedOffset).ToArray();
                newMesh.triangles = new NativeSlice<int>(job.vIndices, 0, fixedTriOffset).ToArray();

                if (decal.tangents) newMesh.RecalculateTangents();

                //Debug.LogError("Setting mesh data time: " + time2.Elapsed.TotalMilliseconds);

                //time2 = new System.Diagnostics.Stopwatch();
                //time2.Start();

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

                newGO.name = "NEW_DECAL#" + decal.name+"#"+parentObject.name;
                GameObject existingPrefab = null;

        #if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    if (newGO.scene != decal.gameObject.scene) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(newGO, decal.gameObject.scene);
                    decal.sceneObjects.Add(newGO);
                    EditorUtility.SetDirty(decal);
                    if (!decal.linkToParent) GameObjectUtility.SetStaticEditorFlags(newGO, StaticEditorFlags.BatchingStatic);
                }
        #endif
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
        }
        else
        {
            // Existing mesh
            int start, end;
            start = fixedOffsetStart;
            end = fixedOffset >= fixedOffsetStart ?  fixedOffset : (vPosSize + fixedOffset); // overshoot because we wrap in the loop

            newMesh.vertices =  new NativeSlice<Vector3>(job.vPos.Reinterpret<Vector3>(), 0, vPosSize).ToArray();
            newMesh.normals =   new NativeSlice<Vector3>(job.vNormal.Reinterpret<Vector3>(), 0, vPosSize).ToArray();
            newMesh.uv =        new NativeSlice<Vector2>(job.vUV.Reinterpret<Vector2>(), 0, vPosSize).ToArray();
            newMesh.uv2 =       new NativeSlice<Vector2>(job.vUV2.Reinterpret<Vector2>(), 0, vPosSize).ToArray();
            if (hasSkin) newMesh.boneWeights =  new NativeSlice<BoneWeight>(job.vSkin, 0, vPosSize).ToArray();
            if (useVColor) newMesh.colors =     new NativeSlice<Color>(job.vColor.Reinterpret<Color>(), 0, vPosSize).ToArray();
            if (decal.tangents) newMesh.tangents = new NativeSlice<Vector4>(job.vTangents.Reinterpret<Vector4>(), 0, vPosSize).ToArray();

            _fixedOffset = fixedOffset;
        }

        //time2 = new System.Diagnostics.Stopwatch();
        //time2.Start();

        /*if (fixedArray)
        {
            _nvPos.Dispose();
            _nvNormal.Dispose();
            _nvColor.Dispose();
            _nvUV2.Dispose();
            _nvUV.Dispose();
            if (hasSkin) _nvSkin.Dispose();
            _nvDecalID.Dispose();
        }*/

#if USE_TERRAINS
        if (!isTerrain)
#endif
        {
            nStaticPos.Dispose();
            nStaticPosW.Dispose();
            nStaticNorm.Dispose();
            nStaticNormW.Dispose();
            nStaticUV2.Dispose();
            nStaticTris.Dispose();
        }
        //nStaticTriCulled.Dispose();
        if (hasSkin) nStaticSkin.Dispose();

        //nvPos.Dispose();
        //nvNormal.Dispose();
        //nvUV2.Dispose();
        //nvUV.Dispose();
        //nvColor.Dispose();
        //nvSkin.Dispose();
        //nvDecalID.Dispose();
        //if (optimizedIndices) job.vIndices.Dispose();

        //nvTempSplitPos.Dispose();
        //nvTempSplitNormal.Dispose();
        //nvTempSplitColor.Dispose();
        //nvTempSplitUV.Dispose();
        //nvTempSplitUV2.Dispose();
        //nvTempSplitSkin.Dispose();
        //nvTempSplitIndices.Dispose();

        nDecalUV.Dispose();
        nDecalPosW.Dispose();
        nDecalTris.Dispose();
        if (hasVColor) nDecalColor.Dispose();

        //nOutputs.Dispose();
        //nAvgDir.Dispose();

        //Debug.LogError("Array dispose time: " + time2.Elapsed.TotalMilliseconds);
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
            Debug.Log("No mesh on " + decal.name);
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
            //Debug.LogError("Generation time (burst): " + time1.Elapsed.TotalMilliseconds);
        }
    }

#if UNITY_2018_1_OR_NEWER
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    static float GetDistanceToPoint(float3 pn, float pd, float3 p)
    {
        return math.dot(pn, p) + pd;
    }

#if UNITY_2018_1_OR_NEWER
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    static bool PlaneRaycast(float3 planeN, float planeD, float3 rayO, float3 rayDir, out float enter)
    {
        float vdot = math.dot(rayDir, planeN);
        float ndot = -math.dot(rayO, planeN) - planeD;

        if (math.abs(vdot) < 0.00001f) //if (Mathf.Approximately(vdot, 0.0f))
        {
            enter = 0.0F;
            return false;
        }

        enter = ndot / vdot;

        return enter > 0.0F;
    }

    // Old list function
    static void SplitTriangle(NativeArray<float3> triPos, NativeArray<float3> triNormal, NativeArray<float2> triUV, NativeArray<float4> triColor, NativeArray<float2> triUV2, NativeArray<BoneWeight> triSkin, float3 planeN, float planeD, NativeArray<float3> newPos, NativeArray<float3> newNormal, NativeArray<float2> newUV, 
        NativeArray<float4> newColor, NativeArray<float2> newUV2, NativeArray<BoneWeight> newSkin, NativeArray<int> newIndices, ref int fixedOffset, ref int fixedTriOffset, bool useVColor, bool useUV, bool hasSkin, BoneWeight tmpWeight)
    {
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

        if (hasSkin)
        {
            Ab = triSkin[0];
            Bb = triSkin[1];
            Cb = triSkin[2];
        }

        var Ac = triColor[0];
        var Bc = triColor[1];
        var Cc = triColor[2];

        float pa = GetDistanceToPoint(planeN, planeD, Ap);
        float pb = GetDistanceToPoint(planeN, planeD, Bp);
        float pc = GetDistanceToPoint(planeN, planeD, Cp);

        int A,B,C,D,E;
        float3 dp,ep;

        float signA = math.sign(pa);
        float signB = math.sign(pb);
        float signC = math.sign(pc);

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
                newPos[fixedOffset] = Ap;
                newPos[fixedOffset+1] = Bp;
                newPos[fixedOffset+2] = Cp;

                newNormal[fixedOffset] = An;
                newNormal[fixedOffset+1] = Bn;
                newNormal[fixedOffset+2] = Cn;

                newUV2[fixedOffset] = At2;
                newUV2[fixedOffset+1] = Bt2;
                newUV2[fixedOffset+2] = Ct2;

                if (useVColor)
                {
                    newColor[fixedOffset] = Ac;
                    newColor[fixedOffset+1] = Bc;
                    newColor[fixedOffset+2] = Cc;
                }

                if (useUV)
                {
                    newUV[fixedOffset] = At;
                    newUV[fixedOffset+1] = Bt;
                    newUV[fixedOffset+2] = Ct;
                }

                if (hasSkin)
                {
                    newSkin[fixedOffset] = Ab;
                    newSkin[fixedOffset+1] = Bb;
                    newSkin[fixedOffset+2] = Cb;
                }

                newIndices[fixedTriOffset] = fixedOffset;
                newIndices[fixedTriOffset+1] = fixedOffset+1;
                newIndices[fixedTriOffset+2] = fixedOffset+2;

                fixedOffset += 3;
                fixedTriOffset += 3;
            }
            return;
        }

        if (signA == signB) //AB|C
        {
            float dist;
            var rayAC_O = Ap;
            var rayAC_D = math.normalize(Cp - Ap);
            PlaneRaycast(planeN, planeD, rayAC_O, rayAC_D, out dist);
            dp = rayAC_O + rayAC_D * dist;
            var di = math.saturate(dist / math.length(Cp-Ap));
            dp = math.lerp(Ap, Cp, di);
            var dn = math.lerp(An, Cn, di);
            var dt = math.lerp(At, Ct, di);
            var dt2 = math.lerp(At2, Ct2, di);
            BoneWeight db = tmpWeight;
            if (hasSkin) db = LerpBoneWeights(Ab, Cb, di);
            var dc = math.lerp(Ac, Cc, di);

            var rayBC_O = Bp;
            var rayBC_D = math.normalize(Cp-Bp);
            PlaneRaycast(planeN, planeD, rayBC_O, rayBC_D, out dist);
            ep = rayBC_O + rayBC_D * dist;
            var ei = math.saturate(dist / math.length(Cp-Bp));
            ep = math.lerp(Bp, Cp, ei);
            var en = math.lerp(Bn, Cn, ei);
            var et = math.lerp(Bt, Ct, ei);
            var et2 = math.lerp(Bt2, Ct2, ei);
            BoneWeight eb = tmpWeight;
            if (hasSkin) eb = LerpBoneWeights(Bb, Cb, ei);
            var ec = math.lerp(Bc, Cc, ei);

            // surf a

            A = fixedOffset;
            B = fixedOffset+1;
            C = fixedOffset+2;
            D = fixedOffset+3;
            E = fixedOffset+4;

            newPos[A] = Ap;
            newPos[B] = Bp;
            newPos[C] = Cp;
            newPos[D] = dp;
            newPos[E] = ep;

            newNormal[A] = An;
            newNormal[B] = Bn;
            newNormal[C] = Cn;
            newNormal[D] = dn;
            newNormal[E] = en;

            newUV2[A] = At2;
            newUV2[B] = Bt2;
            newUV2[C] = Ct2;
            newUV2[D] = dt2;
            newUV2[E] = et2;

            if (useVColor)
            {
                newColor[A] = Ac;
                newColor[B] = Bc;
                newColor[C] = Cc;
                newColor[D] = dc;
                newColor[E] = ec;
            }

            if (useUV)
            {
                newUV[A] = At;
                newUV[B] = Bt;
                newUV[C] = Ct;
                newUV[D] = dt;
                newUV[E] = et;
            }

            if (hasSkin)
            {
                newSkin[A] = Ab;
                newSkin[B] = Bb;
                newSkin[C] = Cb;
                newSkin[D] = db;
                newSkin[E] = eb;
            }

            fixedOffset += 5;

            if (pa<0.0f)
            {
                newIndices[fixedTriOffset] = D;
                newIndices[fixedTriOffset+1] = A;
                newIndices[fixedTriOffset+2] = B;

                newIndices[fixedTriOffset+3] = D;
                newIndices[fixedTriOffset+4] = B;
                newIndices[fixedTriOffset+5] = E;

                fixedTriOffset += 6;
            }

            if (pa>0.0f)
            {
                newIndices[fixedTriOffset] = C;
                newIndices[fixedTriOffset+1] = D;
                newIndices[fixedTriOffset+2] = E;

                fixedTriOffset += 3;
            }

            return;
        }

        if (signA == signC) //AC|B
        {
            float dist;
            var rayAB_O = Ap;
            var rayAB_D = math.normalize(Bp-Ap);
            PlaneRaycast(planeN, planeD, rayAB_O, rayAB_D, out dist);
            dp = rayAB_O + rayAB_D * dist;
            var di = math.saturate(dist / math.length(Bp-Ap));
            dp = math.lerp(Ap, Bp, di);
            var dn = math.lerp(An, Bn, di);
            var dt = math.lerp(At, Bt, di);
            var dt2 = math.lerp(At2, Bt2, di);
            BoneWeight db = tmpWeight;
            if (hasSkin) db = LerpBoneWeights(Ab, Bb, di);
            var dc = math.lerp(Ac, Bc, di);

            var rayBC_O = Bp;
            var rayBC_D = math.normalize(Cp-Bp);
            PlaneRaycast(planeN, planeD, rayBC_O, rayBC_D, out dist);
            ep = rayBC_O + rayBC_D * dist;
            var ei = math.saturate(dist / math.length(Cp-Bp));
            ep = math.lerp(Bp, Cp, ei);
            var en = math.lerp(Bn, Cn, ei);
            var et = math.lerp(Bt, Ct, ei);
            var et2 = math.lerp(Bt2, Ct2, ei);
            BoneWeight eb = tmpWeight;
            if (hasSkin) eb = LerpBoneWeights(Bb, Cb, ei);
            var ec = math.lerp(Bc, Cc, ei);

            // surf a
            A = fixedOffset;
            B = fixedOffset+1;
            C = fixedOffset+2;
            D = fixedOffset+3;
            E = fixedOffset+4;

            newPos[A] = Ap;
            newPos[B] = Bp;
            newPos[C] = Cp;
            newPos[D] = dp;
            newPos[E] = ep;

            newNormal[A] = An;
            newNormal[B] = Bn;
            newNormal[C] = Cn;
            newNormal[D] = dn;
            newNormal[E] = en;

            newUV2[A] = At2;
            newUV2[B] = Bt2;
            newUV2[C] = Ct2;
            newUV2[D] = dt2;
            newUV2[E] = et2;

            if (useVColor)
            {
                newColor[A] = Ac;
                newColor[B] = Bc;
                newColor[C] = Cc;
                newColor[D] = dc;
                newColor[E] = ec;
            }

            if (useUV)
            {
                newUV[A] = At;
                newUV[B] = Bt;
                newUV[C] = Ct;
                newUV[D] = dt;
                newUV[E] = et;
            }

            if (hasSkin)
            {
                newSkin[A] = Ab;
                newSkin[B] = Bb;
                newSkin[C] = Cb;
                newSkin[D] = db;
                newSkin[E] = eb;
            }

            fixedOffset += 5;

            if (pa<0.0f)
            {
                newIndices[fixedTriOffset] = A;
                newIndices[fixedTriOffset+1] = D;
                newIndices[fixedTriOffset+2] = C;

                newIndices[fixedTriOffset+3] = D;
                newIndices[fixedTriOffset+4] = E;
                newIndices[fixedTriOffset+5] = C;

                fixedTriOffset += 6;
            }

            if (pa>0.0f)
            {
                newIndices[fixedTriOffset] = B;
                newIndices[fixedTriOffset+1] = E;
                newIndices[fixedTriOffset+2] = D;

                fixedTriOffset += 3;
            }

            return;
        }

        if (signB == signC) //BC|A
        {
            float dist;
            var rayAC_O = Ap;
            var rayAC_D = math.normalize(Cp-Ap);
            PlaneRaycast(planeN, planeD, rayAC_O, rayAC_D, out dist);
            dp = rayAC_O + rayAC_D * dist;
            var di = math.saturate(dist / math.length(Cp-Ap));
            dp = math.lerp(Ap, Cp, di);
            var dn = math.lerp(An, Cn, di);
            //var dT = math.lerp(AT, CT, di);
            var dt = math.lerp(At, Ct, di);
            var dt2 = math.lerp(At2, Ct2, di);
            BoneWeight db = tmpWeight;
            if (hasSkin) db = LerpBoneWeights(Ab, Cb, di);
            var dc = math.lerp(Ac, Cc, di);

            var rayBA_O = Bp;
            var rayBA_D = math.normalize(Ap-Bp);
            PlaneRaycast(planeN, planeD, rayBA_O, rayBA_D, out dist);
            ep = rayBA_O + rayBA_D * dist;
            var ei = math.saturate(dist / math.length(Ap-Bp));
            ep = math.lerp(Bp, Ap, ei);
            var en = math.lerp(Bn, An, ei);
            //var eT = Vector3.Lerp(BT, AT, ei);
            var et = math.lerp(Bt, At, ei);
            var et2 = math.lerp(Bt2, At2, ei);
            BoneWeight eb = tmpWeight;
            if (hasSkin) eb = LerpBoneWeights(Bb, Ab, ei);
            var ec = math.lerp(Bc, Cc, ei);

            // surf b
            A = fixedOffset;
            B = fixedOffset+1;
            C = fixedOffset+2;
            D = fixedOffset+3;
            E = fixedOffset+4;

            newPos[A] = Ap;
            newPos[B] = Bp;
            newPos[C] = Cp;
            newPos[D] = dp;
            newPos[E] = ep;

            newNormal[A] = An;
            newNormal[B] = Bn;
            newNormal[C] = Cn;
            newNormal[D] = dn;
            newNormal[E] = en;

            newUV2[A] = At2;
            newUV2[B] = Bt2;
            newUV2[C] = Ct2;
            newUV2[D] = dt2;
            newUV2[E] = et2;

            if (useVColor)
            {
                newColor[A] = Ac;
                newColor[B] = Bc;
                newColor[C] = Cc;
                newColor[D] = dc;
                newColor[E] = ec;
            }

            if (useUV)
            {
                newUV[A] = At;
                newUV[B] = Bt;
                newUV[C] = Ct;
                newUV[D] = dt;
                newUV[E] = et;
            }

            if (hasSkin)
            {
                newSkin[A] = Ab;
                newSkin[B] = Bb;
                newSkin[C] = Cb;
                newSkin[D] = db;
                newSkin[E] = eb;
            }

            fixedOffset += 5;

            if (pa>0.0f)
            {
                newIndices[fixedTriOffset] = B;
                newIndices[fixedTriOffset+1] = C;
                newIndices[fixedTriOffset+2] = D;

                newIndices[fixedTriOffset+3] = B;
                newIndices[fixedTriOffset+4] = D;
                newIndices[fixedTriOffset+5] = E;

                fixedTriOffset += 6;
            }

            if (pa<=0.0f)
            {
                newIndices[fixedTriOffset] = A;
                newIndices[fixedTriOffset+1] = E;
                newIndices[fixedTriOffset+2] = D;

                fixedTriOffset += 3;
            }

            return;
        }
    }

#if UNITY_2018_1_OR_NEWER
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    static void AddTriangle(ref int fixedOffset, NativeArray<float3> vPos, NativeArray<float3> vNormal, bool hasVColor, NativeArray<float4> vColor, NativeArray<float2> vUV2, bool hasVUV, NativeArray<float2> vUV, bool hasVSkin, NativeArray<BoneWeight> vSkin, NativeArray<byte> vDecalID,
                                 float3 Ap, float3 An, float4 Ac, float2 At2, float2 At, BoneWeight Ab,
                                 float3 Bp, float3 Bn, float4 Bc, float2 Bt2, float2 Bt, BoneWeight Bb,
                                 float3 Cp, float3 Cn, float4 Cc, float2 Ct2, float2 Ct, BoneWeight Cb, byte decalID)
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
        if (hasVColor)
        {
            vColor[A] = Ac;
            vColor[B] = Bc;
            vColor[C] = Cc;
        }
        vUV2[A] = At2;
        vUV2[B] = Bt2;
        vUV2[C] = Ct2;
        if (hasVUV)
        {
            vUV[A] = At;
            vUV[B] = Bt;
            vUV[C] = Ct;
        }
        if (hasVSkin)
        {
            vSkin[A] = Ab;
            vSkin[B] = Bb;
            vSkin[C] = Cb;
        }
        vDecalID[A] = vDecalID[B] = vDecalID[C] = decalID;
        fixedOffset += 3;
    }

    // New fixed array function
    static void SplitTriangle2(NativeArray<float3> triPos, NativeArray<float3> triNormal, NativeArray<float2> triUV, NativeArray<float4> triColor, NativeArray<float2> triUV2, NativeArray<BoneWeight> triSkin, float3 planeN, float planeD, byte decalID, ref int fixedOffset, 
     NativeArray<float3> vPos, NativeArray<float3> vNormal, NativeArray<float2> vUV, NativeArray<float4> vColor, NativeArray<float2> vUV2, NativeArray<BoneWeight> vSkin, NativeArray<byte> vDecalID, bool useVColor, bool useUV, bool hasSkin, BoneWeight tmpWeight)
    {
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
        if (hasSkin)
        {
            Ab = triSkin[0];
            Bb = triSkin[1];
            Cb = triSkin[2];
        }

        var Ac = triColor[0];
        var Bc = triColor[1];
        var Cc = triColor[2];

        float pa = GetDistanceToPoint(planeN, planeD, Ap);
        float pb = GetDistanceToPoint(planeN, planeD, Bp);
        float pc = GetDistanceToPoint(planeN, planeD, Cp);

        float3 dp,ep;

        float signA = math.sign(pa);
        float signB = math.sign(pb);
        float signC = math.sign(pc);

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
                AddTriangle(ref fixedOffset, vPos, vNormal, useVColor, vColor, vUV2, useUV, vUV, hasSkin, vSkin, vDecalID,
                                             Ap, An, Ac, At2, At, Ab,
                                             Bp, Bn, Bc, Bt2, Bt, Bb,
                                             Cp, Cn, Cc, Ct2, Ct, Cb, decalID);
            }

            return;
        }

        if (signA == signB) //AB|C
        {
            float dist;
            var rayAC_O = Ap;
            var rayAC_D = math.normalize(Cp - Ap);
            PlaneRaycast(planeN, planeD, rayAC_O, rayAC_D, out dist);
            dp = rayAC_O + rayAC_D * dist;
            var di = math.saturate(dist / math.length(Cp-Ap));
            dp = math.lerp(Ap, Cp, di);
            var dn = math.lerp(An, Cn, di);
            var dt = math.lerp(At, Ct, di);
            var dt2 = math.lerp(At2, Ct2, di);
            BoneWeight db = tmpWeight;
            if (hasSkin) db = LerpBoneWeights(Ab, Cb, di);
            var dc = math.lerp(Ac, Cc, di);

            var rayBC_O = Bp;
            var rayBC_D = math.normalize(Cp-Bp);
            PlaneRaycast(planeN, planeD, rayBC_O, rayBC_D, out dist);
            ep = rayBC_O + rayBC_D * dist;
            var ei = math.saturate(dist / math.length(Cp-Bp));
            ep = math.lerp(Bp, Cp, ei);
            var en = math.lerp(Bn, Cn, ei);
            var et = math.lerp(Bt, Ct, ei);
            var et2 = math.lerp(Bt2, Ct2, ei);
            BoneWeight eb = tmpWeight;
            if (hasSkin) eb = LerpBoneWeights(Bb, Cb, ei);
            var ec = math.lerp(Bc, Cc, ei);

            if (fixedOffset + 9 >= vPosSize) fixedOffset = 0;

            // surf a
            if (pa < 0.0f)
            {
                AddTriangle(ref fixedOffset, vPos, vNormal, useVColor, vColor, vUV2, useUV, vUV, hasSkin, vSkin, vDecalID,
                                             dp, dn, dc, dt2, dt, db,
                                             Ap, An, Ac, At2, At, Ab,
                                             Bp, Bn, Bc, Bt2, Bt, Bb, decalID);

                AddTriangle(ref fixedOffset, vPos, vNormal, useVColor, vColor, vUV2, useUV, vUV, hasSkin, vSkin, vDecalID,
                                             dp, dn, dc, dt2, dt, db,
                                             Bp, Bn, Bc, Bt2, Bt, Bb,
                                             ep, en, ec, et2, et, eb, decalID);
            }

            // surf b
            if (pa >= 0.0f)
            {
                AddTriangle(ref fixedOffset, vPos, vNormal, useVColor, vColor, vUV2, useUV, vUV, hasSkin, vSkin, vDecalID,
                                             Cp, Cn, Cc, Ct2, Ct, Cb,
                                             dp, dn, dc, dt2, dt, db,
                                             ep, en, ec, et2, et, eb, decalID);
            }

            return;
        }

        if (signA == signC) //AC|B
        {
            float dist;
            var rayAB_O = Ap;
            var rayAB_D = math.normalize(Bp-Ap);
            PlaneRaycast(planeN, planeD, rayAB_O, rayAB_D, out dist);
            dp = rayAB_O + rayAB_D * dist;
            var di = math.saturate(dist / math.length(Bp-Ap));
            dp = math.lerp(Ap, Bp, di);
            var dn = math.lerp(An, Bn, di);
            var dt = math.lerp(At, Bt, di);
            var dt2 = math.lerp(At2, Bt2, di);
            BoneWeight db = tmpWeight;
            if (hasSkin) db = LerpBoneWeights(Ab, Bb, di);
            var dc = math.lerp(Ac, Bc, di);

            var rayBC_O = Bp;
            var rayBC_D = math.normalize(Cp-Bp);
            PlaneRaycast(planeN, planeD, rayBC_O, rayBC_D, out dist);
            ep = rayBC_O + rayBC_D * dist;
            var ei = math.saturate(dist / math.length(Cp-Bp));
            ep = math.lerp(Bp, Cp, ei);
            var en = math.lerp(Bn, Cn, ei);
            var et = math.lerp(Bt, Ct, ei);
            var et2 = math.lerp(Bt2, Ct2, ei);
            BoneWeight eb = tmpWeight;
            if (hasSkin) eb = LerpBoneWeights(Bb, Cb, ei);
            var ec = math.lerp(Bc, Cc, ei);

            if (fixedOffset + 9 >= vPosSize) fixedOffset = 0;

            // surf a
            if (pa < 0.0f)
            {
                AddTriangle(ref fixedOffset, vPos, vNormal, useVColor, vColor, vUV2, useUV, vUV, hasSkin, vSkin, vDecalID,
                                             Ap, An, Ac, At2, At, Ab,
                                             dp, dn, dc, dt2, dt, db,
                                             Cp, Cn, Cc, Ct2, Ct, Cb, decalID);

                AddTriangle(ref fixedOffset, vPos, vNormal, useVColor, vColor, vUV2, useUV, vUV, hasSkin, vSkin, vDecalID,
                                             dp, dn, dc, dt2, dt, db,
                                             ep, en, ec, et2, et, eb,
                                             Cp, Cn, Cc, Ct2, Ct, Cb, decalID);
            }

            // surf b
            if (pa >= 0.0f)
            {
                AddTriangle(ref fixedOffset, vPos, vNormal, useVColor, vColor, vUV2, useUV, vUV, hasSkin, vSkin, vDecalID,
                                             Bp, Bn, Bc, Bt2, Bt, Bb,
                                             ep, en, ec, et2, et, eb,
                                             dp, dn, dc, dt2, dt, db, decalID);
            }

            return;
        }

        if (signB == signC) //BC|A
        {
            float dist;
            var rayAC_O = Ap;
            var rayAC_D = math.normalize(Cp-Ap);
            PlaneRaycast(planeN, planeD, rayAC_O, rayAC_D, out dist);
            dp = rayAC_O + rayAC_D * dist;
            var di = math.saturate(dist / math.length(Cp-Ap));
            dp = math.lerp(Ap, Cp, di);
            var dn = math.lerp(An, Cn, di);
            //var dT = math.lerp(AT, CT, di);
            var dt = math.lerp(At, Ct, di);
            var dt2 = math.lerp(At2, Ct2, di);
            BoneWeight db = tmpWeight;
            if (hasSkin) db = LerpBoneWeights(Ab, Cb, di);
            var dc = math.lerp(Ac, Cc, di);

            var rayBA_O = Bp;
            var rayBA_D = math.normalize(Ap-Bp);
            PlaneRaycast(planeN, planeD, rayBA_O, rayBA_D, out dist);
            ep = rayBA_O + rayBA_D * dist;
            var ei = math.saturate(dist / math.length(Ap-Bp));
            ep = math.lerp(Bp, Ap, ei);
            var en = math.lerp(Bn, An, ei);
            //var eT = Vector3.Lerp(BT, AT, ei);
            var et = math.lerp(Bt, At, ei);
            var et2 = math.lerp(Bt2, At2, ei);
            BoneWeight eb = tmpWeight;
            if (hasSkin) eb = LerpBoneWeights(Bb, Ab, ei);
            var ec = math.lerp(Bc, Cc, ei);

            if (fixedOffset + 9 >= vPosSize) fixedOffset = 0;

            // surf a
            if (pa > 0.0f)
            {
                AddTriangle(ref fixedOffset, vPos, vNormal, useVColor, vColor, vUV2, useUV, vUV, hasSkin, vSkin, vDecalID,
                                             Bp, Bn, Bc, Bt2, Bt, Bb,
                                             Cp, Cn, Cc, Ct2, Ct, Cb,
                                             dp, dn, dc, dt2, dt, db, decalID);

                AddTriangle(ref fixedOffset, vPos, vNormal, useVColor, vColor, vUV2, useUV, vUV, hasSkin, vSkin, vDecalID,
                                             Bp, Bn, Bc, Bt2, Bt, Bb,
                                             dp, dn, dc, dt2, dt, db,
                                             ep, en, ec, et2, et, eb, decalID);
            }

            // surf b
            if (pa <= 0.0f)
            {
                AddTriangle(ref fixedOffset, vPos, vNormal, useVColor, vColor, vUV2, useUV, vUV, hasSkin, vSkin, vDecalID,
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

        /*group.vPos = new Vector3[desc.maxTrisTotal * 3];
        group.vNormal = new Vector3[desc.maxTrisTotal * 3];
        group.vUV = new Vector2[desc.maxTrisTotal * 3];
        group.vUV2 = new Vector2[desc.maxTrisTotal * 3];
        group.vColor = new Color[desc.maxTrisTotal * 3];
        group.vDecalID = new byte[desc.maxTrisTotal * 3];*/

        group.nvPos = new     NativeArray<Vector3>(desc.maxTrisTotal * 3, Allocator.Persistent);
        group.nvNormal = new  NativeArray<Vector3>(desc.maxTrisTotal * 3, Allocator.Persistent);
        group.nvColor = new   NativeArray<Color>(desc.maxTrisTotal * 3, Allocator.Persistent);
        group.nvUV2 = new     NativeArray<Vector2>(desc.maxTrisTotal * 3, Allocator.Persistent);
        group.nvUV = new      NativeArray<Vector2>(desc.maxTrisTotal * 3, Allocator.Persistent);
        group.nvDecalID =  new NativeArray<byte>(desc.maxTrisTotal * 3, Allocator.Persistent);

        if (isSkinned) group.nvSkin = new NativeArray<BoneWeight>(desc.maxTrisTotal * 3, Allocator.Persistent);
        if (desc.tangents) group.nvTangents = new NativeArray<Vector4>(desc.maxTrisTotal * 3, Allocator.Persistent);

        //if (desc.tangents) group.vTangents = new Vector4[desc.maxTrisTotal * 3];
        //if (isSkinned) group.vSkin = new BoneWeight[desc.maxTrisTotal * 3];

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

        UpdateDecalFor(decal, group.material, decalUV, decalPosW, null, decalTris, null, receiver.gameObject, null, desc.opacity, ref group.fixedOffset, (byte)((group.numDecals % 255) + 1), group.mesh, group, group.maxTrisInDecal, transformToParent, cull, transformUV1);

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

#else

// Fallback
public class CPUBurstDecalUtils : MonoBehaviour
{
#if UNITY_EDITOR
    public static bool checkPrefabs = false;
#endif
    
    public static void UpdateDecal(DecalGroup decal)
    {
        Debug.LogError("Burst/Mathematics packages are not installed; Please use any other mode or install them.");
    }

    public static DecalUtils.Group CreateGroup(DecalUtils.GroupDesc desc)
    {
        Debug.LogError("Burst/Mathematics packages are not installed; Please use any other mode or install them.");
        return null;
    }

    public static void AddDecal(DecalUtils.Group group, DecalUtils.DecalDesc desc, GameObject receiver)
    {
    }

    public static void ClearDecals(DecalUtils.Group group)
    {
    }
}

#endif
    