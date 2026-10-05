// G-SH-007 Sky gradient (skybox material per biome, e.g. MAT_Sky_Grassland). No HDRI, no texture:
// top, horizon and below-horizon colors, a soft sun glow and (T33) stylized drifting clouds.
// The material holds the biome's default look. At runtime WorldAtmosphere blends the moods (morning, day,
// evening) through globals: when _PW_SkyBlend is 1 the globals replace the material colors, so moods change
// without touching the material asset.
Shader "PocketWilds/SkyGradient"
{
    Properties
    {
        _SkyTop ("Sky Top", Color) = (0.42, 0.66, 0.92, 1)
        _SkyHorizon ("Sky Horizon", Color) = (0.84, 0.92, 0.96, 1)
        _SkyBottom ("Below Horizon", Color) = (0.78, 0.86, 0.88, 1)
        _HorizonSharpness ("Horizon Sharpness", Range(0.2, 3)) = 0.6
        _SunTint ("Sun Glow Tint", Color) = (1, 0.93, 0.78, 1)
        _SunGlow ("Sun Glow Strength", Range(0, 1)) = 0.3
        _SunDirection ("Sun Direction (towards the sun)", Vector) = (0.3, 0.7, -0.6, 0)
        _CloudLight ("Cloud Lit Color", Color) = (1, 0.99, 0.96, 1)
        _CloudShade ("Cloud Shade Color", Color) = (0.78, 0.82, 0.92, 1)
        _CloudCoverage ("Cloud Coverage", Range(0, 1)) = 0.45
        _CloudScale ("Cloud Scale", Range(0.2, 4)) = 1.2
        _CloudSpeed ("Cloud Drift Speed", Range(0, 0.2)) = 0.012
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "RenderPipeline" = "UniversalPipeline" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            Name "Sky"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _SkyTop;
                half4 _SkyHorizon;
                half4 _SkyBottom;
                half _HorizonSharpness;
                half4 _SunTint;
                half _SunGlow;
                float4 _SunDirection;
                half4 _CloudLight;
                half4 _CloudShade;
                half _CloudCoverage;
                half _CloudScale;
                half _CloudSpeed;
            CBUFFER_END

            // Mood blend (WorldAtmosphere). _PW_SkyBlend 0 = material colors, 1 = globals.
            float _PW_SkyBlend;
            half4 _PW_SkyTop;
            half4 _PW_SkyHorizon;
            half4 _PW_SkyBottom;
            half4 _PW_SunTint;
            float4 _PW_SunDirection;
            half4 _PW_CloudLight;
            half4 _PW_CloudShade;
            // x cloud coverage (0 hides clouds on Low), y sun glow.
            float4 _PW_SkyParams;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                // View ray in world space: exact per pixel whatever the skybox mesh and its transform.
                output.direction = positionWS - _WorldSpaceCameraPos;
                return output;
            }

            // Integer hash of a lattice corner. A float hash (frac of large products) is not stable: the compiler
            // may reassociate (i + 1) * k differently in neighbor cells, so corners disagree and cells show seams.
            float Hash(float2 p)
            {
                uint2 q = (uint2)(int2)p;
                uint h = (q.x * 1597334677u) ^ (q.y * 3812015801u);
                h = h * 747796405u + 2891336453u;
                h = ((h >> ((h >> 28u) + 4u)) ^ h) * 277803737u;
                h = (h >> 22u) ^ h;
                return (float)h * (1.0 / 4294967295.0);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }

            float Clouds(float2 p)
            {
                return Noise(p) * 0.6 + Noise(p * 2.13 + 7.3) * 0.28 + Noise(p * 4.37 + 3.1) * 0.12;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 d = normalize(input.direction);
                half blend = (half)saturate(_PW_SkyBlend);
                half3 top = lerp(_SkyTop.rgb, _PW_SkyTop.rgb, blend);
                half3 horizon = lerp(_SkyHorizon.rgb, _PW_SkyHorizon.rgb, blend);
                half3 bottom = lerp(_SkyBottom.rgb, _PW_SkyBottom.rgb, blend);
                half3 sunTint = lerp(_SunTint.rgb, _PW_SunTint.rgb, blend);
                float3 sunDir = normalize(lerp(_SunDirection.xyz, _PW_SunDirection.xyz, blend) + float3(0, 1e-4, 0));
                half sunGlow = lerp(_SunGlow, (half)_PW_SkyParams.y, blend);
                half coverage = lerp(_CloudCoverage, (half)_PW_SkyParams.x, blend);

                half up = (half)d.y;
                half3 color = up >= 0.0h
                    ? lerp(horizon, top, pow(saturate(up), _HorizonSharpness))
                    : lerp(horizon, bottom, saturate(-up * 4.0h));
                half sun = pow(saturate(dot(d, sunDir)), 24.0) * sunGlow;
                color += sunTint * sun;

                // Stylized clouds on a virtual plane above the world: crisp soft-edged puffs, shaded away
                // from the sun, fading toward the horizon so the diorama silhouette stays clean.
                if (up > 0.02h && coverage > 0.001h)
                {
                    float2 uv = d.xz / (d.y + 0.12) * _CloudScale + float2(_Time.y * _CloudSpeed, _Time.y * _CloudSpeed * 0.37);
                    float density = Clouds(uv);
                    float threshold = 1.0 - coverage;
                    half mask = (half)smoothstep(threshold, threshold + 0.08, density);
                    // Painted volume: rims stay bright, thick cores and the side away from the sun take the shade.
                    float shadeSample = Clouds(uv + sunDir.xz * 0.12);
                    half core = (half)saturate((density - threshold) * 3.5);
                    half self = (half)saturate((shadeSample - density) * 7.0);
                    half3 cloudLight = lerp(_CloudLight.rgb, _PW_CloudLight.rgb, blend);
                    half3 cloudShade = lerp(_CloudShade.rgb, _PW_CloudShade.rgb, blend);
                    half3 cloud = lerp(cloudLight, cloudShade, saturate(core * 0.55h + self * 0.6h)) + sunTint * sun * 0.5h;
                    half fade = saturate((up - 0.02h) * 5.0h);
                    color = lerp(color, cloud, mask * fade);
                }
                return half4(color, 1);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
