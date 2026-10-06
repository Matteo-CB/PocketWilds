// Blob shadow (MAT_BlobShadow). Low quality has no shadow map: a soft multiplied disc under each
// animal and egg keeps them grounded for the cost of one tiny quad. Uses tile 0 (soft dot) of the
// shared VFX atlas. Multiplicative blend, no depth write, drawn right after opaque geometry.
Shader "PocketWilds/BlobShadow"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("VFX Atlas (R, tile 0 soft dot)", 2D) = "white" {}
        _ShadowColor ("Shadow Color", Color) = (0.42, 0.5, 0.48, 1)
        _Strength ("Strength", Range(0, 1)) = 0.7
    }

    SubShader
    {
        Tags { "Queue" = "Geometry+450" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "BlobShadow"
            Tags { "LightMode" = "UniversalForward" }
            Blend DstColor Zero
            ZWrite Off
            ZTest LEqual
            Offset -1, -1
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _ShadowColor;
                half _Strength;
            CBUFFER_END

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
                // Tile 0 (soft dot) of the 8 x 1 VFX atlas (T34A).
                output.uv = input.uv * float2(0.125, 1.0);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half mask = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).r;
                // Widen the soft dot: the core sits under the body, the visible part is the falloff.
                half shade = saturate(pow(max(mask, 0.0h), 0.55h) * 1.25h) * _Strength;
                return half4(lerp(half3(1, 1, 1), _ShadowColor.rgb, shade), 1);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
