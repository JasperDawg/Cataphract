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

#define PI 3.14159265359
#define TAU 6.28318530718

// todo: make into separate header
float3x3 rotateX(float f)
{
    return float3x3(
    float3(1.0,    0.0,      0.0),
    float3(0.0,    cos(f),  -sin(f)),     
    float3(.0, sin(f), cos(f))
    );
}


float3x3 rotateY(float f)
{
    return float3x3(
    float3(cos(f), 0.0,  sin(f)),
    float3(0.0,    1.0,  0.0),    
    float3(-sin(f), 0.0, cos(f))
    );
}

float3x3 rotateZ(float f)
{
    return float3x3(
    float3(cos(f),    -sin(f),  0.0),
    float3(sin(f),     cos(f),  0.0),     
    float3(0.0, 0.0, 1.0)
    );
    
}

float aafi(float2 p)
{
    float fi = atan2(p.y, p.x);
    fi += step(p.y, 0.0) * TAU;
    return fi;
}    

float2 ToSphereCoordinates (float3 p)
{
    float lon = aafi(p.xy)/TAU;
    float lat = aafi(float2(p.z, length(p.xy)))/PI;
    return float2(1.0-lon, lat);
}
float sdSphere(float3 p, float s)
{
  return length(p)-s;
}

float2 hash2f(float2 p)
{
    return frac(cos(mul(p,float2x2(56.,37.,81.,-26.)))*28.9);   
}

// https://iquilezles.org/articles/smoothvoronoi/
float smoothVoronoi( in float2 x )
{
    int2 p = floor( x );
    float2  f = frac( x );

    float res = 0.0;
    for( int j=-1; j<=1; j++ )
    for( int i=-1; i<=1; i++ )
    {
        int2 b = int2( i, j );
        float2  radius = float2( b ) - f + hash2f( p + b );
        float d = length( radius );

        res += exp2( -32.0*d );
    }
    return -(1.0/32.0)*log2( res );
}

// https://www.shadertoy.com/view/dtcBRB
float gFBM3(float3 p) { // adapted from nimitz's fast gyroid fBm
                            // replaces Perlin noise base function with gyroid + cumulated domain distortion
                            float3x3 m3 = float3x3( .3338,  .56034, -.71817,
               -.87887, .32651, -.15323,
                .15162, .69596,  .61339) * 1.93;
                
    float d, z = 1., trk = 1.5;
  
    for(int i; i < 5; i++, z *= .7, trk *= 1.4, p = mul(p, m3)  )// --- fractal loop, like Perlin noise
        p += sin( p.yzx * trk ) * .1,          // scale p ~ 2^i , + *.1 distortion at scale 1.4^i
        d += abs( dot(cos(p), sin(p.zxy)) ) * z ;          // abs(gyroïd) / 1.43^i , instead of base Perlin noise function
                                                           // abs() = Perlin turbulence. try without.
    return d/3;                                           // 3 = max of dot(cos,sin)
}

float4 main(float4 sampleColor : COLOR0, float2 coords : TEXCOORD) : COLOR0
{
    float radius = 0.33;

    float3 forcefieldOpposite = float3(0, 10.0, 10.);

    float2 uv = coords;
    float aspectRatio = uSource.x / uSource.y;
    uv -= float2(0.5, 0.5);
    uv.x *= aspectRatio;


    float l = length(uv);
    float3 col = pow( float(max(1.0 -l + radius, 0.)), float4(18.6,7.3,3.3,1.));

    float3 otherCol = col;

    if (l > radius) 
    {
         return float4(col + smoothVoronoi(sin(uv * uTime * uv)) * -l - radius, 1.0);
    }
    
    float z = radius*sin(acos(l/radius));

    float3 sphere = float3(uv, z);
    
    float3 rotatedSphere = mul(sphere, rotateX(PI/2.0));
    
    float2 projectedCoordinates = ToSphereCoordinates(rotatedSphere);

    float v = smoothVoronoi(gFBM3(float3(8 * projectedCoordinates, uTime)));
    col = float(max(v, 0.));
    col = smoothstep(col, otherCol , 1.0 - l + 1. - l);
    
    float forcefieldShadow = dot(normalize(sphere), normalize(forcefieldOpposite));
    forcefieldShadow = l < radius ? clamp(forcefieldShadow, 0.8, 2.0) : 1.0;
    col /= forcefieldShadow;
    
    return float4(col, 1.0);
}

#ifdef FX
technique Technique1
{
    pass ShieldShader
    {
        PixelShader = compile ps_3_0 main();
    }
}
#endif // FX