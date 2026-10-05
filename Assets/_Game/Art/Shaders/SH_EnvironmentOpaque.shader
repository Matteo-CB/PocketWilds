// G-SH-002 Environment opaque master (MAT_Environment_Common): trees, rocks, grass, flowers, decor.
// One material for every prop of every biome. Same lighting as the animal master (Include/PW_Stylized.hlsl).
// Albedo is resolved per vertex from the global palette table:
//   base color, then vertex color R/G/B/A blend toward palette columns 1-4,
//   UV0.x (baked occlusion, darker at the root and inside clusters) blends toward column 5.
// Per-renderer Shader User Value (SRP Batcher compatible, no material clones):
//   bits 0-9   palette row
//   bits 10-13 brightness jitter (0-15, 8 = neutral, about +-8%): repeated props never look stamped
Shader "PocketWilds/EnvironmentOpaque"
{
    Properties
    {
        _WrapDiffuse ("Wrap Diffuse", Range(0, 1)) = 0.4
        _ShadowTint ("Shadow Tint", Color) = (0.58, 0.6, 0.74, 1)
        _RimColor ("Rim Color", Color) = (1, 0.98, 0.9, 1)
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.08
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        _OcclusionStrength ("Baked Occlusion", Range(0, 1)) = 1
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    CBUFFER_START(UnityPerMaterial)
        half _WrapDiffuse;
        half4 _ShadowTint;
        half4 _RimColor;
        half _RimStrength;
        half _RimPower;
        half _OcclusionStrength;
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

            #include "Include/PW_Stylized.hlsl"

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
                half3 albedo : TEXCOORD2;
                half3 ambient : TEXCOORD3;
                half fogFactor : TEXCOORD4;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);

                uint data = unity_RendererUserValue;
                uint row = data & 1023u;
                // Bit 14: merged chunk props carry each instance's brightness in uv.y (0..1 = level 0..15).
                half level = (data & 16384u) != 0u ? saturate(half(input.uv.y)) * 15.0h : half((data >> 10) & 15u);
                half brightness = 0.92h + level / 15.0h * 0.16h;

                half3 c0, c1, c2, c3, c4, c5;
                if (!PW_HasPalettes())
                {
                    // No palette loaded (editor preview): soft meadow defaults.
                    c0 = half3(0.55, 0.42, 0.32); c1 = half3(0.55, 0.77, 0.42); c2 = half3(0.71, 0.87, 0.52);
                    c3 = half3(0.44, 0.67, 0.36); c4 = half3(0.96, 0.92, 0.84); c5 = half3(0.30, 0.40, 0.30);
                    brightness = 1.0h;
                }
                else
                {
                    c0 = PW_PaletteColor(row, 0); c1 = PW_PaletteColor(row, 1); c2 = PW_PaletteColor(row, 2);
                    c3 = PW_PaletteColor(row, 3); c4 = PW_PaletteColor(row, 4); c5 = PW_PaletteColor(row, 5);
                }

                half3 color = c0;
                color = lerp(color, c1, input.color.r);
                color = lerp(color, c2, input.color.g);
                color = lerp(color, c3, input.color.b);
                color = lerp(color, c4, input.color.a);
                color = lerp(color, c5, saturate(input.uv.x) * _OcclusionStrength);

                output.albedo = color * brightness;
                output.ambient = SampleSH(output.normalWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 normalWS = normalize(input.normalWS);
                half3 viewWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                PWLightResult lighting = PW_StylizedLighting(input.albedo, normalWS, viewWS, input.positionWS, input.ambient);
                return half4(MixFog(lighting.color, input.fogFactor), 1);
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
