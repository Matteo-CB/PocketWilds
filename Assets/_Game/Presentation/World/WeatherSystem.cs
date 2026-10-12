using System;
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
    /// T50: one kit per biome of the world strip (<see cref="AddBiome"/>). The first kit keeps the shower schedule; the
    /// camera's region (<see cref="SetRegion"/>) blends the drop count and the sun rays of two kits and picks the meshes of
    /// the one that dominates. A kit can bring its rays down to Équilibrée and keep them to some x ranges (the Forest).
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

        /// <summary>T50: the rays fade over this distance past each end of a kit's ray area (m).</summary>
        public const float RayAreaFade = 2f;

        private readonly WeatherKit _kit;
        private WeatherKit[] _kits;
        private RenderParams[] _kitRain;
        private RenderParams[] _kitRays;
        private RenderParams[] _kitRainbow;
        private int _from;
        private int _to;
        private float _blend;
        private QualityTier _tier = QualityTier.High;
        private readonly WorldAtmosphere _atmosphere;
        private Matrix4x4[] _drops;
        private readonly Matrix4x4[] _rays = new Matrix4x4[RayCount];
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

        /// <param name="tier">T50: the weather tier, for kits whose rays start below Magnifique (<see cref="WeatherKit.sunRaysFrom"/>).</param>
        public WeatherSystem(WeatherKit kit, WorldAtmosphere atmosphere, in WorldQualityProfile.Tier quality, QualityTier tier = QualityTier.High)
        {
            _kit = kit;
            _atmosphere = atmosphere;
            _drops = new Matrix4x4[0];
            _kits = new WeatherKit[0];
            _kitRain = new RenderParams[0];
            _kitRays = new RenderParams[0];
            _kitRainbow = new RenderParams[0];
            AddBiome(kit);
            SetQuality(quality, tier);
        }

        /// <summary>T50: kits (biomes) known; the constructor's is the first.</summary>
        public int BiomeCount => _kits.Length;

        /// <summary>T50: the kit of a biome (null when it has none: the first kit's look stands in).</summary>
        public WeatherKit KitOf(int biome) => biome >= 0 && biome < _kits.Length ? _kits[biome] : null;

        /// <summary>T50: the share of the sun rays drawn by the last <see cref="Render"/> (0 none to 1 full).</summary>
        public float LastRayAlpha { get; private set; }

        /// <summary>
        /// T50: the weather look of another biome of the strip; returns its index. A null kit borrows the first kit's look.
        /// Allocates (the drops grow to the largest kit): call at scene start.
        /// </summary>
        public int AddBiome(WeatherKit kit)
        {
            int index = _kits.Length;
            Array.Resize(ref _kits, index + 1);
            Array.Resize(ref _kitRain, index + 1);
            Array.Resize(ref _kitRays, index + 1);
            Array.Resize(ref _kitRainbow, index + 1);
            _kits[index] = kit;
            if (kit == null)
                return index;
            _kitRain[index] = new RenderParams(kit.rainMaterial) { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false };
            _kitRays[index] = new RenderParams(kit.rayMaterial) { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false };
            _kitRainbow[index] = new RenderParams(kit.rainbowMaterial) { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false };
            int drops = _drops.Length;
            if (kit.maxDrops > drops)
            {
                Array.Resize(ref _drops, kit.maxDrops);
                for (int i = drops; i < _drops.Length; i++)
                    _drops[i] = Matrix4x4.Translate(new Vector3(Next(), Next(), Next()));
            }
            return index;
        }

        /// <summary>T50: the camera's place on the strip (BiomeRegions.Resolve). No allocation.</summary>
        public void SetRegion(int from, int to, float t)
        {
            int last = Math.Max(0, _kits.Length - 1);
            _from = Mathf.Clamp(from, 0, last);
            _to = Mathf.Clamp(to, 0, last);
            _blend = _from == _to ? 0f : Mathf.Clamp01(t);
        }

        public int RegionFrom => _from;
        public int RegionTo => _to;
        public float RegionBlend => _blend;

        /// <summary>
        /// T50: share of a kit's rays at a world x: 1 inside one of its areas (or everywhere without areas), fading to 0 over
        /// <see cref="RayAreaFade"/> past each end. No allocation.
        /// </summary>
        public static float RayAreaWeight(Vector2[] areas, float x)
        {
            if (areas == null || areas.Length == 0)
                return 1f;
            float best = 0f;
            for (int i = 0; i < areas.Length; i++)
            {
                float min = Mathf.Min(areas[i].x, areas[i].y), max = Mathf.Max(areas[i].x, areas[i].y);
                float outside = x < min ? min - x : x > max ? x - max : 0f;
                best = Mathf.Max(best, 1f - Mathf.SmoothStep(0f, 1f, outside / RayAreaFade));
            }
            return best;
        }

        /// <summary>T50: whether a kit shows rays at this tier (DEF_WorldQuality's switch, or the kit's own lower tier).</summary>
        public static bool RaysOn(WeatherKit kit, bool qualityRays, QualityTier tier) =>
            kit != null && kit.rayMesh != null && kit.rayMaterial != null
            && (qualityRays || (kit.sunRaysFrom < QualityTier.High && tier >= kit.sunRaysFrom));

        /// <summary>T50: rays of one biome's kit at x (0, or the area fade; a biome without a kit uses the first kit's rays).</summary>
        public float RayShare(int biome, float x)
        {
            var kit = KitOrFirst(biome);
            return RaysOn(kit, _sunRays, _tier) ? RayAreaWeight(kit.sunRayAreas, x) : 0f;
        }

        private WeatherKit KitOrFirst(int biome) => biome >= 0 && biome < _kits.Length && _kits[biome] != null ? _kits[biome] : _kit;

        /// <summary>The kit whose meshes stand for the region now: the dominant one, the first kit when it has none.</summary>
        private int LookKit()
        {
            int k = _blend >= 0.5f ? _to : _from;
            return k < _kits.Length && _kits[k] != null ? k : 0;
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

        /// <summary>T50: the tier as well, for kits whose rays start below Magnifique.</summary>
        public void SetQuality(in WorldQualityProfile.Tier quality, QualityTier tier)
        {
            SetQuality(quality);
            _tier = tier;
        }

        public QualityTier Tier => _tier;

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
            LastRayAlpha = 0f;
            if (_kit == null || camera == null)
                return;
            int look = LookKit();
            var kit = _kits[look];
            // T50: the drop count follows the region; the meshes are the dominant kit's.
            int share = Mathf.Min(_drops.Length, Mathf.RoundToInt(Mathf.Lerp(KitOrFirst(_from).maxDrops, KitOrFirst(_to).maxDrops, _blend)));
            int drops = Mathf.CeilToInt(share * _intensity * Rain - 0.0001f);
            if (drops > 0 && kit.rainMesh != null && kit.rainMaterial != null)
            {
                // The rain box sits between the focus and the camera: drops pass in front of the scene, where they read.
                var box = kit.rainBox;
                var center = Vector3.Lerp(focus, camera.transform.position, 0.3f);
                center.y = focus.y + box.y * 0.35f;
                Shader.SetGlobalVector(RainBoxId, new Vector4(center.x, center.y, center.z, box.y));
                Shader.SetGlobalVector(RainSizeId, new Vector4(box.x, 0f, box.z, 0f));
                Shader.SetGlobalFloat(RainAlphaId, Mathf.SmoothStep(0f, 1f, Rain));
                ref var rain = ref _kitRain[look];
                rain.worldBounds = new Bounds(center, box + Vector3.one);
                rain.camera = camera;
                Graphics.RenderMeshInstanced(rain, kit.rainMesh, 0, _drops, drops);
                LastDrops = drops;
                LastDraws++;
            }
            if (Rainbow > 0.001f && kit.rainbowMesh != null && kit.rainbowMaterial != null)
            {
                // Opposite the sun, pulled toward where the camera looks so the arc stays in view; facing the camera.
                var antiSolar = RenderSettings.sun != null ? RenderSettings.sun.transform.forward : Vector3.forward;
                antiSolar.y = 0f;
                var view = camera.transform.forward;
                view.y = 0f;
                if (antiSolar.sqrMagnitude < 1e-4f)
                    antiSolar = Vector3.forward;
                if (view.sqrMagnitude < 1e-4f)
                    view = antiSolar;
                var toward = Vector3.Slerp(view.normalized, antiSolar.normalized, 0.3f).normalized;
                var center = focus + toward * kit.rainbowDistance + Vector3.down * (kit.rainbowRadius * 0.18f);
                var facing = center - camera.transform.position;
                facing.y = 0f;
                var matrix = Matrix4x4.TRS(center, Quaternion.LookRotation(facing.sqrMagnitude > 1e-4f ? facing : toward, Vector3.up),
                    Vector3.one * kit.rainbowRadius);
                Shader.SetGlobalFloat(RainbowAlphaId, Rainbow);
                ref var rainbow = ref _kitRainbow[look];
                rainbow.worldBounds = new Bounds(center, Vector3.one * (kit.rainbowRadius * 2.2f));
                rainbow.camera = camera;
                Graphics.RenderMesh(rainbow, kit.rainbowMesh, 0, matrix);
                LastDraws++;
            }
            bool night = _atmosphere != null && _atmosphere.Night;
            if (night || Rain >= 0.1f)
                return;
            // T50: each side of the region shows its rays where its kit wants them; the meshes come from the side showing more.
            float fromRays = RayShare(_from, focus.x);
            float toRays = _to != _from ? RayShare(_to, focus.x) : fromRays;
            float alpha = Mathf.Lerp(fromRays, toRays, _blend);
            int rayKit = toRays * _blend > fromRays * (1f - _blend) ? _to : _from;
            if (_kits[rayKit] == null)
                rayKit = 0;
            var rays = _kits[rayKit];
            if (alpha <= 0.001f || rays == null || rays.rayMesh == null || rays.rayMaterial == null)
                return;
            // A few long soft rays standing in the far half of the frame, slanted toward the sun.
            for (int i = 0; i < RayCount; i++)
            {
                float x = (i - (RayCount - 1) * 0.5f) * 3.1f + Mathf.Sin(i * 2.3f) * 0.8f;
                float z = rays.rayDepth + (i % 2) * 1.8f;
                var foot = focus + new Vector3(x, -0.4f, z);
                _rays[i] = Matrix4x4.TRS(foot, Quaternion.identity, new Vector3(2.2f + (i % 3) * 0.7f, 8f + (i % 2) * 2.5f, 1f));
            }
            Shader.SetGlobalFloat(SunRaysAlphaId, (1f - Rain * 10f) * alpha);
            ref var rayParams = ref _kitRays[rayKit];
            rayParams.worldBounds = new Bounds(focus + Vector3.up * 5f, new Vector3(30f, 24f, 30f));
            rayParams.camera = camera;
            Graphics.RenderMeshInstanced(rayParams, rays.rayMesh, 0, _rays, RayCount);
            LastRayAlpha = alpha;
            LastDraws++;
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
