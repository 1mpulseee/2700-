/*
   Copyright (c) 2023 Léo Chaumartin
   All rights reserved.
*/

#include "Common.hlsl"

void Voronoise_float(
    float2 UV,
    float Voronoi,
    float Blur,
    float2 Pan,
    float2 Scale,
    out float Value)
{

    float lPattern = voronoise(float2((UV.x + (Pan.x * (0.5 / Scale.x)) + 1.0) * Scale.x, (UV.y + (Pan.y * (0.5 / Scale.y))) * Scale.y), clamp(Voronoi, 0.0, 1.0), clamp(Blur, 0.0, 1.0), Scale);
    Value = lPattern;
}

void Voronoise_half(
    half2 UV,
    half Voronoi,
    half Blur,
    half2 Pan,
    half2 Scale,
    out half Value)
{
    float lPattern = voronoise(float2((UV.x + (Pan.x * (0.5 / Scale.x)) + 1.0) * Scale.x, (UV.y + (Pan.y * (0.5 / Scale.y))) * Scale.y), clamp(Voronoi, 0.0, 1.0), clamp(Blur, 0.0, 1.0), Scale);
    Value = lPattern;
}


// FRACTAL NOISE

float voronoise_fractal(float2 x, float u, float v, float2 Scale, float Lacunarity, int iter)
{
    float va = 0;
    float wt = 0;
    for (float j = -2; j <= 2; j+=1)
        for (float i = -2; i <= 2; i+=1)
        {
            float3 o = hash3(mod((floor(x) + float2(i, j)), Scale * pow(abs(Lacunarity), float(iter)))) * float3(u, u, 1);
            float2 r = float2(i, j) - frac(x) + float2(o.xy);
            float ww = pow(abs(1 - smoothstep(.0f, 1.414f, sqrt(dot(r, r)))), 1 + 63 * pow(abs(1 - v), 4));
            va += o.z * ww;
            wt += ww;
        }
    if (wt == 0.0)
        return 0.0;
    return va / wt;
}


float fbm(float2 coord, uint Octaves, float2 Scale, float Voronoi, float Blur, float Lacunarity, float Gain) {
    float value = 0.0;
    float amplitude = 1;
    Voronoi = clamp(Voronoi, 0.0, 1.0);
    Blur = clamp(Blur, 0.0, 1.0);
    Octaves = min(Octaves, 16);
    for (uint i = 0; i < Octaves; i++) {
        value += amplitude * abs(voronoise_fractal(coord, Voronoi, Blur, Scale, Lacunarity, i) * 2.0 - 1.0);
        coord *= Lacunarity;
        amplitude *= Gain;
    }
    return value;
}


void FractalNoise_float(float2 UV, uint Octaves, float2 Pan, float2 Scale, float Voronoi, float Blur, float Lacunarity, float Gain, out float Value)
{
    float2 p = float2((UV.x + (Pan.x * (0.5 / Scale.x))) * Scale.x, (UV.y + (Pan.y * (0.5 / Scale.y))) * Scale.y);
    float n = fbm(p, Octaves, Scale, Voronoi, Blur, Lacunarity, Gain);
    Value = n;
}
void FractalNoise_half(float2 UV, uint Octaves, float2 Pan, float2 Scale, float Voronoi, float Blur, float Lacunarity, float Gain, out float Value)
{
    float2 p = float2((UV.x + (Pan.x * (0.5 / Scale.x))) * Scale.x, (UV.y + (Pan.y * (0.5 / Scale.y))) * Scale.y);
    float n = fbm(p, Octaves, Scale, Voronoi, Blur, Lacunarity, Gain);
    Value = n;
}

// FRACTAL WARP


