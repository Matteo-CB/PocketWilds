using System;
using PocketWilds.Core;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>The device's heat, read from the platform (adaptive quality acts on it before the frame rate drops).</summary>
    public interface IThermalSource
    {
        ThermalLevel Read();
    }

    /// <summary>Platforms without a thermal API (PC, editor): never acts.</summary>
    public sealed class NoThermalSource : IThermalSource
    {
        public ThermalLevel Read() => ThermalLevel.Unknown;
    }

    /// <summary>Tests and benchmarks: the level is set by hand.</summary>
    public sealed class ScriptedThermalSource : IThermalSource
    {
        public ThermalLevel Level = ThermalLevel.Nominal;
        public ThermalLevel Read() => Level;
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    /// <summary>
    /// Android PowerManager.getCurrentThermalStatus (API 29+): 0 none, 1 light, 2 moderate, 3 severe, 4 critical,
    /// 5 emergency, 6 shutdown. Read every few seconds by the quality service, never per frame.
    /// </summary>
    public sealed class AndroidThermalSource : IThermalSource, IDisposable
    {
        private AndroidJavaObject _power;

        public AndroidThermalSource()
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                _power = activity.Call<AndroidJavaObject>("getSystemService", "power");
        }

        public ThermalLevel Read()
        {
            if (_power == null)
                return ThermalLevel.Unknown;
            int status = _power.Call<int>("getCurrentThermalStatus");
            return ThermalSources.FromAndroidStatus(status);
        }

        public void Dispose()
        {
            _power?.Dispose();
            _power = null;
        }
    }
#endif

    public static class ThermalSources
    {
        /// <summary>The platform's source, or <see cref="NoThermalSource"/> when there is none (or it fails).</summary>
        public static IThermalSource ForPlatform()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    if (version.GetStatic<int>("SDK_INT") >= 29)
                        return new AndroidThermalSource();
                }
            }
            catch (Exception ex)
            {
                GameLog.Warning(LogCategory.Boot, "Thermal status unavailable: " + ex.Message);
            }
#endif
            return new NoThermalSource();
        }

        /// <summary>Android thermal status to the levels adaptive quality uses.</summary>
        public static ThermalLevel FromAndroidStatus(int status) => status switch
        {
            <= 1 => ThermalLevel.Nominal,
            2 => ThermalLevel.Fair,
            3 => ThermalLevel.Serious,
            _ => ThermalLevel.Critical,
        };
    }
}
