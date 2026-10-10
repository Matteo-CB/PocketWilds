// T44A « Albums » (doc « 01 Albums et autocollants », section 1.12): the shared silhouette of a sticker not found yet,
// one UI material (MAT_UI_StickerSilhouette, built by AlbumScreenPipeline): the sticker's own shape (its sprite's alpha) in
// a soft grey at 40 %, with a dotted outline of round dots; without a sprite (art not captured, or « bientôt ») the soft
// generic sticker shape, a rounded square. TEXCOORD1 comes from StickerUv (xy 0 to 1 over the Image, w flag 2: generic).
// No extra texture: the outline is the alpha's edge sampled around the pixel, the dots a round grid over the rect.
// UI default stencil, clipping by RectMask2D, one pass, alpha blended.
Shader "PocketWilds/UIStickerSilhouette"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sticker", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Fill ("Fill (soft grey, 40 %)", Color) = (0.62, 0.60, 0.58, 0.4)
        _Line ("Dotted outline", Color) = (0.55, 0.52, 0.49, 0.85)
        _Edge ("Outline reach (texels)", Range(0.5, 6)) = 2.5
        _Dots ("Dots across the sticker", Range(6, 40)) = 18
        _DotSize ("Dot radius (cell)", Range(0.1, 0.5)) = 0.3
        _Corner ("Generic shape: corner radius", Range(0, 0.5)) = 0.18
        _Inset ("Generic shape: inset", Range(0, 0.3)) = 0.06

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True" }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "StickerSilhouette"

            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct Attributes
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 local : TEXCOORD1;
                float4 world : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            float4 _ClipRect;
            fixed4 _Fill;
            fixed4 _Line;
            float _Edge;
            float _Dots;
            float _DotSize;
            float _Corner;
            float _Inset;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.world = input.vertex;
                output.position = UnityObjectToClipPos(input.vertex);
                output.color = input.color * _Color;
                output.uv = input.uv;
                output.local = input.uv1;
                return output;
            }

            // Signed distance to the rounded square (negative inside), in rect units.
            float RoundedSquare(float2 p)
            {
                float2 halfSize = 0.5 - _Inset;
                float2 q = abs(p - 0.5) - (halfSize - _Corner);
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - _Corner;
            }

            fixed4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.local.xy;
                bool generic = input.local.w >= 1.5;
                float shape;
                float edge;
                if (generic)
                {
                    float d = RoundedSquare(p);
                    float aa = max(fwidth(d), 0.002);
                    shape = 1.0 - smoothstep(-aa, aa, d);
                    edge = 1.0 - smoothstep(0.0, 0.018, abs(d + 0.012));
                }
                else
                {
                    float2 t = _MainTex_TexelSize.xy * _Edge;
                    float a = tex2D(_MainTex, input.uv).a;
                    float a1 = tex2D(_MainTex, input.uv + float2(t.x, 0)).a;
                    float a2 = tex2D(_MainTex, input.uv - float2(t.x, 0)).a;
                    float a3 = tex2D(_MainTex, input.uv + float2(0, t.y)).a;
                    float a4 = tex2D(_MainTex, input.uv - float2(0, t.y)).a;
                    float high = max(max(a1, a2), max(max(a3, a4), a));
                    float low = min(min(a1, a2), min(min(a3, a4), a));
                    shape = a;
                    edge = saturate((high - low) * 2.0) * step(0.02, high);
                }
                // Round dots on a grid over the rect: the outline shows only where it crosses a dot.
                float2 cell = frac(p * _Dots) - 0.5;
                float dots = 1.0 - smoothstep(_DotSize - 0.08, _DotSize, length(cell));
                float lineAlpha = edge * dots * _Line.a;
                fixed4 color;
                color.rgb = lerp(_Fill.rgb, _Line.rgb, saturate(lineAlpha / max(lineAlpha + _Fill.a * shape, 0.001)));
                color.a = max(_Fill.a * shape, lineAlpha);
                color *= input.color;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(input.world.xy, _ClipRect);
                #endif
                return color;
            }
            ENDCG
        }
    }

    Fallback Off
}
