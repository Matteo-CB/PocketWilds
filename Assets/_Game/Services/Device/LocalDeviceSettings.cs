#nullable enable
using PocketWilds.Core;
using UnityEngine;

namespace PocketWilds.Services.Device
{
    /// <summary>
    /// Settings that belong to this phone, not to the profile: they never sync to the cloud.
    /// </summary>
    public sealed class LocalDeviceSettings
    {
        /// <summary>Null means "pick automatically for this device".</summary>
        public QualityTier? QualityOverride;

        /// <summary>30 FPS, lighter rendering and slower secondary ticks.</summary>
        public bool BatterySaver;
    }

    /// <summary>PlayerPrefs storage. Main thread only; read at boot, written when the player changes a setting.</summary>
    public static class LocalDeviceSettingsStore
    {
        private const string QualityKey = "pw.device.quality";
        private const string BatteryKey = "pw.device.battery";

        public static LocalDeviceSettings Load()
        {
            var settings = new LocalDeviceSettings();
            int quality = PlayerPrefs.GetInt(QualityKey, -1);
            if (quality >= (int)QualityTier.Low && quality <= (int)QualityTier.High)
                settings.QualityOverride = (QualityTier)quality;
            settings.BatterySaver = PlayerPrefs.GetInt(BatteryKey, 0) == 1;
            return settings;
        }

        public static void Save(LocalDeviceSettings settings)
        {
            PlayerPrefs.SetInt(QualityKey, settings.QualityOverride.HasValue ? (int)settings.QualityOverride.Value : -1);
            PlayerPrefs.SetInt(BatteryKey, settings.BatterySaver ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
