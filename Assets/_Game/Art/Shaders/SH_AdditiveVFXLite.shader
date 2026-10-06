// G-SH-008 Additive VFX lite. One shared material for every feedback effect (glints, flashes, motes, hearts).
// Texture: single-channel atlas, 8 tiles in a row (VfxPipeline: dot, glint, heart, paw, ring, star, petal, bubble); particle systems
// pick the tile with Texture Sheet Animation, so every effect shares one material and one texture.
// Additive, unlit, no depth write, no soft-particle depth sampling (cheap on mobile).
Shader "PocketWilds/AdditiveVFXLite"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Atlas (R)", 2D) = "white" {}
        _Intensity ("Intensity", Range(0, 4)) = 1.4
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        LOD 100

        Pass
        {
            Name "Additive"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half _Intensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half shape = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).r;
                half3 color = input.color.rgb * (input.color.a * shape * _Intensity);
                return half4(color, 0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
