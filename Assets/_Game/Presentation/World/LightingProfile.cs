using PocketWilds.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// Biome lighting data (LP_&lt;Biome&gt;_Day). One directional sun, trilight ambient, cheap linear fog toward
    /// the horizon color, a gradient sky material. Shadow distance and resolution belong to the quality tier
    /// (URP assets): the profile never makes Low more expensive.
    /// </summary>
    [CreateAssetMenu(menuName = "Pocket Wilds/Lighting Profile", fileName = "LP_Biome_Day")]
    public sealed class LightingProfile : ScriptableObject
    {
        [Header("Sun")]
        public Color sunColor = new Color(1f, 0.95f, 0.87f);
        [Min(0f)] public float sunIntensity = 1.05f;
        [Tooltip("Pitch (down from horizon) and yaw, degrees.")] public Vector2 sunAngles = new Vector2(50f, -32f);
        [Range(0f, 1f)] public float shadowStrength = 0.65f;

        [Header("Ambient (trilight)")]
        public Color ambientTop = new Color(0.62f, 0.7f, 0.82f);
        public Color ambientEquator = new Color(0.6f, 0.66f, 0.62f);
        public Color ambientBottom = new Color(0.38f, 0.42f, 0.34f);

        [Header("Sky (written into the sky material by the lighting pipeline)")]
        public Material sky;
        public Color skyTop = new Color(0.42f, 0.66f, 0.92f);
        public Color skyHorizon = new Color(0.84f, 0.92f, 0.96f);
        public Color skyBottom = new Color(0.78f, 0.86f, 0.88f);

        [Header("Fog (linear, per vertex)")]
        public bool fog = true;
        public Color fogColor = new Color(0.84f, 0.92f, 0.96f);
        [Min(0f)] public float fogStart = 20f;
        [Min(0f)] public float fogEnd = 48f;

        [Header("Sky glow and clouds (T33)")]
        public Color sunGlowTint = new Color(1f, 0.93f, 0.78f);
        [Range(0f, 1f)] public float sunGlow = 0.3f;
        public Color cloudLight = new Color(1f, 0.99f, 0.96f);
        public Color cloudShade = new Color(0.78f, 0.82f, 0.92f);
        [Range(0f, 1f)] public float cloudCoverage = 0.45f;

        [Header("Height fog (T33): pools in the low distance")]
        [Range(0f, 1f)] public float heightFogStrength = 0.35f;
        public float heightFogBase = 0f;
        [Min(0f)] public float heightFogFalloff = 0.45f;
        [Min(0f)] public float heightFogStart = 14f;
        [Min(1f)] public float heightFogRange = 24f;

        [Header("Wind (T33): vegetation sway, global")]
        [Range(0f, 1f)] public float windStrength = 0.35f;
        [Tooltip("Direction the wind blows toward, degrees around world Y (0 = +Z).")]
        public float windYaw = 35f;
        [Min(0f)] public float windSpeed = 1.6f;
        [Min(0f)] public float windWaveFrequency = 0.35f;
        [Range(0f, 1f)] public float gustStrength = 0.5f;
        [Min(0f)] public float gustFrequency = 0.6f;
        [Range(0f, 0.2f)] public float flutter = 0.035f;

        /// <summary>Applies the profile to the scene, the sun and the camera for a quality tier. Called on scene entry.</summary>
        public void Apply(Light sun, Camera camera, QualityTier tier)
        {
            if (sun != null)
            {
                sun.type = LightType.Directional;
                sun.color = sunColor;
                sun.intensity = sunIntensity;
                sun.transform.rotation = Quaternion.Euler(sunAngles.x, sunAngles.y, 0f);
                // Low has no shadow map at all (URP_Low): animals and eggs use blob shadows instead.
                sun.shadows = tier == QualityTier.Low ? LightShadows.None : LightShadows.Soft;
                sun.shadowStrength = shadowStrength;
                RenderSettings.sun = sun;
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = ambientTop;
            RenderSettings.ambientEquatorColor = ambientEquator;
            RenderSettings.ambientGroundColor = ambientBottom;

            RenderSettings.fog = fog;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;

            if (sky != null)
                RenderSettings.skybox = sky;

            if (camera != null)
            {
                camera.clearFlags = sky != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
                camera.backgroundColor = skyHorizon;
                if (camera.TryGetComponent<UniversalAdditionalCameraData>(out var data))
                {
                    // The game must stay attractive without post: Low turns it off completely.
                    data.renderPostProcessing = tier != QualityTier.Low;
                    data.renderShadows = tier != QualityTier.Low;
                }
            }

            PresentationQuality.Apply(tier);
            // Sky colors, clouds, height fog and wind globals for this single profile (no mood blending).
            var state = AtmosphereState.From(this);
            state.ApplyShaderGlobals(WorldQualityProfile.Default(tier));
        }
    }

    /// <summary>Presentation-wide quality switches read by pooled views when they are configured.</summary>
    public static class PresentationQuality
    {
        public static QualityTier Tier { get; private set; } = QualityTier.Medium;

        /// <summary>Blob shadows replace the shadow map on Low.</summary>
        public static bool BlobShadows { get; private set; }

        public static void Apply(QualityTier tier)
        {
            Tier = tier;
            BlobShadows = tier == QualityTier.Low;
        }
    }
}
