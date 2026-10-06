#ifndef PW_RARE_GLOW_INCLUDED
#define PW_RARE_GLOW_INCLUDED

// SH_RareGlow module (doc 07 G-SH-006, option 3: a parameter in AnimalMaster because it is nearly free).
// The level rides in bits 17-20 of the renderer user value: 0 off, 1-7 warm gold (Legendary, Golden),
// 8-15 soft sky-to-pink gradient (Fabulous). No keyword, no extra renderer, no light, no bloom needed:
// a common animal pays one interpolator and a few multiply-adds that end in zero.

// x: gold strength, y: sky strength (0-1). Evaluated per vertex.
half2 PW_RareGlowLevels(uint data)
{
    uint level = (data >> 17) & 15u;
    half gold = (level >= 1u && level <= 7u) ? half(level) / 7.0h : 0.0h;
    half sky = level >= 8u ? half(level - 7u) / 8.0h : 0.0h;
    return half2(gold, sky);
}

// Rim glow applied to the lit color: the fur edge takes the glow's color (visible on white fur too), then a soft
// lift of light. Wide and plush, slow shimmer for the sky gradient. Levels at zero leave the color untouched.
half3 PW_ApplyRareGlow(half3 color, half2 levels, half3 normalWS, half3 viewWS, float3 positionWS)
{
    half fresnel = 1.0h - saturate(dot(normalWS, viewWS));
    half rim = fresnel * fresnel * (0.45h + 0.55h * fresnel);
    half shimmer = 0.5h + 0.5h * sin(_Time.y * 1.3h + positionWS.x * 2.1h + positionWS.z * 1.7h);
    half t = saturate((normalWS.y * 0.5h + 0.5h) * 0.7h + shimmer * 0.3h);
    half3 sky = lerp(half3(1.0h, 0.62h, 0.84h), half3(0.52h, 0.8h, 1.0h), t);
    half3 gold = half3(1.0h, 0.74h, 0.3h);
    half strength = levels.x + levels.y;
    half3 glow = (gold * levels.x + sky * levels.y) / max(strength, 0.001h);
    half k = saturate(rim * strength * 1.6h);
    return lerp(color, glow, k * 0.85h) + glow * (k * 0.35h);
}

#endif
