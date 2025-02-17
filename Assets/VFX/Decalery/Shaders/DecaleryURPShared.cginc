
SamplerState bakery_trilinear_clamp_sampler;

#ifdef INDIRECT_DRAW
    Texture2D _Lightmap, _LightmapInd, _ShadowMask;
    #define unity_Lightmap _Lightmap
    #define unity_LightmapInd _LightmapInd
    #define unity_ShadowMask _ShadowMask
    #define samplerunity_Lightmap bakery_trilinear_clamp_sampler
    #define samplerunity_ShadowMask bakery_trilinear_clamp_sampler
    #define sampler_Lightmap bakery_trilinear_clamp_sampler
    #define sampler_ShadowMask bakery_trilinear_clamp_sampler
#endif

#define unity_ColorSpaceDielectricSpec half4(0.04, 0.04, 0.04, 1.0 - 0.04) // standard dielectric reflectivity coef at incident angle (= 4%)

#if defined(BAKERY_SH) || defined(BAKERY_MONOSH) || defined(BAKERY_VOLUME)
    #include "BakeryDecalSupport.cginc"
#endif

inline float LinearToGammaSpaceExact (float value)
{
    if (value <= 0.0F)
        return 0.0F;
    else if (value <= 0.0031308F)
        return 12.92F * value;
    else if (value < 1.0F)
        return 1.055F * pow(value, 0.4166667F) - 0.055F;
    else
        return pow(value, 0.45454545F);
}

inline half3 LinearToGammaSpace (half3 linRGB)
{
    linRGB = max(linRGB, half3(0.h, 0.h, 0.h));
    // An almost-perfect approximation from http://chilliant.blogspot.com.au/2012/08/srgb-approximations-for-hlsl.html?m=1
    return max(1.055h * pow(linRGB, 0.416666667h) - 0.055h, 0.h);

    // Exact version, useful for debugging.
    //return half3(LinearToGammaSpaceExact(linRGB.r), LinearToGammaSpaceExact(linRGB.g), LinearToGammaSpaceExact(linRGB.b));
}

// normal should be normalized, w=1.0
half3 SHEvalLinearL0L1 (half4 normal)
{
    half3 x;

    // Linear (L1) + constant (L0) polynomial terms
    x.r = dot(unity_SHAr,normal);
    x.g = dot(unity_SHAg,normal);
    x.b = dot(unity_SHAb,normal);

    return x;
}

// normal should be normalized, w=1.0
half3 SHEvalLinearL2 (half4 normal)
{
    half3 x1, x2;
    // 4 of the quadratic (L2) polynomials
    half4 vB = normal.xyzz * normal.yzzx;
    x1.r = dot(unity_SHBr,vB);
    x1.g = dot(unity_SHBg,vB);
    x1.b = dot(unity_SHBb,vB);

    // Final (5th) quadratic (L2) polynomial
    half vC = normal.x*normal.x - normal.y*normal.y;
    x2 = unity_SHC.rgb * vC;

    return x1 + x2;
}

// normal should be normalized, w=1.0
// output in active color space
half3 ShadeSH9 (half4 normal)
{
    // Linear + constant polynomial terms
    half3 res = SHEvalLinearL0L1 (normal);

    // Quadratic polynomials
    res += SHEvalLinearL2 (normal);

#   ifdef UNITY_COLORSPACE_GAMMA
        res = LinearToGammaSpace (res);
#   endif

    return res;
}

inline half3 DecodeDirectionalLightmap(half3 color, float4 dirTex, half3 normalWorld)
{
    // In directional (non-specular) mode Enlighten bakes dominant light direction
    // in a way, that using it for half Lambert and then dividing by a "rebalancing coefficient"
    // gives a result close to plain diffuse response lightmaps, but normalmapped.

    // Note that dir is not unit length on purpose. Its length is "directionality", like
    // for the directional specular lightmaps.

    half halfLambert = dot(normalWorld, dirTex.xyz - 0.5) + 0.5;

    return color * halfLambert / max(1e-4h, dirTex.w);
}

#define UNITY_SPECCUBE_LOD_STEPS 6

float BakeryPerceptualRoughnessToMipmapLevel(float perceptualRoughness, uint mipMapCount)
{
    perceptualRoughness = perceptualRoughness * (1.7 - 0.7 * perceptualRoughness);

    return perceptualRoughness * mipMapCount;
}

float BakeryPerceptualRoughnessToMipmapLevel(float perceptualRoughness)
{
    return BakeryPerceptualRoughnessToMipmapLevel(perceptualRoughness, UNITY_SPECCUBE_LOD_STEPS);
}

float SmoothnessToPerceptualRoughness(float smoothness)
{
    return (1 - smoothness);
}

float BakeryPerceptualRoughnessToRoughness(float perceptualRoughness)
{
    return perceptualRoughness * perceptualRoughness;
}

void SmoothnessToMip_float(float smoothness, out float mip)
{
    float pr = SmoothnessToPerceptualRoughness(smoothness);
    mip = BakeryPerceptualRoughnessToMipmapLevel(pr);
}

