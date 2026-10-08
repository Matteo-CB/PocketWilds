// G-SH-001 Animal master. One material for every species and variant.
// Colors come from the global palette table (_PW_Palettes, 8 colors per row).
// The row and pattern come from the per-renderer Shader User Value (SRP Batcher compatible, no material clones):
//   bits 0-9   palette row
//   bits 10-12 pattern mask (0 none, 1 A = uv0.x, 2 B = uv0.y, 3 C = uv1.x, 4 D = uv1.y)
//   bits 13-16 pattern strength (0-15)
//   bits 17-20 rare glow level (Include/SH_RareGlow.hlsl): 0 off, 1-7 warm gold, 8-15 sky gradient
// Regions come from vertex colors: R secondary, G belly, B detail (nose, paws), A eyes.
// Albedo is resolved per vertex: cheap and soft, which suits the plush style.
// T38 local keyword _PW_DUST (Include/PW_GroomDust.hlsl): the groomed animal of the grooming set only (its own material
// instance, a per-zone dust array in a MaterialPropertyBlock). Off, nothing changes: same interpolators, same cost, and the
// SRP Batcher path of every refuge animal stays as it was (the per-material CBUFFER is untouched).
Shader "PocketWilds/AnimalMaster"
{
    Properties
    {
        _WrapDiffuse ("Wrap Diffuse", Range(0, 1)) = 0.5
        _ShadowTint ("Shadow Tint", Color) = (0.62, 0.58, 0.72, 1)
        _RimColor ("Rim Color", Color) = (1, 0.97, 0.92, 1)
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.16
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        _EyeGloss ("Eye Gloss", Range(0, 1)) = 0.7
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    CBUFFER_START(UnityPerMaterial)
        half _WrapDiffuse;
        half4 _ShadowTint;
        half4 _RimColor;
        half _RimStrength;
        half _RimPower;
        half _EyeGloss;
    CBUFFER_END
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
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
            // T38: compiled in (a runtime material instance turns it on), but only for this pass.
            #pragma multi_compile_local _ _PW_DUST

            #include "Include/PW_Stylized.hlsl"
            #include "Include/SH_RareGlow.hlsl"
            #if defined(_PW_DUST)
            #include "Include/PW_GroomDust.hlsl"
            #endif

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                float2 mask01 : TEXCOORD0;
                float2 mask23 : TEXCOORD1;
            #if defined(_PW_DUST)
                float2 groomZone : TEXCOORD2;
                float3 groomBind : TEXCOORD3;
            #endif
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half3 albedo : TEXCOORD2;
                half eye : TEXCOORD3;
                half3 ambient : TEXCOORD4;
                half fogFactor : TEXCOORD5;
                half2 rareGlow : TEXCOORD6;
            #if defined(_PW_DUST)
                // x dust (eyes and details masked), y clean glint
                half2 groom : TEXCOORD7;
                float3 groomBind : TEXCOORD8;
                // The fur's own color, for the content eyes' lid.
                half3 fur : TEXCOORD9;
            #endif
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.fogFactor = PW_FogFactor(position.positionCS.z, position.positionWS);
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);

                uint data = unity_RendererUserValue;
                uint row = data & 1023u;
                uint patternChannel = (data >> 10) & 7u;
                half patternStrength = half((data >> 13) & 15u) / 15.0h;

                half3 primary, secondary, belly, detail, eyeColor, patternColor;
                if (!PW_HasPalettes())
                {
                    // No palette loaded (editor preview): neutral plush defaults.
                    primary = half3(0.80, 0.70, 0.60); secondary = half3(0.95, 0.80, 0.80);
                    belly = half3(0.97, 0.94, 0.88); detail = half3(0.95, 0.60, 0.65);
                    eyeColor = half3(0.04, 0.03, 0.04); patternColor = half3(0.55, 0.42, 0.34);
                }
                else
                {
                    primary = PW_PaletteColor(row, 0); secondary = PW_PaletteColor(row, 1);
                    belly = PW_PaletteColor(row, 2); detail = PW_PaletteColor(row, 3);
                    eyeColor = PW_PaletteColor(row, 4); patternColor = PW_PaletteColor(row, 5);
                }

                half pattern = 0;
                if (patternChannel == 1u) pattern = input.mask01.x;
                else if (patternChannel == 2u) pattern = input.mask01.y;
                else if (patternChannel == 3u) pattern = input.mask23.x;
                else if (patternChannel == 4u) pattern = input.mask23.y;

                half3 color = primary;
                color = lerp(color, secondary, input.color.r);
                color = lerp(color, belly, input.color.g);
                color = lerp(color, patternColor, saturate(pattern) * patternStrength);
                color = lerp(color, detail, input.color.b);
                color = lerp(color, eyeColor, input.color.a);

                output.albedo = color;
                output.eye = input.color.a;
            #if defined(_PW_DUST)
                half2 zone = PW_GroomZone(input.groomZone.x);
                output.groom = half2(zone.x * (1.0h - input.color.a) * (1.0h - input.color.b), zone.y);
                output.groomBind = input.groomBind;
                output.fur = lerp(lerp(primary, secondary, input.color.r), patternColor, saturate(pattern) * patternStrength);
            #endif
                output.ambient = SampleSH(output.normalWS);
                output.rareGlow = PW_RareGlowLevels(data);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 normalWS = normalize(input.normalWS);
                half3 viewWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                half3 albedo = input.albedo;
                half eye = input.eye;
            #if defined(_PW_DUST)
                half2 lid = PW_GroomLid(input.eye, normalWS.y, (half)_PW_GroomParams.x);
                albedo = lerp(albedo, input.fur * (1.0h - 0.45h * lid.y), saturate(lid.x + lid.y * 0.6h));
                eye *= 1.0h - lid.x;
                if (input.groom.x > 0.003h)
                    albedo = PW_ApplyGroomDust(albedo, input.groom.x, input.groomBind);
            #endif
                PWLightResult lighting = PW_StylizedLighting(albedo, normalWS, viewWS, input.positionWS, input.ambient);

                half spec = PW_Specular(normalWS, viewWS, lighting.mainLight, 64.0h) * eye * _EyeGloss;
                half3 color = lighting.color + spec * lighting.mainLight.color;
            #if defined(_PW_DUST)
                color = PW_ApplyGroomGlint(color, input.groom.y, normalWS, viewWS);
            #endif
                color = PW_ApplyRareGlow(color, input.rareGlow, normalWS, viewWS, input.positionWS);
                return half4(MixFog(color, input.fogFactor), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Include/PW_ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #include "Include/PW_DepthOnlyPass.hlsl"
            ENDHLSL
        }
    }

    FallBack Off
}