void FractalWarp_float(float2 UV, uint Octaves, float2 Pan, float2 Scale, float Voronoi, float Blur, float Lacunarity, float Gain, out float Value)
{
    float2 p = float2((UV.x + (Pan.x * (0.5 / Scale.x))) * Scale.x, (UV.y + (Pan.y * (0.5 / Scale.y))) * Scale.y);
    float n = fbm(p + float2(fbm(p, Octaves, Scale, Voronoi, Blur, Lacunarity, Gain), fbm(p - 1.0, Octaves, Scale, Voronoi, Blur, Lacunarity, Gain)), Octaves, Scale, Voronoi, Blur, Lacunarity, Gain);
    Value = n;
}
void FractalWarp_half(float2 UV, uint Octaves, float2 Pan, float2 Scale, float Voronoi, float Blur, float Lacunarity, float Gain, out float Value)
{
    float2 p = float2((UV.x + (Pan.x * (0.5 / Scale.x))) * Scale.x, (UV.y + (Pan.y * (0.5 / Scale.y))) * Scale.y);
    float n = fbm(p + float2(fbm(p, Octaves, Scale, Voronoi, Blur, Lacunarity, Gain), fbm(p - 1.0, Octaves, Scale, Voronoi, Blur, Lacunarity, Gain)), Octaves, Scale, Voronoi, Blur, Lacunarity, Gain);
    Value = n;
}

// VORONOI ULTIMATE


float4 VoronoiUltimate(float2 x, float2 aTiling, float2 aEdges, float aSeed)
{
    x *= aTiling;
    float2 p = floor(x);
    float2  f = frac(x);

    float2 mb = float2(0.0, 0.0);
    float2 mr = float2(0.0, 0.0);
    float i = 0.0, j = 0.0;
    float res = 8.0;
    for (j = -1.0; j <= 1.0; j += 1.0)
        for (i = -1.0; i <= 1.0; i += 1.0)
        {
            float2 b = float2(i, j);
            float2  r = b + hash2(mod(p + b, aTiling), aSeed) - f;
            float d = dot(r, r);

            if (d < res)
            {
                res = d;
                mr = r;
                mb = b;
            }
        }

    float va = 0;
    float wt = 0;
    float cells = 1.0e10;
    res = 8.0;
    for (j = -2.0; j <= 2.0; j += 1.0)
        for (i = -2.0; i <= 2.0; i += 1.0)
        {
            float2 b = mb + float2(i, j);
            float2  o = hash2(mod(p + b, aTiling), aSeed);
            float2  r = float2(b)+o - f;
            float d = dot(0.5 * (mr + r), normalize(r - mr));
            float drr = dot(r, r);
            if (d < res)
                res = d;
            if (drr < cells)
                cells = drr;
            float ww = pow(abs(1.0 - smoothstep(0.0, 1.414, sqrt(drr))), 64.0);
            va += o.y * ww;
            wt += ww;
        }

    float border = 1.0 - smoothstep(aEdges.x, aEdges.y, res);
    float eschema = va / wt;
    return float4(res, border, eschema, 1.0 - cells);
}

void Voronoi_float(float2 UV,
    int Seed,
    float Thickness,
    float Hardness,
    float2 Pan,
    float2 Scale,
    out float Gems,
    out float Cracks,
    out float Code,
    out float Cells
)
{
    float4 lPattern = VoronoiUltimate(mod(UV + float2(Pan.x, Pan.y), float2(1.0, 1.0)), float2(Scale.x, Scale.y), float2(Hardness, Thickness), float(Seed));
    Gems = lPattern.x;
    Cracks = lPattern.y;
    Code = lPattern.z;
    Cells = lPattern.w;
}
void Voronoi_half(
    half2 UV,
    int Seed,
    half Thickness,
    half Hardness,
    half2 Pan,
    half2 Scale,
    out half Gems,
    out half Cracks,
    out half Code,
    out half Cells
)
{
    half4 lPattern = VoronoiUltimate(mod(UV + half2(Pan.x, Pan.y), half2(1.0, 1.0)), half2(Scale.x, Scale.y), half2(Hardness, Thickness), half(Seed));
    Gems = lPattern.x;
    Cracks = lPattern.y;
    Code = lPattern.z;
    Cells = lPattern.w;
}

// SQUIRCLE

