#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using PocketWilds.Core;
using UnityEngine;

namespace PocketWilds.Services.Feedback
{
    /// <summary>
    /// Android haptics through PwHaptics (Platform/Android/PocketWildsHaptics.androidlib). Each profile becomes one
    /// VibrationEffect at start: composed primitives (API 30+, when the phone supports them), else a waveform with
    /// amplitudes, else on/off. Playing is one static JNI call with a cached argument: no allocation.
    /// The library declares android.permission.VIBRATE; touch vibrations follow the system touch feedback setting
    /// (VibrationAttributes USAGE_TOUCH on API 33+).
    /// </summary>
    public sealed class AndroidHapticBackend : IHapticBackend, IDisposable
    {
        private const string JavaClass = "com.pocketwilds.game.haptics.PwHaptics";

        private AndroidJavaClass _class;
        private IntPtr _classPtr;
        private IntPtr _play;
        private jvalue[][] _args;

        public AndroidHapticBackend(HapticProfileSet set)
        {
            try
            {
                int count = 0;
                foreach (HapticEventId value in Enum.GetValues(typeof(HapticEventId)))
                    count = Math.Max(count, (int)value + 1);
                _class = new AndroidJavaClass(JavaClass);
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    Supported = _class.CallStatic<int>("init", activity, count) != 0;
                if (!Supported)
                {
                    GameLog.Info(LogCategory.Boot, "Haptics: no vibrator on this device.");
                    return;
                }
                var modes = new int[4];
                for (int i = 0; i < count; i++)
                {
                    var profile = set != null ? set.Find((HapticEventId)i) : null;
                    if (profile == null)
                        continue;
                    var kinds = new int[profile.steps.Length];
                    var scales = new float[profile.steps.Length];
                    var delays = new int[profile.steps.Length];
                    for (int s = 0; s < profile.steps.Length; s++)
                    {
                        kinds[s] = (int)profile.steps[s].primitive;
                        scales[s] = profile.steps[s].scale;
                        delays[s] = profile.steps[s].delayMs;
                    }
                    int mode = _class.CallStatic<int>("define", i, kinds, scales, delays, profile.timingsMs ?? new long[0],
                        profile.amplitudes ?? new int[0]);
                    if (mode >= 0 && mode < modes.Length)
                        modes[mode]++;
                }
                _classPtr = _class.GetRawClass();
                _play = AndroidJNIHelper.GetMethodID(_classPtr, "play", "(I)V", true);
                _args = new jvalue[count][];
                for (int i = 0; i < count; i++)
                    _args[i] = new[] { new jvalue { i = i } };
                GameLog.Info(LogCategory.Boot, $"Haptics: {modes[3]} composed, {modes[2]} with amplitudes, {modes[1]} on/off.");
            }
            catch (Exception ex)
            {
                Supported = false;
                GameLog.Warning(LogCategory.Boot, "Haptics unavailable: " + ex.Message);
            }
        }

        public bool Supported { get; private set; }

        public void Play(int profile)
        {
            if (!Supported || profile < 0 || profile >= _args.Length)
                return;
            AndroidJNI.CallStaticVoidMethod(_classPtr, _play, _args[profile]);
        }

        public void Dispose()
        {
            if (_class != null && Supported)
                _class.CallStatic("cancel");
            _class?.Dispose();
            _class = null;
            Supported = false;
        }
    }
}
#endif
