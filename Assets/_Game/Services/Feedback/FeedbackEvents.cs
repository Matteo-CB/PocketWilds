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

        /// <summary>
        /// T44A J-SFX-REWARD (doc 05 section 6.4, slot 43): a sticker is won (a soft paper peel, a light round pop on A over
        /// a quiet wooden tine). SFX_Reward_Sticker_01, Rewards, 1 voice, 0.5 s.
        /// </summary>
        Sticker = 43,

        /// <summary>
        /// T44A J-SFX-REWARD (doc 05 section 6.4, slot 44): an album page turns (the air of a thick page, then two warm
        /// wooden notes, F and A). SFX_Reward_AlbumPage_01, Rewards, 1 voice, 0.9 s.
        /// </summary>
        AlbumPage = 44,

        /// <summary>
        /// T42 J-SFX-FEEL-016: the team is home (doc 05 sheet 23: small hurried felted steps coming closer, the postcard slid
        /// out of the bag, two wooden notes, C then F). Its slot in doc 05 section 6.4's order. Played once at the start of
        /// the welcome, while the team walks in; its two notes land 0.6 and 0.7 s later, as the postcard pivots in. See
        /// ExpeditionAudio.
        /// </summary>
        ExpeditionReturn = 45,

        /// <summary>
        /// T43 J-SFX-FEEL-017 and J-SFX-ACT-006-04: a ripe plant is harvested (doc 05 sheet 24: a plop of earth and a rustle
        /// of leaves, then a warm pop on F), 3 variants. Its slot in doc 05 section 6.4's order (Harvest). Played at a pitch
        /// rising up the F major pentatonic for chained beds: GardenAudio.HarvestPitch.
        /// </summary>
        GardenHarvest = 46,

        // 47 and 48 are reserved by doc 05 section 6.4 (VisitorArrive, BondUp).

        /// <summary>
        /// T38 J-SFX-FEEL-020: a soft puff of bubbles (a breath of air, a small cluster of round bubbles popping around F).
        /// Its slot in doc 05 section 6.4's order. The grooming's soap bubble: variant 0 when it is tapped (the full
        /// puff), 1 when it appears (lighter); doc 05 also gives it to a bond level's change of look.
        /// </summary>
        BubblePuff = 49,

        // 50 to 66 are reserved by doc 05 section 6.4 (the 17 surprise sounds, SurpriseButterfly to SurpriseBranchCarry).

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

        // T38: the grooming (doc 01 Contenu, S-ACT-001 « Sons et effets »; J-SFX-ACT-001-04 giggle and -07 content sigh
        // are the species voices, AnimalHappy and AnimalSleep through IAudioService.PlayPitched). See GroomingAudio.

        /// <summary>
        /// T38 J-SFX-ACT-001-01: one pass of the brush (a soft felted whoosh, 3 variants). Played on every pass while the
        /// finger slides; its cooldown and voices turn the passes into a steady soft texture.
        /// </summary>
        GroomBrush = 73,

        /// <summary>T38 J-SFX-ACT-001-02: a debris picked off (a leaf pop, then the leaf flying away), 2 variants.</summary>
        GroomDebris = 74,

        /// <summary>
        /// T38 J-SFX-ACT-001-03: a burr. Variant 0 the double pop when it comes off (second tap), 1 its first tap (a softer
        /// pop, it loosens): GroomingAudio.BurrRemovedVariant and BurrLoosenedVariant.
        /// </summary>
        GroomBurr = 75,

        /// <summary>
        /// T38 J-SFX-ACT-001-05: a zone is clean: a small glass bead a semitone higher at each zone cleaned in a row. Play
        /// variant streak - 1 (0 to 7, GroomingAudio.CleanVariant); past the eighth it stays on the top. Grooming's own
        /// streak sound: the shared ActivityStreak does not play for it (ActivityAudio.HasOwnStreakSound).
        /// </summary>
        GroomZoneClean = 76,

        /// <summary>T38 J-SFX-ACT-001-06: the bow is placed (a ribbon settles, two music box notes, C then F).</summary>
        GroomBow = 77,

        // T39: the hide and seek (doc 01 Contenu, S-ACT-002 « Sons et effets »; the « ip ? » of J-SFX-ACT-002-01 is the
        // species' happy voice through IAudioService.PlayPitched). See HideSeekAudio.

        /// <summary>
        /// T39 J-SFX-ACT-002-01: the pop of the « coucou » (an animal found pops out: a whisper of leaves, a round pop
        /// lifting like a question), 2 variants; HideSeekAudio.Coucou plays the species' voice with it.
        /// </summary>
        HideSeekCoucou = 78,

        /// <summary>
        /// T39 J-SFX-ACT-002-02: a touch on an empty spot (a soft shake of leaves, a few leaves flying off), 3 variants.
        /// Kind and light, never a « wrong » sound; its cooldown keeps quick taps from piling up.
        /// </summary>
        HideSeekEmpty = 79,

        /// <summary>
        /// T39 J-SFX-ACT-002-03: an animal gives its hint (two soft swells of grass), 2 variants, under the empty touch.
        /// Animals hinting at the same moment make one rustle (cooldown).
        /// </summary>
        HideSeekHint = 80,

        /// <summary>T39 J-SFX-ACT-002-04: the magnifier (a soft breath across glass, a small glass bead as the ring settles).</summary>
        HideSeekMagnifier = 81,

        /// <summary>T39 J-SFX-ACT-002-05: the whistle (two soft notes of a wooden slide whistle, F sliding up to C).</summary>
        HideSeekWhistle = 82,

        /// <summary>T39 J-SFX-ACT-002-06: every animal found (a three-note music box motif, C, D, F, over the end chord).</summary>
        HideSeekAllFound = 83,

        // T40: the nut drop (doc 01 Contenu, S-ACT-003 « Sons et effets »; J-SFX-ACT-003-04, the streak step, is the shared
        // ActivityStreak, played on each +0.1 of the multiplier). See NutDropAudio.

        /// <summary>
        /// T40 J-SFX-ACT-003-01: an item lands in the basket (a round woody pop over the knock of its shell). Its pitch
        /// follows the item: variant 0 hazelnut (F), 1 acorn (A), 2 berry (C), NutDropAudio.CatchVariant.
        /// </summary>
        NutDropCatch = 84,

        /// <summary>T40 J-SFX-ACT-003-02: the golden hazelnut lands (its woody pop, then a small chime of glass, A, C, F).</summary>
        NutDropGolden = 85,

        /// <summary>T40 J-SFX-ACT-003-03: a pinecone lands in the basket (a soft felted « bonk », gentle), 2 variants.</summary>
        NutDropPinecone = 86,

        /// <summary>T40 J-SFX-ACT-003-05: a special is caught (a breath, then a soft F major chord).</summary>
        NutDropSpecial = 87,

        // T41: the pawprint memory (doc 01 Contenu, S-ACT-004 « Sons et effets »). See MemoryAudio.

        /// <summary>
        /// T41 J-SFX-ACT-004-01: a card turns over (a soft rustle of card stock, the card laid on the tablecloth), 3
        /// variants. Also the preview turning every card, the cards of a miss turning back, and two cards swapping places.
        /// </summary>
        MemoryFlip = 88,

        /// <summary>
        /// T41 J-SFX-ACT-004-02: a pair is found (two notes a third apart at the music box). The third climbs the F major
        /// scale with the pairs found: variant 0 (F and A) to 7 (F and A an octave higher), MemoryAudio.MatchVariant.
        /// </summary>
        MemoryMatch = 89,

        /// <summary>T41 J-SFX-ACT-004-03: two cards do not match (one low felted note, very soft, never a buzzer).</summary>
        MemoryMiss = 90,

        /// <summary>T41 J-SFX-ACT-004-04: the glow card is turned (a warm cup of glass while its pair glows, never a bell).</summary>
        MemoryGlow = 91,

        // T42: the expeditions (doc 01 Contenu, S-ACT-005 « Sons et effets »; J-SFX-FEEL-016 is ExpeditionReturn, 45). See
        // ExpeditionAudio.

        /// <summary>T42 J-SFX-ACT-005-01: the team leaves (the little cloth bag closes, then small steps trot away), 1 s.</summary>
        ExpeditionDepart = 92,

        /// <summary>T42 J-SFX-ACT-005-02: a team is back (a small shop door bell rings three times on its spring).</summary>
        ExpeditionBack = 93,

        /// <summary>T42 J-SFX-ACT-005-03: the postcard comes in front (a card slid across paper, then laid flat).</summary>
        ExpeditionPostcard = 94,

        /// <summary>
        /// T42 J-SFX-ACT-005-04: a loot item turns over (a warm pop), one step higher per item up the F major pentatonic:
        /// variant 0 (F) to 7 (the A an octave and a third up), ExpeditionAudio.LootVariant.
        /// </summary>
        ExpeditionLoot = 95,

        // T43: the garden (doc 01 Contenu, S-ACT-006 « Sons et effets »; J-SFX-FEEL-017 is GardenHarvest, 46). See
        // GardenAudio.

        /// <summary>T43 J-SFX-ACT-006-01: a seed is planted (soft soil: a press, the crumbs closing, two pats), 2 variants.</summary>
        GardenPlant = 96,

        /// <summary>T43 J-SFX-ACT-006-02: the watering can pours on a sprout (a thin trickle with small round drops), 1 s.</summary>
        GardenWater = 97,

        /// <summary>T43 J-SFX-ACT-006-03: a sprout reaches its next stage (leaves unfold, a small vegetal pop on A).</summary>
        GardenStage = 98,

        /// <summary>T43 J-SFX-REWARD-002 (doc 01 S-ACT-006): the garden levels up, the species mastery motif played again.</summary>
        GardenLevelUp = 99,
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

        /// <summary>
        /// T38: the activity sounds its own streak, so the shared ActivityStreak (J-SFX-ACT-000-06) does not play over it:
        /// the grooming's clean zone climbs a semitone per zone in a row (J-SFX-ACT-001-05, GroomZoneClean). T40: the nut
        /// drop plays the shared step on each +0.1 of its multiplier (every 5 catches, J-SFX-ACT-003-04, NutDropAudio), not
        /// on every catch. T41: the memory's pair third already climbs with the pairs found (MemoryAudio.MatchVariant).
        /// </summary>
        public static bool HasOwnStreakSound(PocketWilds.Simulation.ActivityId activity) =>
            activity == PocketWilds.Simulation.ActivityId.Grooming || activity == PocketWilds.Simulation.ActivityId.NutRain
                                                                   || activity == PocketWilds.Simulation.ActivityId.PawMemory;
    }

    /// <summary>
    /// T38: how a grooming game sounds (doc 01 Contenu, S-ACT-001 « Sons et effets »; doc Audio, J-SFX-ACT-001-01 to 07).
    /// The stage plays <see cref="Brush"/> on each pass of the finger and hands every drained grooming event to
    /// <see cref="Play"/>; the content sigh (<see cref="Sigh"/>) comes when the animal closes its eyes at the end. The
    /// giggle and the sigh are the animal's own voice (its species bank and pitch); everything else plays from the centre.
    /// No allocation.
    /// </summary>
    public static class GroomingAudio
    {
        /// <summary>J-SFX-ACT-001-04: the giggle is the species' happy voice, 10 % higher.</summary>
        public const float GigglePitch = 1.1f;

        /// <summary>
        /// J-SFX-ACT-001-07: the content sigh is the species' sleep voice, a touch lower than its sleep (relaxed, eyes
        /// closed). Our default: the doc gives the bank, not the pitch.
        /// </summary>
        public const float SighPitch = 0.95f;

        /// <summary>J-SFX-ACT-001-05: 8 glass steps (F up to C); past the eighth zone in a row the step stays on the top.</summary>
        public const int CleanSteps = 8;

        /// <summary>GroomBurr's clips: the double pop when the burr comes off (its second tap).</summary>
        public const int BurrRemovedVariant = 0;

        /// <summary>GroomBurr's clips: the burr's first tap, a softer pop (it loosens).</summary>
        public const int BurrLoosenedVariant = 1;

        /// <summary>BubblePuff's clips: the full puff of a soap bubble tapped.</summary>
        public const int BubblePoppedVariant = 0;

        /// <summary>BubblePuff's clips: the lighter puff of a soap bubble appearing on the coat.</summary>
        public const int BubbleSpawnedVariant = 1;

        /// <summary>The brush's pitch for a slow stroke and for a quick one (a quick stroke sounds a little brighter).</summary>
        public const float BrushSlowPitch = 0.94f;

        public const float BrushFastPitch = 1.08f;

        /// <summary>The clean step of a zone cleaned with this streak (zones in a row, 1 for the first): 0 to 7.</summary>
        public static int CleanVariant(int streak) => streak <= 1 ? 0 : streak >= CleanSteps ? CleanSteps - 1 : streak - 1;

        /// <summary>
        /// One pass of the brush (J-SFX-ACT-001-01), whatever the zone (dirty, clean, already brushed). Its cooldown and
        /// voices (0.12 s, 3) make the passes of a sliding finger one steady soft texture; nothing plays when the finger
        /// rests. speed01: the finger's speed over the coat, 0 slow to 1 quick.
        /// </summary>
        public static void Brush(IAudioService audio, float speed01)
        {
            float pitch = BrushSlowPitch + (BrushFastPitch - BrushSlowPitch) * Mathf.Clamp01(speed01);
            audio.PlayPitched(AudioEventId.GroomBrush, Vector3.zero, ContentId.None, pitch);
        }

        /// <summary>
        /// The sound of one grooming event (doc 01 S-ACT-001 « Sons et effets »). Passes are silent here (the stage plays
        /// <see cref="Brush"/> for each); the game already spaces the rebrush giggles (one per 0.6 s at most). The golden
        /// brush has no sound in the doc: our default is two small rising glass notes when it appears (ToggleOn) and the
        /// soft reward chord when it is taken (Claim). A soap bubble's three zones play one clean step under its puff (the
        /// step's cooldown keeps the first). species: the groomed animal's species; position: the animal.
        /// </summary>
        public static void Play(IAudioService audio, in PocketWilds.Simulation.GroomingEvent e, ContentId species, Vector3 position)
        {
            switch (e.Kind)
            {
                case PocketWilds.Simulation.GroomingEventKind.ZoneCleaned:
                    audio.PlayVariant(AudioEventId.GroomZoneClean, CleanVariant(e.Streak));
                    break;
                case PocketWilds.Simulation.GroomingEventKind.Rebrush:
                    audio.PlayPitched(AudioEventId.AnimalHappy, position, species, GigglePitch);
                    break;
                case PocketWilds.Simulation.GroomingEventKind.DebrisRemoved:
                    audio.Play(AudioEventId.GroomDebris, position, ContentId.None);
                    break;
                case PocketWilds.Simulation.GroomingEventKind.BurrLoosened:
                    audio.PlayVariant(AudioEventId.GroomBurr, BurrLoosenedVariant);
                    break;
                case PocketWilds.Simulation.GroomingEventKind.BurrRemoved:
                    audio.PlayVariant(AudioEventId.GroomBurr, BurrRemovedVariant);
                    break;
                case PocketWilds.Simulation.GroomingEventKind.BubbleSpawned:
                    audio.PlayVariant(AudioEventId.BubblePuff, BubbleSpawnedVariant);
                    break;
                case PocketWilds.Simulation.GroomingEventKind.BubblePopped:
                    audio.PlayVariant(AudioEventId.BubblePuff, BubblePoppedVariant);
                    break;
                case PocketWilds.Simulation.GroomingEventKind.GoldenBrushOffered:
                    audio.Play(AudioEventId.ToggleOn, position, ContentId.None);
                    break;
                case PocketWilds.Simulation.GroomingEventKind.GoldenBrushStart:
                    audio.Play(AudioEventId.Claim, position, ContentId.None);
                    break;
                case PocketWilds.Simulation.GroomingEventKind.BowPlaced:
                    audio.Play(AudioEventId.GroomBow, position, ContentId.None);
                    break;
            }
        }

        /// <summary>
        /// J-SFX-ACT-001-07: the content sigh, the animal's own sleep voice at the end, when it closes its eyes under the
        /// hearts (about 0.6 s after the bow, so it is not lost under the bow's notes and the end chord).
        /// </summary>
        public static void Sigh(IAudioService audio, ContentId species, Vector3 position) =>
            audio.PlayPitched(AudioEventId.AnimalSleep, position, species, SighPitch);
    }

    /// <summary>
    /// T39: how a hide-and-seek game sounds (doc 01 Contenu, S-ACT-002 « Sons et effets »; doc Audio, J-SFX-ACT-002-01 to
    /// 06). The stage hands every drained game event to <see cref="Play"/>, with the species of the animal it concerns for
    /// the finds (ContentId.None otherwise). The « coucou » is the pop plus the animal's own happy voice, a little higher
    /// (the « ip ? »); everything else plays from the centre. The end chord stays the presenter's (ActivityEnd), the
    /// all-found motif lands with it. No allocation.
    /// </summary>
    public static class HideSeekAudio
    {
        /// <summary>
        /// J-SFX-ACT-002-01: the « ip ? » is the species' happy voice, 12 % higher: a little question (the pop under it
        /// lifts a whole tone). Our default: the doc gives the bank, not the pitch.
        /// </summary>
        public const float CoucouPitch = 1.12f;

        /// <summary>
        /// The sound of one hide-and-seek event (doc 01 S-ACT-002 « Sons et effets »). Our defaults where the doc is
        /// silent: a find after its hint (or the magnifier, the whistle) is the same « coucou » as a find before it (the
        /// points and the ring tell the difference); the pause after 3 empty touches, a challenge 6 mistake (its touch
        /// already rustled) and a decoy moving in the wind are silent, so the hint's rustle always means an animal; an
        /// animal slipping to another spot (challenge 9) rustles like a hint, quietly, so the player knows to look again.
        /// species: the found animal's species (Found, FoundAfterHint); position: its spot.
        /// </summary>
        public static void Play(IAudioService audio, in PocketWilds.Simulation.HideSeekEvent e, ContentId species, Vector3 position)
        {
            switch (e.Kind)
            {
                case PocketWilds.Simulation.HideSeekEventKind.Found:
                case PocketWilds.Simulation.HideSeekEventKind.FoundAfterHint:
                    Coucou(audio, species, position);
                    break;
                case PocketWilds.Simulation.HideSeekEventKind.Empty:
                    audio.Play(AudioEventId.HideSeekEmpty, position, ContentId.None);
                    break;
                case PocketWilds.Simulation.HideSeekEventKind.Hint:
                case PocketWilds.Simulation.HideSeekEventKind.Moved:
                    audio.Play(AudioEventId.HideSeekHint, position, ContentId.None);
                    break;
                case PocketWilds.Simulation.HideSeekEventKind.Magnifier:
                    audio.Play(AudioEventId.HideSeekMagnifier, position, ContentId.None);
                    break;
                case PocketWilds.Simulation.HideSeekEventKind.Whistle:
                    audio.Play(AudioEventId.HideSeekWhistle, position, ContentId.None);
                    break;
                case PocketWilds.Simulation.HideSeekEventKind.AllFound:
                    audio.Play(AudioEventId.HideSeekAllFound, position, ContentId.None);
                    break;
            }
        }

        /// <summary>
        /// J-SFX-ACT-002-01, the « coucou »: the pop as the animal pops out, and its own happy voice at
        /// <see cref="CoucouPitch"/> (a close-up: full level from the centre, whatever the refuge's view).
        /// </summary>
        public static void Coucou(IAudioService audio, ContentId species, Vector3 position)
        {
            audio.Play(AudioEventId.HideSeekCoucou, position, ContentId.None);
            audio.PlayPitched(AudioEventId.AnimalHappy, position, species, CoucouPitch);
        }
    }

    /// <summary>
    /// T40: how a nut drop game sounds (doc 01 Contenu, S-ACT-003 « Sons et effets »; doc Audio, J-SFX-ACT-003-01 to 05).
    /// The stage hands every drained game event to <see cref="Play"/>; everything plays from the centre (the set's
    /// close-up). The catch's pitch follows the item (F, A, C: the hazelnut, the acorn, the berry); the streak step is the
    /// shared ActivityStreak, once per +0.1 of the multiplier (the presenter keeps its per-catch step quiet for this
    /// activity, <see cref="ActivityAudio.HasOwnStreakSound"/>). The end chord stays the presenter's (ActivityEnd). No
    /// allocation.
    /// </summary>
    public static class NutDropAudio
    {
        /// <summary>NutDropCatch's clips, one pitch per item: the hazelnut's pop on F.</summary>
        public const int HazelnutVariant = 0;

        /// <summary>NutDropCatch's clips: the acorn's pop on A (a little drier, the tap of its cap).</summary>
        public const int AcornVariant = 1;

        /// <summary>NutDropCatch's clips: the berry's pop on C (rounder, softer).</summary>
        public const int BerryVariant = 2;

        /// <summary>ActivityStreak's steps (F up to C, a semitone each); past the eighth +0.1 the step stays on the top.</summary>
        public const int StreakSteps = 8;

        /// <summary>The catch's pitch for an item: its NutDropCatch variant (any other item: the hazelnut's).</summary>
        public static int CatchVariant(PocketWilds.Simulation.NutDropItemKind kind)
        {
            switch (kind)
            {
                case PocketWilds.Simulation.NutDropItemKind.Acorn:
                    return AcornVariant;
                case PocketWilds.Simulation.NutDropItemKind.Berry:
                    return BerryVariant;
                default:
                    return HazelnutVariant;
            }
        }

        /// <summary>
        /// The streak step for a multiplier (in tenths, 11 for × 1.1 at 5 catches in a row): 0 for the first step, then a
        /// semitone higher each step, the top (7) from × 1.8 on.
        /// </summary>
        public static int StreakVariant(int multiplierTenths)
        {
            int step = multiplierTenths - 11;
            return step <= 0 ? 0 : step >= StreakSteps ? StreakSteps - 1 : step;
        }

        /// <summary>True for the three specials (the wide basket, the feather, the golden shower).</summary>
        public static bool IsSpecial(PocketWilds.Simulation.NutDropItemKind kind) =>
            kind == PocketWilds.Simulation.NutDropItemKind.WideBasket || kind == PocketWilds.Simulation.NutDropItemKind.Feather
                                                                       || kind == PocketWilds.Simulation.NutDropItemKind.GoldenShower;

        /// <summary>
        /// The sound of one nut drop event (doc 01 S-ACT-003 « Sons et effets »). A catch plays its item's pop, the golden
        /// hazelnut its glass chime, a pinecone the felted bonk, a special the breath and chord, a +0.1 of the multiplier
        /// the shared streak step. Whether the game reports a pinecone or a special as a catch, as its own event, or both in
        /// the same frame, it sounds once (the definitions' cooldowns keep the first). Our defaults where the doc is silent:
        /// a spawned item, a missed one (it rolls into the grass: no loss, no sound), the end of a special and the end of
        /// the game (the presenter's chord) are silent. position: the basket.
        /// </summary>
        public static void Play(IAudioService audio, in PocketWilds.Simulation.NutDropEvent e, Vector3 position)
        {
            switch (e.Kind)
            {
                case PocketWilds.Simulation.NutDropEventKind.Caught:
                    if (e.ItemKind == PocketWilds.Simulation.NutDropItemKind.GoldenNut)
                        audio.Play(AudioEventId.NutDropGolden, position, ContentId.None);
                    else if (e.ItemKind == PocketWilds.Simulation.NutDropItemKind.Pinecone)
                        audio.Play(AudioEventId.NutDropPinecone, position, ContentId.None);
                    else if (IsSpecial(e.ItemKind))
                        audio.Play(AudioEventId.NutDropSpecial, position, ContentId.None);
                    else
                        audio.PlayVariant(AudioEventId.NutDropCatch, CatchVariant(e.ItemKind));
                    break;
                case PocketWilds.Simulation.NutDropEventKind.Pinecone:
                    audio.Play(AudioEventId.NutDropPinecone, position, ContentId.None);
                    break;
                case PocketWilds.Simulation.NutDropEventKind.SpecialStarted:
                    audio.Play(AudioEventId.NutDropSpecial, position, ContentId.None);
                    break;
                case PocketWilds.Simulation.NutDropEventKind.MultiplierUp:
                    audio.PlayVariant(AudioEventId.ActivityStreak, StreakVariant(e.MultiplierTenths));
                    break;
            }
        }
    }

    /// <summary>
    /// T41: how a pawprint memory game sounds (doc 01 Contenu, S-ACT-004 « Sons et effets »; doc Audio, J-SFX-ACT-004-01 to
    /// 04). An interface screen: everything plays from the centre. The stage hands every drained game event to
    /// <see cref="Play"/> with the game's PairsFound and PairCount. A card turned over rustles; a pair sounds a third at the
    /// music box that climbs with the pairs found, the last pair on the top F whatever the grid; a miss is one low felted
    /// note; the glow card a warm cup of glass. The end chord stays the presenter's (ActivityEnd), and the presenter keeps
    /// its streak step quiet for this activity (<see cref="ActivityAudio.HasOwnStreakSound"/>). No allocation.
    /// </summary>
    public static class MemoryAudio
    {
        /// <summary>MemoryMatch's clips: the third up the F major scale, F and A (0) to F and A an octave higher (7).</summary>
        public const int MatchSteps = 8;

        /// <summary>
        /// The pair's third for a game's pairsFound-th pair (1 for the first) out of pairCount: the steps spread evenly from
        /// the first (F and A) to the top (an octave higher), so the third climbs through the game and the last pair always
        /// lands on the top: 6 pairs (4 × 3) steps 0, 1, 3, 4, 6, 7; 8 pairs (4 × 4) 0 to 7; 10 pairs (5 × 4) 0, 1, 2, 2, 3,
        /// 4, 5, 5, 6, 7. A game of one pair plays the top.
        /// </summary>
        public static int MatchVariant(int pairsFound, int pairCount)
        {
            int last = pairCount - 1;
            if (last <= 0)
                return MatchSteps - 1;
            int k = pairsFound - 1;
            k = k < 0 ? 0 : k > last ? last : k;
            return (k * (MatchSteps - 1) + last / 2) / last;
        }

        /// <summary>
        /// The sound of one memory event (doc 01 S-ACT-004 « Sons et effets »). A card turned face up rustles (the second
        /// card of a try rustles too, under the pair's third or the miss's felt); a pair plays its third
        /// (<see cref="MatchVariant"/>, the double pair the same: its 200 points tell the difference); a mismatch the low
        /// felted note, whether it counts as a mistake or not (never a punishment). Our defaults where the doc is silent:
        /// the preview turning every card up and back, the two cards of a miss turning back and two cards swapping places
        /// (challenge 6) rustle once per event (cards moving on the cloth); the end bonus and the end of the game are the
        /// presenter's (silent here). pairsFound, pairCount: the
        /// game's PairsFound and PairCount when the events are drained.
        /// </summary>
        public static void Play(IAudioService audio, in PocketWilds.Simulation.MemoryEvent e, int pairsFound, int pairCount)
        {
            switch (e.Kind)
            {
                case PocketWilds.Simulation.MemoryEventKind.Flipped:
                case PocketWilds.Simulation.MemoryEventKind.FlippedBack:
                case PocketWilds.Simulation.MemoryEventKind.PreviewStarted:
                case PocketWilds.Simulation.MemoryEventKind.PreviewEnded:
                case PocketWilds.Simulation.MemoryEventKind.Swapped:
                    audio.Play(AudioEventId.MemoryFlip, Vector3.zero, ContentId.None);
                    break;
                case PocketWilds.Simulation.MemoryEventKind.Matched:
                    audio.PlayVariant(AudioEventId.MemoryMatch, MatchVariant(pairsFound, pairCount));
                    break;
                case PocketWilds.Simulation.MemoryEventKind.Mismatched:
                    audio.Play(AudioEventId.MemoryMiss, Vector3.zero, ContentId.None);
                    break;
                case PocketWilds.Simulation.MemoryEventKind.Glow:
                    audio.Play(AudioEventId.MemoryGlow, Vector3.zero, ContentId.None);
                    break;
            }
        }
    }

    /// <summary>
    /// T42: how the expeditions sound (doc 01 Contenu, S-ACT-005 « Sons et effets »; doc 05 Sensations, sheet 23). An
    /// interface screen: everything plays from the centre (<see cref="Vector3.zero"/>). Where to play them:
    /// - « Partir » pressed and the team gone: <see cref="AudioEventId.ExpeditionDepart"/> (in place of the
    ///   confirm; H-VFX-ACT-005-01's dust puffs go with its steps);
    /// - a slot turns Back while the screen is open (its timer ends, or « Finir maintenant » is paid):
    ///   <see cref="AudioEventId.ExpeditionBack"/>, once per slot (its 1 s cooldown makes slots ending together one bell);
    /// - the welcome starts (« Voir la carte postale »): <see cref="AudioEventId.ExpeditionReturn"/> while the team walks
    ///   in (its steps hurry closer for 0.4 s, its two wooden notes land at 0.6 and 0.7 s);
    /// - the postcard pivots in (400 ms): <see cref="AudioEventId.ExpeditionPostcard"/>, and for each postcard of a
    ///   « Tout réclamer » / "Claim all" (300 ms apart: 0.25 s cooldown, 2 voices);
    /// - each loot item turning over in the result: <see cref="AudioEventId.ExpeditionLoot"/> with
    ///   <see cref="LootVariant"/>. Its first five steps are the reward card's five notes (CardFlip, J-SFX-FEEL-012) an
    ///   octave apart at most, so the card's paper and the pop can sound together.
    /// No allocation.
    /// </summary>
    public static class ExpeditionAudio
    {
        /// <summary>ExpeditionLoot's clips: the pop up the F major pentatonic, F, G, A, C, D, F, G, A (0 to 7).</summary>
        public const int LootSteps = 8;

        /// <summary>The pop of the item-th loot item turned (0 for the first): one step higher per item, the top past the eighth.</summary>
        public static int LootVariant(int item) => item < 0 ? 0 : item >= LootSteps ? LootSteps - 1 : item;

        /// <summary>The pop of one loot item (see <see cref="LootVariant"/>).</summary>
        public static void PlayLoot(IAudioService audio, int item) => audio.PlayVariant(AudioEventId.ExpeditionLoot, LootVariant(item));
    }

    /// <summary>
    /// T43: how the garden sounds (doc 01 Contenu, S-ACT-006 « Sons et effets »; doc 05 Sensations, sheet 24). The garden
    /// set is the overlay's close-up: everything plays from the centre (<see cref="Vector3.zero"/>). Where to play them:
    /// - a seed picked on the seed sheet and planted (the soil puff): <see cref="Plant"/>, in place of the confirm;
    /// - the can tapped then a sprout (the 6 drops of H-VFX-ACT-006-01): <see cref="Water"/>, once per watering (1 s);
    /// - a plant reaching stage 2 or ripe while the garden shows (the green ring of H-VFX-ACT-006-02): <see cref="Stage"/>;
    ///   beds catching up together when the garden opens make one pop (0.15 s cooldown, 2 voices);
    /// - a ripe plant tapped (it hops and flies into the basket, H-VFX-077): <see cref="Harvest"/> with the bed's place in
    ///   the chain: 0 for a single harvest, then 1, 2, ... for each next bed of a « Tout récolter » / "Harvest all" (120 ms
    ///   apart) or a bed harvested within <see cref="ChainSeconds"/> of the one before. Its pop climbs F, G, A, C, D and
    ///   stays on D past the fifth bed (doc 05 section 6.4: a bounded rising run);
    /// - the garden level rises: <see cref="LevelUp"/> (J-SFX-REWARD-002, the mastery motif).
    /// No allocation.
    /// </summary>
    public static class GardenAudio
    {
        /// <summary>GardenPlant's clips: two variants of pats and crumbs.</summary>
        public const int PlantVariants = 2;

        /// <summary>GardenHarvest's clips: three variants of plop and rustle, all with their pop on F.</summary>
        public const int HarvestVariants = 3;

        /// <summary>The harvest's steps up the F major pentatonic (F, G, A, C, D); past the fifth bed the pitch stays on the top.</summary>
        public const int HarvestSteps = 5;

        /// <summary>A bed harvested this soon after the one before continues the chain (our default: the doc gives the rule, not the window).</summary>
        public const float ChainSeconds = 1.5f;

        /// <summary>The step of the chain-th bed in a run (0 for the first): 0 to 4.</summary>
        public static int HarvestStep(int chain) => chain <= 0 ? 0 : chain >= HarvestSteps ? HarvestSteps - 1 : chain;

        /// <summary>The harvest's pitch for the chain-th bed in a run: 0, 2, 4, 7, 9 semitones (F, G, A, C, D).</summary>
        public static float HarvestPitch(int chain)
        {
            switch (HarvestStep(chain))
            {
                case 1:
                    return 1.122462f;
                case 2:
                    return 1.259921f;
                case 3:
                    return 1.498307f;
                case 4:
                    return 1.681793f;
                default:
                    return 1f;
            }
        }

        /// <summary>J-SFX-ACT-006-01: a seed planted (soft soil).</summary>
        public static void Plant(IAudioService audio) => audio.Play(AudioEventId.GardenPlant, Vector3.zero, ContentId.None);

        /// <summary>J-SFX-ACT-006-02: a sprout watered (a thin trickle, 1 s).</summary>
        public static void Water(IAudioService audio) => audio.Play(AudioEventId.GardenWater, Vector3.zero, ContentId.None);

        /// <summary>J-SFX-ACT-006-03: a plant reaches its next stage (a small vegetal pop).</summary>
        public static void Stage(IAudioService audio) => audio.Play(AudioEventId.GardenStage, Vector3.zero, ContentId.None);

        /// <summary>J-SFX-FEEL-017: a harvest, its pop one step higher per chained bed (see <see cref="HarvestPitch"/>).</summary>
        public static void Harvest(IAudioService audio, int chain) =>
            audio.PlayPitched(AudioEventId.GardenHarvest, Vector3.zero, ContentId.None, HarvestPitch(chain));

        /// <summary>J-SFX-REWARD-002: the garden levels up.</summary>
        public static void LevelUp(IAudioService audio) => audio.Play(AudioEventId.GardenLevelUp, Vector3.zero, ContentId.None);
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
        /// T38: plays a sound at a chosen pitch. An animal voice (AnimalHappy, AnimalChatter, AnimalSleep, AnimalEat) takes
        /// its species' own clips and pitch, times pitchScale: the grooming giggle is AnimalHappy at 1.1 (doc 01 Contenu,
        /// S-ACT-001, J-SFX-ACT-001-04), its content sigh AnimalSleep. Any other sound plays at pitchScale (the brush a
        /// little brighter for a quick stroke). A close-up sound: the animal the camera frames, so it plays at full level
        /// from the centre whatever the refuge's view, and the battery saver does not skip it. The default plays the sound
        /// as usual.
        /// </summary>
        void PlayPitched(AudioEventId id, Vector3 position, ContentId context, float pitchScale)
        {
            Play(id, position, context);
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
