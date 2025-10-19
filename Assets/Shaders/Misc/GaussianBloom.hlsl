#include "../blurs.h"

sampler uImage0 : register(s0);
float uTime;
float uHoverIntensity;
float uPixel;
float uColorResolution;
float uGrayness;
float uSpeed;
float passes;
float4 uSource;
float3 uInColor;

// variations of this need to be purpose built because of hlsl limitations
// this one only does a basic gaussian bloom at fixed parameters

float4 main(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    return gauss_bloom(uImage0, coords, AspectCorrectedGBlurScale(uSource, 1.0f), 8, 1., 0.3f);
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