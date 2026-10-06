package com.pocketwilds.game.haptics;

import android.app.Activity;
import android.content.Context;
import android.os.Build;
import android.os.VibrationAttributes;
import android.os.VibrationEffect;
import android.os.Vibrator;
import android.os.VibratorManager;

/**
 * Pocket Wilds haptics (T34B, J-HAPTIC-*). C# (AndroidHapticBackend) defines every pattern once at boot, then plays
 * one by index. Each pattern takes the best form the phone supports: composed primitives (API 30+, when all its
 * primitives are supported), a waveform with amplitudes, or the same waveform on/off. Patterns are short and never
 * repeat. On API 33+ they are touch vibrations, so the system touch feedback setting applies.
 */
public final class PwHaptics {
    /** Mode returned by define: how the pattern will play on this phone. */
    public static final int MODE_NONE = 0;
    public static final int MODE_ON_OFF = 1;
    public static final int MODE_AMPLITUDE = 2;
    public static final int MODE_PRIMITIVES = 3;

    private static Vibrator vibrator;
    private static VibrationEffect[] effects;
    private static Object touchAttributes;

    private PwHaptics() {
    }

    /** Returns 1 when the phone can vibrate, 0 otherwise (every play is then skipped). */
    public static int init(Activity activity, int count) {
        effects = new VibrationEffect[Math.max(count, 0)];
        vibrator = null;
        touchAttributes = null;
        if (activity == null) {
            return 0;
        }
        try {
            if (Build.VERSION.SDK_INT >= 31) {
                VibratorManager manager = (VibratorManager) activity.getSystemService(Context.VIBRATOR_MANAGER_SERVICE);
                vibrator = manager != null ? manager.getDefaultVibrator() : null;
            } else {
                vibrator = (Vibrator) activity.getSystemService(Context.VIBRATOR_SERVICE);
            }
            if (vibrator == null || !vibrator.hasVibrator()) {
                vibrator = null;
                return 0;
            }
            if (Build.VERSION.SDK_INT >= 33) {
                touchAttributes = VibrationAttributes.createForUsage(VibrationAttributes.USAGE_TOUCH);
            }
            return 1;
        } catch (RuntimeException e) {
            vibrator = null;
            return 0;
        }
    }

    /**
     * Defines pattern index. kinds/scales/delays: composed form (see primitive()); timings/amplitudes: waveform
     * fallback (alternating off/on durations in ms, amplitudes 0-255). Returns the MODE used.
     */
    public static int define(int index, int[] kinds, float[] scales, int[] delays, long[] timings, int[] amplitudes) {
        if (vibrator == null || effects == null || index < 0 || index >= effects.length) {
            return MODE_NONE;
        }
        VibrationEffect effect = null;
        int mode = MODE_NONE;
        try {
            if (Build.VERSION.SDK_INT >= 30 && kinds != null && kinds.length > 0) {
                int[] ids = new int[kinds.length];
                boolean known = true;
                for (int i = 0; i < kinds.length; i++) {
                    ids[i] = primitive(kinds[i]);
                    known &= ids[i] >= 0;
                }
                if (known && vibrator.areAllPrimitivesSupported(ids)) {
                    VibrationEffect.Composition composition = VibrationEffect.startComposition();
                    for (int i = 0; i < ids.length; i++) {
                        composition.addPrimitive(ids[i], clamp01(scales[i]), Math.max(0, delays[i]));
                    }
                    effect = composition.compose();
                    mode = MODE_PRIMITIVES;
                }
            }
            if (effect == null && timings != null && timings.length > 0) {
                if (vibrator.hasAmplitudeControl() && amplitudes != null && amplitudes.length == timings.length) {
                    effect = VibrationEffect.createWaveform(timings, amplitudes, -1);
                    mode = MODE_AMPLITUDE;
                } else {
                    effect = VibrationEffect.createWaveform(timings, -1);
                    mode = MODE_ON_OFF;
                }
            }
        } catch (RuntimeException e) {
            effect = null;
            mode = MODE_NONE;
        }
        effects[index] = effect;
        return mode;
    }

    public static void play(int index) {
        if (vibrator == null || effects == null || index < 0 || index >= effects.length) {
            return;
        }
        VibrationEffect effect = effects[index];
        if (effect == null) {
            return;
        }
        try {
            if (Build.VERSION.SDK_INT >= 33 && touchAttributes != null) {
                vibrator.vibrate(effect, (VibrationAttributes) touchAttributes);
            } else {
                vibrator.vibrate(effect);
            }
        } catch (RuntimeException ignored) {
            // A refused vibration is never an error for the game.
        }
    }

    public static void cancel() {
        if (vibrator != null) {
            vibrator.cancel();
        }
    }

    private static float clamp01(float value) {
        return value < 0f ? 0f : (value > 1f ? 1f : value);
    }

    /** C# HapticPrimitive to VibrationEffect.Composition constants (-1 when this Android has none). */
    private static int primitive(int kind) {
        if (Build.VERSION.SDK_INT < 30) {
            return -1;
        }
        switch (kind) {
            case 0:
                return VibrationEffect.Composition.PRIMITIVE_CLICK;
            case 1:
                return VibrationEffect.Composition.PRIMITIVE_TICK;
            case 2:
                return Build.VERSION.SDK_INT >= 31 ? VibrationEffect.Composition.PRIMITIVE_LOW_TICK : -1;
            case 3:
                return Build.VERSION.SDK_INT >= 31 ? VibrationEffect.Composition.PRIMITIVE_THUD : -1;
            case 4:
                return VibrationEffect.Composition.PRIMITIVE_QUICK_RISE;
            case 5:
                return VibrationEffect.Composition.PRIMITIVE_SLOW_RISE;
            case 6:
                return VibrationEffect.Composition.PRIMITIVE_QUICK_FALL;
            default:
                return -1;
        }
    }
}
