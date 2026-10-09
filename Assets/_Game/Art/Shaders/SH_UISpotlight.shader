// T39 night (doc 01 Contenu S-ACT-002, challenge 7 « Dans le noir »: « nuit, cercle éclairé de 25 % de l'écran autour du
// doigt », an interface mask, no real-time light): one full screen UI Image of PF_UI_ActivityHud (built by
// ActivityUiPipeline), a deep blue-violet night over the play area with a soft clear circle around a point. Never pure
// black: about 80 % opaque, a touch denser far from the circle. Unlit, alpha blended, one quad, one draw call, no texture
// read. The circle comes from the Image's UVs (0 to 1 over its full screen rect, the Image has no sprite):
// _Spot.xy the center in UVs, _Spot.z the radius in widths of the rect (the middle of the soft edge), _Spot.w the rect's
// height over its width (a round circle on any screen). ActivityHudScreen.SpotlightOpacity mirrors this math for tests.
Shader "PocketWilds/UISpotlight"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture (unused, the Image's white)", 2D) = "white" {}
        _Color ("Night", Color) = (0.13, 0.12, 0.28, 1)
        _Spot ("Center (uv), radius (widths), height over width", Vector) = (0.5, 0.5, 0.25, 2.1667)
        _Feather ("Soft edge (widths)", Range(0.005, 0.3)) = 0.07
        _Near ("Opacity by the circle", Range(0, 1)) = 0.76
        _Far ("Opacity far from it", Range(0, 1)) = 0.86
        _Reach ("Distance to the far opacity (widths)", Range(0.05, 2)) = 0.8
        _Fade ("Fade", Range(0, 1)) = 1

        // The default UI shader's stencil and color mask (a Mask parent, never used by the HUD but kept compatible).
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
            Name "Spotlight"

            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 2.0

            #include "UnityCG.cginc"

            struct Attributes
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            float4 _Spot;
            float _Feather;
            float _Near;
            float _Far;
            float _Reach;
            float _Fade;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.position = UnityObjectToClipPos(input.vertex);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            fixed4 Frag(Varyings input) : SV_Target
            {
                // Distance to the center in widths of the screen: the circle stays round on every aspect.
                float2 offset = (input.uv - _Spot.xy) * float2(1.0, _Spot.w);
                float away = length(offset);
                // Clear inside, a soft edge of _Feather around the radius, the night outside (smoothstep: no rim, no band).
                float night = smoothstep(_Spot.z - _Feather * 0.5, _Spot.z + _Feather * 0.5, away);
                float depth = lerp(_Near, _Far, saturate((away - _Spot.z) / _Reach));
                fixed4 color = _Color * input.color;
                color.a = night * depth * _Fade * input.color.a;
                return color;
            }
            ENDCG
        }
    }

    Fallback Off
}
