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

        /// <summary>T35A J-SFX-UPGRADE-001: an upgrade bought (a small rising pluck, three steps).</summary>
        UpgradeBuy = 29,

        /// <summary>T35A J-SFX-UPGRADE-002: a x2 milestone of Production reached (a soft two-note chord).</summary>
        UpgradeMilestone = 30,

        /// <summary>T35A J-SFX-REWARD-005: a zone opens (leaves and a rising arpeggio).</summary>
        ZoneOpen = 31,

        /// <summary>T35A J-SFX-REWARD-006: the refuge levels up (a warm four-note fanfare).</summary>
        RefugeLevelUp = 32,

        /// <summary>T35A J-SFX-REWARD-007: a biome's mastery levels up (two wooden notes and leaves).</summary>
        BiomeMasteryUp = 33,

        // 34 to 37 are reserved by doc 05 section 6.4 (secret reveal, new species, new variant, mastery milestone).

        /// <summary>T36 J-SFX-FEEL-009: a wish fulfilled (a bubble pops, two warm glass notes).</summary>
        WishDone = 38,

        /// <summary>T36 J-SFX-FEEL-010: the daily gift opens (a wooden lid knock, rising glass notes).</summary>
        GiftOpen = 39,

        /// <summary>T36 J-SFX-FEEL-011: the paw stamp lands on a calendar box (a soft felt puff).</summary>
        PawStamp = 40,

        /// <summary>
        /// T37 J-SFX-FEEL-012: a reward card turns over (paper, and a small wooden note climbing F, G, A, C, D). Its
        /// slot in doc 05 section 6.4's order. Cards 1 to 5 play variants 0 to 4; past the fifth card, no sound per
        /// card (doc 05 section 6.4: a bounded rising run, never one sound per item beyond 5).
        /// </summary>
        CardFlip = 41,

        /// <summary>
        /// T37 J-SFX-FEEL-013 and J-SFX-ACT-000-03: a result star lands (a kalimba tine with its glass octave). Its slot
        /// in doc 05 section 6.4's order. Play variant 0, 1, 2 for the first, second, third star: F, A, C.
        /// </summary>
        StarEarned = 42,

        // 43 to 66 are reserved by doc 05 section 6.4 (Sticker to BubblePuff, then the 17 surprise sounds).

        /// <summary>T36 J-SFX-CURRENCY-004: Gems land in the Gems chip (a rolled glass chord).</summary>
        CollectGems = 67,

        // T37: the shared activity sounds (doc 01 Contenu, section 12.0), after the doc 05 list and CollectGems.

        /// <summary>T37 J-SFX-ACT-000-01: a game starts (one small glass chime; the music comes in after it).</summary>
        ActivityStart = 68,

        /// <summary>T37 J-SFX-ACT-000-02: the game is over (a soft kalimba chord rolled downward).</summary>
        ActivityEnd = 69,

        /// <summary>T37 J-SFX-ACT-000-04: a new record (a four-note music box motif).</summary>
        ActivityRecord = 70,

        /// <summary>T37 J-SFX-ACT-000-05: pause (variant 0) and resume (variant 1), a soft wooden click.</summary>
        ActivityPause = 71,

        /// <summary>
        /// T37 J-SFX-ACT-000-06: a streak step: the star's tine a semitone higher at each step. Play variant step - 1
        /// (0 to 7); past the eighth step it stays on the top.
        /// </summary>
        ActivityStreak = 72,
    }

    /// <summary>T37: the three activity musics (doc 01 Contenu, section 12.0, J-MUS-ACT-01 to 03). Stable values.</summary>
    public enum ActivityMusicId : byte
    {
        /// <summary>The activity keeps the biome's music (Expeditions, Garden) or plays its own tracks (Firefly Rhythm).</summary>
        None = 0,

        /// <summary>J-MUS-ACT-01 « Petite partie » (Little Game): calm, 84 BPM, F major, an 80 s loop.</summary>
        LittleGame = 1,

        /// <summary>J-MUS-ACT-02 « Pattes pressées » (Hurried Paws): lively, 112 BPM, F major, soft shaker, a 120 s loop.</summary>
        HurriedPaws = 2,

        /// <summary>J-MUS-ACT-03 « Câlins en série » (Hug Streak): puzzle, 96 BPM, D minor moving to F major, a 100 s loop.</summary>
        HugStreak = 3,
    }

    /// <summary>
    /// T37: how an activity sounds (doc 01 Contenu, section 12.0; doc 04 Biomes tardifs). Its music, with the theme
    /// biome's ambience under it at -6 dB; the music 5 dB lower from the end of a game until the result closes. The
    /// music clips are Addressables (group Audio_Activities, one bundle each, streamed), loaded when a game is chosen
    /// and released once the refuge's music is back.
    /// </summary>
    public static class ActivityAudio
    {
        /// <summary>The theme biome's ambience under an activity's music (doc 01 Contenu, section 12.0).</summary>
        public const float AmbienceTrimDb = -6f;

        /// <summary>J-SFX-ACT-000-02: the music lowers by 5 dB at the end of a game, under the result.</summary>
        public const float ResultMusicTrimDb = -5f;

        /// <summary>The activity's music comes in after the start chime (J-SFX-ACT-000-01): a short cross-fade.</summary>
        public const float MusicFadeSeconds = 1f;

        /// <summary>The music of an activity (doc 01 Contenu, section 12.0); None keeps the biome's beds.</summary>
        public static ActivityMusicId MusicOf(PocketWilds.Simulation.ActivityId activity)
        {
            switch (activity)
            {
                case PocketWilds.Simulation.ActivityId.Grooming:
                case PocketWilds.Simulation.ActivityId.PawMemory:
                case PocketWilds.Simulation.ActivityId.Bakery:
                case PocketWilds.Simulation.ActivityId.Constellations:
                case PocketWilds.Simulation.ActivityId.TilePuzzle:
                case PocketWilds.Simulation.ActivityId.PhotoSafari:
                case PocketWilds.Simulation.ActivityId.Test:
                    return ActivityMusicId.LittleGame;
                case PocketWilds.Simulation.ActivityId.HideAndSeek:
                case PocketWilds.Simulation.ActivityId.NutRain:
                case PocketWilds.Simulation.ActivityId.TreasureFishing:
                case PocketWilds.Simulation.ActivityId.LilyHop:
                case PocketWilds.Simulation.ActivityId.Slide:
                case PocketWilds.Simulation.ActivityId.Bubbles:
                case PocketWilds.Simulation.ActivityId.BurrowMaze:
                    return ActivityMusicId.HurriedPaws;
                case PocketWilds.Simulation.ActivityId.CuddleMatch:
                case PocketWilds.Simulation.ActivityId.GrandContest:
                    return ActivityMusicId.HugStreak;
                default:
                    return ActivityMusicId.None;
            }
        }

        /// <summary>Addressables address of a music clip (null for None).</summary>
        public static string Address(ActivityMusicId music)
        {
            switch (music)
            {
                case ActivityMusicId.LittleGame:
                    return "audio.activity.little_game";
                case ActivityMusicId.HurriedPaws:
                    return "audio.activity.hurried_paws";
                case ActivityMusicId.HugStreak:
                    return "audio.activity.hug_streak";
                default:
                    return null;
            }
        }
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

        /// <summary>
        /// T37: plays one chosen variant of a sound instead of a random one: a result star's pitch (StarEarned 0 F, 1 A,
        /// 2 C), a reward card (CardFlip 0 to 4), a streak step (ActivityStreak 0 to 7), pause or resume (ActivityPause
        /// 0, 1). A variant past the last plays the last: the streak stays at the top (doc 01 Contenu, section 12.0) and
        /// a run of cards never climbs past five notes (doc 05 Sensations, section 6.4). The default plays any variant.
        /// </summary>
        void PlayVariant(AudioEventId id, int variant)
        {
            Play(id, Vector3.zero, ContentId.None);
        }

        /// <summary>
        /// T37: trims of the beds in dB (0: as mastered; never above 0), smoothed by the service. An activity plays its
        /// music with the theme biome's ambience at ActivityAudio.AmbienceTrimDb, lowers its music by
        /// ActivityAudio.ResultMusicTrimDb from the end of a game until the result closes, and sets both back to 0 when
        /// the player returns to the refuge.
        /// </summary>
        void SetBedLevels(float musicDb, float ambienceDb)
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
