// T36 wish bubble (doc 06 screen 002, doc 05 Sensations sheet 17): the bubble above an animal with a wish, its icon and
// its progress ring, drawn by SpriteRenderers (PF_World_WishBubble, built by WishBubblePipeline).
// Unlit, alpha blended (premultiplied), no depth write, both faces: the cheapest sprite there is on mobile.
// _Fill (0 to 1) shows the ring clockwise from the top, by the angle around the sprite's pivot (the circle's center,
// object space: atlas packing never moves it). 1 shows the whole sprite: the bubble and the icon share the material.
// Color: the SpriteRenderer's (URP passes it in unity_SpriteColor, older paths in the vertex color).
Shader "PocketWilds/WishBubble"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Fill ("Fill (clockwise from the top)", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True" }
        LOD 100

        Pass
        {
            Name "WishBubble"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half _Fill;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 local : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.color = input.color * unity_SpriteColor;
                output.uv = input.uv;
                output.local = input.positionOS.xy;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                if (_Fill < 0.999h)
                {
                    // Turn from the top, clockwise, 0 to 1; a soft edge a few pixels wide.
                    float turn = atan2(input.local.x, input.local.y) * (0.5 / PI);
                    turn = turn < 0.0 ? turn + 1.0 : turn;
                    color *= _Fill > 0.0005h ? (half)saturate((_Fill - turn) * 160.0 + 0.5) : 0.0h;
                }
                color.rgb *= color.a;
                return color;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