void Squircle_float(float2 UV, float2 Margins, float Falloff, float Convex, out float Value)
{
    float2 c = (UV - 0.5) * 2.0; //remap from 0->1 to -1 -> 1
    c = abs(c);
    c *= (1.0 + Margins);
    c = abs(pow(abs(c.xy), float2(Convex, Convex)));
    float f = 1.0 - length(c);
    f = smoothstep(0.0, Falloff, f);
    Value = clamp(f, 0.0, 1.0);
}
void Squircle_half(half2 UV, half2 Margins, half Falloff, half Convex, out half Value)
{
    half2 c = (UV - 0.5) * 2.0; //remap from 0->1 to -1 -> 1
    c = abs(c);
    c *= (1.0 + Margins);
    c = abs(pow(abs(c.xy), half2(Convex, Convex)));
    half f = 1.0 - length(c);
    f = smoothstep(0.0, Falloff, f);
    Value = clamp(f, 0.0, 1.0);
}

// POLYGON

void Polygon_half(half2 uv, half2 Size, int Sides, half Falloff, out half Value)
{
    half2 st = uv - half2(0.5, 0.5);
    st.x *= 1.0 - Size.x;
    st.y *= 1.0 - Size.y;
    st.y -= (Sides == 3) ? 0.25 : 0.0;
    half ata = atan2(st.x, -st.y) + M_PI;
    half r = M_TWO_PI / half(Sides);
    half dist = cos(floor(.5f + ata / r) * r - ata) * length(st);
    half polygon = 1.0f - smoothstep(.5f - Falloff, 0.5f, dist);
    Value = polygon;
}
void Polygon_float(float2 uv, float2 Size, int Sides, float Falloff, out float Value)
{
    float2 st = uv - float2(0.5, 0.5);
    st.x *= 2.0 - Size.x;
    st.y *= 2.0 - Size.y;
    st.y -= (Sides == 3) ? 0.25 : 0.0;
    float ata = atan2(st.x, -st.y) + M_PI;
    float r = M_TWO_PI / float(Sides);
    float dist = cos(floor(.5f + ata / r) * r - ata) * length(st);
    float polygon = 1.0f - smoothstep(.5f - Falloff, 0.5f, dist);
    Value = polygon;
}

// ROUNDED RECT

float roundBox(float2 p, float2 b, float r) {
    float2 d = abs(p) - b + float2(r, r);
    return min(max(d.x, d.y), 0.0) + length(max(d, 0.0)) - r;
}

void RoundedRect_float(float2 uv, float2 Size, float Radius, float Falloff, out float Value) {
    float2 p = uv - 0.5;
    float2 halfSize = Size * 0.5;
    float roundedRectDist = roundBox(p, halfSize, min(1.0,Radius/4.0));
    Value = smoothstep(0.0, -Falloff, roundedRectDist);
}


void RoundedRect_half(half2 uv, half2 Size, half Radius, half Falloff, out half Value) {
    half2 p = uv - 0.5;
    half2 halfSize = Size * 0.5;
    half roundedRectDist = roundBox(p, halfSize, min(1.0, Radius / 4.0));
    Value = smoothstep(0.0, -Falloff, roundedRectDist);
}

/* Caustics

 * Inpired by https://www.shadertoy.com/view/flSyDt
 * Changed the noise source to make it tileable with https://www.shadertoy.com/view/sllSDl
 */

