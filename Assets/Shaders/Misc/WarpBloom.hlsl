#include "../blurs.h"
#include "../colorsandpalettes.h"

sampler uImage0 : register(s0);
float uTime;
float uHoverIntensity;
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
    float4 image = tex2D(uImage0, coords);
    float3 hsv = RGBtoHSV(image.rgb);
    float4 bloom = gauss_bloom(uImage0, coords, AspectCorrectedGBlurScale(uSource, 1.0f), 32, 1., 0.3f);
    float4 finalColor = image;
    finalColor.rgb = hsv.x > 0.0 ? finalColor.rgb : finalColor.a > 0.0 ? RecolorGreyscale(finalColor.rgb + bloom.rgb) : finalColor.rgb;
    return finalColor;
}

#ifdef FX
technique Technique1
{
    pass BloomShader
    {
        PixelShader = compile ps_3_0 main();
    }
}
#endif // FX