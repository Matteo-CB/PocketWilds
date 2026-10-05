// Soft sun rays (Magnifique only, doc 07; WeatherSystem, T33B): a few long additive cards slanted along the sun
// direction (_PW_SunDirection, toward the sun) and turned toward the camera around that axis. Each instance
// matrix carries the foot position and its scale: x = width, y = length. Gentle breathing so they feel alive.
Shader "PocketWilds/SunRays"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.93, 0.74, 1)
        _Intensity ("Intensity", Range(0, 1)) = 0.1
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    CBUFFER_START(UnityPerMaterial)
        half4 _Color;
        half _Intensity;
    CBUFFER_END
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "SunRays"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            float4 _PW_SunDirection;
            half _PW_SunRaysAlpha;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half2 uv : TEXCOORD0;
                half alpha : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                float3 foot = TransformObjectToWorld(float3(0, 0, 0));
                float width = length(float3(UNITY_MATRIX_M[0].x, UNITY_MATRIX_M[1].x, UNITY_MATRIX_M[2].x));
                float len = length(float3(UNITY_MATRIX_M[0].y, UNITY_MATRIX_M[1].y, UNITY_MATRIX_M[2].y));
                float3 axis = normalize(_PW_SunDirection.xyz + float3(0, 1e-3, 0));
                float3 view = normalize(_WorldSpaceCameraPos - foot);
                float3 right = normalize(cross(axis, view));
                float3 positionWS = foot + right * (input.positionOS.x * width) + axis * (input.positionOS.y * len);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                float phase = foot.x * 0.37 + foot.z * 0.21;
                output.alpha = (half)(0.75 + 0.25 * sin(_Time.y * 0.45 + phase)) * _PW_SunRaysAlpha;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half across = 1.0h - abs(input.uv.x * 2.0h - 1.0h);
                across = across * across * (3.0h - 2.0h * across);
                half along = smoothstep(0.0h, 0.35h, input.uv.y) * (1.0h - input.uv.y) * (1.0h - input.uv.y);
                half a = across * along * _Intensity * input.alpha;
                return half4(_Color.rgb * a, a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
