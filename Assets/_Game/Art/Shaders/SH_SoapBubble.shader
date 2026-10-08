// T38 soap bubble and foam of the grooming (doc 01 Contenu S-ACT-001 « Bulle de savon », H-VFX-ACT-001-01 « mousse douce »):
// GR_SoapBubble (a smooth sphere, uv0.y = height) drawn like the wish bubble (SH_WishBubble: unlit, premultiplied alpha,
// no depth write) but in 3D: a clear body, a soft rim, a pastel thin-film sheen that drifts slowly, one round window
// highlight and a soft lilac edge so it reads on white fur too. Round shapes only (owner rule: no four-point sparkle).
// Per renderer (Shader User Value, SRP Batcher compatible): bits 0-1 the sheen's lean (0 neutral, 1 pink, 2 sky).
Shader "PocketWilds/SoapBubble"
{
    Properties
    {
        _Tint ("Body Tint", Color) = (0.93, 0.97, 1, 1)
        _Edge ("Edge Color", Color) = (0.72, 0.74, 0.9, 1)
        _BodyAlpha ("Body Alpha", Range(0, 1)) = 0.14
        _RimAlpha ("Rim Alpha", Range(0, 1)) = 0.8
        _Iridescence ("Thin-Film Sheen", Range(0, 1)) = 0.55
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        LOD 100

        Pass
        {
            Name "SoapBubble"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half4 _Edge;
                half _BodyAlpha;
                half _RimAlpha;
                half _Iridescence;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half height : TEXCOORD2;
                half lean : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.height = input.uv.y;
                output.lean = half(unity_RendererUserValue & 3u);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 n = normalize(input.normalWS);
                half3 v = normalize(GetWorldSpaceViewDir(input.positionWS));
                half fresnel = 1.0h - saturate(dot(n, v));

                // Thin film: soft pastel bands that follow the rim and the height, drifting slowly.
                half phase = fresnel * 1.4h + input.height * 0.6h + (half)(_Time.y * 0.12) + input.lean * 0.33h;
                half3 film = 0.5h + 0.5h * cos(6.2831h * (phase + half3(0.0h, 0.33h, 0.67h)));
                film = lerp(half3(1.0h, 1.0h, 1.0h), film, 0.45h);
                half3 lean = input.lean > 1.5h ? half3(0.86h, 0.94h, 1.0h) : input.lean > 0.5h ? half3(1.0h, 0.9h, 0.95h) : half3(1.0h, 1.0h, 1.0h);
                half3 color = _Tint.rgb * lean;
                color = lerp(color, film * lean, _Iridescence * smoothstep(0.25h, 0.9h, fresnel));

                // The rim: clear in the middle, soft and bright toward the edge, a thin lilac line at the very silhouette.
                half alpha = lerp(_BodyAlpha, _RimAlpha, pow(fresnel, 2.2h));
                half outline = smoothstep(0.82h, 0.97h, fresnel);
                color = lerp(color, _Edge.rgb, outline * 0.6h);

                // One round window highlight from the sun, and a smaller soft one opposite it.
                Light light = GetMainLight();
                half3 r = reflect(-v, n);
                half spot = pow(saturate(dot(r, light.direction)), 60.0h);
                half back = pow(saturate(dot(r, -light.direction * half3(1.0h, -1.0h, 1.0h))), 30.0h) * 0.35h;
                half shine = saturate(spot + back);
                color = lerp(color, half3(1.0h, 1.0h, 1.0h), shine);
                alpha = saturate(alpha + shine * 0.9h);
                return half4(color * alpha, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
