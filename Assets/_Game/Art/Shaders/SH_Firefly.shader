// T48B fireflies of Soleil evenings (AmbientLife, behaviour Firefly): a round soft glow on the shared quad, facing the camera,
// breathing slowly (each one at its own pace). Additive, no texture, no point: a warm core in a pale green glow. The instance
// matrix carries the position (steered around every volume on the CPU) and the body scale; the soft glow is drawn a little larger.
Shader "PocketWilds/Firefly"
{
    Properties
    {
        _Core ("Core", Color) = (1, 0.96, 0.7, 1)
        _Glow ("Glow", Color) = (0.74, 0.9, 0.42, 1)
        _GlowScale ("Glow Scale", Range(1, 4)) = 1.8
        _Pulse ("Pulse Rate", Range(0.2, 4)) = 1.2
        _Intensity ("Intensity", Range(0, 3)) = 1.3
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    CBUFFER_START(UnityPerMaterial)
        half4 _Core;
        half4 _Glow;
        float _GlowScale;
        float _Pulse;
        half _Intensity;
    CBUFFER_END
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Firefly"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

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
                half glow : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                float3 center = TransformObjectToWorld(float3(0, 0, 0));
                float scale = length(TransformObjectToWorldDir(float3(1, 0, 0), false));
                float3 right = UNITY_MATRIX_V[0].xyz;
                float3 up = UNITY_MATRIX_V[1].xyz;
                float3 positionWS = center + (right * input.positionOS.x + up * input.positionOS.y) * (scale * _GlowScale);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                // Phase from the position itself (continuous): neighbours breathe apart, a slow drift never pops the pulse.
                float phase = dot(center, float3(1.7, 2.3, 1.3));
                float breath = 0.5 + 0.5 * sin(_Time.y * _Pulse + phase * 3.0);
                output.glow = (half)(0.25 + 0.75 * breath * breath);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half d = length(input.uv * 2.0h - 1.0h);
                half core = 1.0h - smoothstep(0.0h, 0.28h, d);
                half soft = 1.0h - smoothstep(0.15h, 1.0h, d);
                soft *= soft;
                half3 color = _Core.rgb * core + _Glow.rgb * soft * 0.55h;
                return half4(color * input.glow * _Intensity, 0.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
