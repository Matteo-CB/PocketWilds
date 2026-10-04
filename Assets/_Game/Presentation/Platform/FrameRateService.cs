using PocketWilds.Core;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// Single owner of the target frame rate. 60 FPS standard, 30 FPS in battery saver.
    /// 90/120 stays opt-in for later, only when thermal measurements allow it.
    /// </summary>
    public sealed class FrameRateService
    {
        public const int StandardFps = 60;
        public const int BatterySaverFps = 30;

        public int TargetFps { get; private set; }

        public void Apply(bool batterySaver)
        {
            // Frame pacing comes from targetFrameRate on mobile; VSync count must stay 0 for it to apply.
            QualitySettings.vSyncCount = 0;
            TargetFps = batterySaver ? BatterySaverFps : StandardFps;
            Application.targetFrameRate = TargetFps;
            GameLog.Info(LogCategory.Boot, "Target frame rate " + TargetFps);
        }
    }
}
