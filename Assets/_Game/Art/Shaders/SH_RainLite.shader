// Light rain (WeatherSystem, T33B): thin streaks drawn with GPU instancing. Each instance matrix only carries a
// position inside a unit box; the vertex shader wraps it through the rain box that follows the camera focus
// (_PW_RainBox: xyz center, w height; _PW_RainSize: xz extent), makes it fall and faces it to the camera.
// No CPU work per drop. Bounded by WeatherSystem: count from the tier (doc 07: -60 % Fluide, -30 % Équilibrée).
Shader "PocketWilds/RainLite"
{
    Properties
    {
        _Color ("Color", Color) = (0.86, 0.92, 1, 0.38)
        _Speed ("Fall Speed (m/s)", Range(1, 20)) = 9
        _Width ("Streak Width (m)", Range(0.002, 0.05)) = 0.016
        _Length ("Streak Length (m)", Range(0.05, 1)) = 0.42
        _Slant ("Wind Slant", Range(0, 1)) = 0.18
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    CBUFFER_START(UnityPerMaterial)
        half4 _Color;
        float _Speed;
        float _Width;
        float _Length;
        float _Slant;
    CBUFFER_END
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Rain"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            float4 _PW_RainBox;
            float4 _PW_RainSize;
            float4 _PW_Wind;
            half _PW_RainAlpha;

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

            float Hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                float3 unit = TransformObjectToWorld(float3(0, 0, 0));
                float h = Hash(unit * 7.31);
                float speed = _Speed * (0.8 + 0.4 * h);
                float3 size = float3(_PW_RainSize.x, _PW_RainBox.w, _PW_RainSize.z);
                float3 cell = unit;
                cell.y = frac(unit.y - _Time.y * speed / size.y);
                float3 baseWS = _PW_RainBox.xyz + (cell - 0.5) * size;

                float3 fall = normalize(float3(_PW_Wind.x * _Slant, -1.0, _PW_Wind.y * _Slant));
                float3 view = normalize(_WorldSpaceCameraPos - baseWS);
                float3 right = normalize(cross(fall, view));
                float3 positionWS = baseWS + right * (input.positionOS.x * _Width) - fall * (input.positionOS.y * _Length);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                // Fade in at the top and out near the bottom of the box: drops never pop.
                // Drops right in front of the lens would be huge streaks: they fade out under 4 m.
                float near = smoothstep(1.5, 4.0, distance(_WorldSpaceCameraPos, baseWS));
                output.alpha = (half)(smoothstep(0.0, 0.12, 1.0 - cell.y) * smoothstep(0.0, 0.15, cell.y) * near) * _PW_RainAlpha;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half across = 1.0h - abs(input.uv.x * 2.0h - 1.0h);
                half along = smoothstep(0.0h, 0.35h, input.uv.y) * smoothstep(1.0h, 0.6h, input.uv.y);
                return half4(_Color.rgb, _Color.a * across * along * input.alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
