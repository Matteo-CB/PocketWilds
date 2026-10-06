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
        FabulousReveal = 8,

        /// <summary>Species reaction ("RabbitHappy"): the species ID travels as context.</summary>
        AnimalHappy = 9,

        /// <summary>Light collected (offline return, later rewards).</summary>
        CollectLight = 10,

        /// <summary>A UI panel opens (soft whoosh).</summary>
        PanelOpen = 11,

        /// <summary>Species mastery level up (the species travels as context).</summary>
        MasteryLevelUp = 12,

        /// <summary>A purchase was granted (shop, offer).</summary>
        PurchaseComplete = 13,

        /// <summary>T34B: a calm "not yet" (not enough Light...). Never a buzzer.</summary>
        ErrorSoft = 14,

        /// <summary>T34B: small interface touches (toggle, tab, card).</summary>
        UiTap = 15,

        /// <summary>T34B: a choice confirmed (modal OK, favorite on).</summary>
        UiConfirm = 16,

        /// <summary>T34B: closing, cancelling, going back.</summary>
        UiBack = 17,

        /// <summary>T34B: an animal falls asleep near the view (soft breath; the species travels as context).</summary>
        AnimalSleep = 18,

        /// <summary>
        /// T34B: an animal greets or rejoices on its own near the view (same voice as AnimalHappy, quieter, rare and
        /// rate-limited for the whole refuge: 20 animals never become a chorus).
        /// </summary>
        AnimalChatter = 19,

        /// <summary>T34B2: a full screen closes (Codex, shop): a short downward whoosh.</summary>
        PanelClose = 20,

        /// <summary>T34B2: a page of the Codex turns (species page in, back to the list).</summary>
        UiTab = 21,

        /// <summary>T34B2: something switched on (favorite heart on).</summary>
        ToggleOn = 22,

        /// <summary>T34B2: switched off.</summary>
        ToggleOff = 23,

        /// <summary>T34B2: the store is still answering (pending purchase).</summary>
        PurchasePending = 24,

        /// <summary>T34B2: Light paid for an egg (a soft tick under the egg tap).</summary>
        SpendLight = 25,

        /// <summary>T34B2: a reward or an unlock is yours (unlock toast, later daily gifts).</summary>
        Claim = 26,

        /// <summary>T34B2: a near animal eats (soft nibbles; the species travels as context).</summary>
        AnimalEat = 27,

        /// <summary>T34B2: a small bird flies off near the view (wing beats).</summary>
        BirdTakeOff = 28,
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
        Fabulous = 6,
        ErrorSoft = 7,
        PurchaseSuccess = 8,
        AnimalTouch = 9,
    }

    public interface IAudioService
    {
        /// <param name="context">Optional content context, for example the species of AnimalHappy.</param>
        void Play(AudioEventId id, Vector3 position, ContentId context);

        /// <summary>
        /// T34B: where the player looks. World sounds (animals) fade with their distance to the focus point on the
        /// ground and pan with their side of the screen. cameraDistance: the camera's distance to that point (zoom).
        /// </summary>
        void SetView(Vector3 focus, Vector3 right, float cameraDistance)
        {
        }

        /// <summary>T34B: the music and ambience of the current place, cross-faded. Null stops that bed.</summary>
        void SetBeds(AudioClip music, AudioClip ambience, float fadeSeconds)
        {
        }

        /// <summary>T34B: the place is leaving and its clips are about to be released: beds stop at once.</summary>
        void StopBeds()
        {
        }

        /// <summary>T34B2: the layers mixed over the day ambience: evening and night (crickets), rain. Null: none.</summary>
        void SetAmbienceLayers(AudioClip evening, AudioClip rain)
        {
        }

        /// <summary>
        /// T34B2: evening 0 (day) to 1 (evening and night) and rain 0 (dry) to 1 (full shower). Called every frame by
        /// the place; the service smooths the changes.
        /// </summary>
        void SetAmbienceMix(float evening, float rain)
        {
        }
    }

    public interface IHapticService
    {
        void Play(HapticEventId id);
    }

    /// <summary>Headless tests and tools: events are emitted and testable, nothing plays.</summary>
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
