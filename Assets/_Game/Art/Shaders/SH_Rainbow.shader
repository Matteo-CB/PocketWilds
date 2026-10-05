// Rainbow after a shower (WeatherSystem, T33B): one soft arc mesh far in the sky, opposite the sun.
// uv.x runs along the arc (ends fade), uv.y across the bands (0 inner violet, 1 outer red). Additive, pastel.
Shader "PocketWilds/Rainbow"
{
    Properties
    {
        _Intensity ("Intensity", Range(0, 1)) = 0.55
        _Pastel ("Pastel", Range(0, 1)) = 0.12
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    CBUFFER_START(UnityPerMaterial)
        half _Intensity;
        half _Pastel;
    CBUFFER_END
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-50" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Rainbow"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            half _PW_RainbowAlpha;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half3 Spectrum(half t)
            {
                // Violet (inner) to red (outer), soft.
                half3 c = saturate(half3(abs(t * 6.0h - 3.0h) - 1.0h, 2.0h - abs(t * 6.0h - 2.0h), 2.0h - abs(t * 6.0h - 4.0h)));
                return c;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half band = (half)input.uv.y;
                half3 color = Spectrum(1.0h - band * 0.83h);
                color = lerp(color, half3(1, 1, 1), _Pastel);
                half edges = smoothstep(0.0h, 0.18h, band) * smoothstep(1.0h, 0.82h, band);
                half ends = smoothstep(0.0h, 0.22h, (half)input.uv.x) * smoothstep(1.0h, 0.78h, (half)input.uv.x);
                return half4(color, edges * ends * _Intensity * _PW_RainbowAlpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
