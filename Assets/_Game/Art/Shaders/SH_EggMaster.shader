// Egg master (MAT_Egg_Master). Every egg type shares this material, mesh and mask texture.
// Same lighting as the animal master (Include/PW_Stylized.hlsl), albedo resolved per pixel from
// T_EggMask_Common: R spots, G leaf band, B flowers, A cracks (1.0 stage 1 lines, 0.6 stage 2 lines).
// Per-renderer Shader User Value (SRP Batcher compatible, no material clones):
//   bits 0-9   palette row (shell, spots, band, flowers, crack, inner shell, accent, reserved)
//   bits 10-11 crack stage (0 intact, 1 first crack, 2 second crack, 3 reserved)
//   bits 12-15 spots strength, bits 16-19 band strength, bits 20-23 flowers strength (0-15)
//   bits 24-27 glow (0-15): accent rim used during the reveal anticipation
// Vertex color G marks the inner shell of fragments.
Shader "PocketWilds/EggMaster"
{
    Properties
    {
        [NoScaleOffset] _EggMask ("Egg Mask (R spots, G band, B flowers, A cracks)", 2D) = "black" {}
        _WrapDiffuse ("Wrap Diffuse", Range(0, 1)) = 0.45
        _ShadowTint ("Shadow Tint", Color) = (0.62, 0.58, 0.72, 1)
        _RimColor ("Rim Color", Color) = (1, 0.97, 0.92, 1)
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.14
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        _ShellGloss ("Shell Gloss", Range(0, 1)) = 0.22
        _CrackSoftness ("Crack Edge Softness", Range(0.01, 0.2)) = 0.06
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    CBUFFER_START(UnityPerMaterial)
        half _WrapDiffuse;
        half4 _ShadowTint;
        half4 _RimColor;
        half _RimStrength;
        half _RimPower;
        half _ShellGloss;
        half _CrackSoftness;
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

            TEXTURE2D(_EggMask);
            SAMPLER(sampler_EggMask);

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
                float2 uv : TEXCOORD2;
                half inner : TEXCOORD3;
                half3 ambient : TEXCOORD4;
                half fogFactor : TEXCOORD5;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.inner = input.color.g;
                output.ambient = SampleSH(output.normalWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                uint data = unity_RendererUserValue;
                uint row = data & 1023u;
                uint stage = (data >> 10) & 3u;
                half strengthR = half((data >> 12) & 15u) / 15.0h;
                half strengthG = half((data >> 16) & 15u) / 15.0h;
                half strengthB = half((data >> 20) & 15u) / 15.0h;
                half glow = half((data >> 24) & 15u) / 15.0h;

                half3 shell, spots, band, flowers, crackColor, innerColor, accent;
                if (!PW_HasPalettes())
                {
                    // No palette loaded (editor preview): Basic Grassland look.
                    shell = half3(0.96, 0.92, 0.85); spots = half3(0.66, 0.82, 0.55); band = half3(0.49, 0.71, 0.39);
                    flowers = half3(0.95, 0.65, 0.73); crackColor = half3(0.42, 0.29, 0.23); innerColor = half3(1.0, 0.98, 0.93);
                    accent = half3(1.0, 0.93, 0.7);
                    strengthR = 1.0h; strengthB = 1.0h;
                }
                else
                {
                    shell = PW_PaletteColor(row, 0); spots = PW_PaletteColor(row, 1); band = PW_PaletteColor(row, 2);
                    flowers = PW_PaletteColor(row, 3); crackColor = PW_PaletteColor(row, 4); innerColor = PW_PaletteColor(row, 5);
                    accent = PW_PaletteColor(row, 6);
                }

                half4 mask = SAMPLE_TEXTURE2D(_EggMask, sampler_EggMask, input.uv);
                half3 albedo = shell;
                albedo = lerp(albedo, spots, mask.r * strengthR);
                albedo = lerp(albedo, band, mask.g * strengthG);
                albedo = lerp(albedo, flowers, mask.b * strengthB);

                // Stage 1 shows only the strongest lines; stage 2 also reveals the 0.6 lines and widens the first ones.
                half threshold = stage >= 2u ? 0.35h : 0.75h;
                half crack = stage == 0u ? 0.0h : smoothstep(threshold - _CrackSoftness, threshold + _CrackSoftness, mask.a);
                albedo = lerp(albedo, crackColor, crack);
                albedo = lerp(albedo, innerColor, input.inner);

                half3 normalWS = normalize(input.normalWS);
                half3 viewWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                PWLightResult lighting = PW_StylizedLighting(albedo, normalWS, viewWS, input.positionWS, input.ambient);

                half gloss = PW_Specular(normalWS, viewWS, lighting.mainLight, 40.0h) * _ShellGloss * (1.0h - input.inner) * (1.0h - crack);
                half3 color = lighting.color + gloss * lighting.mainLight.color;

                // Reveal anticipation: warm accent rim that grows with the glow bits.
                half fresnel = pow(1.0h - saturate(dot(normalWS, viewWS)), 2.0h);
                color += accent * glow * (0.25h + 0.75h * fresnel);
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
