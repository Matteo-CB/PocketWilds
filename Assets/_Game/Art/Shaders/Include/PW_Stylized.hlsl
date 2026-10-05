// Shared look for every Pocket Wilds opaque master shader (animals, eggs, props later).
// The including shader declares _WrapDiffuse, _ShadowTint, _RimColor, _RimStrength and _RimPower
// in its UnityPerMaterial CBUFFER, then includes this file inside the ForwardLit pass.
#ifndef PW_STYLIZED_INCLUDED
#define PW_STYLIZED_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "PW_World.hlsl"

// Global palette table (PaletteTable.cs): 8 colors per row, row from the Shader User Value.
TEXTURE2D(_PW_Palettes);
SAMPLER(sampler_PW_Palettes);
float _PW_PaletteRowCount;

bool PW_HasPalettes()
{
    return _PW_PaletteRowCount > 0.5;
}

half3 PW_PaletteColor(uint row, uint column)
{
    float2 uv = float2((column + 0.5) / 8.0, (row + 0.5) / max(_PW_PaletteRowCount, 1.0));
    return SAMPLE_TEXTURE2D_LOD(_PW_Palettes, sampler_PW_Palettes, uv, 0).rgb;
}

struct PWLightResult
{
    half3 color;
    half lit;
    Light mainLight;
};

// Wrapped diffuse, colored shadow side, soft rim. Plush and readable at phone scale.
PWLightResult PW_StylizedLighting(half3 albedo, half3 normalWS, half3 viewWS, float3 positionWS, half3 ambient)
{
    PWLightResult result;
    result.mainLight = GetMainLight(TransformWorldToShadowCoord(positionWS));
    half ndl = dot(normalWS, result.mainLight.direction);
    half wrapped = saturate((ndl + _WrapDiffuse) / (1.0h + _WrapDiffuse));
    result.lit = wrapped * result.mainLight.shadowAttenuation;

    // Shadowed side keeps a soft colored tint instead of going grey.
    half3 lightTerm = result.mainLight.color * result.lit + _ShadowTint.rgb * (1.0h - result.lit) * 0.35h;
    half3 color = albedo * (ambient + lightTerm);

    half rim = pow(1.0h - saturate(dot(normalWS, viewWS)), _RimPower) * _RimStrength;
    color += rim * _RimColor.rgb * (0.5h + 0.5h * result.lit);
    result.color = color;
    return result;
}

half PW_Specular(half3 normalWS, half3 viewWS, Light mainLight, half power)
{
    half3 halfDir = normalize(mainLight.direction + viewWS);
    return pow(saturate(dot(normalWS, halfDir)), power);
}

#endif
