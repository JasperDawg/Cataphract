
sampler uImage0 : register(s0);

texture2D uTexture1;
sampler uImage1 = sampler_state
{
    texture = <uTexture1>;
    magfilter = POINT;
    minfilter = POINT;
    mipfilter = POINT;
    AddressU = wrap;
    AddressV = wrap;
};

float uTime;
float uHoverIntensity;
float uPixel;
float uSpeed;
float4 uSource;

float4 Particles[100];
float3 ParticleColors[100];

// iq's functions
// https://www.iquilezles.org/www/articles/distfunctions2d/distfunctions2
float sdCircle( float2 p, float r )
{
    return length(p) - r;
}

float sdPentagram(in float2 p, in float r )
{
    const float k1x = 0.809016994; // cos(π/ 5) = ¼(√5+1)
    const float k2x = 0.309016994; // sin(π/10) = ¼(√5-1)
    const float k1y = 0.587785252; // sin(π/ 5) = ¼√(10-2√5)
    const float k2y = 0.951056516; // cos(π/10) = ¼√(10+2√5)
    const float k1z = 0.726542528; // tan(π/ 5) = √(5-2√5)
    const float2  v1  = float2( k1x,-k1y);
    const float2  v2  = float2(-k1x,-k1y);
    const float2  v3  = float2( k2x,-k2y);
    
    p.x = abs(p.x);
    p -= 2.0*max(dot(v1,p),0.0)*v1;
    p -= 2.0*max(dot(v2,p),0.0)*v2;
    p.x = abs(p.x);
    p.y -= r;
    return length(p-v3*clamp(dot(p,v3),0.0,k1z*r))
           * sign(p.y*v3.x-p.x*v3.y);
}


// cubic polynomial
float smin( float a, float b, float k )
{
    k *= 6.0;
    float h = max( k-abs(a-b), 0.0 )/k;
    return min(a,b) - h*h*h*k*(1.0/6.0);
}

float4 main(float4 sampleColor : COLOR0, float2 coords : VPOS, float2 texCoord : TEXCOORD1) : COLOR0
{
    float2 uv = coords / uSource.xy;
    float aspectRatio = uSource.x / uSource.y;
    uv.x *= aspectRatio;
    uv *= -1. + 2. ; // map to -1 to 1

    float4 cumulativeColor = float4(0,0,0,0);
    float lifeTime = 0.0f;
    float totalDist = 1.0f;
    float aDist = sdCircle(uv - float2(0.2, 0.2), 0.3); 
    for (int i = 0; i < 100; i++)
    {
        if (Particles[i].w > 0.0)
        {
            float2 pos = Particles[i].xy;
            pos.x *= aspectRatio;
            float dist = sdPentagram(uv - pos, Particles[i].z / uSource.y);
            float alpha = smoothstep(0.1, 0.0, dist);
            totalDist = smin(totalDist, dist, 0.006);
            cumulativeColor.rgb = lerp(cumulativeColor.rgb, ParticleColors[i], alpha);
        }
    }

    float outline = 1 / uSource.y;
    float colorBrightness = (cumulativeColor.r + cumulativeColor.g + cumulativeColor.b) / 3.0;

    float4 outlineColor = lerp(float4(cumulativeColor.rgb, 1.0), float4(0.0, 0.0, 1.0, 1.0), colorBrightness * 1.5);

    float4 noise = tex2D(uImage1, uv + uSource.zw / uSource.xy);
    float4 finalColor = totalDist < 0.0 ? lerp(float4(cumulativeColor.rgb, 1.0), float4(0.5, 0.0, 0.3, 1.0), noise * 1.5f) : float4(0.0, 0.0, 0.0, 0.0);
    
    finalColor = totalDist < outline && totalDist > 0.0 ? outlineColor : finalColor;

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