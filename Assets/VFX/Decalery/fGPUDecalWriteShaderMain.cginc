#include "clip.cginc"

struct appdata
{
	float4 vertex : POSITION;
	float2 uv : TEXCOORD1;
	float3 normal : NORMAL0;
};

struct v2g
{
	float2 uv : TEXCOORD0;
	float3 worldNormal : TEXCOORD1;
	float3 worldPos : TEXCOORD2;
};

struct g2f
{
	float4 vertex : SV_POSITION;
	float3 worldPosA : TEXCOORD0;
	float3 worldPosB : TEXCOORD1;
	float3 worldPosC : TEXCOORD2;
	float3 worldNormalA : TEXCOORD3;
	float3 worldNormalB : TEXCOORD4;
	float3 worldNormalC : TEXCOORD5;
	float2 uvA : TEXCOORD6;
	float2 uvB : TEXCOORD7;
	float2 uvC : TEXCOORD8;
	uint triID : TEXCOORD9;
};

v2g vert2(appdata IN, uint vertexID : SV_VertexID)
{
	Triangle tri = _TempVertexBuffer[vertexID/3];
	v2g OUT;

	if (vertexID % 3 == 0)
	{
		OUT.worldPos = tri.worldPosA;
		OUT.worldNormal = tri.worldNormalA;
		OUT.uv = tri.uvA;
	}
	else if (vertexID % 3 == 1)
	{
		OUT.worldPos = tri.worldPosB;
		OUT.worldNormal = tri.worldNormalB;
		OUT.uv = tri.uvB;
	}
	else
	{
		OUT.worldPos = tri.worldPosC;
		OUT.worldNormal = tri.worldNormalC;
		OUT.uv = tri.uvC;
	}

	return OUT;
}

#ifdef TRAIL
float2 _DecalTrailFT0, _DecalTrailFT1, _DecalTrailFT2;
float3 _DecalTrailFP0, _DecalTrailFP1, _DecalTrailFP2;

float2 _DecalTrailST0, _DecalTrailST1, _DecalTrailST2;
float3 _DecalTrailSP0, _DecalTrailSP1, _DecalTrailSP2;


float3 triLerp(float3 a, float3 b, float3 c, float3 pp)
{
    float3 v0 = b - a;
    float3 v1 = c - a;
    float3 v2 = pp - a;
    float d00 = dot(v0, v0);
    float d01 = dot(v0, v1);
    float d11 = dot(v1, v1);
    float d20 = dot(v2, v0);
    float d21 = dot(v2, v1);
    float denom = d00 * d11 - d01 * d01;
    float invDenom = 1.0f / denom;
    float v = (d11 * d20 - d01 * d21) * invDenom;
    float w = (d00 * d21 - d01 * d20) * invDenom;
    float u = 1.0f - v - w;
    return float3(u,v,w);
}
#endif

bool BoundsIntersect(float3 amin, float3 amax, float3 bmin, float3 bmax)
{
	bmax += 0.0001f;
	bmin -= 0.0001f;

	return	(amin.x <= bmax.x) && (amax.x >= bmin.x) &&
			(amin.y <= bmax.y) && (amax.y >= bmin.y) &&
			(amin.z <= bmax.z) && (amax.z >= bmin.z);
}

void AddTriangle(inout TriangleStream<g2f> triStream, g2f OUT, VertexData A, VertexData B, VertexData C)
{
	OUT.worldPosA = A.pos;
	OUT.worldNormalA = A.normal;

	OUT.worldPosB = B.pos;
	OUT.worldNormalB = B.normal;

	OUT.worldPosC = C.pos;
	OUT.worldNormalC = C.normal;
	
	OUT.uvA = A.uv;
	OUT.uvB = B.uv;
	OUT.uvC = C.uv;

	OUT.vertex = float4(-1,  3, 0.5, 1);
	triStream.Append(OUT);

	OUT.vertex = float4(3,  -1, 0.5, 1);
	triStream.Append(OUT);

	OUT.vertex = float4(-1, -1, 0.5, 1);
	triStream.Append(OUT);

	triStream.RestartStrip();
}

