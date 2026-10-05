// G-SH-011 Ambient life (MAT_AmbientLife_*): butterflies, bees and birds drawn by AmbientLife with GPU
// instancing (no GameObject, no Animator). Same palette scheme and lighting as SH_EnvironmentOpaque:
//   base color, vertex color R/G/B/A toward palette columns 1-4, uv.x occlusion toward column 5,
//   column 6 = second wing (or breast) color taken by every other instance, so one draw shows two variants.
// Wings: vertices with uv.y > 0 rotate around an axis along the body (object Z) through the shoulder
// (_Shoulder.x to the side, _Shoulder.y up). Angle = rest + sin(time * speed + phase) * amplitude (_Flap xyz);
// a folded bird uses a negative rest and no amplitude. The phase comes from the instance id.
Shader "PocketWilds/AmbientLife"
{
    Properties
    {
        _WrapDiffuse ("Wrap Diffuse", Range(0, 1)) = 0.6
        _ShadowTint ("Shadow Tint", Color) = (0.58, 0.62, 0.74, 1)
        _RimColor ("Rim Color", Color) = (1, 0.98, 0.9, 1)
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.2
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.5
        _OcclusionStrength ("Baked Occlusion", Range(0, 1)) = 1
        _PaletteRow ("Palette Row", Float) = 0
        _Flap ("Flap (speed rad/s, amplitude rad, rest rad)", Vector) = (14, 0.9, 0.25, 0)
        _Shoulder ("Shoulder (side, up)", Vector) = (0.004, 0, 0, 0)
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    CBUFFER_START(UnityPerMaterial)
        half _WrapDiffuse;
        half4 _ShadowTint;
        half4 _RimColor;
        half _RimStrength;
        half _RimPower;
        half _OcclusionStrength;
        float _PaletteRow;
        float4 _Flap;
        float4 _Shoulder;
    CBUFFER_END
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            // Wings are modelled two-sided (Blender): back faces stay culled.
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ FOG_LINEAR

            #include "Include/PW_Stylized.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half3 albedo : TEXCOORD2;
                half3 ambient : TEXCOORD3;
                half fogFactor : TEXCOORD4;
            };

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;

                float id = 0.0;
                #if defined(UNITY_INSTANCING_ENABLED)
                    id = (float)unity_InstanceID;
                #endif
                float phase = id * 2.39996;
                float speed = _Flap.x * (0.85 + 0.3 * frac(id * 0.618034));

                float3 positionOS = input.positionOS.xyz;
                float3 normalOS = input.normalOS;
                if (input.uv.y > 0.001)
                {
                    float side = positionOS.x >= 0.0 ? 1.0 : -1.0;
                    float angle = _Flap.z + sin(_Time.y * speed + phase) * _Flap.y;
                    float s, c;
                    sincos(angle, s, c);
                    float r = abs(positionOS.x) - _Shoulder.x;
                    float h = positionOS.y - _Shoulder.y;
                    positionOS.x = side * (_Shoulder.x + r * c - h * s);
                    positionOS.y = _Shoulder.y + r * s + h * c;
                    float nr = normalOS.x * side;
                    normalOS.x = side * (nr * c - normalOS.y * s);
                    normalOS.y = nr * s + normalOS.y * c;
                }

                float3 positionWS = TransformObjectToWorld(positionOS);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.fogFactor = PW_FogFactor(output.positionCS.z, positionWS);
                output.normalWS = TransformObjectToWorldNormal(normalOS);

                uint row = (uint)max(_PaletteRow, 0.0);
                half3 c0, c1, c2, c3, c4, c5, c6;
                if (!PW_HasPalettes())
                {
                    c0 = half3(0.3, 0.26, 0.24); c1 = half3(1.0, 0.95, 0.85); c2 = half3(0.95, 0.72, 0.4);
                    c3 = half3(0.3, 0.26, 0.24); c4 = c3; c5 = half3(0.2, 0.18, 0.16); c6 = half3(0.98, 0.88, 0.5);
                }
                else
                {
                    c0 = PW_PaletteColor(row, 0); c1 = PW_PaletteColor(row, 1); c2 = PW_PaletteColor(row, 2);
                    c3 = PW_PaletteColor(row, 3); c4 = PW_PaletteColor(row, 4); c5 = PW_PaletteColor(row, 5);
                    c6 = PW_PaletteColor(row, 6);
                }
                // Every other instance wears the variant color (column 6) on region R.
                c1 = frac(id * 0.5) > 0.25 ? c6 : c1;

                half3 color = c0;
                color = lerp(color, c1, input.color.r);
                color = lerp(color, c2, input.color.g);
                color = lerp(color, c3, input.color.b);
                color = lerp(color, c4, input.color.a);
                color = lerp(color, c5, saturate(input.uv.x) * _OcclusionStrength);

                output.albedo = color;
                output.ambient = SampleSH(output.normalWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 normalWS = normalize(input.normalWS);
                half3 viewWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                PWLightResult lighting = PW_StylizedLighting(input.albedo, normalWS, viewWS, input.positionWS, input.ambient);
                return half4(MixFog(lighting.color, input.fogFactor), 1);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
