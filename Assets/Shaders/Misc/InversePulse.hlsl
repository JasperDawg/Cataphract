#include "../colorsandpalettes.h"

sampler uImage0 : register(s0)
{
    magfilter = POINT;
    minfilter = POINT;
    mipfilter = POINT;
    AddressU = wrap;
    AddressV = wrap;
};
float uTime;
float uHoverIntensity;
float uScale;
float uPixel;
float uColorResolution;
float uGrayness;
float uSpeed;
float4 uSource;
float3 uInColor;

float3 RecolorGreyscale(in float3 rgb) {
return pal(sin(rgb.x + uTime), rgb, float3(lerp(.5, .7, (sin(uTime.xxx)+1.)/2.)), float3(.4,(sin(uTime)+1.)/2.,.2), float3(0.7,0.4,0.1));
}

float4 main(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    float2 uv = coords * 2.0 - uSource.xy / uSource.y;
    uv *= uScale;
    float d = length(uv);
    d = 1.0 - dot(d, d);

    float w = sin(d-uTime)*100.;
    w = abs(w) * smoothstep(0.1, 4., d);

    float3 col = float3(lerp(1.0, w, smoothstep(0.4, 0.45, d)).xxx);
    float4 finalColor = col.xxxx;

    finalColor = 1.0 - saturate(finalColor);
    float4 noise = tex2D(uImage0, uv.xy / sin(w + uTime) ) * 1.0;
    finalColor = smoothstep(finalColor.x, noise, d - w * 0.1);
    finalColor.rgb = RecolorGreyscale(finalColor.rgb) + d - w;

    return finalColor;
}

#ifdef FX
technique Technique1
{
    pass InversePulseShader
    {
        PixelShader = compile ps_3_0 main();
    }
}
#endif // FX