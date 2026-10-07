// G-SH-004 WaterLite (MAT_Water_<Biome>): streams, ponds and small waterfalls in one opaque pass. No grab pass,
// no depth texture, no reflection probe: everything comes from vertex data, the palette and the sky globals.
//   vertex color R = foam weight (banks, spring, rocks), G = depth (0 at the banks, 1 in the middle),
//   A = falling water (waterfalls flow faster and foam more); uv.x across the flow, uv.y along it in meters.
// Palette columns (renderer Shader User Value row, like SH_EnvironmentOpaque): 0 deep, 1 shallow, 2 foam,
// 3 sparkle, 6 sky reflection tint.
// Quality (_PW_WaterQuality, set by WorldAtmosphere from the tier): 0 Fluide = depth color + ripples,
// 1 Équilibrée = + foam and sky reflection, 2 Magnifique = + detailed reflection and soft caustics.
Shader "PocketWilds/WaterLite"
{
    Properties
    {
        _FlowSpeed ("Flow Speed (m/s)", Range(0, 2)) = 0.35
        _FallSpeed ("Falling Water Speed (m/s)", Range(0, 4)) = 1.6
        _RippleScale ("Ripple Scale", Range(0.5, 8)) = 2.6
        _RippleStrength ("Ripple Normal Strength", Range(0, 1)) = 0.35
        _FoamAmount ("Foam Amount", Range(0, 1)) = 0.7
        _Sparkle ("Sun Sparkle", Range(0, 2)) = 0.9
        _Fresnel ("Sky Reflection", Range(0, 1)) = 0.55
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    CBUFFER_START(UnityPerMaterial)
        half _FlowSpeed;
        half _FallSpeed;
        half _RippleScale;
        half _RippleStrength;
        half _FoamAmount;
        half _Sparkle;
        half _Fresnel;
    CBUFFER_END
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+10" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ FOG_LINEAR

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Include/PW_World.hlsl"

            // Same globals as PW_Stylized.hlsl (palette table, 8 colors per row) and SH_SkyGradient (sky colors, linear).
            TEXTURE2D(_PW_Palettes);
            SAMPLER(sampler_PW_Palettes);
            float _PW_PaletteRowCount;
            half4 _PW_SkyTop;
            half4 _PW_SkyHorizon;
            float _PW_SkyBlend;
            float _PW_WaterQuality;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 data : TEXCOORD2;   // foam, depth, falling, palette row
                float2 flow : TEXCOORD3;
                half fogFactor : TEXCOORD4;
            };

            half3 Palette(uint row, uint column)
            {
                float2 uv = float2((column + 0.5) / 8.0, (row + 0.5) / max(_PW_PaletteRowCount, 1.0));
                return SAMPLE_TEXTURE2D_LOD(_PW_Palettes, sampler_PW_Palettes, uv, 0).rgb;
            }

            float Hash(float2 p)
            {
                uint2 q = (uint2)(int2)floor(p);
                uint h = (q.x * 1597334677u) ^ (q.y * 3812015801u);
                h = h * 747796405u + 2891336453u;
                h = ((h >> ((h >> 28u) + 4u)) ^ h) * 277803737u;
                return (float)((h >> 22u) ^ h) * (1.0 / 4294967295.0);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                // A breath of swell on still water; falling water stays where the mesh puts it.
                half still = 1.0h - input.color.a;
                positionWS.y += sin(_Time.y * 1.3 + positionWS.x * 1.7 + positionWS.z * 1.1) * 0.008 * still;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fogFactor = PW_FogFactor(output.positionCS.z, positionWS);
                uint row = unity_RendererUserValue & 1023u;
                output.data = half4(input.color.r, input.color.g, input.color.a, (half)row);
                half speed = lerp(_FlowSpeed, _FallSpeed, input.color.a);
                output.flow = float2(input.uv.x, input.uv.y - _Time.y * speed);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                uint row = (uint)(input.data.w + 0.5);
                bool palettes = _PW_PaletteRowCount > 0.5;
                half3 deep = palettes ? Palette(row, 0) : half3(0.16, 0.5, 0.62);
                half3 shallow = palettes ? Palette(row, 1) : half3(0.46, 0.79, 0.8);
                half3 foamColor = palettes ? Palette(row, 2) : half3(0.93, 0.98, 0.96);
                half3 sparkleColor = palettes ? Palette(row, 3) : half3(1, 1, 1);
                half3 skyTint = palettes ? Palette(row, 6) : half3(0.72, 0.88, 0.94);

                half foamWeight = input.data.x;
                half depth = input.data.y;
                half falling = input.data.z;
                half quality = (half)_PW_WaterQuality;

                // Ripples: two noise layers drifting with the flow, read as a slope for the normal.
                float2 p = input.flow * float2(1.0, 1.0) * _RippleScale;
                float n1 = Noise(p * float2(2.2, 1.0));
                float n2 = Noise(p * float2(1.3, 2.6) + float2(5.2, -_Time.y * 0.6));
                float ripple = n1 * 0.6 + n2 * 0.4;
                float dx = ddx(ripple), dy = ddy(ripple);
                half3 normalWS = normalize(input.normalWS + half3(dx, 0, dy) * (_RippleStrength * 12.0h));

                half3 color = lerp(shallow, deep, saturate(depth * 1.15h));
                // Moving light and dark bands: the stream reads as flowing even on Fluide.
                color *= 0.94h + 0.12h * (half)ripple;

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 viewWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                half lit = lerp(0.55h, 1.0h, mainLight.shadowAttenuation);
                color *= mainLight.color * lit * 0.85h + SampleSH(normalWS) * 0.45h;

                if (quality > 0.5h)
                {
                    // Sky reflection (fresnel) from the mood's sky colors, then foam on banks, rocks and falls.
                    half3 sky = lerp(skyTint, lerp(_PW_SkyHorizon.rgb, _PW_SkyTop.rgb, 0.35h), saturate(_PW_SkyBlend));
                    half fresnel = pow(1.0h - saturate(dot(normalWS, viewWS)), 3.0h) * _Fresnel;
                    if (quality > 1.5h)
                    {
                        // Detailed reflection: brighter sky in the ripple crests.
                        fresnel *= 0.8h + 0.5h * (half)n2;
                    }
                    color = lerp(color, sky, saturate(fresnel));
                    half foamNoise = (half)Noise(input.flow * float2(6.0, 3.0) + float2(0, -_Time.y * 0.4));
                    half foam = saturate((foamWeight + falling * 0.5h) * _FoamAmount * 1.6h - foamNoise * 0.7h);
                    foam = smoothstep(0.25h, 0.55h, foam);
                    color = lerp(color, foamColor, foam);
                }
                if (quality > 1.5h)
                {
                    // Soft caustics on the shallow edges: thin bright cells drifting.
                    float c = Noise(input.positionWS.xz * 3.1 + _Time.y * 0.35) * Noise(input.positionWS.zx * 2.3 - _Time.y * 0.27);
                    color += sparkleColor * (half)smoothstep(0.32, 0.5, c) * 0.18h * (1.0h - depth) * (1.0h - falling);
                }
                // Sun glints on the ripple crests.
                half3 h = normalize(mainLight.direction + viewWS);
                half glint = pow(saturate(dot(normalWS, h)), 180.0h) * _Sparkle * lit;
                color += sparkleColor * glint;

                color = PW_ClosedSoften(color, input.positionWS);
                return half4(MixFog(color, input.fogFactor), 1);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
