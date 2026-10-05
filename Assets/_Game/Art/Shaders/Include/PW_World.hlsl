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