float3 trinormal(float3 v0, float3 v1, float3 v2)
{
	return normalize(cross(v0 - v1, v1 - v2));
}

[maxvertexcount(18)]
void geom (triangle v2g IN[3], inout TriangleStream<g2f> triStream, uint triID : SV_PrimitiveID)
{
#ifdef CLIP_BOUNDS
	//float3 bmin = min(min(IN[0].worldPos, IN[1].worldPos), IN[2].worldPos);
	//float3 bmax = max(max(IN[0].worldPos, IN[1].worldPos), IN[2].worldPos);
	//if (!BoundsIntersect(bmin, bmax, _DecalMin, _DecalMax)) return;

	float4 ba = mul(_DecalMatrix, float4(IN[0].worldPos, 1));
	float4 bb = mul(_DecalMatrix, float4(IN[1].worldPos, 1));
	float4 bc = mul(_DecalMatrix, float4(IN[2].worldPos, 1));
	float3 bmin = min(min(ba, bb), bc);
	float3 bmax = max(max(ba, bb), bc);

	if (!BoundsIntersect(bmin, bmax, -1, 1)) return;

	float3 decalForward = normalize(float3(_DecalMatrix._31, _DecalMatrix._32, _DecalMatrix._33));
	float3 tnormal = -trinormal(IN[0].worldPos, IN[1].worldPos, IN[2].worldPos);
	if (dot(tnormal, decalForward) < _DecalAngleClip) return;
#endif
	g2f OUT;
	OUT.triID = triID;
	OUT.worldPosA = IN[0].worldPos;
	OUT.worldPosB = IN[1].worldPos;
	OUT.worldPosC = IN[2].worldPos;

	OUT.worldNormalA = IN[0].worldNormal;
	OUT.worldNormalB = IN[1].worldNormal;
	OUT.worldNormalC = IN[2].worldNormal;
	OUT.uvA = IN[0].uv;
	OUT.uvB = IN[1].uv;
	OUT.uvC = IN[2].uv;

	float4 plane = _DecalPlane0;//float4(1, 0, 0, dot(float3(-251.285, 0.816, -94.5), float3(-1,0,0)));

	VertexData newVerts[5];


#ifdef CLIP_PLANE0
	newVerts[0].pos = OUT.worldPosA + plane.xyz * plane.w;
	newVerts[0].normal = OUT.worldNormalA;
	newVerts[0].uv = OUT.uvA;				
	
	newVerts[1].pos = OUT.worldPosB + plane.xyz * plane.w;
	newVerts[1].normal = OUT.worldNormalB;
	newVerts[1].uv = OUT.uvB;
	
	newVerts[2].pos = OUT.worldPosC + plane.xyz * plane.w;
	newVerts[2].normal = OUT.worldNormalC;
	newVerts[2].uv = OUT.uvC;

	newVerts[3] = newVerts[0];
	newVerts[4] = newVerts[0];
	int numVerts = clip3(-plane.xyz, newVerts[0], newVerts[1], newVerts[2], newVerts[3]);
	if (numVerts > 0)
	{
	#ifdef CLIP_PLANE1
		newVerts[0].pos -= plane.xyz * plane.w;
		newVerts[1].pos -= plane.xyz * plane.w;
		newVerts[2].pos -= plane.xyz * plane.w;
		newVerts[3].pos -= plane.xyz * plane.w;

		plane = _DecalPlane1;
		newVerts[0].pos += plane.xyz * plane.w;
		newVerts[1].pos += plane.xyz * plane.w;
		newVerts[2].pos += plane.xyz * plane.w;
		newVerts[3].pos += plane.xyz * plane.w;
		if (numVerts > 3)
		{

			numVerts = clip4(-plane.xyz, newVerts[0], newVerts[1], newVerts[2], newVerts[3], newVerts[4]);
		}
		else
		{
			numVerts = clip3(-plane.xyz, newVerts[0], newVerts[1], newVerts[2], newVerts[3]);
		}
	#endif
		if (numVerts > 0)
		{
			newVerts[0].pos -= plane.xyz * plane.w;
			newVerts[1].pos -= plane.xyz * plane.w;
			newVerts[2].pos -= plane.xyz * plane.w;
			newVerts[3].pos -= plane.xyz * plane.w;
			newVerts[4].pos -= plane.xyz * plane.w;

			AddTriangle(triStream, OUT, newVerts[0], newVerts[1], newVerts[2]);
			if (numVerts > 3)
			{
				AddTriangle(triStream, OUT, newVerts[3], newVerts[0], newVerts[2]);
				if (numVerts > 4)
				{
					//AddTriangle(triStream, OUT, newVerts[4], newVerts[1], newVerts[3]);
					AddTriangle(triStream, OUT, newVerts[4], newVerts[0], newVerts[3]);
				}
			}
		}
	}
#else
	newVerts[0].pos = OUT.worldPosA;
	newVerts[0].normal = OUT.worldNormalA;
	newVerts[0].uv = OUT.uvA;				
	
	newVerts[1].pos = OUT.worldPosB;
	newVerts[1].normal = OUT.worldNormalB;
	newVerts[1].uv = OUT.uvB;
	
	newVerts[2].pos = OUT.worldPosC;
	newVerts[2].normal = OUT.worldNormalC;
	newVerts[2].uv = OUT.uvC;

	AddTriangle(triStream, OUT, newVerts[0], newVerts[1], newVerts[2]);
#endif
}

