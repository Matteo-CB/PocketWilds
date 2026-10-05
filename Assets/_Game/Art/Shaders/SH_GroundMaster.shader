// G-SH-003 Ground master (MAT_Ground_<Biome>). Opaque, no displacement, no texture.
// Same palette rows as props: base grass, then vertex color R light patches, G dark patches,
// B path, A soil (diorama edge), UV0.x baked occlusion toward column 5.
// A soft world-space macro variation (two cheap sine octaves, per vertex) breaks repetition across
// chunks toward palette column 6. The ground receives shadows but never casts them (no caster pass).
// Shader User Value bits 0-9: palette row.
Shader "PocketWilds/GroundMaster"
{
    Properties
    {
        _WrapDiffuse ("Wrap Diffuse", Range(0, 1)) = 0.3
        _ShadowTint ("Shadow Tint", Color) = (0.55, 0.62, 0.74, 1)
        _RimColor ("Rim Color", Color) = (1, 1, 1, 1)
        _RimStrength ("Rim Strength", Range(0, 1)) = 0
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        _MacroScale ("Macro Variation Scale", Range(0.02, 1)) = 0.21
        _MacroStrength ("Macro Variation Strength", Range(0, 1)) = 0.45
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    CBUFFER_START(UnityPerMaterial)
        half _WrapDiffuse;
        half4 _ShadowTint;
        half4 _RimColor;
        half _RimStrength;
        half _RimPower;
        half _MacroScale;
        half _MacroStrength;
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
                output.fogFactor = PW_FogFactor(position.positionCS.z, position.positionWS);
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);

                uint row = unity_RendererUserValue & 1023u;
                half3 c0, c1, c2, c3, c4, c5, c6;
                if (!PW_HasPalettes())
                {
                    c0 = half3(0.56, 0.76, 0.43); c1 = half3(0.66, 0.84, 0.50); c2 = half3(0.47, 0.68, 0.38);
                    c3 = half3(0.86, 0.78, 0.60); c4 = half3(0.62, 0.47, 0.35); c5 = half3(0.36, 0.48, 0.34);
                    c6 = half3(0.70, 0.84, 0.47);
                }
                else
                {
                    c0 = PW_PaletteColor(row, 0); c1 = PW_PaletteColor(row, 1); c2 = PW_PaletteColor(row, 2);
                    c3 = PW_PaletteColor(row, 3); c4 = PW_PaletteColor(row, 4); c5 = PW_PaletteColor(row, 5);
                    c6 = PW_PaletteColor(row, 6);
                }

                float2 p = position.positionWS.xz * _MacroScale;
                half macro = sin(p.x) * sin(p.y * 1.31) + 0.5 * sin(p.x * 2.7 + p.y * 1.9 + 1.7);
                macro = saturate(0.5h + macro * 0.33h);

                half3 color = lerp(c0, c6, macro * _MacroStrength);
                color = lerp(color, c1, input.color.r);
                color = lerp(color, c2, input.color.g);
                color = lerp(color, c3, input.color.b);
                color = lerp(color, c4, input.color.a);
                color = lerp(color, c5, saturate(input.uv.x));

                output.albedo = color;
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
