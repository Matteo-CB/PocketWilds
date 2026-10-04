// Shadow caster pass body shared by the master shaders (vertex ShadowVert, fragment ShadowFrag).
#ifndef PW_SHADOW_CASTER_PASS_INCLUDED
#define PW_SHADOW_CASTER_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

float3 _LightDirection;
float3 _LightPosition;

struct PWShadowAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
};

float4 ShadowVert(PWShadowAttributes input) : SV_POSITION
{
    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
#if _CASTING_PUNCTUAL_LIGHT_SHADOW
    float3 lightDirection = normalize(_LightPosition - positionWS);
#else
    float3 lightDirection = _LightDirection;
#endif
    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirection));
#if UNITY_REVERSED_Z
    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
#else
    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
#endif
    return positionCS;
}

half4 ShadowFrag() : SV_Target
{
    return 0;
}

#endif