void WeightReflection_float(float smoothness, float metallic, float occlusion,
                                     float3 baseColor, float3 normal, float3 viewDir, float3 reflection,
                                     out float3 newReflection)
{
    half perceptualRoughness = SmoothnessToPerceptualRoughness(smoothness);
    half roughness = BakeryPerceptualRoughnessToRoughness(perceptualRoughness);
    half surfaceReduction = 1.0 / (roughness*roughness + 1.0);

    float3 pSpecular = lerp(unity_ColorSpaceDielectricSpec.rgb, baseColor, metallic);
    //baseColor = lerp(baseColor, 0, metallic);

    half reflectivity = max(max(pSpecular.r, pSpecular.g), pSpecular.b);
    half grazingTerm = saturate(smoothness + reflectivity);

    surfaceReduction *= occlusion;

    float3 pNdotV = saturate(dot(normal, viewDir));

    float fresnel = 1 - pNdotV;
    float t2 = fresnel * fresnel;
    fresnel *= t2 * t2;

    newReflection = surfaceReduction * reflection * lerp(pSpecular, grazingTerm, fresnel);
}

// Energy-conserving (hopefully) Blinn-Phong
// Better than horrible URP's LightingSpecular but less complicated than PBR URP one
float getLightSpecular(float3 lightDir, float3 viewDir, float3 normal, float specPow)
{
    float3 h = normalize( lightDir + viewDir );
    float nh = saturate( dot( h, normal ) );
    return pow(nh, specPow) * (specPow + 2.0) / 8.0;
}

void Lighting_float(float2 uv, float3 normalWorld, float3 objPos, float3 baseColor, float3 reflection, float smoothness, float metallic, out float3 color)
{
    float3 diffuse = 0;
    float3 specular = 0;

    VertexPositionInputs vertexInput = GetVertexPositionInputs(objPos);
    float3 viewDir = normalize(_WorldSpaceCameraPos - vertexInput.positionWS);
    float occlusion = 1.0f;
	float4 mask = 1.0f;

#ifndef INDIRECT_DRAW
        uv = uv * unity_LightmapST.xy + unity_LightmapST.zw;
#endif

#ifdef LIGHTMAP_ON
    float3 lmColor = DecodeLightmap(unity_Lightmap.Sample(samplerunity_Lightmap, uv), half4(LIGHTMAP_HDR_MULTIPLIER, LIGHTMAP_HDR_EXPONENT, 0.0h, 0.0h));

    #ifdef DIRLIGHTMAP_COMBINED
    #ifdef NORMALMAP
                float4 dominantDir = unity_LightmapInd.Sample(bakery_trilinear_clamp_sampler, uv);
                lmColor = DecodeDirectionalLightmap(lmColor, dominantDir, normalWorld);
    #endif
    #endif

    #ifdef BAKERY_SH
                float3 sh;
                BakerySH_float(lmColor, normalWorld, uv, sh);
                lmColor = sh;
    #elif BAKERY_MONOSH
                float3 sh;
                BakeryMonoSH_float(lmColor, normalWorld, uv, sh);
                lmColor = sh;
    #endif

    diffuse = lmColor;

	#if SHADOWS_SHADOWMASK
		mask = unity_ShadowMask.Sample(bakery_trilinear_clamp_sampler, uv);
	#endif

#else
	diffuse = ShadeSH9(float4(normalWorld,1));
	mask = unity_ProbesOcclusion;

    #ifdef BAKERY_VOLUME
                float3 sh;
                BakeryVolume_float(vertexInput.positionWS, normalWorld, sh);
                diffuse = sh;

                float4 masks;
                VolumeShadowmask_float(vertexInput.positionWS, masks);
                mask = masks;
    #endif

#endif

#ifdef UNIVERSAL_LIGHTING_INCLUDED
    Light light = GetMainLight(GetShadowCoord(vertexInput), vertexInput.positionWS, mask);
    float3 specColor = lerp(unity_ColorSpaceDielectricSpec.rgb, baseColor, metallic);
    float3 albedo = baseColor * (1-metallic);
    float specPow = exp2(10 * smoothness + 1); // from URP code

    float3 attenColor = light.color * light.shadowAttenuation;
    diffuse += LightingLambert(attenColor, light.direction, normalWorld);
    specular += getLightSpecular(light.direction, viewDir, normalWorld, specPow) * attenColor;
     //LightingSpecular(attenColor, light.direction, normalWorld, viewDir, float4(specColor,1), );

    int pixelLightCount = GetAdditionalLightsCount();
    for (int i = 0; i < pixelLightCount; i++)
    {
        light = GetAdditionalLight(i, vertexInput.positionWS, mask);
        attenColor = light.color * light.distanceAttenuation * light.shadowAttenuation;
        diffuse += LightingLambert(attenColor, light.direction, normalWorld); // not PBR at all! TODO: replace with normal BRDF
        specular += getLightSpecular(light.direction, viewDir, normalWorld, specPow) * attenColor;
        //specular += LightingSpecular(attenColor, light.direction, normalWorld, viewDir, float4(specColor,1), exp2(10 * smoothness + 1));
    }
    
#endif

    WeightReflection_float(smoothness, metallic, occlusion,
                                     baseColor, normalWorld, viewDir, reflection + specular,
                                     reflection);

    color = diffuse * albedo + reflection * specColor;
}

