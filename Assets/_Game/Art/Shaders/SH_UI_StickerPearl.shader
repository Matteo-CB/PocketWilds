// T44A « Albums » (doc « 01 Albums et autocollants », section 1.2): the Shiny sticker's pearly sheen, one shared UI
// material (MAT_UI_StickerPearl, built by AlbumScreenPipeline) for every Shiny sticker of the albums screen and the reveal.
// A nacre gradient (cream, pale pink, pale blue) crosses the sticker diagonally, slowly, once every 4 seconds, masked by the
// sticker's own alpha: no extra texture, never a pointed glint. The sticker's place in its rect comes from TEXCOORD1
// (StickerUv: xy 0 to 1 over the Image, z a manual phase, w flags: 1 the reveal's single pass follows z). With Reduce
// Motion (the global _PW_StickerStill at 1) the sheen rests still across the sticker. UI default stencil, clipping by
// RectMask2D, one pass, alpha blended.
Shader "PocketWilds/UIStickerPearl"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sticker", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Cream ("Nacre cream", Color) = (1.0, 0.97, 0.90, 1)
        _Pink ("Nacre pale pink", Color) = (0.99, 0.86, 0.90, 1)
        _Blue ("Nacre pale blue", Color) = (0.84, 0.92, 0.99, 1)
        _Period ("Seconds between two passes", Float) = 4
        _Sweep ("Part of the period the pass lasts", Range(0.1, 1)) = 0.35
        _Width ("Band width (sticker diagonals)", Range(0.05, 0.6)) = 0.22
        _Strength ("Sheen strength", Range(0, 1)) = 0.55
        _StillPhase ("Where the sheen rests with Reduce Motion", Range(0, 1)) = 0.42

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
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
            Name "StickerPearl"

            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 2.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

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
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            fixed4 _Cream;
            fixed4 _Pink;
            fixed4 _Blue;
            float _Period;
            float _Sweep;
            float _Width;
            float _Strength;
            float _StillPhase;
            float _PW_StickerStill;

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

            fixed4 Frag(Varyings input) : SV_Target
            {
                fixed4 color = (tex2D(_MainTex, input.uv) + _TextureSampleAdd) * input.color;

                // The pass: the band's center travels the diagonal from beyond one corner to beyond the other during
                // the first _Sweep of every _Period, then rests out of sight (one slow pass every 4 s).
                bool manual = fmod(input.local.w, 2.0) >= 0.5;
                float clock = frac(_Time.y / max(_Period, 0.1));
                float progress = manual ? input.local.z : saturate(clock / _Sweep);
                bool still = _PW_StickerStill > 0.5;
                progress = still ? _StillPhase : progress;
                float center = lerp(-0.4, 1.4, progress);
                float along = (input.local.x + input.local.y) * 0.5;
                float offset = (along - center) / _Width;
                float band = saturate(1.0 - abs(offset));
                band = band * band * (3.0 - 2.0 * band);
                // Nacre: pale pink ahead, cream in the middle, pale blue behind; resting softer when still.
                fixed3 nacre = offset < 0.0 ? lerp(_Pink.rgb, _Cream.rgb, saturate(offset + 1.0)) : lerp(_Cream.rgb, _Blue.rgb, saturate(offset));
                float strength = _Strength * (still ? 0.6 : 1.0) * (manual && input.local.z < 0.0 ? 0.0 : 1.0);
                color.rgb = lerp(color.rgb, max(color.rgb, nacre), band * strength);

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(input.world.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif
                return color;
            }
            ENDCG
        }
    }

    Fallback Off
}
