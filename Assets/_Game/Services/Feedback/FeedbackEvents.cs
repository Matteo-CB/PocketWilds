using PocketWilds.Core;
using UnityEngine;

namespace PocketWilds.Services.Feedback
{
    /// <summary>
    /// Named audio events (MVP Asset Pack A13). Gameplay and presentation emit IDs; the audio service
    /// owns clips, voices and mixing. Values are stable: never renumber, only append.
    /// </summary>
    public enum AudioEventId : ushort
    {
        None = 0,
        EggTap = 1,
        Crack1 = 2,
        Crack2 = 3,
        HatchPop = 4,
        RareReveal = 5,
        EpicReveal = 6,
        LegendaryReveal = 7,
        MythicReveal = 8,

        /// <summary>Species reaction ("RabbitHappy"): the species ID travels as context.</summary>
        AnimalHappy = 9,

        /// <summary>Light collected (offline return, later rewards).</summary>
        CollectLight = 10,

        /// <summary>A UI panel opens (soft whoosh).</summary>
        PanelOpen = 11,

        /// <summary>Species mastery level up (the species travels as context).</summary>
        MasteryLevelUp = 12,
    }

    /// <summary>Haptic profiles (J-HAPTIC-*). Stable values.</summary>
    public enum HapticEventId : byte
    {
        None = 0,
        TapLight = 1,
        Confirm = 2,
        HatchCommon = 3,
        Rare = 4,
        Legendary = 5,
        Mythic = 6,
        ErrorSoft = 7,
        PurchaseSuccess = 8,
        AnimalTouch = 9,
    }

    public interface IAudioService
    {
        /// <param name="context">Optional content context, for example the species of AnimalHappy.</param>
        void Play(AudioEventId id, Vector3 position, ContentId context);
    }

    public interface IHapticService
    {
        void Play(HapticEventId id);
    }

    /// <summary>Used until the audio service and clips land (J-SFX-*). Events are still emitted and testable.</summary>
    public sealed class NullAudioService : IAudioService
    {
        public static readonly NullAudioService Instance = new NullAudioService();

        public void Play(AudioEventId id, Vector3 position, ContentId context)
        {
        }
    }

    public sealed class NullHapticService : IHapticService
    {
        public static readonly NullHapticService Instance = new NullHapticService();

        public void Play(HapticEventId id)
        {
        }
    }
}
