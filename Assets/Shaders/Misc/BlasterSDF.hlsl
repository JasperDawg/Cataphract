
sampler uImage0 : register(s0);
float uTime;
float uHoverIntensity;
float uPixel;
float uColorResolution;
float uGrayness;
float uSpeed;
float4 uSource;
float3 uInColor;

float4 Particles[100];
float3 ParticleColors[100];

// iq's functions
// https://www.iquilezles.org/www/articles/distfunctions2d/distfunctions2
float sdCircle( float2 p, float r )
{
    return length(p) - r;
}

// cubic polynomial
float smin( float a, float b, float k )
{
    k *= 6.0;
    float h = max( k-abs(a-b), 0.0 )/k;
    return min(a,b) - h*h*h*k*(1.0/6.0);
}

float4 main(float4 sampleColor : COLOR0, float2 coords : VPOS) : COLOR0
{
    float2 uv = coords / uSource.xy;
    float aspectRatio = uSource.x / uSource.y;
    uv.x *= aspectRatio;
    uv *= -1. + 2. ; // map to -1 to 1
    float4 cumulativeColor = float4(0,0,0,0);
    float totalDist = 1.0f;
    float aDist = sdCircle(uv - float2(0.2, 0.2), 0.3); 
    for (int i = 0; i < 100; i++)
    {
        if (Particles[i].w > 0.0)
        {
            float2 pos = Particles[i].xy;
            pos.x *= aspectRatio;
            float dist = sdCircle(uv - pos, Particles[i].z / uSource.y);
            float alpha = smoothstep(0.03, 0.0, dist);
            totalDist = smin(totalDist, dist, 0.01);
            cumulativeColor.rgb = lerp(cumulativeColor.rgb, ParticleColors[i], alpha);
        }
    }

    float outline = 2 / uSource.y;


    float4 finalColor = totalDist < 0.0 ? float4(cumulativeColor.rgb, 1.0) : float4(0.0, 0.0, 0.0, 0.0);
    finalColor = totalDist < outline && totalDist > 0.0 ? float4(1.0, 1.0, 1.0, 1.0) : finalColor;

    // apply gamma correction
    float gamma = 2.2f;
    finalColor.rgb = pow(finalColor.rgb, float3(1.0/gamma, 1.0/gamma, 1.0/gamma));
    return finalColor;
}


#ifdef FX
technique Technique1
{
    pass SDFShader
    {
        PixelShader = compile ps_3_0 main();
    }
}
#endif // FX