float4 noised(float3 x, float3 scale)
{
    x *= scale;
    float3 i = floor(x);
    float3 w = frac(x);

    float3 u = w * w * w * (w * (w * 6.0 - 15.0) + 10.0);
    float3 du = 30.0 * w * w * (w * (w - 2.0) + 1.0);

    float3 ga = hash3(i + float3(0.0, 0.0, 0.0), scale);
    float3 gb = hash3(i + float3(1.0, 0.0, 0.0), scale);
    float3 gc = hash3(i + float3(0.0, 1.0, 0.0), scale);
    float3 gd = hash3(i + float3(1.0, 1.0, 0.0), scale);
    float3 ge = hash3(i + float3(0.0, 0.0, 1.0), scale);
    float3 gf = hash3(i + float3(1.0, 0.0, 1.0), scale);
    float3 gg = hash3(i + float3(0.0, 1.0, 1.0), scale);
    float3 gh = hash3(i + float3(1.0, 1.0, 1.0), scale);

    float va = dot(ga, w - float3(0.0, 0.0, 0.0));
    float vb = dot(gb, w - float3(1.0, 0.0, 0.0));
    float vc = dot(gc, w - float3(0.0, 1.0, 0.0));
    float vd = dot(gd, w - float3(1.0, 1.0, 0.0));
    float ve = dot(ge, w - float3(0.0, 0.0, 1.0));
    float vf = dot(gf, w - float3(1.0, 0.0, 1.0));
    float vg = dot(gg, w - float3(0.0, 1.0, 1.0));
    float vh = dot(gh, w - float3(1.0, 1.0, 1.0));

    float4 res = float4(va + u.x * (vb - va) + u.y * (vc - va) + u.z * (ve - va) + u.x * u.y * (va - vb - vc + vd) + u.y * u.z * (va - vc - ve + vg) + u.z * u.x * (va - vb - ve + vf) + (-va + vb + vc - vd + ve - vf - vg + vh) * u.x * u.y * u.z,    // value
        ga + u.x * (gb - ga) + u.y * (gc - ga) + u.z * (ge - ga) + u.x * u.y * (ga - gb - gc + gd) + u.y * u.z * (ga - gc - ge + gg) + u.z * u.x * (ga - gb - ge + gf) + (-ga + gb + gc - gd + ge - gf - gg + gh) * u.x * u.y * u.z +   // derivatives
        du * (float3(vb, vc, ve) - va + u.yzx * float3(va - vb - vc + vd, va - vc - ve + vg, va - vb - ve + vf) + u.zxy * float3(va - vb - ve + vf, va - vb - vc + vd, va - vc - ve + vg) + u.yzx * u.zxy * (-va + vb + vc - vd + ve - vf - vg + vh)));

    res = res.yzwx;
    return res;
}

float4 cloud(float3 v, int oct, float2 scale, float gain)
{
    float4 outp = float4(0.0, 0.0, 0.0, 0.0);
    for (int o = 1; o < oct; o++)
    {
        outp += noised(float3(-143 * o, 842 * o, 0.0) + v * float3(1., 1., pow(abs(float(o)), 0.1) * pow(abs(float(o)), 1.2)), float3(scale.x, scale.y, max(scale.x, scale.y)) * pow(2, o)) * (1.0 / pow(abs(float(o)), 1.9)) * pow(abs(gain + 0.0001), (o - 1));
    }
    return outp;
}
float caustic(float3 uv, int octaves, float2 st, float2 scale, float dimming, float gain)
{
    float4 val = 0.0;
    for (int i = 0; i < 10; i++)
    {
        val = cloud(float3(uv), octaves, scale, gain);
        uv.xy -= val.xy * st;
    }
    return exp(cloud(float3(uv), octaves, scale, gain).w * dimming - dimming / 2.0);
}

void Caustics_float(float2 UV, uint Octaves, float Gain, float Strength, float Dimming, float2 Pan, float2 Scale, float Z, out float Value)
{
    float2 p = float2(UV.x + Pan.x, UV.y + Pan.y);

    float v = caustic(float3(p, Z), Octaves + 1, ((Strength) / float2(Scale.x, Scale.y)) / 25.0, Scale, Dimming, Gain);
    Value = v;
}
void Caustics_half(half2 UV, uint Octaves, half Gain, half Strength, half Dimming, half2 Pan, half2 Scale, half Z, out half Value)
{
    Caustics_float(UV, Octaves, Gain, Strength, Dimming, Pan, Scale, Z, Value);
}

/* Erosion
 * Adapted from https://www.shadertoy.com/view/MtGcWh
 */

 //Copyright 2020 Clay John

 //Permission is hereby granted, free of charge, to any person obtaining a copy of this software 
 //and associated documentation files (the "Software"), to deal in the Software without restriction, 
 //including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, 
 //and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do 
 //so, subject to the following conditions:

 //The above copyright notice and this permission notice shall be included in all copies or 
 //substantial portions of the Software.

 //THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT 
 //NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. 
 //IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, 
 //WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE 
 //SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

