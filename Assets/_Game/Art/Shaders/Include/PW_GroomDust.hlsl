#ifndef PW_GROOM_DUST_INCLUDED
#define PW_GROOM_DUST_INCLUDED

// T38 « Toilettage » (doc 01 Contenu, S-ACT-001; doc « Mini-jeux & activités », S-ACT-001): the groomed animal's dust,
// under the local keyword _PW_DUST of SH_AnimalMaster. Only the grooming stage's material instance turns it on, for the
// one animal on its set: the refuge's animals never run a line of this.
//
// Per vertex (the groomed mesh copy made by GroomingPipeline): uv2.x = the vertex's zone, measured on the model; uv3 = the
// vertex's bind-pose position, so the dust's noise stays glued to the fur whatever the animation does.
// Per renderer (MaterialPropertyBlock, written only when a zone changes): _PW_GroomZones[64], one float per zone =
// dust (0 clean to 1 dirty) + 2 x the clean-glint step (0 to 15). _PW_GroomParams.x: content eyes (0 open, 1 half closed).
// Soft warm-grey dust in round patches of object-space noise: the patches shrink and pale as the passes are made, a short
// warm sheen when a zone comes clean. Round shapes only (owner rules: no four-point sparkle).

#define PW_GROOM_MAX_ZONES 64

float _PW_GroomZones[PW_GROOM_MAX_ZONES];
float4 _PW_GroomParams;

// x: dust 0-1, y: clean glint 0-1 of a zone.
half2 PW_GroomZone(float zone)
{
    int index = clamp((int)(zone + 0.5), 0, PW_GROOM_MAX_ZONES - 1);
    float v = max(_PW_GroomZones[index], 0.0);
    float glintStep = floor(v * 0.5);
    return half2((half)saturate(v - 2.0 * glintStep), (half)(glintStep / 15.0));
}

// Integer lattice hash: stable on every GPU (a float hash may disagree between neighbor cells and show seams).
float PW_GroomHash(float3 p)
{
    uint3 q = (uint3)(int3)floor(p);
    uint h = q.x * 1597334677u ^ q.y * 3812015801u ^ q.z * 2741598733u;
    h = h * 747796405u + 2891336453u;
    h = ((h >> ((h >> 28u) + 4u)) ^ h) * 277803737u;
    h = (h >> 22u) ^ h;
    return (float)h * (1.0 / 4294967295.0);
}

float PW_GroomNoise(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    float3 u = f * f * (3.0 - 2.0 * f);
    float a = lerp(PW_GroomHash(i), PW_GroomHash(i + float3(1, 0, 0)), u.x);
    float b = lerp(PW_GroomHash(i + float3(0, 1, 0)), PW_GroomHash(i + float3(1, 1, 0)), u.x);
    float c = lerp(PW_GroomHash(i + float3(0, 0, 1)), PW_GroomHash(i + float3(1, 0, 1)), u.x);
    float d = lerp(PW_GroomHash(i + float3(0, 1, 1)), PW_GroomHash(i + float3(1, 1, 1)), u.x);
    return lerp(lerp(a, b, u.y), lerp(c, d, u.y), u.z);
}

// The dust over the fur's albedo. Patches about 3 to 5 cm wide with soft edges, a fine grain inside them; at full dust
// most of the zone is covered, as the passes go the patches shrink and thin out, at 0 nothing is left.
half3 PW_ApplyGroomDust(half3 albedo, half dust, float3 bindPosition)
{
    float3 p = bindPosition * 24.0;
    half n = (half)(PW_GroomNoise(p) * 0.68 + PW_GroomNoise(p * 2.37 + 11.3) * 0.32);
    half threshold = 1.0h - dust * 0.92h;
    half patch = smoothstep(threshold - 0.12h, threshold + 0.1h, n);
    half grain = (half)PW_GroomNoise(p * 7.1 + 5.7);
    half3 dustColor = half3(0.66h, 0.59h, 0.5h) * (0.86h + 0.24h * grain);
    // The dust sits on the fur: it greys it and takes away a little of its color, never a flat paint.
    half luma = dot(albedo, half3(0.299h, 0.587h, 0.114h));
    half3 dulled = lerp(albedo, half3(luma, luma, luma), 0.35h);
    half amount = patch * (0.38h + 0.5h * dust);
    return lerp(dulled, dustColor, amount * 0.85h) * (1.0h - 0.04h * amount);
}

// A zone just came clean: a short warm sheen, stronger on the rim of the fur (round, soft; no star shape anywhere).
half3 PW_ApplyGroomGlint(half3 color, half glint, half3 normalWS, half3 viewWS)
{
    half fresnel = 1.0h - saturate(dot(normalWS, viewWS));
    return color + half3(1.0h, 0.9h, 0.72h) * (glint * (0.2h + 0.45h * fresnel * fresnel));
}

// The content eyes (« ferme les yeux »): the eye's upper part takes the fur's color, like a lid coming down to half,
// with a soft darker lash line at its edge. eye: the eye region (vertex color A); content 0 open, 1 half closed.
// Returns x: lid coverage, y: lash line.
half2 PW_GroomLid(half eye, half normalY, half content)
{
    half edge = lerp(1.3h, -0.1h, content);
    half lid = smoothstep(edge - 0.06h, edge + 0.06h, normalY);
    half lash = smoothstep(edge - 0.16h, edge - 0.04h, normalY) * (1.0h - lid);
    return half2(lid, lash) * eye * step(0.001h, content);
}

#endif
