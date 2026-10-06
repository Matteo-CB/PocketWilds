using System;
using PocketWilds.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// Owner of the picture's quality (T34, doc 07 « Beauté maximale » and the Performance Guide).
    /// <list type="bullet">
    /// <item>Mode: Automatique by default (the device's tier from its memory), or the tier the player picked.</item>
    /// <item>Adaptive quality: <see cref="AdaptiveQualityController"/> watches the frame's work time (FrameTimingManager:
    /// the longer of CPU main thread, render thread and GPU) and the device's heat, and lowers or raises one dimension
    /// at a time. The renderer part (quality level, shadow distance and cascades) is applied here; the world part
    /// (life, ground details, weather, sun shadows, blob shadows) by the scene, through <see cref="StateChanged"/>.</item>
    /// <item>Shadows below the renderer's tier are set on the active URP asset; the original values are restored
    /// before any level change and when the service stops, so no asset ever keeps a runtime value.</item>
    /// </list>
    /// <see cref="CurrentTier"/> stays the chosen tier: it sizes populations and pools, which never shrink first.
    /// No allocation per frame.
    /// </summary>
    public sealed class QualityService : IDisposable
    {
        /// <summary>Localization key of the HUD note shown while heat keeps the picture lighter.</summary>
        public const string ThermalNoticeKey = "quality.thermal_notice";

        /// <summary>The platform thermal API is read this often (it is a system call, not a per-frame value).</summary>
        public const float ThermalPollSeconds = 5f;

        private struct ShadowSettings
        {
            public float Distance;
            public int Cascades;
            public float Split2;
            public Vector3 Split4;
            public int Resolution;
        }

        private readonly FrameTiming[] _timing = new FrameTiming[1];
        private UniversalRenderPipelineAsset[] _assets = Array.Empty<UniversalRenderPipelineAsset>();
        private ShadowSettings[] _originalShadows = Array.Empty<ShadowSettings>();
        private int _modifiedLevel = -1;
        private AdaptiveQualityController _adaptive;
        private IThermalSource _thermalSource = new NoThermalSource();
        private FrameRateService _frameRate;
        private float _thermalPoll;
        private bool _frameCapped;
        private bool _adaptiveEnabled = true;

        public QualityService()
        {
            _adaptive = new AdaptiveQualityController(QualityTier.Medium, QualityMode.Automatic, AdaptiveQualitySettings.Default(60));
        }

        /// <summary>The tier the player chose, or the device's tier in automatic mode (sizes populations and pools).</summary>
        public QualityTier CurrentTier => _adaptive.Ceiling;
        public QualityTier DeviceTier => _adaptive.DeviceTier;
        public QualityMode Mode => _adaptive.Mode;
        public QualityState State => _adaptive.State;
        public ThermalLevel Thermal { get; private set; } = ThermalLevel.Unknown;

        /// <summary>Heat lowered the picture: the HUD shows a short note.</summary>
        public bool ThermalNotice => _adaptive.ThermalNotice;

        public AdaptiveQualityController Adaptive => _adaptive;

        /// <summary>Work time of the last frame (ms), as adaptive quality sees it.</summary>
        public float LastWorkMs { get; private set; }

        /// <summary>The state changed (adaptive step, heat, new mode): the scene applies the world part.</summary>
        public event Action<QualityState> StateChanged;

        /// <summary>
        /// Boot. <paramref name="fixedTier"/> (benchmarks, tests) wins, then the player's choice (null = automatic).
        /// </summary>
        public QualityTier Initialize(QualityTier? fixedTier, QualityTier? playerChoice, FrameRateService frameRate, IThermalSource thermal = null)
        {
            CaptureAssets();
            _frameRate = frameRate;
            _thermalSource = thermal ?? ThermalSources.ForPlatform();
            var chosen = fixedTier ?? playerChoice;
            var mode = chosen.HasValue ? (QualityMode)((int)chosen.Value + 1) : QualityMode.Automatic;
            var device = fixedTier ?? GuessTierForDevice();
            _adaptive = new AdaptiveQualityController(device, mode, AdaptiveQualitySettings.Default(frameRate != null ? frameRate.TargetFps : 60));
            ApplyRenderer(_adaptive.State);
            GameLog.Info(LogCategory.Boot, $"Quality {mode} (device {device}): {_adaptive.State}");
            return CurrentTier;
        }

        /// <summary>Legacy entry (tests): a fixed tier or automatic, no frame-rate service.</summary>
        public QualityTier Apply(QualityTier? playerOverride) => Initialize(playerOverride, null, null, new NoThermalSource());

        /// <summary>Benchmarks and tests: adaptive steps and heat off, the state stays where it is put.</summary>
        public void SetAdaptiveEnabled(bool enabled) => _adaptiveEnabled = enabled;

        /// <summary>Replaces the heat source (tests, benchmarks).</summary>
        public void SetThermalSource(IThermalSource source) => _thermalSource = source ?? new NoThermalSource();

        /// <summary>The player's choice from the settings: stored on this phone and applied now.</summary>
        public void SetMode(QualityMode mode)
        {
            _adaptive.SetMode(mode);
            var settings = Services.Device.LocalDeviceSettingsStore.Load();
            settings.QualityOverride = mode == QualityMode.Automatic ? (QualityTier?)null : (QualityTier)((int)mode - 1);
            Services.Device.LocalDeviceSettingsStore.Save(settings);
            Publish();
        }

        /// <summary>Battery saving changes the frame budget (60 or 30 FPS).</summary>
        public void SetFrameBudget(int targetFps) => _adaptive.SetBudget(1000f / Mathf.Max(1, targetFps));

        /// <summary>Benchmarks and tests: puts the picture in a given state now (renderer and world).</summary>
        public void ForceState(QualityState state)
        {
            _adaptive.Force(state);
            Publish();
        }

        /// <summary>One frame (game loop, unscaled time). No allocation.</summary>
        public void Tick(float unscaledDeltaTime)
        {
            if (unscaledDeltaTime <= 0f)
                return;
            _thermalPoll -= unscaledDeltaTime;
            if (_thermalPoll <= 0f)
            {
                _thermalPoll = ThermalPollSeconds;
                Thermal = _thermalSource.Read();
            }
            LastWorkMs = ReadWorkMs();
            var before = _adaptive.State;
            // Benchmarks and tests switch adaptive steps off: the state stays exactly where they put it.
            bool changed = _adaptiveEnabled && _adaptive.Tick(unscaledDeltaTime, LastWorkMs, Thermal);
            if (_adaptive.ThermalFrameCap != _frameCapped)
            {
                _frameCapped = _adaptive.ThermalFrameCap;
                _frameRate?.SetThermalCap(_frameCapped);
            }
            if (changed && !before.Equals(_adaptive.State))
            {
                GameLog.Info(LogCategory.Presentation, $"Quality now {_adaptive.State} (p95 {_adaptive.LastP95Ms:F1} ms, heat {Thermal})");
                Publish();
            }
        }

        private void Publish()
        {
            ApplyRenderer(_adaptive.State);
            StateChanged?.Invoke(_adaptive.State);
        }

        // ---------------- renderer ----------------

        private void CaptureAssets()
        {
            if (_assets.Length > 0)
                return;
            int levels = QualitySettings.names.Length;
            _assets = new UniversalRenderPipelineAsset[levels];
            _originalShadows = new ShadowSettings[levels];
            for (int i = 0; i < levels; i++)
            {
                _assets[i] = QualitySettings.GetRenderPipelineAssetAt(i) as UniversalRenderPipelineAsset;
                if (_assets[i] != null)
                    _originalShadows[i] = Read(_assets[i]);
            }
#if UNITY_EDITOR
            // Play mode must never leave a runtime shadow value in a project asset.
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeChanged;
#endif
        }

#if UNITY_EDITOR
        private void OnPlayModeChanged(UnityEditor.PlayModeStateChange change)
        {
            if (change == UnityEditor.PlayModeStateChange.ExitingPlayMode)
                RestoreAssets();
        }
#endif

        private void ApplyRenderer(QualityState state)
        {
            if (_assets.Length == 0)
                return;
            int level = Mathf.Clamp((int)state.Resolution, 0, _assets.Length - 1);
            if (QualitySettings.GetQualityLevel() != level)
            {
                RestoreAssets();
                QualitySettings.SetQualityLevel(level, true);
            }
            // Shadows a tier below the renderer: that tier's distance, cascades and map size on the active asset.
            // Shadows at Low need no asset change: the scene turns the sun's shadows off (blob shadows instead).
            int shadows = Mathf.Clamp((int)state.Shadows, 0, _assets.Length - 1);
            if (shadows < level && shadows > (int)QualityTier.Low && _assets[level] != null)
            {
                Write(_assets[level], _originalShadows[shadows]);
                _modifiedLevel = level;
            }
            else if (_modifiedLevel == level)
            {
                Write(_assets[level], _originalShadows[level]);
                _modifiedLevel = -1;
            }
        }

        /// <summary>Puts every URP asset back to its project values.</summary>
        public void RestoreAssets()
        {
            if (_modifiedLevel >= 0 && _modifiedLevel < _assets.Length && _assets[_modifiedLevel] != null)
                Write(_assets[_modifiedLevel], _originalShadows[_modifiedLevel]);
            _modifiedLevel = -1;
        }

        public void Dispose()
        {
            RestoreAssets();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeChanged;
#endif
            (_thermalSource as IDisposable)?.Dispose();
        }

        private static ShadowSettings Read(UniversalRenderPipelineAsset asset) => new ShadowSettings
        {
            Distance = asset.shadowDistance,
            Cascades = asset.shadowCascadeCount,
            Split2 = asset.cascade2Split,
            Split4 = asset.cascade4Split,
            Resolution = asset.mainLightShadowmapResolution,
        };

        private static void Write(UniversalRenderPipelineAsset asset, in ShadowSettings s)
        {
            asset.shadowDistance = s.Distance;
            asset.shadowCascadeCount = s.Cascades;
            asset.cascade2Split = s.Split2;
            asset.cascade4Split = s.Split4;
            asset.mainLightShadowmapResolution = s.Resolution;
        }

        /// <summary>
        /// The frame's work (ms): the longest of main thread, render thread and GPU (FrameTimingManager; the main thread
        /// time already excludes the wait for the target frame rate). 0 when unknown: the controller then uses the frame.
        /// </summary>
        private float ReadWorkMs()
        {
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, _timing) == 0)
                return 0f;
            var t = _timing[0];
            double work = Math.Max(t.cpuMainThreadFrameTime, Math.Max(t.cpuRenderThreadFrameTime, t.gpuFrameTime));
            // Now and then the timing manager returns a value far above the real frame (T23): the clock decides then.
            double wall = Time.unscaledDeltaTime * 1000.0;
            if (work > Math.Max(100.0, wall * 3.0))
                return 0f;
            return work > 0.0 ? (float)work : 0f;
        }

        /// <summary>Low tier for 3-4 GB devices and below, as in the Performance Test Matrix.</summary>
        public static QualityTier GuessTierForDevice()
        {
            if (Application.isEditor)
                return QualityTier.Medium;

            int memoryMb = SystemInfo.systemMemorySize;
            if (memoryMb > 0 && memoryMb <= 4096)
                return QualityTier.Low;
            if (memoryMb >= 8192 && SystemInfo.graphicsMemorySize >= 2048)
                return QualityTier.High;
            return QualityTier.Medium;
        }
    }
}
