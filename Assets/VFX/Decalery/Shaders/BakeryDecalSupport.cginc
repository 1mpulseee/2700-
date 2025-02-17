#ifndef BAKERYDECALSHARED
#define BAKERYDECALSHARED

Texture2D _RNM0, _RNM1, _RNM2;

#if UNITY_VERSION >= 201740 // ADDED FIX
SamplerState bakery2_trilinear_clamp_sampler;
#else
#define bakery2_trilinear_clamp_sampler samplerunity_Lightmap
#endif

Texture3D _Volume0, _Volume1, _Volume2, _VolumeMask;
#ifdef BAKERY_COMPRESSED_VOLUME
    Texture3D _Volume3;
#endif
float4x4 _VolumeMatrix, _GlobalVolumeMatrix;
float3 _VolumeMin, _VolumeInvSize;
float3 _GlobalVolumeMin, _GlobalVolumeInvSize;

float shEvaluateDiffuseL1Geomerics(float L0, float3 L1, float3 n)
{
    // average energy
    float R0 = L0;

    // avg direction of incoming light
    float3 R1 = 0.5f * L1;

    // directional brightness
    float lenR1 = length(R1);

    // linear angle between normal and direction 0-1
    //float q = 0.5f * (1.0f + dot(R1 / lenR1, n));
    //float q = dot(R1 / lenR1, n) * 0.5 + 0.5;
    float q = dot(normalize(R1), n) * 0.5 + 0.5;

    // power for q
    // lerps from 1 (linear) to 3 (cubic) based on directionality
    float p = 1.0f + 2.0f * lenR1 / R0;

    // dynamic range constant
    // should vary between 4 (highly directional) and 0 (ambient)
    float a = (1.0f - lenR1 / R0) / (1.0f + lenR1 / R0);

    return R0 * (a + (1.0f - a) * (p + 1.0f) * pow(q, p));
}

void BakerySH_float(float3 L0, float3 normalWorld, float2 lightmapUV, out float3 sh)
{
    float3 nL1x = _RNM0.Sample(bakery2_trilinear_clamp_sampler, lightmapUV) * 2 - 1;
    float3 nL1y = _RNM1.Sample(bakery2_trilinear_clamp_sampler, lightmapUV) * 2 - 1;
    float3 nL1z = _RNM2.Sample(bakery2_trilinear_clamp_sampler, lightmapUV) * 2 - 1;
    float3 L1x = nL1x * L0 * 2;
    float3 L1y = nL1y * L0 * 2;
    float3 L1z = nL1z * L0 * 2;

    float lumaL0 = dot(L0, 1);
    float lumaL1x = dot(L1x, 1);
    float lumaL1y = dot(L1y, 1);
    float lumaL1z = dot(L1z, 1);
    float lumaSH = shEvaluateDiffuseL1Geomerics(lumaL0, float3(lumaL1x, lumaL1y, lumaL1z), normalWorld);

    sh = L0 + normalWorld.x * L1x + normalWorld.y * L1y + normalWorld.z * L1z;
    float regularLumaSH = dot(sh, 1);

    sh *= lerp(1, lumaSH / regularLumaSH, saturate(regularLumaSH*16));

    sh = max(sh, 0);
}

void BakeryMonoSH_float(float3 L0, float3 normalWorld, float2 lightmapUV, out float3 sh)
{
#ifdef LIGHTMAP_ON
#ifdef DIRLIGHTMAP_COMBINED
    float3 dominantDir = unity_LightmapInd.Sample(bakery2_trilinear_clamp_sampler, lightmapUV);

    float3 nL1 = dominantDir * 2 - 1;
    float3 L1x = nL1.x * L0 * 2;
    float3 L1y = nL1.y * L0 * 2;
    float3 L1z = nL1.z * L0 * 2;

    float lumaL0 = dot(L0, 1);
    float lumaL1x = dot(L1x, 1);
    float lumaL1y = dot(L1y, 1);
    float lumaL1z = dot(L1z, 1);
    float lumaSH = shEvaluateDiffuseL1Geomerics(lumaL0, float3(lumaL1x, lumaL1y, lumaL1z), normalWorld);

    sh = L0 + normalWorld.x * L1x + normalWorld.y * L1y + normalWorld.z * L1z;
    float regularLumaSH = dot(sh, 1);

    sh *= lerp(1, lumaSH / regularLumaSH, saturate(regularLumaSH*16));

    sh = max(sh, 0);
    return;
#endif
#endif
    sh = 0;
}

void BakeryVolume(float3 lpUV, float3 normalWorld, out float3 sh)
{
    #ifdef BAKERY_COMPRESSED_VOLUME
        float4 tex0, tex1, tex2, tex3;
        float3 L0, L1x, L1y, L1z;
        tex0 = _Volume0.Sample(bakery2_trilinear_clamp_sampler, lpUV);
        tex1 = _Volume1.Sample(bakery2_trilinear_clamp_sampler, lpUV) * 2 - 1;
        tex2 = _Volume2.Sample(bakery2_trilinear_clamp_sampler, lpUV) * 2 - 1;
        tex3 = _Volume3.Sample(bakery2_trilinear_clamp_sampler, lpUV) * 2 - 1;
        L0 = tex0.xyz;
        L1x = tex1.xyz * L0 * 2;
        L1y = tex2.xyz * L0 * 2;
        L1z = tex3.xyz * L0 * 2;
    #else
        float4 tex0, tex1, tex2;
        float3 L0, L1x, L1y, L1z;
        tex0 = _Volume0.Sample(bakery2_trilinear_clamp_sampler, lpUV);
        tex1 = _Volume1.Sample(bakery2_trilinear_clamp_sampler, lpUV);
        tex2 = _Volume2.Sample(bakery2_trilinear_clamp_sampler, lpUV);
        L0 = tex0.xyz;
        L1x = tex1.xyz;
        L1y = tex2.xyz;
        L1z = float3(tex0.w, tex1.w, tex2.w);
    #endif
    sh.r = shEvaluateDiffuseL1Geomerics(L0.r, float3(L1x.r, L1y.r, L1z.r), normalWorld);
    sh.g = shEvaluateDiffuseL1Geomerics(L0.g, float3(L1x.g, L1y.g, L1z.g), normalWorld);
    sh.b = shEvaluateDiffuseL1Geomerics(L0.b, float3(L1x.b, L1y.b, L1z.b), normalWorld);
    sh = max(sh, 0);
}

void BakeryVolume_float(float3 posWorld, float3 normalWorld, out float3 sh)
{
    bool isGlobal = dot(abs(_VolumeInvSize),1) == 0;
    float3 lpUV = (posWorld - (isGlobal ? _GlobalVolumeMin : _VolumeMin)) * (isGlobal ? _GlobalVolumeInvSize : _VolumeInvSize);
    BakeryVolume(lpUV, normalWorld, sh);
}

void VolumeShadowmask_float(float3 posWorld, out float4 shadowmask)
{
    bool isGlobal = dot(abs(_VolumeInvSize),1) == 0;
    float3 lpUV = (posWorld - (isGlobal ? _GlobalVolumeMin : _VolumeMin)) * (isGlobal ? _GlobalVolumeInvSize : _VolumeInvSize);
    shadowmask = _VolumeMask.Sample(bakery2_trilinear_clamp_sampler, lpUV);
}

#endif