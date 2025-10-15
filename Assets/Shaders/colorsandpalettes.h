#ifndef COLORSANDPALETTES_HLSL
#define COLORSANDPALETTES_HLSL

static const float EPSILON = 1e-10;
#define TAU 6.283185307179586476925286766559

float3 RGBtoHCV(in float3 rgb)
{
    // RGB [0..1] to Hue-Chroma-Value [0..1]
    // Based on work by Sam Hocevar and Emil Persson
    float4 p = (rgb.g < rgb.b) ? float4(rgb.bg, -1., 2. / 3.) : float4(rgb.gb, 0., -1. / 3.);
    float4 q = (rgb.r < p.x) ? float4(p.xyw, rgb.r) : float4(rgb.r, p.yzx);
    float c = q.x - min(q.w, q.y);
    float h = abs((q.w - q.y) / (6. * c + EPSILON) + q.z);
    return float3(h, c, q.x);
}

float3 RGBtoHSV(in float3 rgb)
{
    // RGB [0..1] to Hue-Saturation-Value [0..1]
    float3 hcv = RGBtoHCV(rgb);
    float s = hcv.y / (hcv.z + EPSILON);
    return float3(hcv.x, s, hcv.z);
}

// palette jacked from from https://www.shadertoy.com/view/clcXzr
float3 pal( in float t, in float3 brightness, in float3 contrast, in float3 osc, in float3 phase )
{
    return brightness + contrast * cos( TAU * (osc*t+phase) );
}

#endif