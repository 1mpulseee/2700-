// The MIT License
// https://www.shadertoy.com/view/Xd23Dh
// Copyright © 2014 Inigo Quilez
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions: The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software. THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.


#ifndef M_PI
#define M_PI 3.14159
#endif
#ifndef M_TWO_PI
#define M_TWO_PI 6.2831853
#endif

float mod(float x, float y) {
    if(y > 0)
        return x - y * floor(x / y);
    return 0.0;
}
float2 mod(float2 x, float2 y) {
    if (y.x > 0 && y.y > 0)
        return x - y * floor(x / y);
    return float2(0.0, 0.0);
}
float3 mod(float3 x, float3 y) {
    if (y.x > 0 && y.y > 0 && y.z > 0)
        return x - y * floor(x / y);
    return float3(0.0, 0.0, 0.0);
}


float2 hash2(float2 x, float2 scale)
{
    x = mod(x, scale);
    x = float2(dot(x, float2(127.1, 311.7)),
        dot(x, float2(269.5, 183.3)));

    return -1.0 + 2.0 * frac(sin(x) * 43758.5453123);
}

float2 hash2(float2 p, float aSeed) {
    p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
    return frac(sin(p) * (aSeed + 43758.5453));
}

float3 hash3(float2 p) { 
    return frac(sin(float3(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)), dot(p, float2(419.2, 371.9)))) * 43758.5453f); 
}

float3 hash3(float3 p, float3 scale)
{
    p = mod(p, scale);

    p = float3(dot(p, float3(127.1, 311.7, 74.7)),
        dot(p, float3(269.5, 183.3, 246.1)),
        dot(p, float3(113.5, 271.9, 124.6)));

    return -1.0 + 2.0 * frac(sin(p) * 43758.5453123);
}

float voronoise(float2 x, float u, float v, float2 scale)
{
    float va = 0;
    float wt = 0;
    for (float j = -2; j <= 2; j += 1)
        for (float i = -2; i <= 2; i += 1)
        {
            float3 o = hash3(mod(floor(x) + float2(i, j), scale)) * float3(u, u, 1);
            float2 r = float2(i, j) - frac(x) + float2(o.xy);
            float ww = pow(abs(1 - smoothstep(.0f, 1.414f, sqrt(dot(r, r)))), 1 + 63 * pow(abs(1 - v), 4));
            va += o.z * ww;
            wt += ww;
        }
    if (wt == 0.0)
        return 0.0;
    return va / wt;
}



