using PocketWilds.Core;
using PocketWilds.Simulation;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>T42: where the expeditions screen lives and its sizes (shared by the presenter, the view and the pipeline).</summary>
    public static class ExpeditionLayout
    {
        public const string ScreenAddress = "activity.expeditions.screen";
        public const string ScreenPrefabName = "PF_UI_Expeditions";
        public const string PrefabPath = "Assets/_Game/Prefabs/Activities/" + ScreenPrefabName + ".prefab";

        /// <summary>Agent EA's sprites; the destination cards' art is in <see cref="PostcardLayout.CardArtFolder"/>.</summary>
        public const string SpriteFolder = "Assets/_Game/Art/UI/Activities/Expeditions";

        public const string Group = PostcardLayout.Group;

        /// <summary>Over the hub (22), under the result card (30) that shows the loot over the postcard.</summary>
        public const int SortingOrder = 23;

        public const int SlotCapacity = 4;
        public const int MaxTeam = 3;

        /// <summary>The map's cards: the 13 biome destinations and a few event ones while their event runs.</summary>
        public const int DestinationCapacity = 16;

        /// <summary>The team picker's pooled portraits (free animals; the most suited first).</summary>
        public const int AnimalCapacity = 40;

        public const int DurationCount = 4;
        public const int LootCapacity = 7;
        public const int ChallengeCapacity = 10;

        /// <summary>The postcards of « Tout réclamer » (one per slot).</summary>
        public const int PostcardCapacity = SlotCapacity;

        /// <summary>Doc 05 sheet 23: the postcard pivots in over 400 ms.</summary>
        public const float PostcardPivotSeconds = 0.4f;

        /// <summary>Doc 05 sheet 23, repeated version: one card per expedition, 300 ms apart.</summary>
        public const float ClaimAllGap = 0.3f;

        /// <summary>H-VFX-ACT-005-02: a soft ring and 6 round petals.</summary>
        public const int Petals = 6;

        /// <summary>H-VFX-ACT-005-01: 4 soft dust dots at the team's feet on the slot card.</summary>
        public const int DustDots = 4;

        /// <summary>The planning sheet slides up in this long (a fade with Reduce Motion).</summary>
        public const float SheetSeconds = 0.22f;

        /// <summary>The screen fades aside for the welcome (the world shows), and back.</summary>
        public const float FadeSeconds = 0.25f;
    }

    /// <summary>The expeditions screen's touches (raised by the view, handled by <see cref="ExpeditionScreenPresenter"/>).</summary>
    public interface IExpeditionScreenActions
    {
        void CloseScreen();

        /// <summary>Empty: the planning sheet. Back: the welcome. Locked: the 800 Gems slot or the Wild Club.</summary>
        void TapSlot(int slot);

        void SpeedUp(int slot);

        void TapDestination(int card);

        void TapAnimal(int cell);

        void SuggestTeam();

        void PickDuration(int duration);

        void Go();

        void CloseSheet();

        void ClaimAll();

        void TapChallenge(int challenge);
    }

    /// <summary>One slot card as shown.</summary>
    public struct ExpeditionSlotModel
    {
        public ExpeditionSlotState State;
        public ExpeditionSlotUnlock Unlock;
        public int TeamSize;
        public uint Variant0;
        public uint Variant1;
        public uint Variant2;
        public uint Species0;
        public uint Species1;
        public uint Species2;

        /// <summary>The destination's name (away or back).</summary>
        public string Destination;

        public byte Stars;
        public int SpeedUpCost;

        /// <summary>Speed-ups left today (« Finir maintenant » greys out at 0).</summary>
        public int SpeedUpsLeft;

        /// <summary>The Gems slot's price.</summary>
        public long Price;

        public uint VariantAt(int index) => index == 0 ? Variant0 : index == 1 ? Variant1 : Variant2;

        public uint SpeciesAt(int index) => index == 0 ? Species0 : index == 1 ? Species1 : Species2;
    }

    /// <summary>A destination card of the map: its art (EXP_{suffix}), its name, open or a soft locked silhouette.</summary>
    public struct ExpeditionDestinationModel
    {
        public bool Shown;
        public string Key;
        public string Name;
        public bool Open;
        public bool Selected;
    }

    /// <summary>A free animal of the team picker.</summary>
    public struct ExpeditionAnimalModel
    {
        public bool Shown;
        public uint Instance;
        public uint Variant;
        public uint Species;
        public bool Picked;

        /// <summary>From the destination's biome (+10, a small leaf).</summary>
        public bool FromBiome;

        public bool Favorite;
    }

    /// <summary>The team rating with its reasons (doc 01, section 7.2), live while the team is picked.</summary>
    public struct ExpeditionRatingModel
    {
        public int TeamSize;
        public int Points;
        public byte Stars;
        public byte FromBiome;
        public bool FavoriteFamily;
        public bool ThreeSpecies;
    }

    /// <summary>The planning sheet: destination, team, durations, loot preview, « Partir ».</summary>
    public struct ExpeditionSheetModel
    {
        public string Title;
        public string Destination;

        /// <summary>A line under the durations: the challenge's goal, or the Discovery egg guarantee (null: none).</summary>
        public string Note;

        /// <summary>The account's first expedition: one duration, « Première balade : 3 min ».</summary>
        public bool FirstStroll;

        public int Duration;

        /// <summary>A challenge or the first stroll fixes the duration (the other chips hide).</summary>
        public bool DurationLocked;

        public bool CanGo;

        /// <summary>The challenge played (−1: none).</summary>
        public int Challenge;
    }

    /// <summary>One challenge of the « Défis » row.</summary>
    public struct ExpeditionChallengeModel
    {
        public bool Shown;
        public int Index;
        public string Name;
        public string Goal;
        public byte Stars;
        public bool Selected;
    }

    /// <summary>
    /// T42: PF_UI_Expeditions' view (implemented in Game.UI by ExpeditionScreenView, built by ExpeditionPipeline): the title
    /// and close, the Gems, the map card with its destination cards, the 4 slot cards, the « Défis » row, « Tout réclamer »,
    /// the planning sheet, the welcome's postcards, the tutorial paw. Built once with pooled parts; nothing is instantiated
    /// after, and no call allocates once <see cref="Bind"/> ran (timers are written into reused char buffers when their
    /// shown text changes). Portraits, the UI kit's sprites and the fonts are borrowed from what the scene already loaded
    /// (never copied into the screen's bundle: CommonContentTests).
    /// </summary>
    public interface IExpeditionScreenView
    {
        /// <summary>The prefab carries every part; false: rebuild it with ExpeditionPipeline.</summary>
        bool Valid { get; }

        bool Shown { get; }

        bool ReduceMotion { get; set; }

        /// <summary>Once after loading: borrows fonts, kit sprites and portraits; reads the templates. Allocates (lookups).</summary>
        void Bind(ILocalization text, IExpeditionScreenActions actions);

        void Show();

        void Hide();

        void SetGems(long gems);

        void SetSlot(int slot, in ExpeditionSlotModel model);

        /// <summary>Per frame for a team away: « De retour dans 3 h 12 » and the ring, touched only when the shown text changes.</summary>
        void SetSlotTime(int slot, double secondsLeft, double totalSeconds);

        /// <summary>H-VFX-ACT-005-01: 4 soft dust dots at the team's feet on the slot card.</summary>
        void PlayDeparture(int slot);

        void SetDestination(int card, in ExpeditionDestinationModel model);

        void SetChallenge(int index, in ExpeditionChallengeModel model);

        /// <summary>« Tout réclamer » (2 teams back or more).</summary>
        void SetClaimAll(bool shown);

        /// <summary>A short line in a soft pill at the bottom for 2 s (the refuge's toasts are under the screen).</summary>
        void ShowNote(string text);

        void ShowSheet(bool shown);

        bool SheetShown { get; }

        void SetSheet(in ExpeditionSheetModel model);

        void SetAnimal(int cell, in ExpeditionAnimalModel model);

        void SetRating(in ExpeditionRatingModel model);

        /// <summary>A duration chip: its label (« 4 h », « 3 h » on the weekend), shown, selected.</summary>
        void SetDuration(int index, string label, bool shown, bool selected);

        /// <summary>What the team would bring back (chances in percent).</summary>
        void SetLoot(in ExpeditionLoot preview);

        // ---------------- the welcome (doc 05 sheet 23) ----------------

        /// <summary>The screen fades aside (false: the world shows) or back (true).</summary>
        void SetContentShown(bool shown);

        /// <summary>A soft dim over the world behind the postcards.</summary>
        void SetDim(bool dimmed);

        /// <summary>A postcard pivots in (400 ms) after <paramref name="delay"/> s with its soft ring and 6 round petals.</summary>
        void ShowPostcard(int card, int count, string destination, float delay);

        /// <summary>The postcard's picture once rendered (null: the soft paper placeholder).</summary>
        void SetPostcardPicture(int card, Texture picture);

        /// <summary>The postcards move up out of the result card's way.</summary>
        void DockPostcards(bool docked);

        void HidePostcards();

        bool PostcardShown(int card);

        // ---------------- the tutorial paw (A18, A19) ----------------

        void SetPaw(bool visible, Vector2 screenPoint);

        Vector2 SlotPoint(int slot);

        Vector2 DestinationPoint(int card);

        Vector2 AnimalPoint(int cell);

        Vector2 GoPoint { get; }

        /// <summary>Per frame: sheet slide, fades, the postcards' pivots, petals and ring, dust, glows, the paw. No allocation.</summary>
        void Tick(float deltaTime);
    }
}
