using PocketWilds.Core;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// Chooses the quality tier: player override first, otherwise a conservative guess from device memory.
    /// Adaptive downgrades from GPU time and temperature come later and will also go through here.
    /// </summary>
    public sealed class QualityService
    {
        public QualityTier CurrentTier { get; private set; } = QualityTier.Medium;

        public QualityTier Apply(QualityTier? playerOverride)
        {
            var tier = playerOverride ?? GuessTierForDevice();
            int level = Mathf.Clamp((int)tier, 0, QualitySettings.names.Length - 1);
            QualitySettings.SetQualityLevel(level, true);
            CurrentTier = (QualityTier)level;
            GameLog.Info(LogCategory.Boot, "Quality tier " + CurrentTier);
            return CurrentTier;
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