float3 noised(float2 p, float2 scale)
{
    float2 i = floor(p);
    float2 f = frac(p);

    float2 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
    float2 du = 30.0 * f * f * (f * (f - 2.0) + 1.0);

    float2 ga = hash2(i + float2(0.0, 0.0), scale);
    float2 gb = hash2(i + float2(1.0, 0.0), scale);
    float2 gc = hash2(i + float2(0.0, 1.0), scale);
    float2 gd = hash2(i + float2(1.0, 1.0), scale);

    float va = dot(ga, f - float2(0.0, 0.0));
    float vb = dot(gb, f - float2(1.0, 0.0));
    float vc = dot(gc, f - float2(0.0, 1.0));
    float vd = dot(gd, f - float2(1.0, 1.0));

    return float3(va + u.x * (vb - va) + u.y * (vc - va) + u.x * u.y * (va - vb - vc + vd),
        ga + u.x * (gb - ga) + u.y * (gc - ga) + u.x * u.y * (ga - gb - gc + gd) +
        du * (u.yx * (va - vb - vc + vd) + float2(vb, vc) - va));
}

float3 erosion(in float2 p, float2 dir, float2 scale) {
    float2 ip = floor(p);
    float2 fp = frac(p);
    float f = 2. * PI;
    float3 va = 0.0;
    float wt = 0.0;
    for (int i = -2; i <= 1; i++) {
        for (int j = -2; j <= 1; j++) {
            float2 o = float2(i, j);
            float2 h = hash2(ip - o, scale) * 0.5;
            float2 pp = fp + o - h;
            float d = dot(pp, pp);
            float w = exp(-d * 2.0);
            wt += w;
            float mag = dot(pp, dir);
            va += float3(cos(mag * f), -sin(mag * f) * (pp + dir)) * w;
        }
    }
    return va / wt;
}

float3 mountain(float2 p, uint octaves, float2 scale, float lacunarity, float gain, float valleyAlt, float strength) {
    float3 n = 0.0;
    float nf = 1.0;
    float na = 0.6;
    int i;
    for (i = 0; i < octaves; i++) {
        n += noised(p * nf, float2(scale.x, scale.y) * pow(abs(lacunarity), float(i))) * na * float3(1.0, nf, nf);
        na *= gain;
        nf *= lacunarity;
    }

    float2 dir = n.zy * float2(1.0, -1.0);

    float3 h = 0.0;
    float a = 0.7 * (smoothstep(valleyAlt, valleyAlt + 0.1, n.x * 0.5 + 0.5));
    float f = 10.0;
    for (i = 0; i < octaves; i++) {
        h += strength * erosion(p * f, dir + h.zy * float2(1.0, -1.0), scale) * a * float3(1.0, f, f);
        a *= 0.5;
        f *= 2.0;
    }
    return float3(smoothstep(-1.0, 1.0, n.x) + h.x * 0.05, (n.yz + h.yz) * 0.5 + 0.5);
}

void Erosion_float(float2 UV, uint Octaves, float Gain, float Lacunarity, float Strength, float ValleyAlt, float2 Pan, float2 Scale, out float Value)
{
    float2 p = float2((UV.x + (Pan.x * (0.5 / Scale.x))) * Scale.x, (UV.y + (Pan.y * (0.5 / Scale.y))) * Scale.y);

    float3 erodedHeight = mountain(p, Octaves, Scale, Lacunarity, Gain, ValleyAlt, Strength);
    Value = clamp(erodedHeight.x, 0.0, 1.0);
}

void Erosion_half(half2 UV, uint uOctaves, half Gain, half Lacunarity, half Strength, half ValleyAlt, half2 Pan, half2 Scale, out half Value)
{
    Erosion_float(UV, uOctaves, Gain, Lacunarity, Strength, ValleyAlt, Pan, Scale, Value);
}