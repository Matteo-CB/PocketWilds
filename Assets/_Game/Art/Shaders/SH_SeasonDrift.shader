// T48B season drift (SeasonDrift): round soft petals (Éveil), rounded falling leaves (Récolte), fine round snow (Givre), drawn
// as ONE instanced quad. Each instance matrix only carries a position inside a unit box; the vertex shader wraps it through
// the drift box that follows the view (_PW_DriftBox: xyz center, w height; _PW_DriftSize: xz extent), makes it fall, sway
// and tumble, and faces it to the camera. Every shape is rounded: no point, no four-branch glint. Each particle fades out
// before the box floor, which stays above the ground: nothing ever rests inside anything. No CPU work per particle.
Shader "PocketWilds/SeasonDrift"
{
    Properties
    {
        _ColorA ("Color A", Color) = (0.96, 0.78, 0.84, 1)
        _ColorB ("Color B", Color) = (0.98, 0.95, 0.86, 1)
        _Shape ("Shape (0 petal, 1 leaf, 2 snow)", Range(0, 2)) = 0
        _Size ("Size (m)", Range(0.01, 0.2)) = 0.06
        _Fall ("Fall Speed (m/s)", Range(0.05, 3)) = 0.5
        _Sway ("Sway (m)", Range(0, 1.5)) = 0.4
        _Alpha ("Opacity", Range(0, 1)) = 0.92
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    CBUFFER_START(UnityPerMaterial)
        half4 _ColorA;
        half4 _ColorB;
        float _Shape;
        float _Size;
        float _Fall;
        float _Sway;
        half _Alpha;
    CBUFFER_END
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Drift"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            float4 _PW_DriftBox;
            float4 _PW_DriftSize;
            float4 _PW_Wind;

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
                half4 color : TEXCOORD1;
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
                float h2 = Hash(unit * 3.17 + 1.3);
                float t = _Time.y;
                float3 size = float3(_PW_DriftSize.x, _PW_DriftBox.w, _PW_DriftSize.z);
                float speed = _Fall * (0.75 + 0.5 * h);
                float3 cell = unit;
                cell.y = frac(unit.y - t * speed / max(size.y, 0.01));
                float3 baseWS = _PW_DriftBox.xyz + (cell - 0.5) * size;

                // Lazy side sway and a little wind push; snow drifts straighter.
                float phase = h2 * 6.2831853;
                float snow = step(1.5, _Shape);
                baseWS.x += sin(t * (0.55 + 0.5 * h) + phase) * _Sway + _PW_Wind.x * _PW_Wind.z * 0.25;
                baseWS.z += cos(t * (0.45 + 0.4 * h2) + phase * 1.7) * _Sway * 0.7 + _PW_Wind.y * _PW_Wind.z * 0.25;

                // Camera-facing quad, spinning slowly in its plane; petals and leaves also flip (squash across).
                float3 view = normalize(_WorldSpaceCameraPos - baseWS);
                float3 right = normalize(cross(float3(0, 1, 0), view));
                float3 up = cross(view, right);
                float spin = t * (0.6 + 1.4 * h2) * lerp(1.0, 0.25, snow) + phase;
                float flip = lerp(0.3 + 0.7 * abs(cos(t * (0.9 + h) + phase)), 1.0, snow);
                float aspect = _Shape < 0.5 ? 0.8 : (_Shape < 1.5 ? 0.6 : 1.0);
                float2 corner = input.positionOS.xy * float2(flip, aspect);
                float s = sin(spin);
                float c = cos(spin);
                float2 turned = float2(c * corner.x - s * corner.y, s * corner.x + c * corner.y);
                float diameter = _Size * (0.7 + 0.6 * h);
                float3 positionWS = baseWS + (right * turned.x + up * turned.y) * diameter;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;

                // In at the top, out well above the floor (the box floor is itself above the ground); none right at the lens.
                float near = smoothstep(1.2, 3.0, distance(_WorldSpaceCameraPos, baseWS));
                float fade = smoothstep(0.05, 0.25, cell.y) * smoothstep(0.0, 0.1, 1.0 - cell.y) * near;
                half4 color = lerp(_ColorA, _ColorB, step(0.5, h2));
                color.rgb *= 0.9 + 0.2 * h;
                color.a = _Alpha * fade;
                output.color = color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half2 p = input.uv * 2.0h - 1.0h;
                half shape;
                half shade = 1.0h;
                if (_Shape < 0.5)
                {
                    // Petal: a soft rounded oval, a touch lighter in its heart.
                    half d = length(p * half2(1.0h, 1.15h));
                    shape = 1.0h - smoothstep(0.72h, 1.0h, d);
                    shade = 1.0h + 0.12h * (1.0h - d);
                }
                else if (_Shape < 1.5)
                {
                    // Leaf: a rounded oval with a soft darker midrib, both ends round.
                    half d = length(p * half2(1.25h, 1.0h));
                    shape = 1.0h - smoothstep(0.75h, 1.0h, d);
                    shade = 1.0h - 0.16h * (1.0h - smoothstep(0.0h, 0.1h, abs(p.x))) * step(abs(p.y), 0.7h);
                }
                else
                {
                    // Snow: a fine round soft dot.
                    half d = length(p);
                    shape = 1.0h - smoothstep(0.35h, 1.0h, d);
                    shape *= shape;
                }
                return half4(input.color.rgb * shade, input.color.a * shape);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
