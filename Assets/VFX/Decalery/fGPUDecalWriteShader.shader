Shader "Hidden/fGPUDecalWriteShader"
{
	Properties
	{
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 100
		Cull Off
		ZTest Always
		ZWrite Off

		Pass
		{
			Name "WriteDecal"
			CGPROGRAM
			#pragma exclude_renderers gles gles3 switch
			#pragma target 5.0
			#pragma vertex vert
			#pragma geometry geom
			#pragma fragment frag

			#define CLIP_BOUNDS
			#define CLIP_PLANE0
			#define CLIP_PLANE1
			//#define COMPRESS
			
			#include "UnityCG.cginc"
			#include "fGPUDecalWriteShaderShared.cginc"
			#include "fGPUDecalWriteShaderMain.cginc"

			v2g vert (appdata IN)
			{
				v2g OUT;
				OUT.uv = IN.uv * _DecalLightmapST.xy + _DecalLightmapST.zw;
				OUT.worldNormal = normalize(mul((float3x3)unity_ObjectToWorld, IN.normal));
				OUT.worldPos = mul(unity_ObjectToWorld, IN.vertex).xyz;
				return OUT;
			}
			ENDCG
		}

		Pass
		{
			Name "WriteDecalSecondPass"
			CGPROGRAM
			#pragma exclude_renderers gles gles3 switch
			#pragma target 5.0
			#pragma vertex vert2
			#pragma geometry geom
			#pragma fragment frag

			#define CLIP_PLANE0
			#define CLIP_PLANE1
			#define TRANSFORM_TO_PARENT
			#define PROJECT_UV
			//#define COMPRESS

			#include "UnityCG.cginc"
			#include "fGPUDecalWriteShaderShared.cginc"
			#include "fGPUDecalWriteShaderMain.cginc"

			ENDCG
		}

		Pass
		{
			Name "WriteDecalSecondPassTrail"
			CGPROGRAM
			#pragma exclude_renderers gles gles3 switch
			#pragma target 5.0
			#pragma vertex vert2
			#pragma geometry geom
			#pragma fragment frag

			#define CLIP_PLANE0
			#define CLIP_PLANE1
			#define TRANSFORM_TO_PARENT
			#define PROJECT_UV
			//#define COMPRESS
			#define TRAIL

			#include "UnityCG.cginc"
			#include "fGPUDecalWriteShaderShared.cginc"
			#include "fGPUDecalWriteShaderMain.cginc"

			ENDCG
		}

		Pass
		{
			Name "WriteDecalSecondPassNormal"
			CGPROGRAM
			#pragma exclude_renderers gles gles3 switch
			#pragma target 5.0
			#pragma vertex vert2
			#pragma geometry geom
			#pragma fragment frag

			#define CLIP_PLANE0
			#define CLIP_PLANE1
			#define TRANSFORM_TO_PARENT
			#define PROJECT_UV
			//#define COMPRESS
			#define TANGENTS

			#include "UnityCG.cginc"
			#include "fGPUDecalWriteShaderShared.cginc"
			#include "fGPUDecalWriteShaderMain.cginc"

			ENDCG
		}

		Pass
		{
			Name "WriteDecalSecondPassTrailNormal"
			CGPROGRAM
			#pragma exclude_renderers gles gles3 switch
			#pragma target 5.0
			#pragma vertex vert2
			#pragma geometry geom
			#pragma fragment frag

			#define CLIP_PLANE0
			#define CLIP_PLANE1
			#define TRANSFORM_TO_PARENT
			#define PROJECT_UV
			//#define COMPRESS
			#define TRAIL
			#define TANGENTS

			#include "UnityCG.cginc"
			#include "fGPUDecalWriteShaderShared.cginc"
			#include "fGPUDecalWriteShaderMain.cginc"

			ENDCG
		}

		Pass
		{
			Name "ClearPartialDecal"
			CGPROGRAM
			#pragma exclude_renderers gles gles3 switch
			#pragma target 5.0
			#pragma vertex vertClear
			#pragma fragment fragClear

			#define PROJECT_UV
			
			#include "UnityCG.cginc"
			#include "fGPUDecalWriteShaderShared.cginc"
			#include "fGPUDecalWriteShaderMain.cginc"

			ENDCG
		}

		Pass
		{
			Name "ClearPartialDecalNormal"
			CGPROGRAM
			#pragma exclude_renderers gles gles3 switch
			#pragma target 5.0
			#pragma vertex vertClear
			#pragma fragment fragClear

			#define PROJECT_UV
			#define TANGENTS
			
			#include "UnityCG.cginc"
			#include "fGPUDecalWriteShaderShared.cginc"
			#include "fGPUDecalWriteShaderMain.cginc"

			ENDCG
		}
	}
}
