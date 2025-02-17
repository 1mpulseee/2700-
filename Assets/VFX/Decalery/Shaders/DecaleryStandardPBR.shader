Shader "Decalery/DecaleryStandardPBR"
{
    Properties
    {
        _Color ("Color (RGB), Opacity (A)", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
        _BumpMap ("Normal map", 2D) = "bump" {}
        _GlossMap ("Gloss map", 2D) = "white" {}
        _MetallicMap ("Metallic map", 2D) = "white" {}
        _AOMap ("AO map", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _Bias ("Bias", Range(0,0.1)) = 0.003

        [Toggle(NORMALMAP)] _NORMALMAP ("Use normal map", Int) = 0
        [Toggle(MODE_NORMALONLY)] _MODE_NORMALONLY ("Normal map only (deferred)", Int) = 0
        [Toggle(MODE_NORMAL_AO_ONLY)] _MODE_NORMAL_AO_ONLY ("Normal map and AO only (deferred)", Int) = 0
        [Toggle(SPECULAR)] _SPECULAR ("Specular highlights (forward)", Int) = 0
        [Toggle(REFLECTIONS)] _REFLECTIONS ("Reflections (forward)", Int) = 0
        [Toggle(BAKERY_SH)] _BAKERY_SH ("Bakery SH Mode", Int) = 0
        [Toggle(BAKERY_MONOSH)] _BAKERY_MONOSH ("Bakery MonoSH Mode", Int) = 0
        [Toggle(BAKERY_VOLUME)] _BAKERY_VOLUME ("Bakery Volume Mode", Int) = 0

        [Enum(UnityEngine.Rendering.BlendMode)] _BlendSrc ("Blend mode: Source", Int) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _BlendDst ("Blend mode: Destination", Int) = 10
        [Enum(UnityEngine.Rendering.BlendMode)] _BlendSrcA ("Blend mode alpha: Source", Int) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _BlendDstA ("Blend mode alpha: Destination", Int) = 10
        [Enum(UnityEngine.Rendering.BlendMode)] _BlendSrcE ("Blend mode emission: Source", Int) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _BlendDstE ("Blend mode emission: Destination", Int) = 10
        __ColorMask ("Color mask", Int) = 14
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="AlphaTest+50" "ForceNoShadowCasting"="True" }
        Blend [_BlendSrc] [_BlendDst], [_BlendSrcA] [_BlendDstA]
        ZWrite Off
        ColorMask [__ColorMask]

        Pass
        {
            Name "ForwardBase"
            Tags { "LightMode" = "ForwardBase" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_fwdbase
            #pragma multi_compile_instancing

            #pragma shader_feature NORMALMAP
            #pragma shader_feature SPECULAR
            #pragma shader_feature REFLECTIONS
            #pragma multi_compile _ INDIRECT_DRAW
            #pragma shader_feature _ BAKERY_SH BAKERY_MONOSH BAKERY_VOLUME
            #pragma multi_compile _ BAKERY_COMPRESSED_VOLUME

            #define pos Position
            #define v IN
            #define vertex Position

#if UNITY_VERSION >= 201740 // ADDED FIX
            SamplerState bakery_trilinear_clamp_sampler;
            #define samplerunity_Lightmap bakery_trilinear_clamp_sampler
#else
            #define bakery_trilinear_clamp_sampler samplerunity_Lightmap
#endif

#ifdef INDIRECT_DRAW
            #include "decalShared.cginc"
            Texture2D _Lightmap, _LightmapInd, _ShadowMask;
            #define unity_Lightmap _Lightmap
            #define unity_LightmapInd _LightmapInd
            #define unity_ShadowMask _ShadowMask
            #define sampler_Lightmap bakery_trilinear_clamp_sampler
            #define sampler_ShadowMask bakery_trilinear_clamp_sampler
#endif

            #include "UnityCG.cginc"
            #include "UnityShadowLibrary.cginc"
            #include "UnityPBSLighting.cginc"
            #include "AutoLight.cginc"

            float SampleBakedOcclusion (float2 lightmapUV, float3 worldPos)
            {
                #if defined (SHADOWS_SHADOWMASK)
                    #if defined(LIGHTMAP_ON)
                        fixed4 rawOcclusionMask = unity_ShadowMask.Sample(bakery_trilinear_clamp_sampler, lightmapUV.xy);
                        return saturate(dot(rawOcclusionMask, unity_OcclusionMaskSelector));
                    #endif
                #endif
                return 1;
            }

            struct appdata
            {
                float4 Position : POSITION;
                float2 TexCoord0 : TEXCOORD0;
                float2 TexCoord1 : TEXCOORD1;
                float3 Normal : NORMAL;
                float4 Tangent : TANGENT;
                float Fade : COLOR0;
#ifdef INDIRECT_DRAW
                uint VertexID : SV_VertexID;
#endif

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 Position : SV_POSITION;
                float2 TexCoord0 : TEXCOORD0;
                float2 TexCoord1 : TEXCOORD1;
                float3 Normal : TEXCOORD2;
                UNITY_FOG_COORDS(3)
#ifdef NORMALMAP
                float3 Tangent : TEXCOORD4;
                float3 Binormal : TEXCOORD5;
#endif
                float3 WorldPos : TEXCOORD6;
                float Fade : COLOR0;
#if UNITY_VERSION >= 201740 // ADDED FIX
                UNITY_LIGHTING_COORDS(7,8)
#else
                LIGHTING_COORDS(7,8) // ADDED FIX
#endif
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex, _BumpMap, _GlossMap, _MetallicMap, _AOMap;
            float4 _Color;
            float _Glossiness, _Metallic;
            float _Bias;

            v2f vert (appdata IN)
            {
                v2f OUT;

                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_OUTPUT(v2f, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.TexCoord0 = IN.TexCoord0;
                OUT.TexCoord1 = IN.TexCoord1 * unity_LightmapST.xy + unity_LightmapST.zw;
                OUT.Fade = IN.Fade;

#ifdef INDIRECT_DRAW
                IN.Position.xyz = GetDecalLocalPos(IN.VertexID);
                IN.Normal = GetDecalLocalNormal(IN.VertexID);
                OUT.TexCoord0 = GetDecalUV0(IN.VertexID);
                OUT.TexCoord1 = GetDecalUV1(IN.VertexID);
                OUT.Fade = GetDecalFade(IN.VertexID);
    #ifdef NORMALMAP
                IN.Tangent = GetDecalLocalTangent(IN.VertexID);
    #endif
#endif

                float3 worldPos = mul(unity_ObjectToWorld, float4(IN.Position.xyz,1)).xyz;
                OUT.WorldPos = worldPos;

                worldPos += normalize(_WorldSpaceCameraPos - worldPos) * _Bias; // hardcoded, but pretty good
                OUT.Position = mul(UNITY_MATRIX_VP, float4(worldPos,1));

                OUT.Normal = normalize(mul((float3x3)unity_ObjectToWorld, IN.Normal));

#ifdef NORMALMAP
                half sign = IN.Tangent.w * unity_WorldTransformParams.w;
                OUT.Tangent = normalize(mul((float3x3)unity_ObjectToWorld, IN.Tangent.xyz));
                OUT.Binormal = (cross(OUT.Normal, OUT.Tangent) * sign);
#endif
                UNITY_TRANSFER_FOG(OUT, OUT.Position);
#if UNITY_VERSION >= 201740 // ADDED FIX
                UNITY_TRANSFER_LIGHTING(OUT, IN.TexCoord1.xy); // pass shadow and, possibly, light cookie coordinates to pixel shader
#else
                UNITY_TRANSFER_SHADOW(OUT, IN.TexCoord1.xy) // ADDED FIX
#endif
                return OUT;
            }

#if defined(BAKERY_SH) || defined(BAKERY_MONOSH) || defined(BAKERY_VOLUME)
            #include "BakeryDecalSupport.cginc"
#endif

            float4 frag (v2f IN) : SV_Target
            {
                float4 albedoAlpha = tex2D(_MainTex, IN.TexCoord0) * _Color;
                albedoAlpha.a *= IN.Fade;
                if (albedoAlpha.a < 1.0f / 256) discard;

#ifdef NORMALMAP
                float3 normalMap = UnpackNormal(tex2D(_BumpMap, IN.TexCoord0));
                float3 worldNormal = normalize(IN.Tangent * normalMap.x + IN.Binormal * normalMap.y + IN.Normal * normalMap.z);
#else
                float3 worldNormal = normalize(IN.Normal);
#endif

#if defined(SPECULAR) || defined(REFLECTIONS)
                float Glossiness = _Glossiness * tex2D(_GlossMap, IN.TexCoord0).g;
                float Metallic = _Metallic * tex2D(_MetallicMap, IN.TexCoord0).g;

                half oneMinusReflectivity;
                half3 specColor;
                albedoAlpha.rgb = DiffuseAndSpecularFromMetallic(albedoAlpha.rgb, Metallic, /*out*/ specColor, /*out*/ oneMinusReflectivity);
#endif

                float3 lightmap = 0;
                float shadowmask = 1;

#ifdef LIGHTMAP_ON
                lightmap = DecodeLightmap(unity_Lightmap.Sample(bakery_trilinear_clamp_sampler, IN.TexCoord1)).rgb;

    #ifdef BAKERY_SH
                float3 sh;
                BakerySH_float(lightmap, worldNormal, IN.TexCoord1, sh);
                lightmap = sh;
    #elif BAKERY_MONOSH
                float3 sh;
                BakeryMonoSH_float(lightmap, worldNormal, IN.TexCoord1, sh);
                lightmap = sh;
    #endif

    #ifdef DIRLIGHTMAP_COMBINED
    #ifdef NORMALMAP
                float4 dominantDir = unity_LightmapInd.Sample(bakery_trilinear_clamp_sampler, IN.TexCoord1);
                lightmap = DecodeDirectionalLightmap(lightmap, dominantDir, normalize(worldNormal));
    #endif
    #endif


    #ifdef SHADOWS_SHADOWMASK
                //float direct = saturate(dot(worldNormal, _WorldSpaceLightPos0));
                //direct *= SampleBakedOcclusion(IN.TexCoord1, 0);
                //lightmap += direct * _LightColor0;
                shadowmask = SampleBakedOcclusion(IN.TexCoord1, 0);
    #endif

#else
                lightmap = ShadeSH9(float4(worldNormal,1));

    #ifdef BAKERY_VOLUME
                float3 sh;
                BakeryVolume_float(IN.WorldPos, worldNormal, sh);
                lightmap = sh;

                float4 masks;
                VolumeShadowmask_float(IN.WorldPos, masks);
                shadowmask = saturate(dot(masks, unity_OcclusionMaskSelector));
    #endif

#endif
                lightmap *= tex2D(_AOMap, IN.TexCoord0).g;

                float4 color = float4(albedoAlpha.rgb * lightmap, albedoAlpha.a);
                float3 viewDir = -normalize(IN.WorldPos - _WorldSpaceCameraPos);
    
#ifdef DIRECTIONAL
                #ifdef UNITY_COMPILER_HLSL
                    SurfaceOutputStandard o = (SurfaceOutputStandard)0;
                #else
                    SurfaceOutputStandard o;
                #endif
                o.Albedo = albedoAlpha.rgb;
                o.Emission = 0.0;
                o.Alpha = albedoAlpha.a;
                o.Occlusion = 1.0;
                o.Normal = worldNormal;
#ifdef SPECULAR
                o.Smoothness = Glossiness;
                o.Metallic = Metallic;
#endif

                UNITY_LIGHT_ATTENUATION(atten, IN, IN.WorldPos)

                // Setup lighting environment
                UnityGI gi;
                UNITY_INITIALIZE_OUTPUT(UnityGI, gi);
                gi.indirect.diffuse = 0;
                gi.indirect.specular = 0;
                gi.light.color = _LightColor0.rgb;
                gi.light.dir = _WorldSpaceLightPos0;
                gi.light.color *= atten * shadowmask;

                color.rgb += albedoAlpha.rgb * LightingStandard(o, viewDir, gi).rgb;
#endif

#ifdef REFLECTIONS
                float roughness = SmoothnessToPerceptualRoughness(Glossiness);
                float3 reflUVW   = reflect(-viewDir, worldNormal);
                roughness = roughness*(1.7 - 0.7*roughness); // Unity hardcoded
                half mip = perceptualRoughnessToMipmapLevel(roughness);
                half4 rgbm = UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0, reflUVW, mip);
                float3 reflection = DecodeHDR(rgbm, unity_SpecCube0_HDR);

                half surfaceReduction;
                half nonPerceptualRoughness = SmoothnessToRoughness(Glossiness);
#ifdef UNITY_COLORSPACE_GAMMA
                surfaceReduction = 1.0-0.28*nonPerceptualRoughness*roughness;      // 1-0.28*x^3 as approximation for (1/(x^4+1))^(1/2.2) on the domain [0;1]
#else
                surfaceReduction = 1.0 / (nonPerceptualRoughness*nonPerceptualRoughness + 1.0);           // fade \in [0.5;1]
#endif
                half grazingTerm = saturate(Glossiness + (1-oneMinusReflectivity));
                half nv = saturate(dot(worldNormal, viewDir));
                reflection = surfaceReduction * reflection * FresnelLerp(specColor, grazingTerm, nv);
                color.rgb += reflection;
#endif
                UNITY_APPLY_FOG(IN.fogCoord, color);
                return color;
            }
            ENDCG
        }

        Pass
        {
            Name "ForwardAdd"
            Tags { "LightMode" = "ForwardAdd" }

            Blend SrcAlpha One
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_fwdadd_fullshadows
            #pragma multi_compile_instancing

            #pragma shader_feature NORMALMAP
            #pragma shader_feature SPECULAR

            #define vertex Position
            #define v IN
            #define pos Position

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "UnityPBSLighting.cginc"
            #include "AutoLight.cginc"
            #include "UnityShadowLibrary.cginc"

            SamplerState bakery_trilinear_clamp_sampler;

            sampler2D _MainTex, _BumpMap, _GlossMap, _MetallicMap;
            float4 _Color;
            float _Glossiness, _Metallic;
            float _Bias;

            float SampleBakedOcclusion (float2 lightmapUV, float3 worldPos)
            {
                #if defined (SHADOWS_SHADOWMASK)
                    #if defined(LIGHTMAP_ON)
                        fixed4 rawOcclusionMask = unity_ShadowMask.Sample(bakery_trilinear_clamp_sampler, lightmapUV.xy);
                        return saturate(dot(rawOcclusionMask, unity_OcclusionMaskSelector));
                    #endif
                #endif
                return 1;
            }

            struct appdata
            {
                float4 Position : POSITION;
                float2 TexCoord0 : TEXCOORD0;
                float2 TexCoord1 : TEXCOORD1;
                float3 Normal : NORMAL;
                float4 Tangent : TANGENT;
                float Fade : COLOR0;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 Position : SV_POSITION;
                float2 TexCoord0 : TEXCOORD0;
                float2 TexCoord1 : TEXCOORD1;
                float3 Normal : TEXCOORD2;
                //UNITY_FOG_COORDS(3)
                float fogCoord : TEXCOORD3; // ADDED FIX
#ifdef NORMALMAP
                float3 Tangent : TEXCOORD4;
                float3 Binormal : TEXCOORD5;
#endif
#if UNITY_VERSION >= 201740 // ADDED FIX
                UNITY_LIGHTING_COORDS(6,7)
#else
                LIGHTING_COORDS(6,7) // ADDED FIX
#endif
                float3 WorldPos : TEXCOORD8;
                float Fade : COLOR0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata IN)
            {
                v2f OUT;
                OUT = (v2f)0;

                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_OUTPUT(v2f, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.TexCoord0 = IN.TexCoord0;
                OUT.TexCoord1 = IN.TexCoord1 * unity_LightmapST.xy + unity_LightmapST.zw;
                OUT.Fade = IN.Fade;

                float3 worldPos = mul(unity_ObjectToWorld, float4(IN.Position.xyz,1)).xyz;
                OUT.WorldPos = worldPos;

                worldPos += normalize(_WorldSpaceCameraPos - worldPos) * _Bias; // hardcoded, but pretty good
                OUT.Position = mul(UNITY_MATRIX_VP, float4(worldPos,1));

                OUT.Normal = normalize(mul((float3x3)unity_ObjectToWorld, IN.Normal));

#ifdef NORMALMAP
                half sign = IN.Tangent.w * unity_WorldTransformParams.w;
                OUT.Tangent = normalize(mul((float3x3)unity_ObjectToWorld, IN.Tangent.xyz));
                OUT.Binormal = (cross(OUT.Normal, OUT.Tangent) * sign);
#endif
                UNITY_TRANSFER_FOG(OUT, OUT.Position);
                //UNITY_TRANSFER_LIGHTING(OUT, IN.TexCoord1.xy); // pass shadow and, possibly, light cookie coordinates to pixel shader
                UNITY_TRANSFER_SHADOW(OUT, IN.TexCoord1.xy) // ADDED FIX

                return OUT;
            }

            float4 frag (v2f IN) : SV_Target
            {

                float4 color = tex2D(_MainTex, IN.TexCoord0) * _Color;
                color.a *= IN.Fade;
                if (color.a < 1.0f / 256) discard;

                float3 worldPos = IN.WorldPos;

#ifdef NORMALMAP
                float3 normalMap = UnpackNormal(tex2D(_BumpMap, IN.TexCoord0));
                float3 worldNormal = normalize(IN.Tangent * normalMap.x + IN.Binormal * normalMap.y + IN.Normal * normalMap.z);
#else
                float3 worldNormal = normalize(IN.Normal);
#endif

                #ifndef USING_DIRECTIONAL_LIGHT
                    fixed3 lightDir = normalize(UnityWorldSpaceLightDir(worldPos));
                #else
                    fixed3 lightDir = _WorldSpaceLightPos0.xyz;
                #endif
                float3 worldViewDir = normalize(UnityWorldSpaceViewDir(worldPos));
                #ifdef UNITY_COMPILER_HLSL
                    SurfaceOutputStandard o = (SurfaceOutputStandard)0;
                #else
                    SurfaceOutputStandard o;
                #endif
                o.Albedo = color.rgb;
                o.Emission = 0.0;
                o.Alpha = color.a;
                o.Occlusion = 1.0;
                fixed3 normalWorldVertex = -worldNormal;
                o.Normal = worldNormal;
#ifdef SPECULAR
                o.Smoothness = _Glossiness * tex2D(_GlossMap, IN.TexCoord0).g;
                o.Metallic = _Metallic * tex2D(_MetallicMap, IN.TexCoord0).g;
#endif
                UNITY_LIGHT_ATTENUATION(atten, IN, worldPos)
                fixed4 c = 0;

                // Setup lighting environment
                UnityGI gi;
                UNITY_INITIALIZE_OUTPUT(UnityGI, gi);
                gi.indirect.diffuse = 0;
                gi.indirect.specular = 0;
                gi.light.color = _LightColor0.rgb;
                gi.light.dir = lightDir;
                gi.light.color *= atten;
                c += LightingStandard(o, worldViewDir, gi);

                // ADDED FIX
                #define UNITY_EXTRACT_FOG(name) float _unity_fogCoord = name.fogCoord
                #define UNITY_EXTRACT_FOG_FROM_TSPACE(name) float _unity_fogCoord = name.tSpace2.y
                #define UNITY_EXTRACT_FOG_FROM_WORLD_POS(name) float _unity_fogCoord = name.worldPos.w
                #define UNITY_EXTRACT_FOG_FROM_EYE_VEC(name) float _unity_fogCoord = name.eyeVec.w

                #ifdef FOG_COMBINED_WITH_TSPACE
                    UNITY_EXTRACT_FOG_FROM_TSPACE(IN);
                #elif defined (FOG_COMBINED_WITH_WORLD_POS)
                    UNITY_EXTRACT_FOG_FROM_WORLD_POS(IN);
                #else
                    UNITY_EXTRACT_FOG(IN);
                #endif
                UNITY_APPLY_FOG(_unity_fogCoord, c); // apply fog
      

                //c.rgb = float3(1,0,0);
                //c.a = 1;
                //c.rgb *= 0.1;
                c.a = saturate(c.a);

                return c;
            }
            ENDCG
        }


        Pass
        {
            Name "Deferred"
            Tags { "LightMode" = "Deferred" }

            Blend 1 SrcAlpha OneMinusSrcAlpha, SrcAlpha OneMinusSrcAlpha
            Blend 2 SrcAlpha OneMinusSrcAlpha, SrcAlpha OneMinusSrcAlpha
            Blend 3 [_BlendSrcE] [_BlendDstE], SrcAlpha OneMinusSrcAlpha
            Blend 4 SrcAlpha OneMinusSrcAlpha, SrcAlpha OneMinusSrcAlpha

            ColorMask RGBA
            ColorMask [__ColorMask] 2
            ColorMask [__ColorMask] 3
            ColorMask [__ColorMask] 4

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #pragma multi_compile_instancing

            #pragma shader_feature NORMALMAP
            #pragma shader_feature _ MODE_NORMALONLY MODE_NORMAL_AO_ONLY
            #pragma multi_compile _ INDIRECT_DRAW
            #pragma shader_feature _ BAKERY_SH BAKERY_MONOSH BAKERY_VOLUME
            #pragma multi_compile _ BAKERY_COMPRESSED_VOLUME

            #include "UnityCG.cginc"
            #include "UnityPBSLighting.cginc"

            SamplerState bakery_trilinear_clamp_sampler;

#ifdef INDIRECT_DRAW
            #include "decalShared.cginc"
            Texture2D _Lightmap, _LightmapInd, _ShadowMask;
            #define unity_Lightmap _Lightmap
            #define unity_LightmapInd _LightmapInd
#endif

            struct appdata
            {
                float4 Position : POSITION;
                float2 TexCoord0 : TEXCOORD0;
                float2 TexCoord1 : TEXCOORD1;
                float3 Normal : NORMAL;
                float4 Tangent : TANGENT;
                float Fade : COLOR0;
#ifdef INDIRECT_DRAW
                uint VertexID : SV_VertexID;
#endif

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 Position : SV_POSITION;
                float2 TexCoord0 : TEXCOORD0;
                float2 TexCoord1 : TEXCOORD1;
                float3 Normal : TEXCOORD2;
#ifdef NORMALMAP
                float3 Tangent : TEXCOORD3;
                float3 Binormal : TEXCOORD4;
#endif
                float Fade : COLOR0;
                float3 WorldPos : TEXCOORD5;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex, _BumpMap, _GlossMap, _MetallicMap, _AOMap;
            float4 _Color;
            float _Glossiness, _Metallic;
            float _Bias;

            v2f vert (appdata IN)
            {
                v2f OUT;

                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_OUTPUT(v2f, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.TexCoord0 = IN.TexCoord0;
                OUT.TexCoord1 = IN.TexCoord1 * unity_LightmapST.xy + unity_LightmapST.zw;
                OUT.Fade = IN.Fade;

#ifdef INDIRECT_DRAW
                IN.Position.xyz = GetDecalLocalPos(IN.VertexID);
                IN.Normal = GetDecalLocalNormal(IN.VertexID);
                OUT.TexCoord0 = GetDecalUV0(IN.VertexID);
                OUT.TexCoord1 = GetDecalUV1(IN.VertexID);
                OUT.Fade = GetDecalFade(IN.VertexID);
    #ifdef NORMALMAP
                IN.Tangent = GetDecalLocalTangent(IN.VertexID);
    #endif
#endif

                float3 worldPos = mul(unity_ObjectToWorld, float4(IN.Position.xyz,1)).xyz;
                OUT.WorldPos = worldPos;
                worldPos += normalize(_WorldSpaceCameraPos - worldPos) * _Bias; // hardcoded, but pretty good
                OUT.Position = mul(UNITY_MATRIX_VP, float4(worldPos,1));

                OUT.Normal = normalize(mul((float3x3)unity_ObjectToWorld, IN.Normal));

#ifdef NORMALMAP
                half sign = IN.Tangent.w * unity_WorldTransformParams.w;
                OUT.Tangent = normalize(mul((float3x3)unity_ObjectToWorld, IN.Tangent.xyz));
                OUT.Binormal = (cross(OUT.Normal, OUT.Tangent) * sign);
#endif
                return OUT;
            }

#if defined(BAKERY_SH) || defined(BAKERY_MONOSH) || defined(BAKERY_VOLUME)
            #include "BakeryDecalSupport.cginc"
#endif

            struct GBuffer
            {
                half4 outDiffuse : COLOR0;          // RT0: diffuse color (rgb), --unused-- (a)
                half4 outSpecRoughness : COLOR1;    // RT1: spec color (rgb), roughness (a)
                half4 outNormal : COLOR2;           // RT2: normal (rgb), --unused-- (a)
                half4 outEmission : COLOR3;          // RT3: emission (rgb), --unused-- (a)
            };

            GBuffer frag (v2f IN) : SV_Target
            {
                float4 albedoAlpha = tex2D(_MainTex, IN.TexCoord0) * _Color;
                albedoAlpha.a *= IN.Fade;
                if (albedoAlpha.a < 1.0f / 256) discard;


#ifdef NORMALMAP
                float3 normalMap = UnpackNormal(tex2D(_BumpMap, IN.TexCoord0));
                float3 worldNormal = normalize(IN.Tangent * normalMap.x + IN.Binormal * normalMap.y + IN.Normal * normalMap.z);
#else
                float3 worldNormal = normalize(IN.Normal);
#endif

                float3 lightmap = 0;
#ifdef LIGHTMAP_ON
                lightmap = DecodeLightmap(unity_Lightmap.Sample(bakery_trilinear_clamp_sampler, IN.TexCoord1)).rgb;

    #ifdef DIRLIGHTMAP_COMBINED
    #ifdef NORMALMAP
                float4 dominantDir = unity_LightmapInd.Sample(bakery_trilinear_clamp_sampler, IN.TexCoord1);
                lightmap = DecodeDirectionalLightmap(lightmap, dominantDir, normalize(worldNormal));
    #endif
    #endif

    #ifdef BAKERY_SH
                float3 sh;
                BakerySH_float(lightmap, worldNormal, IN.TexCoord1, sh);
                lightmap = sh;
    #elif BAKERY_MONOSH
                float3 sh;
                BakeryMonoSH_float(lightmap, worldNormal, IN.TexCoord1, sh);
                lightmap = sh;
    #endif

#else
                lightmap = ShadeSH9(float4(worldNormal,1));

    #ifdef BAKERY_VOLUME
                float3 sh;
                BakeryVolume_float(IN.WorldPos, worldNormal, sh);
                lightmap = sh;
    #endif

#endif

                GBuffer OUT;
                OUT.outDiffuse = albedoAlpha;
                OUT.outSpecRoughness = 0;
                OUT.outNormal = float4(worldNormal*0.5+0.5, albedoAlpha.a);
                OUT.outEmission = float4(albedoAlpha.rgb * lightmap, albedoAlpha.a);

                half oneMinusReflectivity;
                half3 specColor;
                OUT.outDiffuse.rgb = DiffuseAndSpecularFromMetallic(OUT.outDiffuse.rgb, _Metallic * tex2D(_MetallicMap, IN.TexCoord0).g, /*out*/ OUT.outSpecRoughness.rgb, /*out*/ oneMinusReflectivity);
                OUT.outSpecRoughness.w = _Glossiness * tex2D(_GlossMap, IN.TexCoord0).g;

                //OUT.outSpecRoughness=0;

#ifdef MODE_NORMALONLY
                OUT.outDiffuse = 0;
                OUT.outEmission = 0;//float4(lightmap, albedoAlpha.a);
#endif

#ifdef MODE_NORMAL_AO_ONLY
                OUT.outDiffuse.rgb = 0;
                OUT.outDiffuse.a = tex2D(_AOMap, IN.TexCoord0).g;
                OUT.outEmission = OUT.outDiffuse.a;//float4(lightmap, albedoAlpha.a);
#endif

                return OUT;
            }
            ENDCG
        }
    }
    CustomEditor "DecaleryStandardPBRShaderGUI"
}
