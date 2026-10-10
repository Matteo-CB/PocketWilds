using PocketWilds.Core;
using PocketWilds.Simulation;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>T43: the garden overlay's sizes and timings (shared by the presenter, the view and the pipeline).</summary>
    public static class GardenScreenLayout
    {
        /// <summary>The seed sheet's packets: every plant of the catalogue (25), the open ones first.</summary>
        public const int PacketCapacity = 25;

        public const int ChallengeCapacity = 10;

        /// <summary>The harvest's product chips counting up by the basket (main, side, dye, seeds back; merged by item).</summary>
        public const int ProductCapacity = 6;

        /// <summary>The Gems plot's first tap arms it this long.</summary>
        public const float ConfirmSeconds = 3f;

        /// <summary>Doc 05 sheet 24, repeated version: « Tout récolter » picks the beds 120 ms apart.</summary>
        public const float ChainGap = 0.12f;

        /// <summary>Doc 05 sheet 24: anticipation 60 ms (squash 0.90), the hop (Bond, 1.25), then the flight to the basket (450 ms).</summary>
        public const float SquashSeconds = 0.06f;

        public const float HopSeconds = 0.19f;
        public const float FlightSeconds = 0.45f;
        public const float Squash = 0.9f;
        public const float Bond = 1.25f;

        /// <summary>Reduce Motion: the plant fades in place, no flight.</summary>
        public const float FadeSeconds = 0.3f;

        /// <summary>Doc 05 sheet 24: a ripe plant sways (4 degrees, 1.5 Hz).</summary>
        public const float SwayDegrees = 4f;

        public const float SwayHertz = 1.5f;

        /// <summary>A stage reached: the plant pops from this scale back to 1.</summary>
        public const float StagePop = 0.82f;

        public const float StagePopSeconds = 0.22f;

        /// <summary>The seed sheet slides up in this long (a fade with Reduce Motion).</summary>
        public const float SheetSeconds = 0.22f;

        /// <summary>The level-up banner stays this long.</summary>
        public const float BannerSeconds = 2.6f;

        /// <summary>The product chips count up in this long, then stay, then fade.</summary>
        public const float CountSeconds = 0.4f;

        public const float ProductHoldSeconds = 1.6f;
    }

    /// <summary>The overlay's touches (raised by the view; the beds themselves are picked by the presenter).</summary>
    public interface IGardenScreenActions
    {
        void CloseScreen();

        /// <summary>A packet of the seed sheet: plants the chosen bed.</summary>
        void TapPacket(int cell);

        void CloseSeeds();

        /// <summary>The watering can: armed, the next sprout touched is watered.</summary>
        void TapCan();

        /// <summary>« Tout récolter ».</summary>
        void HarvestAll();

        /// <summary>« Finir maintenant » on the selected bed.</summary>
        void SpeedUp();

        /// <summary>A challenge of the « Défis » row: starts it, or stops the one running.</summary>
        void TapChallenge(int row);
    }

    /// <summary>One bed's floating label as shown.</summary>
    public struct GardenBedModel
    {
        public GardenPlotState State;
        public GardenPlotUnlock Unlock;
        public bool Watered;
        public bool Helped;
        public bool Selected;

        /// <summary>The Gems plot's price (400).</summary>
        public long Price;

        /// <summary>The Gems plot's first tap armed it (« Touche encore pour l'ouvrir » under the sign).</summary>
        public bool Armed;
    }

    /// <summary>A seed packet of the sheet.</summary>
    public struct GardenPacketModel
    {
        public bool Shown;

        /// <summary>The plant's runtime id (its packet sprite ICO_Seed_{Pascal}).</summary>
        public string Key;

        public string Name;
        public int Seeds;
        public bool Open;

        /// <summary>A locked plant: « Jardin niveau {n} » or its biome's name.</summary>
        public string Lock;
    }

    /// <summary>The bottom tools: the can, « Tout récolter », « Finir maintenant ».</summary>
    public struct GardenToolsModel
    {
        public bool CanArmed;
        public bool CanUsable;
        public bool HarvestAll;
        public bool SpeedUpShown;
        public int SpeedUpCost;
        public int SpeedUpsLeft;
    }

    /// <summary>One challenge of the « Défis » row.</summary>
    public struct GardenChallengeModel
    {
        public bool Shown;
        public int Index;
        public string Name;
        public string Goal;
        public byte Stars;
        public bool Open;
        public bool Active;
        public int Harvests;
        public int Needed;
    }

    /// <summary>
    /// T43: PF_UI_Garden's view (implemented in Game.UI by GardenScreenView, built by GardenScreenPipeline): the top bar (title,
    /// the level pill with its progress, the Gems, close), one floating label per bed projected from the set camera, the
    /// seed sheet, the can, « Tout récolter », « Finir maintenant », the « Défis » row, the product chips of a harvest, the
    /// level-up banner, a note pill and the tutorial paw. Built once with pooled parts; no call allocates once
    /// <see cref="Bind"/> ran (the timers are written into reused char buffers when their shown text changes). The fonts, the
    /// UI kit's sprites and the scene's icons are borrowed from what the scene already loaded (never copied into the bundle).
    /// </summary>
    public interface IGardenScreenView
    {
        /// <summary>The prefab carries every part; false: rebuild it with GardenScreenPipeline.</summary>
        bool Valid { get; }

        bool Shown { get; }

        bool ReduceMotion { get; set; }

        /// <summary>Once after loading: borrows fonts, kit sprites and icons; reads the templates. Allocates (lookups).</summary>
        void Bind(ILocalization text, IGardenScreenActions actions);

        void Show();

        void Hide();

        void SetGems(long gems);

        /// <summary>The level pill: « Jardin niveau {n} » and its progress (points in the level, points the next one needs; 0 at 10).</summary>
        void SetLevel(int level, int points, int pointsForNext);

        void SetBed(int plot, in GardenBedModel model);

        /// <summary>Per frame for a growing bed: « Prêt dans 25 min », touched only when the shown text changes.</summary>
        void SetBedTime(int plot, double secondsLeft);

        /// <summary>Where a bed's label sits (screen pixels, from the set camera); written when the framing changes.</summary>
        void PlaceBed(int plot, Vector2 screenPoint, bool visible);

        void ShowSeeds(bool shown);

        bool SeedsShown { get; }

        void SetPacket(int cell, in GardenPacketModel model);

        void SetTools(in GardenToolsModel model);

        void SetChallenge(int row, in GardenChallengeModel model);

        /// <summary>A harvested product counts up in its chip by the basket (merged with a chip of the same item still showing).</summary>
        void AddProduct(ContentId item, int count);

        /// <summary>The soft banner « Jardin niveau {n} » with what opened (null: nothing named).</summary>
        void ShowLevelUp(int level, string opened);

        /// <summary>A short line in a soft pill for 2 s.</summary>
        void ShowNote(string text);

        // ---------------- the tutorial paw (first visit) ----------------

        void SetPaw(bool visible, Vector2 screenPoint);

        Vector2 PacketPoint(int cell);

        Vector2 CanPoint { get; }

        /// <summary>Per frame: the sheet, the chips, the banner, the note, the paw. No allocation.</summary>
        void Tick(float deltaTime);
    }
}
