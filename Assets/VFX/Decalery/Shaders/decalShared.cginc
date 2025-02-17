#ifndef DECALSHARED
#define DECALSHARED

//#define COMPRESS
#ifdef COMPRESS
	struct Triangle2
	{
		float3 worldPosA, worldPosB, worldPosC;
		float decalID;
		half3 worldNormalA, worldNormalB, worldNormalC;
		half2 uvA, uvB, uvC;
		half2 uv2A, uv2B, uv2C;
		uint fade;
	};
#else
	struct Triangle2
	{
	
		float3 worldPosA, worldNormalA;
	#ifdef NORMALMAP
			float4 tangentA;
	#endif
		float fadeA;
		float2 uv2A, uvA;
		float pad1;

		float3 worldPosB, worldNormalB;
	#ifdef NORMALMAP
			float4 tangentB;
	#endif
		float fadeB;
		float2 uv2B, uvB;
		float pad2;

		float3 worldPosC, worldNormalC;
	#ifdef NORMALMAP
			float4 tangentC;
	#endif
		float fadeC;
		float2 uv2C, uvC;
		float decalID;
	};
#endif
StructuredBuffer<Triangle2> fGPUDecalVBuffer;

float3 GetDecalLocalPos(uint vertexID)
{
	Triangle2 tri = fGPUDecalVBuffer[vertexID/3];
	uint v = vertexID % 3;
	return v==0 ? tri.worldPosA : (v==1 ? tri.worldPosB : tri.worldPosC);
}

float3 GetDecalLocalNormal(uint vertexID)
{
	Triangle2 tri = fGPUDecalVBuffer[vertexID/3];
	uint v = vertexID % 3;
	return v==0 ? tri.worldNormalA : (v==1 ? tri.worldNormalB : tri.worldNormalC);
}

float2 GetDecalUV0(uint vertexID)
{
	Triangle2 tri = fGPUDecalVBuffer[vertexID/3];
	uint v = vertexID % 3;
	return v==0 ? tri.uv2A : (v==1 ? tri.uv2B : tri.uv2C);
}

float2 GetDecalUV1(uint vertexID)
{
	Triangle2 tri = fGPUDecalVBuffer[vertexID/3];
	uint v = vertexID % 3;
	return v==0 ? tri.uvA : (v==1 ? tri.uvB : tri.uvC);
}

float GetDecalFade(uint vertexID)
{
	Triangle2 tri = fGPUDecalVBuffer[vertexID/3];
	uint v = vertexID % 3;

	#ifdef COMPRESS
		uint shift = v==0 ? 0 : (v==1 ? 8 : 16);
		return ((tri.fade >> shift) & 0xFF) / 255.0f;
	#else
		return v==0 ? tri.fadeA : (v==1 ? tri.fadeB : tri.fadeC);
	#endif
}

#ifdef NORMALMAP
float4 GetDecalLocalTangent(uint vertexID)
{
	Triangle2 tri = fGPUDecalVBuffer[vertexID/3];
	uint v = vertexID % 3;
	return v==0 ? tri.tangentA : (v==1 ? tri.tangentB : tri.tangentC);
}
#endif


void GetDecalLocalPos_float(uint vertexID, out float3 pos)
{
	pos = GetDecalLocalPos(vertexID);
}

void GetDecalLocalNormal_float(uint vertexID, out float3 normal)
{
	normal = GetDecalLocalNormal(vertexID);
}

void GetDecalUV0_float(uint vertexID, out float2 uv)
{
	uv = GetDecalUV0(vertexID);
}

void GetDecalUV1_float(uint vertexID, out float2 uv)
{
	uv = GetDecalUV1(vertexID);
}

void GetDecalFade_float(uint vertexID, out float fade)
{
	fade = GetDecalFade(vertexID);
}

void GetDecalLocalTangent_float(uint vertexID, out float4 tangent)
{
#ifdef NORMALMAP
	tangent = GetDecalLocalTangent(vertexID);
#else
	tangent = 0;
#endif
}

#endif