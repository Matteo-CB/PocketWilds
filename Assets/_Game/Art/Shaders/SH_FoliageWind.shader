// G-SH-009 Foliage wind master (MAT_Foliage_*): grass, flowers and small plants drawn by the ScatterRenderer
// with GPU instancing (no GameObject per plant). Same palette scheme and lighting as SH_EnvironmentOpaque:
//   base color, vertex color R/G/B/A toward palette columns 1-4, uv.x occlusion toward column 5,
//   column 6 = variation tint mixed per instance so no two tufts or flowers read identical.
// Palette row: _PaletteRow (set per draw by the ScatterRenderer), or the renderer's Shader User Value when -1.
// Wind: global PW_WindOffset (Include/PW_World.hlsl), weighted by uv.y = height factor from Blender.
// Scatter never casts shadows (cost) but receives them on tiers that have a shadow map.
Shader "PocketWilds/FoliageWind"
{
    Properties
    {
        _WrapDiffuse ("Wrap Diffuse", Range(0, 1)) = 0.55
        _ShadowTint ("Shadow Tint", Color) = (0.58, 0.62, 0.74, 1)
        _RimColor ("Rim Color", Color) = (1, 0.98, 0.88, 1)
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.1
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.5
        _OcclusionStrength ("Baked Occlusion", Range(0, 1)) = 1
        _PaletteRow ("Palette Row (-1 = renderer value)", Float) = -1
        _WindScale ("Wind Scale", Range(0, 2)) = 1
        _NormalUp ("Normals Toward Up", Range(0, 1)) = 0.6
        _TintJitter ("Per-Instance Tint Jitter", Range(0, 1)) = 0.45
        _BrightnessJitter ("Per-Instance Brightness Jitter", Range(0, 0.3)) = 0.12
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
        float _PaletteRow;
        half _WindScale;
        half _NormalUp;
        half _TintJitter;
        half _BrightnessJitter;
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
            #pragma multi_compile_instancing
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
                UNITY_VERTEX_INPUT_INSTANCE_ID
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
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;

                float3 originWS = TransformObjectToWorld(float3(0, 0, 0));
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                half tip = saturate(half(input.uv.y));
                positionWS += PW_WindOffset(originWS, max(0.0, positionWS.y - originWS.y), tip) * _WindScale;

                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.fogFactor = PW_FogFactor(output.positionCS.z, positionWS);
                half3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.normalWS = normalize(lerp(normalWS, half3(0, 1, 0), _NormalUp));

                uint row = _PaletteRow >= 0.0 ? (uint)_PaletteRow : (unity_RendererUserValue & 1023u);
                float h0 = PW_Hash21(originWS.xz);
                float h1 = PW_Hash21(originWS.zx + 17.13);
                half brightness = 1.0h + half(h0 - 0.5) * 2.0h * _BrightnessJitter;

                half3 c0, c1, c2, c3, c4, c5, c6;
                if (!PW_HasPalettes())
                {
                    c0 = half3(0.36, 0.55, 0.30); c1 = half3(0.55, 0.77, 0.42); c2 = half3(0.71, 0.87, 0.52);
                    c3 = half3(0.44, 0.67, 0.36); c4 = half3(0.96, 0.92, 0.84); c5 = half3(0.30, 0.40, 0.30);
                    c6 = half3(0.66, 0.78, 0.40);
                    brightness = 1.0h;
                }
                else
                {
                    c0 = PW_PaletteColor(row, 0); c1 = PW_PaletteColor(row, 1); c2 = PW_PaletteColor(row, 2);
                    c3 = PW_PaletteColor(row, 3); c4 = PW_PaletteColor(row, 4); c5 = PW_PaletteColor(row, 5);
                    c6 = PW_PaletteColor(row, 6);
                }

                half3 color = c0;
                color = lerp(color, c1, input.color.r);
                color = lerp(color, c2, input.color.g);
                color = lerp(color, c3, input.color.b);
                color = lerp(color, c4, input.color.a);
                // Variation tint on the upper part: tips catch the meadow's yellow-green or a flower's second shade.
                color = lerp(color, c6, half(h1) * _TintJitter * (0.35h + 0.65h * tip));
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
    }

    FallBack Off
}