float2 ProjectUV(float3 pos)
{
#ifdef TRAIL
	float3 uvw = triLerp(_DecalTrailFP0, _DecalTrailFP1, _DecalTrailFP2, pos);
	if (uvw.x + uvw.y + uvw.z <= 1)
	{
		return _DecalTrailFT0*uvw.x + _DecalTrailFT1*uvw.y + _DecalTrailFT2*uvw.z;
	}
	else
	{
		uvw = triLerp(_DecalTrailSP0, _DecalTrailSP1, _DecalTrailSP2, pos);
		return _DecalTrailST0*uvw.x + _DecalTrailST1*uvw.y + _DecalTrailST2*uvw.z;
	}
#else
	return mul(_DecalMatrix, float4(pos,1)).xy * 0.5 + 0.5;
#endif
}

float4 frag (g2f IN) : SV_Target
{
#ifdef PROJECT_UV
	Triangle2 t;
	t.pad1 = t.pad2 = 0;
#else
	Triangle t;
#endif
	t.worldPosA = IN.worldPosA;
	t.worldPosB = IN.worldPosB;
	t.worldPosC = IN.worldPosC;
	t.worldNormalA.xyz = IN.worldNormalA;
	t.worldNormalB.xyz = IN.worldNormalB;
	t.worldNormalC.xyz = IN.worldNormalC;
#ifdef TRANSFORM_TO_PARENT
	t.worldPosA = mul(_DecalParentMatrix, float4(t.worldPosA, 1));
	t.worldPosB = mul(_DecalParentMatrix, float4(t.worldPosB, 1));
	t.worldPosC = mul(_DecalParentMatrix, float4(t.worldPosC, 1));
	t.worldNormalA.xyz = mul((float3x3)_DecalParentMatrix, IN.worldNormalA);
	t.worldNormalB.xyz = mul((float3x3)_DecalParentMatrix, IN.worldNormalB);
	t.worldNormalC.xyz = mul((float3x3)_DecalParentMatrix, IN.worldNormalC);
#endif
	t.uvA = IN.uvA;
	t.uvB = IN.uvB;
	t.uvC = IN.uvC;
#ifdef PROJECT_UV
	t.uv2A = ProjectUV(IN.worldPosA);
	t.uv2B = ProjectUV(IN.worldPosB);
	t.uv2C = ProjectUV(IN.worldPosC);
	float3 decalForward = -normalize(float3(_DecalMatrix._31, _DecalMatrix._32, _DecalMatrix._33));
	float fadeA = saturate(dot(normalize(IN.worldNormalA), decalForward) * _DecalOpacity);
	float fadeB = saturate(dot(normalize(IN.worldNormalB), decalForward) * _DecalOpacity);
	float fadeC = saturate(dot(normalize(IN.worldNormalC), decalForward) * _DecalOpacity);
	#ifdef COMPRESS
		uint fa = fadeA*255;
		uint fb = fadeB*255;
		uint fc = fadeC*255;
		t.fade = fa | (fb<<8) | (fc<<16);
	#else
		t.fadeA = fadeA;
		t.fadeB = fadeB;
		t.fadeC = fadeC;
	#endif
#endif

#ifdef TANGENTS
	float3 edge1 = t.worldPosB - t.worldPosA;
	float3 edge2 = t.worldPosC - t.worldPosA;
	float2 tedge1 = t.uv2B - t.uv2A;
	float2 tedge2 = t.uv2C - t.uv2A;
	float mul = 1.0f / (tedge1.x * tedge2.y - tedge2.x * tedge1.y);
	float3 tangent =  normalize((tedge2.y * edge1 - tedge1.y * edge2) * mul);
	float3 binormal = normalize((tedge1.x * edge2 - tedge2.x * edge1) * mul);
	float w = (dot(cross(t.worldNormalA, tangent), binormal) < 0.0f) ? -1.0f : 1.0f;
	t.tangentA = t.tangentB = t.tangentC = float4(tangent.x, tangent.y, tangent.z, w);
#endif

	t.decalID = _DecalID;

	//triID = fGPUDecalVBuffer.IncrementCounter();
	
	uint triID;
	InterlockedAdd(_DecalArgBuffer[0], 3, triID);
	triID = (triID / 3) % (uint)_DecalBufferSize;

	fGPUDecalVBuffer[triID] = t;

	return 1;
}

