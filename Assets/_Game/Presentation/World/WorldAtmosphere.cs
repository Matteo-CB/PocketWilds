using System;
using PocketWilds.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace PocketWilds.Presentation
{
    public enum LightingMood : byte
    {
        Morning,
        Day,
        Evening,
    }

    /// <summary>
    /// Everything a lighting mood drives, as plain values so two profiles can be blended without allocating:
    /// sun, trilight ambient, linear fog, sky and clouds (shader globals), height fog and wind (shader globals).
    /// </summary>
    public struct AtmosphereState
    {
        private static readonly int WindId = Shader.PropertyToID("_PW_Wind");
        private static readonly int WindWavesId = Shader.PropertyToID("_PW_WindWaves");
        private static readonly int HeightFogId = Shader.PropertyToID("_PW_HeightFog");
        private static readonly int HeightFogRangeId = Shader.PropertyToID("_PW_HeightFogRange");
        private static readonly int SkyBlendId = Shader.PropertyToID("_PW_SkyBlend");
        private static readonly int SkyTopId = Shader.PropertyToID("_PW_SkyTop");
        private static readonly int SkyHorizonId = Shader.PropertyToID("_PW_SkyHorizon");
        private static readonly int SkyBottomId = Shader.PropertyToID("_PW_SkyBottom");
        private static readonly int SunTintId = Shader.PropertyToID("_PW_SunTint");
        private static readonly int SunDirectionId = Shader.PropertyToID("_PW_SunDirection");
        private static readonly int CloudLightId = Shader.PropertyToID("_PW_CloudLight");
        private static readonly int CloudShadeId = Shader.PropertyToID("_PW_CloudShade");
        private static readonly int SkyParamsId = Shader.PropertyToID("_PW_SkyParams");

        public Color SunColor;
        public float SunIntensity;
        public Vector2 SunAngles;
        public float ShadowStrength;
        public Color AmbientTop, AmbientEquator, AmbientBottom;
        public Color SkyTop, SkyHorizon, SkyBottom;
        public Color FogColor;
        public float FogStart, FogEnd;
        public Color SunGlowTint;
        public float SunGlow;
        public Color CloudLight, CloudShade;
        public float CloudCoverage;
        public float HeightFogStrength, HeightFogBase, HeightFogFalloff, HeightFogStart, HeightFogRange;
        public float WindStrength, WindYaw, WindSpeed, WindWaveFrequency, GustStrength, GustFrequency, Flutter;

        public static AtmosphereState From(LightingProfile p) => new AtmosphereState
        {
            SunColor = p.sunColor,
            SunIntensity = p.sunIntensity,
            SunAngles = p.sunAngles,
            ShadowStrength = p.shadowStrength,
            AmbientTop = p.ambientTop,
            AmbientEquator = p.ambientEquator,
            AmbientBottom = p.ambientBottom,
            SkyTop = p.skyTop,
            SkyHorizon = p.skyHorizon,
            SkyBottom = p.skyBottom,
            FogColor = p.fogColor,
            FogStart = p.fogStart,
            FogEnd = p.fogEnd,
            SunGlowTint = p.sunGlowTint,
            SunGlow = p.sunGlow,
            CloudLight = p.cloudLight,
            CloudShade = p.cloudShade,
            CloudCoverage = p.cloudCoverage,
            HeightFogStrength = p.heightFogStrength,
            HeightFogBase = p.heightFogBase,
            HeightFogFalloff = p.heightFogFalloff,
            HeightFogStart = p.heightFogStart,
            HeightFogRange = p.heightFogRange,
            WindStrength = p.windStrength,
            WindYaw = p.windYaw,
            WindSpeed = p.windSpeed,
            WindWaveFrequency = p.windWaveFrequency,
            GustStrength = p.gustStrength,
            GustFrequency = p.gustFrequency,
            Flutter = p.flutter,
        };

        public static AtmosphereState Lerp(in AtmosphereState a, in AtmosphereState b, float t) => new AtmosphereState
        {
            SunColor = Color.Lerp(a.SunColor, b.SunColor, t),
            SunIntensity = Mathf.Lerp(a.SunIntensity, b.SunIntensity, t),
            SunAngles = new Vector2(Mathf.Lerp(a.SunAngles.x, b.SunAngles.x, t), Mathf.LerpAngle(a.SunAngles.y, b.SunAngles.y, t)),
            ShadowStrength = Mathf.Lerp(a.ShadowStrength, b.ShadowStrength, t),
            AmbientTop = Color.Lerp(a.AmbientTop, b.AmbientTop, t),
            AmbientEquator = Color.Lerp(a.AmbientEquator, b.AmbientEquator, t),
            AmbientBottom = Color.Lerp(a.AmbientBottom, b.AmbientBottom, t),
            SkyTop = Color.Lerp(a.SkyTop, b.SkyTop, t),
            SkyHorizon = Color.Lerp(a.SkyHorizon, b.SkyHorizon, t),
            SkyBottom = Color.Lerp(a.SkyBottom, b.SkyBottom, t),
            FogColor = Color.Lerp(a.FogColor, b.FogColor, t),
            FogStart = Mathf.Lerp(a.FogStart, b.FogStart, t),
            FogEnd = Mathf.Lerp(a.FogEnd, b.FogEnd, t),
            SunGlowTint = Color.Lerp(a.SunGlowTint, b.SunGlowTint, t),
            SunGlow = Mathf.Lerp(a.SunGlow, b.SunGlow, t),
            CloudLight = Color.Lerp(a.CloudLight, b.CloudLight, t),
            CloudShade = Color.Lerp(a.CloudShade, b.CloudShade, t),
            CloudCoverage = Mathf.Lerp(a.CloudCoverage, b.CloudCoverage, t),
            HeightFogStrength = Mathf.Lerp(a.HeightFogStrength, b.HeightFogStrength, t),
            HeightFogBase = Mathf.Lerp(a.HeightFogBase, b.HeightFogBase, t),
            HeightFogFalloff = Mathf.Lerp(a.HeightFogFalloff, b.HeightFogFalloff, t),
            HeightFogStart = Mathf.Lerp(a.HeightFogStart, b.HeightFogStart, t),
            HeightFogRange = Mathf.Lerp(a.HeightFogRange, b.HeightFogRange, t),
            WindStrength = Mathf.Lerp(a.WindStrength, b.WindStrength, t),
            WindYaw = Mathf.LerpAngle(a.WindYaw, b.WindYaw, t),
            WindSpeed = Mathf.Lerp(a.WindSpeed, b.WindSpeed, t),
            WindWaveFrequency = Mathf.Lerp(a.WindWaveFrequency, b.WindWaveFrequency, t),
            GustStrength = Mathf.Lerp(a.GustStrength, b.GustStrength, t),
            GustFrequency = Mathf.Lerp(a.GustFrequency, b.GustFrequency, t),
            Flutter = Mathf.Lerp(a.Flutter, b.Flutter, t),
        };

        /// <summary>Sun, ambient and fog of the scene. The shadow switch stays the tier's (LightingProfile.Apply).</summary>
        public void ApplyScene(Light sun, Camera camera)
        {
            if (sun != null)
            {
                sun.color = SunColor;
                sun.intensity = SunIntensity;
                sun.transform.rotation = Quaternion.Euler(SunAngles.x, SunAngles.y, 0f);
                sun.shadowStrength = ShadowStrength;
            }
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = AmbientTop;
            RenderSettings.ambientEquatorColor = AmbientEquator;
            RenderSettings.ambientGroundColor = AmbientBottom;
            RenderSettings.fogColor = FogColor;
            RenderSettings.fogStartDistance = FogStart;
            RenderSettings.fogEndDistance = FogEnd;
            if (camera != null)
                camera.backgroundColor = SkyHorizon;
        }

        /// <summary>Sky, clouds, height fog and wind globals, scaled by the tier (Fluide: no clouds, no height fog).</summary>
        public void ApplyShaderGlobals(in WorldQualityProfile.Tier quality)
        {
            float yaw = WindYaw * Mathf.Deg2Rad;
            Shader.SetGlobalVector(WindId, new Vector4(Mathf.Sin(yaw), Mathf.Cos(yaw), WindStrength, WindSpeed));
            Shader.SetGlobalVector(WindWavesId, new Vector4(WindWaveFrequency, GustStrength, GustFrequency, Flutter));
            Shader.SetGlobalVector(HeightFogId, new Vector4(HeightFogBase, HeightFogFalloff, HeightFogStrength * quality.heightFog, HeightFogStart));
            Shader.SetGlobalVector(HeightFogRangeId, new Vector4(HeightFogRange, 0f, 0f, 0f));

            var sunDirection = Quaternion.Euler(SunAngles.x, SunAngles.y, 0f) * Vector3.back;
            // Unlike Material.SetColor, global colors are not converted to the linear color space: do it here so
            // the moods match the sky material exactly.
            Shader.SetGlobalFloat(SkyBlendId, 1f);
            Shader.SetGlobalColor(SkyTopId, SkyTop.linear);
            Shader.SetGlobalColor(SkyHorizonId, SkyHorizon.linear);
            Shader.SetGlobalColor(SkyBottomId, SkyBottom.linear);
            Shader.SetGlobalColor(SunTintId, SunGlowTint.linear);
            Shader.SetGlobalVector(SunDirectionId, new Vector4(sunDirection.x, sunDirection.y, sunDirection.z, 0f));
            Shader.SetGlobalColor(CloudLightId, CloudLight.linear);
            Shader.SetGlobalColor(CloudShadeId, CloudShade.linear);
            Shader.SetGlobalVector(SkyParamsId, new Vector4(quality.clouds ? CloudCoverage : 0f, SunGlow, 0f, 0f));
        }
    }

    /// <summary>
    /// Lighting moods over the day (doc 07): morning, day and evening profiles blended from the device's local time,
    /// softened (no black night: late hours stay on a warm evening). Checked once per second, re-applied only when
    /// the clock moved a minute (a few RenderSettings writes and shader globals, no allocation).
    /// </summary>
    public sealed class WorldAtmosphere
    {
        // Hour keys of the softened day: (hour, mood). Between keys the two moods blend linearly.
        private static readonly float[] KeyHours = { 0f, 5f, 7f, 9.5f, 11.5f, 16f, 18.5f, 24f };
        private static readonly LightingMood[] KeyMoods =
        {
            LightingMood.Evening, LightingMood.Evening, LightingMood.Morning, LightingMood.Morning,
            LightingMood.Day, LightingMood.Day, LightingMood.Evening, LightingMood.Evening,
        };

        private readonly AtmosphereState[] _moods = new AtmosphereState[3];
        private readonly Light _sun;
        private readonly Camera _camera;
        private WorldQualityProfile.Tier _quality;
        private float _nextApply = float.NegativeInfinity;
        private float _forcedHour = -1f;
        private bool _dirty = true;

        public WorldAtmosphere(LightingProfile morning, LightingProfile day, LightingProfile evening, Light sun, Camera camera,
            in WorldQualityProfile.Tier quality)
        {
            if (day == null)
                throw new ArgumentNullException(nameof(day));
            _moods[(int)LightingMood.Morning] = AtmosphereState.From(morning != null ? morning : day);
            _moods[(int)LightingMood.Day] = AtmosphereState.From(day);
            _moods[(int)LightingMood.Evening] = AtmosphereState.From(evening != null ? evening : day);
            _sun = sun;
            _camera = camera;
            _quality = quality;
        }

        /// <summary>The hour used by the last apply (0-24).</summary>
        public float Hour { get; private set; }

        public void SetQuality(in WorldQualityProfile.Tier quality)
        {
            _quality = quality;
            _nextApply = float.NegativeInfinity;
            _dirty = true;
        }

        /// <summary>Fixes the hour (captures, tests, photo mode). A negative value returns to the device clock.</summary>
        public void ForceHour(float hour)
        {
            _forcedHour = hour;
            _nextApply = float.NegativeInfinity;
            _dirty = true;
        }

        public void ForceMood(LightingMood mood) => ForceHour(mood switch
        {
            LightingMood.Morning => 8f,
            LightingMood.Evening => 19.5f,
            _ => 13f,
        });

        public void Tick(float time)
        {
            if (time < _nextApply)
                return;
            _nextApply = time + 1f;
            float hour = _forcedHour >= 0f ? _forcedHour : LocalHour();
            // The light only moves with the clock: nothing is rewritten until a minute (0.25 degree of the day) has passed.
            if (!_dirty && Mathf.Abs(Mathf.DeltaAngle(hour * 15f, Hour * 15f)) < 0.25f)
                return;
            Apply(hour);
        }

        public void Apply(float hour)
        {
            using var marker = GameProfilerMarkers.AtmosphereApply.Auto();
            Hour = Mathf.Repeat(hour, 24f);
            Resolve(Hour, out var a, out var b, out float t);
            var state = AtmosphereState.Lerp(_moods[(int)a], _moods[(int)b], Mathf.SmoothStep(0f, 1f, t));
            state.ApplyScene(_sun, _camera);
            state.ApplyShaderGlobals(_quality);
            _dirty = false;
        }

        /// <summary>The two moods around an hour and the blend between them.</summary>
        public static void Resolve(float hour, out LightingMood from, out LightingMood to, out float t)
        {
            hour = Mathf.Repeat(hour, 24f);
            for (int i = 0; i < KeyHours.Length - 1; i++)
            {
                if (hour <= KeyHours[i + 1])
                {
                    from = KeyMoods[i];
                    to = KeyMoods[i + 1];
                    t = Mathf.InverseLerp(KeyHours[i], KeyHours[i + 1], hour);
                    return;
                }
            }
            from = to = LightingMood.Evening;
            t = 0f;
        }

        private static float LocalHour()
        {
            var now = DateTime.Now;
            return now.Hour + now.Minute / 60f;
        }
    }
}
