// G-SH-007 Sky gradient (skybox material per biome, e.g. MAT_Sky_Grassland). No HDRI, no texture:
// top, horizon and below-horizon colors plus a soft sun glow. Colors are written into the biome
// material by the lighting pipeline from its LightingProfile; runtime only assigns the material.
Shader "PocketWilds/SkyGradient"
{
    Properties
    {
        _SkyTop ("Sky Top", Color) = (0.42, 0.66, 0.92, 1)
        _SkyHorizon ("Sky Horizon", Color) = (0.84, 0.92, 0.96, 1)
        _SkyBottom ("Below Horizon", Color) = (0.78, 0.86, 0.88, 1)
        _HorizonSharpness ("Horizon Sharpness", Range(0.2, 3)) = 0.6
        _SunTint ("Sun Glow Tint", Color) = (1, 0.93, 0.78, 1)
        _SunGlow ("Sun Glow Strength", Range(0, 1)) = 0.3
        _SunDirection ("Sun Direction (towards the sun)", Vector) = (0.3, 0.7, -0.6, 0)
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "RenderPipeline" = "UniversalPipeline" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            Name "Sky"

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _SkyTop;
                half4 _SkyHorizon;
                half4 _SkyBottom;
                half _HorizonSharpness;
                half4 _SunTint;
                half _SunGlow;
                float4 _SunDirection;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 d = normalize(input.direction);
                half up = (half)d.y;
                half3 color = up >= 0.0h
                    ? lerp(_SkyHorizon.rgb, _SkyTop.rgb, pow(saturate(up), _HorizonSharpness))
                    : lerp(_SkyHorizon.rgb, _SkyBottom.rgb, saturate(-up * 4.0h));
                half sun = pow(saturate(dot(d, normalize(_SunDirection.xyz))), 24.0) * _SunGlow;
                color += _SunTint.rgb * sun;
                return half4(color, 1);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
