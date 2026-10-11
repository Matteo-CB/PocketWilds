using PocketWilds.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// Weather (doc 07 "Météo et moments de la journée", doc 04 Grassland: light rain, rainbow) and the soft sun rays
    /// of Magnifique. Now and then a light shower rolls in, the atmosphere greys and cools (WorldAtmosphere.SetRain),
    /// streaks fall around the camera focus, then a rainbow shows in daylight. Everything is drawn from a few meshes:
    /// the rain is one instanced draw whose drops move in the vertex shader. No GameObject, no allocation per frame.
    /// The tier scales the number of drops (Fluide -60 %, Équilibrée -30 %).
    /// </summary>
    public sealed class WeatherSystem
    {
        private static readonly int RainBoxId = Shader.PropertyToID("_PW_RainBox");
        private static readonly int RainSizeId = Shader.PropertyToID("_PW_RainSize");
        private static readonly int RainAlphaId = Shader.PropertyToID("_PW_RainAlpha");
        private static readonly int RainbowAlphaId = Shader.PropertyToID("_PW_RainbowAlpha");
        private static readonly int SunRaysAlphaId = Shader.PropertyToID("_PW_SunRaysAlpha");

        public enum Phase : byte
        {
            Clear,
            RainIn,
            Raining,
            RainOut,
            Rainbow,
        }

        private const int RayCount = 5;

        private readonly WeatherKit _kit;
        private readonly WorldAtmosphere _atmosphere;
        private readonly Matrix4x4[] _drops;
        private readonly Matrix4x4[] _rays = new Matrix4x4[RayCount];
        private RenderParams _rainParams;
        private RenderParams _rayParams;
        private RenderParams _rainbowParams;
        private float _intensity = 1f;
        private bool _sunRays;
        private Phase _phase = Phase.Clear;
        private float _phaseStart;
        private float _phaseLength;
        private float _nextShower = -1f;
        private uint _random = 0x6A09E667u;
        private float _time;
        private float _showerFrequency = 1f;
        private bool _rainAllowed = true;
        private bool _rainbowAllowed = true;

        public WeatherSystem(WeatherKit kit, WorldAtmosphere atmosphere, in WorldQualityProfile.Tier quality)
        {
            _kit = kit;
            _atmosphere = atmosphere;
            int drops = kit != null ? kit.maxDrops : 0;
            _drops = new Matrix4x4[drops];
            for (int i = 0; i < drops; i++)
                _drops[i] = Matrix4x4.Translate(new Vector3(Next(), Next(), Next()));
            if (kit != null)
            {
                _rainParams = new RenderParams(kit.rainMaterial) { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false };
                _rayParams = new RenderParams(kit.rayMaterial) { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false };
                _rainbowParams = new RenderParams(kit.rainbowMaterial) { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false };
            }
            SetQuality(quality);
        }

        public Phase Current => _phase;
        /// <summary>0 dry to 1 full shower.</summary>
        public float Rain { get; private set; }
        /// <summary>0 hidden to 1 full rainbow.</summary>
        public float Rainbow { get; private set; }
        /// <summary>Drops submitted by the last <see cref="Render"/>.</summary>
        public int LastDrops { get; private set; }
        public int LastDraws { get; private set; }

        /// <summary>Share of the drops drawn (tier: 40 / 70 / 100 %).</summary>
        public float Intensity => _intensity;
        public bool SunRays => _sunRays;

        public void SetQuality(in WorldQualityProfile.Tier quality)
        {
            _intensity = Mathf.Clamp01(quality.weather);
            _sunRays = quality.sunRays;
        }

        public float ShowerFrequency => _showerFrequency;
        public bool RainAllowed => _rainAllowed;
        public bool RainbowAllowed => _rainbowAllowed;

        /// <summary>
        /// T48B season rules: showers come <paramref name="showerFrequency"/> times as often (Éveil 1.8); Givre has neither rain nor
        /// rainbow (a shower running when they stop ends at once, the light dries with it).
        /// </summary>
        public void SetSeasonRules(float showerFrequency, bool rain, bool rainbow)
        {
            showerFrequency = Mathf.Clamp(showerFrequency, 0.25f, 4f);
            if (_phase == Phase.Clear && _nextShower >= 0f && !Mathf.Approximately(showerFrequency, _showerFrequency))
                _nextShower = _time + Mathf.Max(0f, _nextShower - _time) * _showerFrequency / showerFrequency;
            _showerFrequency = showerFrequency;
            _rainAllowed = rain;
            _rainbowAllowed = rainbow;
            bool stop = (!rain && _phase != Phase.Clear && _phase != Phase.Rainbow) || (!rainbow && _phase == Phase.Rainbow);
            if (!stop || _kit == null)
                return;
            Rain = 0f;
            Rainbow = 0f;
            EndShower(_time);
            _atmosphere?.SetRain(0f);
        }

        /// <summary>Starts a shower now (events, captures, tests). Its length is drawn from the kit range.</summary>
        public void StartShower(float time)
        {
            if (_kit == null || !_rainAllowed)
                return;
            Enter(Phase.RainIn, time, _kit.rampSeconds);
        }

        /// <summary>Freezes a look for captures: rain 0-1 and rainbow 0-1, no schedule.</summary>
        public void Force(float rain, float rainbow)
        {
            _phase = rain > 0f ? Phase.Raining : rainbow > 0f ? Phase.Rainbow : Phase.Clear;
            _phaseLength = float.PositiveInfinity;
            Rain = Mathf.Clamp01(rain);
            Rainbow = Mathf.Clamp01(rainbow);
            _atmosphere?.SetRain(Rain);
        }

        public void Tick(float time)
        {
            if (_kit == null)
                return;
            _time = time;
            if (_nextShower < 0f)
                _nextShower = time + Mathf.Lerp(_kit.showerEveryMinutes.x, _kit.showerEveryMinutes.y, Next()) * 60f / _showerFrequency;
            float t = _phaseLength > 0f && !float.IsPositiveInfinity(_phaseLength) ? Mathf.Clamp01((time - _phaseStart) / _phaseLength) : 0f;
            switch (_phase)
            {
                case Phase.Clear:
                    Rain = 0f;
                    Rainbow = 0f;
                    if (time >= _nextShower && _rainAllowed)
                        Enter(Phase.RainIn, time, _kit.rampSeconds);
                    break;
                case Phase.RainIn:
                    Rain = Mathf.SmoothStep(0f, 1f, t);
                    if (t >= 1f)
                        Enter(Phase.Raining, time, Mathf.Lerp(_kit.showerSeconds.x, _kit.showerSeconds.y, Next()));
                    break;
                case Phase.Raining:
                    Rain = 1f;
                    if (t >= 1f)
                        Enter(Phase.RainOut, time, _kit.rampSeconds);
                    break;
                case Phase.RainOut:
                    Rain = 1f - Mathf.SmoothStep(0f, 1f, t);
                    if (t >= 1f)
                    {
                        Rain = 0f;
                        bool daylight = _atmosphere == null || (!_atmosphere.Night && _atmosphere.Hour > 7.5f && _atmosphere.Hour < 18.5f);
                        if (daylight && _rainbowAllowed)
                            Enter(Phase.Rainbow, time, _kit.rainbowSeconds);
                        else
                            EndShower(time);
                    }
                    break;
                case Phase.Rainbow:
                    Rainbow = Mathf.SmoothStep(0f, 1f, Mathf.Min(t * 6f, 1f)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Max(0f, t * 4f - 3f)));
                    if (t >= 1f)
                    {
                        Rainbow = 0f;
                        EndShower(time);
                    }
                    break;
            }
            _atmosphere?.SetRain(Rain);
        }

        /// <summary>Draws the rain around the focus, the rainbow and the sun rays. No allocation.</summary>
        public void Render(Camera camera, Vector3 focus)
        {
            LastDrops = 0;
            LastDraws = 0;
            if (_kit == null || camera == null)
                return;
            int drops = Mathf.CeilToInt(_drops.Length * _intensity * Rain - 0.0001f);
            if (drops > 0 && _kit.rainMesh != null && _kit.rainMaterial != null)
            {
                // The rain box sits between the focus and the camera: drops pass in front of the scene, where they read.
                var box = _kit.rainBox;
                var center = Vector3.Lerp(focus, camera.transform.position, 0.3f);
                center.y = focus.y + box.y * 0.35f;
                Shader.SetGlobalVector(RainBoxId, new Vector4(center.x, center.y, center.z, box.y));
                Shader.SetGlobalVector(RainSizeId, new Vector4(box.x, 0f, box.z, 0f));
                Shader.SetGlobalFloat(RainAlphaId, Mathf.SmoothStep(0f, 1f, Rain));
                _rainParams.worldBounds = new Bounds(center, box + Vector3.one);
                _rainParams.camera = camera;
                Graphics.RenderMeshInstanced(_rainParams, _kit.rainMesh, 0, _drops, drops);
                LastDrops = drops;
                LastDraws++;
            }
            if (Rainbow > 0.001f && _kit.rainbowMesh != null && _kit.rainbowMaterial != null)
            {
                // Opposite the sun, pulled toward where the camera looks so the arc stays in view; facing the camera.
                var antiSolar = RenderSettings.sun != null ? RenderSettings.sun.transform.forward : Vector3.forward;
                antiSolar.y = 0f;
                var look = camera.transform.forward;
                look.y = 0f;
                if (antiSolar.sqrMagnitude < 1e-4f)
                    antiSolar = Vector3.forward;
                if (look.sqrMagnitude < 1e-4f)
                    look = antiSolar;
                var toward = Vector3.Slerp(look.normalized, antiSolar.normalized, 0.3f).normalized;
                var center = focus + toward * _kit.rainbowDistance + Vector3.down * (_kit.rainbowRadius * 0.18f);
                var facing = center - camera.transform.position;
                facing.y = 0f;
                var matrix = Matrix4x4.TRS(center, Quaternion.LookRotation(facing.sqrMagnitude > 1e-4f ? facing : toward, Vector3.up),
                    Vector3.one * _kit.rainbowRadius);
                Shader.SetGlobalFloat(RainbowAlphaId, Rainbow);
                _rainbowParams.worldBounds = new Bounds(center, Vector3.one * (_kit.rainbowRadius * 2.2f));
                _rainbowParams.camera = camera;
                Graphics.RenderMesh(_rainbowParams, _kit.rainbowMesh, 0, matrix);
                LastDraws++;
            }
            bool night = _atmosphere != null && _atmosphere.Night;
            if (_sunRays && !night && Rain < 0.1f && _kit.rayMesh != null && _kit.rayMaterial != null)
            {
                // A few long soft rays standing in the far half of the frame, slanted toward the sun.
                for (int i = 0; i < RayCount; i++)
                {
                    float x = (i - (RayCount - 1) * 0.5f) * 3.1f + Mathf.Sin(i * 2.3f) * 0.8f;
                    float z = 3.5f + (i % 2) * 1.8f;
                    var foot = focus + new Vector3(x, -0.4f, z);
                    _rays[i] = Matrix4x4.TRS(foot, Quaternion.identity, new Vector3(2.2f + (i % 3) * 0.7f, 8f + (i % 2) * 2.5f, 1f));
                }
                Shader.SetGlobalFloat(SunRaysAlphaId, 1f - Rain * 10f);
                _rayParams.worldBounds = new Bounds(focus + Vector3.up * 5f, new Vector3(30f, 24f, 30f));
                _rayParams.camera = camera;
                Graphics.RenderMeshInstanced(_rayParams, _kit.rayMesh, 0, _rays, RayCount);
                LastDraws++;
            }
        }

        private void Enter(Phase phase, float time, float length)
        {
            _phase = phase;
            _phaseStart = time;
            _phaseLength = Mathf.Max(0.01f, length);
        }

        private void EndShower(float time)
        {
            _phase = Phase.Clear;
            _phaseLength = 0f;
            _nextShower = time + Mathf.Lerp(_kit.showerEveryMinutes.x, _kit.showerEveryMinutes.y, Next()) * 60f / _showerFrequency;
        }

        private float Next()
        {
            _random ^= _random << 13;
            _random ^= _random >> 17;
            _random ^= _random << 5;
            return (_random & 0xFFFFFFu) / 16777216f;
        }
    }
}
