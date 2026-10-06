using PocketWilds.Core;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// Single owner of the target frame rate. 60 FPS standard, 30 FPS in battery saver, and 30 FPS for as long as the
    /// device is critically hot (T34 thermal protection). 90/120 stays opt-in for later, only when thermal measurements
    /// allow it.
    /// </summary>
    public sealed class FrameRateService
    {
        public const int StandardFps = 60;
        public const int BatterySaverFps = 30;

        private bool _batterySaver;
        private bool _thermalCap;

        public int TargetFps { get; private set; } = StandardFps;
        public bool BatterySaver => _batterySaver;
        public bool ThermalCap => _thermalCap;

        public void Apply(bool batterySaver)
        {
            _batterySaver = batterySaver;
            Refresh();
            GameLog.Info(LogCategory.Boot, "Target frame rate " + TargetFps);
        }

        /// <summary>Critical heat: 30 FPS until the device cools down (the player's battery choice is kept).</summary>
        public void SetThermalCap(bool capped)
        {
            if (capped == _thermalCap)
                return;
            _thermalCap = capped;
            Refresh();
            GameLog.Info(LogCategory.Presentation, capped ? "Device hot: 30 FPS until it cools" : "Device cool: frame rate restored");
        }

        private void Refresh()
        {
            // Frame pacing comes from targetFrameRate on mobile; VSync count must stay 0 for it to apply.
            QualitySettings.vSyncCount = 0;
            TargetFps = _batterySaver || _thermalCap ? BatterySaverFps : StandardFps;
            Application.targetFrameRate = TargetFps;
        }
    }
}
