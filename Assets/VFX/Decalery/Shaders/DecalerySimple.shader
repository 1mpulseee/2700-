Shader "Decalery/DecalerySimple"
{
    Properties
    {
        _Color ("Color (RGB), Opacity (A)", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}

        _FogBlend ("Fog blending value (-1 is default fog)", Float) = -1.0
        _Bias ("Bias", Range(0,0.1)) = 0.003

        [Enum(UnityEngine.Rendering.BlendMode)] _BlendSrc ("Blend mode: Source", Int) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _BlendDst ("Blend mode: Destination", Int) = 10
        [Enum(UnityEngine.Rendering.BlendOp)] _BlendOp ("Blend operattion", Int) = 0

    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="AlphaTest+50" "ForceNoShadowCasting"="True" }
        Blend [_BlendSrc] [_BlendDst]
        BlendOp [_BlendOp]
        ZWrite Off
        ColorMask RGB

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile _ INDIRECT_DRAW

            #include "UnityCG.cginc"

#ifdef INDIRECT_DRAW
            #include "decalShared.cginc"
#endif

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR0;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float fade : COLOR0;
                UNITY_FOG_COORDS(1)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _Color;
            float _FogBlend;
            float _Bias;

            v2f vert (appdata v, uint vertexID : SV_VertexID)
            {
                v2f o;

                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.fade = v.color;

#ifdef INDIRECT_DRAW

                v.vertex.xyz = GetDecalLocalPos(vertexID);
                v.uv = GetDecalUV0(vertexID);
                o.fade = GetDecalFade(vertexID);
#endif
                float3 worldPos = mul(unity_ObjectToWorld, float4(v.vertex.xyz,1)).xyz;
                worldPos += normalize(_WorldSpaceCameraPos - worldPos) * _Bias;// 0.001f; // hardcoded, but pretty good
                o.vertex = mul(UNITY_MATRIX_VP, float4(worldPos,1));
                o.uv = v.uv;

                UNITY_TRANSFER_FOG(o, o.vertex);
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float4 color = tex2D(_MainTex, i.uv) * _Color;
                color.a *= i.fade;

                if (_FogBlend < 0) _FogBlend = unity_FogColor;
                UNITY_APPLY_FOG_COLOR(i.fogCoord, color, float4(_FogBlend, _FogBlend, _FogBlend, 1));
                return color;
            }
            ENDCG
        }
    }
    CustomEditor "DecalerySimpleShaderGUI"
}