struct v2fClear
{
	float4 vertex : SV_POSITION;
};

v2fClear vertClear (uint id : SV_VertexID)
{
    v2fClear o;
    float4 pos[3];
	pos[0] = float4(-1,  3, 0.5, 1);
	pos[1] = float4(3,  -1, 0.5, 1);
	pos[2] = float4(-1, -1, 0.5, 1);
    o.vertex = pos[id % 3];
    return o;
}

float4 fragClear (uint triID : SV_PrimitiveID) : SV_Target
{
	uint nextDecalTriID = _DecalArgBuffer[0] / 3;
	nextDecalTriID = nextDecalTriID % (uint)_DecalBufferSize;
	float refValue = fGPUDecalVBuffer[nextDecalTriID].decalID;

	uint procDecalTriID = (_DecalArgBuffer[0] / 3) + triID;
	procDecalTriID = procDecalTriID % (uint)_DecalBufferSize;
#ifdef PROJECT_UV
	Triangle2 decalTri = fGPUDecalVBuffer[procDecalTriID];
#else
	Triangle decalTri = fGPUDecalVBuffer[procDecalTriID];
#endif
	if (decalTri.decalID == refValue)
	{
		decalTri.worldPosA = decalTri.worldPosB = decalTri.worldPosC = 1.0f / 0.0f;
		fGPUDecalVBuffer[procDecalTriID] = decalTri;
	}

	if (triID == 0)
	{
		_TempDecalArgBuffer[0] = 0;
		_DecalDrawArgBuffer[0] = min(_DecalArgBuffer[0], ((uint)_DecalBufferSize)*3);
	}

	return 1;
}