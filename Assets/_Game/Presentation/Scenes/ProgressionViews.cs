using PocketWilds.Core;
using PocketWilds.Simulation;

namespace PocketWilds.Presentation
{
    // T35A screens (doc 02 Améliorations, section 11). Texts arrive localized: views never read the localization
    // tables themselves, except for their own fixed labels. Presentation never references UI types.

    /// <summary>How an upgrade card reads (section 11.2). Values are runtime only.</summary>
    public enum UpgradeCardState : byte
    {
        Affordable = 0,

        /// <summary>Not enough Light: the price in a soft color, never red.</summary>
        NotEnough = 1,
        Maxed = 2,

        /// <summary>Waits for a biome mastery level ("Meadow mastery 2").</summary>
        Locked = 3,
    }

    /// <summary>One card of the Upgrades screen, ready to display.</summary>
    public struct UpgradeCardModel
    {
        public UpgradeKind Kind;
        public string Name;
        public int Level;

        /// <summary>What this purchase brings: "+12% Light", "Eggs −4%", "Variants +1%".</summary>
        public string Gain;

        /// <summary>Second line: the biome's new production for Production, the total effect for the others.</summary>
        public string Detail;

        /// <summary>Total price of the levels the button buys (×1, ×10 or Max).</summary>
        public BigValue Price;
        public int Levels;
        public UpgradeCardState State;

        /// <summary>"Meadow mastery 2" when locked.</summary>
        public string LockText;

        /// <summary>"Best pick" ribbon (shortest payback among affordable Production and Cozy Nests cards).</summary>
        public bool Best;
    }

    /// <summary>The block under the cards: the biome's mastery and the next zone to open.</summary>
    public struct UpgradesFooter
    {
        public string MasteryTitle;
        public int MasteryLevel;
        public float MasteryFraction;

        /// <summary>"Level 4: Cozy Nests".</summary>
        public string MasteryNext;

        public bool HasZone;

        /// <summary>The zone's name, capitalized ("La Prairie fleurie"), under a "Next zone" label.</summary>
        public string ZoneTitle;
        public BigValue ZonePrice;

        /// <summary>Why it cannot open yet (empty when it can).</summary>
        public string ZoneCondition;
        public bool ZoneAffordable;
    }

    /// <summary>What the Upgrades screen reads. Implemented by <see cref="ProgressionPresenter"/>.</summary>
    public interface IUpgradesModel
    {
        PurchaseQty Qty { get; }

        /// <summary>The refuge's whole production per second (header).</summary>
        BigValue Rate { get; }

        string BiomeName { get; }
        int CardCount { get; }
        UpgradeCardModel GetCard(int index);
        UpgradesFooter Footer { get; }
    }

    public interface IUpgradesActions
    {
        void SetQty(PurchaseQty qty);
        void BuyUpgrade(int card);

        /// <summary>"See": closes the screen and shows the next zone's sign in the world.</summary>
        void ShowNextZone();
        void CloseUpgrades();
    }

    /// <summary>PF_UI_Upgrades (UI assembly): bottom panel over two thirds of the screen, the refuge stays visible above.</summary>
    public interface IUpgradesView
    {
        bool IsOpen { get; }
        void Open(IUpgradesModel model, ILocalization text, IUpgradesActions actions);

        /// <summary>The model changed (a purchase, the Light crossed a price, the quantity changed).</summary>
        void Refresh();

        /// <summary>A purchase landed on a card: squash and bounce, round motes, the rate counts up (milestone: a ring).</summary>
        void PlayPurchase(int card, bool milestone);

        /// <summary>A card's button was pressed without enough Light: the card shakes softly.</summary>
        void PlayNotEnough(int card);
        void Close();
        void Tick(float unscaledDeltaTime);

        /// <summary>Builds the screen once at scene entry, invisibly (first open never hitches, T28 lesson).</summary>
        void Prewarm(IUpgradesModel model, ILocalization text, IUpgradesActions actions);
    }

    /// <summary>The Open a zone window (section 11.3), texts localized.</summary>
    public struct ZoneOpenData
    {
        public string Title;
        public string GainProduction;
        public string GainAnimals;
        public string GainDecor;
        public BigValue Price;
        public bool CanOpen;

        /// <summary>Empty when it can open; otherwise what is missing ("Meadow mastery 4 first").</summary>
        public string Condition;

        /// <summary>Progress toward the missing condition (mastery or Light), 0 to 1.</summary>
        public float ConditionFraction;

        /// <summary>A capture of the zone made by the capture pipeline (null: the sign icon stands in).</summary>
        public UnityEngine.Sprite Capture;
    }

