#ifndef RAIN_COMMON_INCLUDED
#define RAIN_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

struct RainAttributes
{
    float3 positionOS : POSITION;
    float2 uv         : TEXCOORD0;
    half4  tint       : COLOR;
    float4 params     : TEXCOORD1;
};

struct RainVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv         : TEXCOORD0;
    half4  tint       : COLOR;
    float4 params     : TEXCOORD1;
    float  opacity    : TEXCOORD2;
};

// Geometry arrives in normalized screen units: y in [-1,1], x in [-aspect,aspect], z = opacity.
RainVaryings RainVert(RainAttributes v)
{
    RainVaryings o;
    float aspect = _ScreenParams.x / _ScreenParams.y;
    // Task 7's orientation check showed the image vertically mirrored when rendering into an
    // intermediate texture, so the render-target flip is applied here (D10).
    o.positionCS = float4(v.positionOS.x / aspect, v.positionOS.y * _ProjectionParams.x, 0.0, 1.0);
    o.uv = v.uv;
    o.tint = v.tint;
    o.params = v.params;
    o.opacity = saturate(v.positionOS.z);
    return o;
}

// Distortion offset in UV: 'px' pixels at 1080p reference height, aspect corrected.
float2 RainDistortionUV(float2 n, float px)
{
    return px * n * float2(_ScreenParams.y / _ScreenParams.x, 1.0) / 1080.0;
}

#endif // RAIN_COMMON_INCLUDED
