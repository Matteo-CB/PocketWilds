// World-wide shader state (T33): wind for vegetation and height fog, set once per mood/tier by
// WorldAtmosphere (Presentation/World). Every global defaults to zero, so a scene that never sets them
// renders exactly as before: no wind, no height fog.
#ifndef PW_WORLD_INCLUDED
#define PW_WORLD_INCLUDED

// x, y: wind direction on the ground plane (world x, z), normalized.
// z: strength (0 calm, 1 breezy). w: wave speed.
float4 _PW_Wind;
// x: wave spatial frequency (1/m). y: gust strength. z: gust frequency. w: flutter strength.
float4 _PW_WindWaves;
// x: base height (m). y: falloff per meter above it. z: strength (0 off). w: start distance (m).
float4 _PW_HeightFog;
// x: distance over which height fog reaches full strength (m).
float4 _PW_HeightFogRange;
// T35A closed zones (doc 02 Améliorations, section 4.6): what lies past the next zone's hedge reads softer.
// x: world x where the softening starts. y: width of the blend (m). z: amount (0.35 = 35 % softer; 0 = off).
float4 _PW_ClosedZone;

float PW_Hash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

// Offset of a vegetation vertex. originWS: the plant's pivot; heightM: vertex height above it (m);
// tip: 0 at the root, 1 at the tip (Blender PW_Occlusion uv.y). Roots never move.
float3 PW_WindOffset(float3 originWS, float heightM, float tip)
{
    float2 dir = _PW_Wind.xy;
    float along = dot(originWS.xz, dir);
    float h = PW_Hash21(originWS.xz);
    float phase = along * _PW_WindWaves.x - _Time.y * _PW_Wind.w;
    float gust = 0.5 + 0.5 * sin(along * 0.11 - _Time.y * _PW_WindWaves.z);
    float sway = sin(phase) * 0.55 + sin(phase * 2.7 + h * 6.2831) * 0.2;
    float lean = _PW_Wind.z * (0.45 + sway + gust * _PW_WindWaves.y);
    float flutter = sin(_Time.y * 8.3 + h * 40.0) * _PW_WindWaves.w;
    float bend = (lean + flutter) * heightM * tip * tip;
    return float3(dir.x * bend, -abs(bend) * 0.3 * tip, dir.y * bend);
}

// T39 local gusts (FoliageGusts, doc 01 S-ACT-002 H-VFX-ACT-002-03 « herbe qui ondule »): up to 4 discs where the
// foliage sways harder, the Hide and Seek hint and decoys. xyz: world position (only xz is read: a plan disc), w: radius
// (m). Strength 0 to 1 per slot (x..w). Both default to zero: nothing changes while no gust is set.
float4 _PW_Gusts[4];
float4 _PW_GustStrength;

// How much of a gust reaches a world point (0 to 1): four distance tests, a soft smoothstep rim, no branch.
float PW_GustAmount(float2 xz)
{
    float g = 0.0;
    [unroll]
    for (int i = 0; i < 4; i++)
    {
        float f = saturate(1.0 - distance(xz, _PW_Gusts[i].xz) / max(_PW_Gusts[i].w, 0.01));
        g += _PW_GustStrength[i] * f * f * (3.0 - 2.0 * f);
    }
    return saturate(g);
}

// PW_WindOffset plus the local gust: inside a gust the lean grows to about three times the ambient breeze at strength 1
// (a calm scene still ripples: the gust keeps a floor of breeze) with a faster ripple that runs across the plant, so blades
// wave one after the other. positionWS: the vertex before wind (the ripple's phase and the gust's reach). The lean stays
// under about 45 degrees. Only what already sways moves: the bend keeps PW_WindOffset's height and tip weights.
float3 PW_WindGustOffset(float3 originWS, float3 positionWS, float heightM, float tip)
{
    float2 dir = dot(_PW_Wind.xy, _PW_Wind.xy) > 1e-4 ? _PW_Wind.xy : float2(0.70710678, 0.70710678);
    float along = dot(originWS.xz, dir);
    float h = PW_Hash21(originWS.xz);
    float phase = along * _PW_WindWaves.x - _Time.y * _PW_Wind.w;
    float gust = 0.5 + 0.5 * sin(along * 0.11 - _Time.y * _PW_WindWaves.z);
    float sway = sin(phase) * 0.55 + sin(phase * 2.7 + h * 6.2831) * 0.2;
    float lean = _PW_Wind.z * (0.45 + sway + gust * _PW_WindWaves.y);
    float flutter = sin(_Time.y * 8.3 + h * 40.0) * _PW_WindWaves.w;

    float local = PW_GustAmount(positionWS.xz);
    float ripple = _Time.y * 9.0 + h * 6.2831 + dot(positionWS.xz, dir) * 7.0;
    float strength = max(_PW_Wind.z, 0.25);
    lean = clamp(lean + local * strength * (1.6 + 1.3 * sin(ripple)), -1.0, 1.0);
    flutter += local * strength * 0.5 * sin(ripple * 1.7 + h * 3.1);

    float bend = (lean + flutter) * heightM * tip * tip;
    float2 across = float2(-dir.y, dir.x) * (local * strength * 0.35 * sin(ripple * 0.8 + 1.3) * heightM * tip * tip);
    return float3(dir.x * bend + across.x, -abs(bend) * 0.3 * tip, dir.y * bend + across.y);
}

// A closed zone's colors, 35 % softer: less saturated and a touch lighter, like the haze of what is still to come.
// Evaluated per vertex on albedo (per pixel only for water). Zero amount (the default) changes nothing.
half3 PW_ClosedSoften(half3 color, float3 positionWS)
{
    half closed = half(saturate((positionWS.x - _PW_ClosedZone.x) / max(_PW_ClosedZone.y, 0.01)) * _PW_ClosedZone.z);
    half luma = dot(color, half3(0.299h, 0.587h, 0.114h));
    half3 soft = lerp(half3(luma, luma, luma), half3(0.93h, 0.95h, 0.91h), 0.18h);
    return lerp(color, soft, closed);
}

// Fog factor (1 = clear) combining the scene's linear distance fog and a height fog that pools in the
// low distance, so the diorama fades into the sky. Branch-free: zero strength changes nothing.
half PW_FogFactor(float clipZ, float3 positionWS)
{
    half fog = ComputeFogFactor(clipZ);
#if defined(FOG_LINEAR)
    float dist = distance(positionWS, _WorldSpaceCameraPos.xyz);
    float far = saturate((dist - _PW_HeightFog.w) / max(_PW_HeightFogRange.x, 1.0));
    float low = exp2(-max(0.0, positionWS.y - _PW_HeightFog.x) * _PW_HeightFog.y);
    fog = min(fog, half(1.0 - _PW_HeightFog.z * far * low));
#endif
    return fog;
}

#endif
