#ifdef COMPRESS
	struct Triangle
	{
		float3 worldPosA, worldPosB, worldPosC;
		half3 worldNormalA, worldNormalB, worldNormalC;
		half2 uvA, uvB, uvC;
		float decalID;
	};

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
	struct Triangle
	{
		float3 worldPosA, worldPosB, worldPosC;
		float3 worldNormalA, worldNormalB, worldNormalC;
		float2 uvA, uvB, uvC;
		float decalID; // can be 1 bit...
	};

	struct Triangle2
	{
		float3 worldPosA, worldNormalA;
#ifdef TANGENTS
		float4 tangentA;
#endif
		float fadeA;
		float2 uv2A, uvA;
		float pad1;

		float3 worldPosB, worldNormalB;
#ifdef TANGENTS
		float4 tangentB;
#endif
		float fadeB;
		float2 uv2B, uvB;
		float pad2;

		float3 worldPosC, worldNormalC;
#ifdef TANGENTS
		float4 tangentC;
#endif
		float fadeC;
		float2 uv2C, uvC;
		float decalID;
	};

	/*struct Triangle2
	{
		float3 worldPosA, worldPosB, worldPosC;
		float3 worldNormalA, worldNormalB, worldNormalC;
		float2 uvA, uvB, uvC;
		float2 uv2A, uv2B, uv2C;
		float fadeA, fadeB, fadeC;
		float decalID;
	};*/
#endif

#ifdef PROJECT_UV
	RWStructuredBuffer<Triangle2> fGPUDecalVBuffer : u1;
#else
	RWStructuredBuffer<Triangle> fGPUDecalVBuffer : u1;

#endif	
RWBuffer<uint> _DecalArgBuffer : u2;

StructuredBuffer<Triangle> _TempVertexBuffer;
RWBuffer<uint> _TempDecalArgBuffer : u3;
RWBuffer<uint> _DecalDrawArgBuffer : u4;

float4x4 _DecalMatrix;
float4x4 _DecalParentMatrix;
//float3 _DecalMin;
//float3 _DecalMax;
float _DecalBufferSize;
float _DecalID;
float4 _DecalPlane0;
float4 _DecalPlane1;
float4 _DecalLightmapST;
//float4 _DecalPlane2;
//float4 _DecalPlane3;
float _DecalAngleClip;
float _DecalOpacity;