    public interface IZoneOpenActions
    {
        void OpenZone();
        void ZoneWindowClosed();
    }

    /// <summary>PF_UI_ZoneOpen (UI assembly): a pooled modal over the refuge.</summary>
    public interface IZoneOpenView
    {
        bool IsOpen { get; }
        void Open(in ZoneOpenData data, ILocalization text, IZoneOpenActions actions);

        /// <summary>The Light changed while open: price color and button state follow.</summary>
        void Refresh(in ZoneOpenData data);
        void Close();
        void Tick(float unscaledDeltaTime);
        void Prewarm(ILocalization text);
    }

    /// <summary>One line of the Refuge level screen's reward track.</summary>
    public struct RefugeTrackEntry
    {
        public int Level;
        public string Reward;
        public bool Reached;
    }

    /// <summary>The Refuge level screen (section 11.4), texts localized.</summary>
    public struct RefugeLevelData
    {
        public const int MaxSources = 7;
        public const int MaxTrack = 6;

        public int Level;
        public float Fraction;

        /// <summary>"340 / 420".</summary>
        public string Score;
        public int SourceCount;
        public string[] SourceNames;
        public long[] SourceValues;
        public int TrackCount;
        public RefugeTrackEntry[] Track;
    }

    public interface IRefugeLevelActions
    {
        void CloseRefugeLevel();
    }

    /// <summary>PF_UI_RefugeLevel (UI assembly).</summary>
    public interface IRefugeLevelView
    {
        bool IsOpen { get; }
        void Open(in RefugeLevelData data, ILocalization text, IRefugeLevelActions actions);
        void Close();
        void Tick(float unscaledDeltaTime);
        void Prewarm(ILocalization text);
    }

    /// <summary>The small card of a refuge level's rewards (section 6.3), claimed with one touch.</summary>
    public struct LevelRewardData
    {
        public int Level;
        public string Title;
        public long Gems;

        /// <summary>Functions that opened with these levels ("Shop"), one per line; may be empty.</summary>
        public string Unlocks;
    }

    public interface ILevelRewardActions
    {
        void ClaimLevelRewards();
    }

    /// <summary>PF_UI_LevelReward (UI assembly).</summary>
    public interface ILevelRewardView
    {
        bool IsOpen { get; }
        void Open(in LevelRewardData data, ILocalization text, ILevelRewardActions actions);
        void Close();
        void Tick(float unscaledDeltaTime);
        void Prewarm(ILocalization text);
    }

    /// <summary>One condition of a biome gate with its check.</summary>
    public struct GateConditionLine
    {
        public string Text;
        public bool Met;
    }

    /// <summary>The Open a biome window (section 11.5), texts localized.</summary>
    public struct BiomeGateData
    {
        public const int MaxConditions = 5;

        public string Title;

        /// <summary>The biome has no content in this build: "Coming soon", no price, no button.</summary>
        public bool Soon;
        public int ConditionCount;
        public GateConditionLine[] Conditions;
        public BigValue Price;
        public bool CanOpen;
    }

    public interface IBiomeGateActions
    {
        void OpenBiome();
        void BiomeGateClosed();
    }

    /// <summary>PF_UI_BiomeGate (UI assembly).</summary>
    public interface IBiomeGateView
    {
        bool IsOpen { get; }
        void Open(in BiomeGateData data, ILocalization text, IBiomeGateActions actions);
        void Close();
        void Tick(float unscaledDeltaTime);
        void Prewarm(ILocalization text);
    }

    /// <summary>The HUD's T35A parts (UI assembly): the Upgrades button with its dot, the refuge level badge.</summary>
    public interface IProgressionHud
    {
        void BindProgression(IProgressionHudActions actions);

        /// <summary>The Upgrades button (after the third hatch) and its dot (section 3.6).</summary>
        void SetUpgrade(bool visible, bool dot);

        /// <summary>The tutorial paw taps on the Upgrades button (once, until the first upgrade).</summary>
        void SetUpgradeHint(bool visible);

        void SetRefugeLevel(int level, float fraction);

        /// <summary>The badge grows and a ring of paws spreads from it (1.2 s).</summary>
        void PlayRefugeLevelUp();

        /// <summary>A dot on the badge while a level's reward card waits for a calm moment.</summary>
        void SetLevelReward(bool pending);

        bool UpgradeVisible { get; }
    }

    public interface IProgressionHudActions
    {
        void OpenUpgrades();
        void OpenRefugeLevel();
    }
}
