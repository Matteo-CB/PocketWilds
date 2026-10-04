// G-SH-001 Animal master. One material for every species and variant.
// Colors come from a global palette texture (_PW_AnimalPalettes, 8 colors per row).
// The row and pattern come from the per-renderer Shader User Value (SRP Batcher compatible, no material clones):
//   bits 0-9   palette row
//   bits 10-12 pattern mask (0 none, 1 A = uv0.x, 2 B = uv0.y, 3 C = uv1.x, 4 D = uv1.y)
//   bits 13-16 pattern strength (0-15)
//   bits 17-20 rare effect intensity (0-15), reserved for the RareAura module
// Regions come from vertex colors: R secondary, G belly, B detail (nose, paws), A eyes.
// Albedo is resolved per vertex: cheap and soft, which suits the plush style.
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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_PW_AnimalPalettes);
            SAMPLER(sampler_PW_AnimalPalettes);
            float _PW_PaletteRowCount;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                float2 mask01 : TEXCOORD0;
                float2 mask23 : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half3 albedo : TEXCOORD2;
                half eye : TEXCOORD3;
                half3 ambient : TEXCOORD4;
            };

            half3 PaletteColor(uint row, uint column)
            {
                float2 uv = float2((column + 0.5) / 8.0, (row + 0.5) / max(_PW_PaletteRowCount, 1.0));
                return SAMPLE_TEXTURE2D_LOD(_PW_AnimalPalettes, sampler_PW_AnimalPalettes, uv, 0).rgb;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);

                uint data = unity_RendererUserValue;
                uint row = data & 1023u;
                uint patternChannel = (data >> 10) & 7u;
                half patternStrength = half((data >> 13) & 15u) / 15.0h;

                half3 primary, secondary, belly, detail, eyeColor, patternColor;
                if (_PW_PaletteRowCount < 0.5)
                {
                    // No palette loaded (editor preview): neutral plush defaults.
                    primary = half3(0.80, 0.70, 0.60); secondary = half3(0.95, 0.80, 0.80);
                    belly = half3(0.97, 0.94, 0.88); detail = half3(0.95, 0.60, 0.65);
                    eyeColor = half3(0.04, 0.03, 0.04); patternColor = half3(0.55, 0.42, 0.34);
                }
                else
                {
                    primary = PaletteColor(row, 0); secondary = PaletteColor(row, 1);
                    belly = PaletteColor(row, 2); detail = PaletteColor(row, 3);
                    eyeColor = PaletteColor(row, 4); patternColor = PaletteColor(row, 5);
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
                output.ambient = SampleSH(output.normalWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 normalWS = normalize(input.normalWS);
                half3 viewWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));

                half ndl = dot(normalWS, mainLight.direction);
                half wrapped = saturate((ndl + _WrapDiffuse) / (1.0h + _WrapDiffuse));
                half lit = wrapped * mainLight.shadowAttenuation;

                // Shadowed side keeps a soft colored tint instead of going grey.
                half3 lightTerm = mainLight.color * lit + _ShadowTint.rgb * (1.0h - lit) * 0.35h;
                half3 color = input.albedo * (input.ambient + lightTerm);

                half rim = pow(1.0h - saturate(dot(normalWS, viewWS)), _RimPower) * _RimStrength;
                color += rim * _RimColor.rgb * (0.5h + 0.5h * lit);

                half3 halfDir = normalize(mainLight.direction + viewWS);
                half spec = pow(saturate(dot(normalWS, halfDir)), 64.0h) * input.eye * _EyeGloss;
                color += spec * mainLight.color;

                return half4(color, 1);
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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            float4 ShadowVert(Attributes input) : SV_POSITION
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirection = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirection = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirection));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                return positionCS;
            }

            half4 ShadowFrag() : SV_Target
            {
                return 0;
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

            float4 DepthVert(float4 positionOS : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(positionOS.xyz);
            }

            half DepthFrag() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
