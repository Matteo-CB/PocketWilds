using PocketWilds.Core;
using PocketWilds.Simulation;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>T44A: where the albums screen lives and its sizes (shared by the presenter, the view and the pipeline).</summary>
    public static class AlbumLayout
    {
        public const string ScreenAddress = "albums.screen";
        public const string ScreenPrefabName = "PF_UI_Album";
        public const string PrefabPath = "Assets/_Game/Prefabs/Albums/" + ScreenPrefabName + ".prefab";
        public const string Group = StickerArtCatalog.Group;

        /// <summary>The shared pearl sheen of the Shiny stickers and the shared silhouette of the empty slots.</summary>
        public const string PearlShaderName = "PocketWilds/UIStickerPearl";

        public const string SilhouetteShaderName = "PocketWilds/UIStickerSilhouette";
        public const string PearlShaderPath = "Assets/_Game/Art/Shaders/SH_UI_StickerPearl.shader";
        public const string SilhouetteShaderPath = "Assets/_Game/Art/Shaders/SH_UI_StickerSilhouette.shader";
        public const string PearlMaterialPath = "Assets/_Game/Art/Materials/MAT_UI_StickerPearl.mat";
        public const string SilhouetteMaterialPath = "Assets/_Game/Art/Materials/MAT_UI_StickerSilhouette.mat";

        /// <summary>A global the view sets with Reduce Motion: the pearl sheen stays still.</summary>
        public const string StillProperty = "_PW_StickerStill";

        /// <summary>Over the hub (22), like the expeditions; the reveal is over the result card (30).</summary>
        public const int SortingOrder = 23;

        public const int RevealSortingOrder = 31;

        public const int AlbumCapacity = 16;
        public const int Columns = 3;
        public const int Rows = 4;
        public const int SlotsPerPage = Columns * Rows;

        /// <summary>The 60 pages of the 720 stickers (the choose list has one row per page at most).</summary>
        public const int PageCapacity = 60;

        /// <summary>Gains waiting for their reveal, copied out of the albums (more stay pending for the next batch).</summary>
        public const int RevealQueueCapacity = 32;

        /// <summary>Page atlases kept: the page shown, its two neighbours and the sticker being revealed.</summary>
        public const int AtlasCapacity = 4;

        /// <summary>H-VFX-074: 6 round confetti and a ring; H-VFX-038 Shiny: 8 pearly bubbles; stars of a duplicate (6 on the Albums weekend).</summary>
        public const int Confetti = 6;

        public const int Bubbles = 8;
        public const int FlyingStars = 6;

        /// <summary>The page comes in from the side of the swipe (a fade with Reduce Motion).</summary>
        public const float PageSlideSeconds = 0.18f;

        /// <summary>A drag this far (canvas units) turns the page.</summary>
        public const float SwipeThreshold = 140f;
    }

    /// <summary>Section 1.10: the reveal's beats (seconds), shared by the presenter and the view.</summary>
    public static class StickerRevealTiming
    {
        /// <summary>The pouch opens alone after this long (or at a touch).</summary>
        public const float PouchAutoOpen = 0.8f;

        public const float PouchIn = 0.2f;

        /// <summary>Moment 21: pop with -8 degrees to 0, squash 0.95 for 80 ms, then the flight (500 ms) or the direct paste.</summary>
        public const float Paste = 0.9f;

        public const float PopEnd = 0.25f;
        public const float SquashEnd = 0.33f;
        public const float FlyStart = 0.4f;

        /// <summary>Several stickers in a row: 250 ms apart.</summary>
        public const float Gap = 0.25f;

        /// <summary>The Shiny's face-down pause before it turns over.</summary>
        public const float ShinyPause = 0.2f;

        /// <summary>H-VFX-038: Common adds nothing, Rare 300 ms, Shiny 700 ms.</summary>
        public static float Exit(StickerRarity rarity) => rarity == StickerRarity.Shiny ? 0.7f : rarity == StickerRarity.Rare ? 0.3f : 0f;

        /// <summary>0.9 s, 1.2 s, 1.6 s from the opening.</summary>
        public static float Total(StickerRarity rarity) => Exit(rarity) + Paste;
    }

    public enum AlbumScreenMode : byte
    {
        Shelf = 0,
        Page = 1,

        /// <summary>« Choisir un autocollant »: the open pages with their missing stickers.</summary>
        ChooseList = 2,

        /// <summary>A page whose missing stickers can be bought with 10 stars.</summary>
        ChoosePage = 3,
    }

    public enum StickerSlotState : byte
    {
        /// <summary>Missing: the silhouette and its hint icon.</summary>
        Empty = 0,

        Owned = 1,

        /// <summary>Not available in this build: a soft « bientôt » silhouette.</summary>
        Soon = 2,
    }

    /// <summary>The albums screen's touches (raised by the view, handled by <see cref="AlbumScreenPresenter"/>).</summary>
    public interface IAlbumScreenActions
    {
        void CloseScreen();

        void OpenAlbum(int cover);

        /// <summary>From a page or the choose list back to the shelf (from a choose page back to the choose list).</summary>
        void Back();

        void NextPage();

        void PreviousPage();

        /// <summary>Owned: nothing; empty: its hint; in the choose page a missing one asks « 10 étoiles pour cet autocollant ? ».</summary>
        void TapSlot(int slot);

        void OpenChoose();

        void PickChoicePage(int row);

        void ConfirmChoice();

        void CancelChoice();

        /// <summary>Opens the pouch, or ends the reveal playing.</summary>
        void TapReveal();
    }

    /// <summary>An album cover of the shelf.</summary>
    public struct AlbumCoverModel
    {
        public bool Shown;
        public string Title;
        public AlbumKind Kind;
        public int Owned;
        public int Total;

        /// <summary>At least one open page (else a soft locked cover).</summary>
        public bool Open;

        public bool Complete;

        /// <summary>A sticker not looked at yet in it.</summary>
        public bool New;
    }

    /// <summary>One of the page's 12 slots.</summary>
    public struct AlbumSlotModel
    {
        public StickerSlotState State;
        public StickerRarity Rarity;
        public StickerHint Hint;

        /// <summary>The sticker's art from its page atlas (null: not loaded or not captured, the silhouette shows).</summary>
        public Sprite Art;

        public string Name;

        /// <summary>« Nouveau » (gained, not seen yet): it settles with a little press as the page opens.</summary>
        public bool New;

        /// <summary>The choose page: a missing one with its price.</summary>
        public bool Choosable;
    }

    /// <summary>The page shown: its album and title, « 6/12 » with the half and full rewards.</summary>
    public struct AlbumPageModel
    {
        public string Album;
        public string Title;
        public int Owned;
        public int Total;
        public bool HasPrevious;
        public bool HasNext;
        public bool Choosing;
    }

    /// <summary>A row of the choose list: an open page with stickers to find.</summary>
    public struct AlbumChoiceRowModel
    {
        public bool Shown;
        public string Title;
        public int Missing;
        public int Owned;
    }

    /// <summary>One sticker's reveal (section 1.10).</summary>
    public struct StickerRevealModel
    {
        public StickerRarity Rarity;
        public Sprite Art;
        public string Name;
        public bool New;

        /// <summary>A duplicate: « Déjà collé : +{n} étoiles » and n stars flying to the counter.</summary>
        public int Stars;

        /// <summary>« Page à moitié pleine », « Page complète ! », « Album complet ! » (null: none).</summary>
        public string Banner;

        /// <summary>The page is open: pasted straight onto its slot (else it flies to the albums icon).</summary>
        public bool Direct;

        /// <summary>Where the sticker goes and where the stars go, in screen pixels.</summary>
        public Vector2 Target;

        public Vector2 StarsTarget;

        /// <summary>The first and the last of a batch get the full effect (confetti and ring).</summary>
        public bool Full;
    }

    /// <summary>
    /// T44A: PF_UI_Album's view (implemented in Game.UI by AlbumScreenView, built by AlbumScreenPipeline): the shelf of 16
    /// covers with « Choisir un autocollant », a page of 12 recycled slots with its « 6/12 » bar and the swipe, the choose
    /// list and its confirmation, a note pill, and the reveal layer on its own canvas (it also plays over the refuge with
    /// the screen part hidden). Built once with pooled parts; nothing is instantiated after, and no call allocates once
    /// <see cref="Bind"/> ran. The UI kit's sprites, the icons and the fonts are borrowed from what the scene loaded.
    /// </summary>
    public interface IAlbumScreenView
    {
        bool Valid { get; }

        bool Shown { get; }

        bool ReduceMotion { get; set; }

        /// <summary>The captured sticker art the prefab carries (DEF_StickerArt; null: every sticker shows its silhouette).</summary>
        StickerArtCatalog ArtCatalog { get; }

        /// <summary>Once after loading: borrows fonts, kit sprites and icons; reads the templates. Allocates (lookups).</summary>
        void Bind(ILocalization text, IAlbumScreenActions actions);

        void Show();

        void Hide();

        /// <summary>The screen part (false: only the reveal layer, over the refuge).</summary>
        void SetScreenShown(bool shown);

        bool ScreenShown { get; }

        void SetMode(AlbumScreenMode mode);

        void SetStars(int stars, int price, bool canChoose);

        void SetCover(int cover, in AlbumCoverModel model);

        void SetPage(in AlbumPageModel model);

        void SetSlot(int slot, in AlbumSlotModel model);

        /// <summary>The page just changed: it comes in from the side of <paramref name="direction"/> (+1 next, -1 previous).</summary>
        void SlidePage(int direction);

        void SetChoiceRow(int row, in AlbumChoiceRowModel model);

        void ShowConfirm(bool shown, in AlbumSlotModel model, int price);

        bool ConfirmShown { get; }

        /// <summary>A short line in a soft pill at the bottom for 2 s (a hint, a refusal).</summary>
        void ShowNote(string text);

        Vector2 SlotScreenPosition(int slot);

        Vector2 StarsScreenPosition { get; }

        Vector2 TitleScreenPosition { get; }

        // ---------------- the reveal (section 1.10) ----------------

        void ShowReveal(in StickerRevealModel model);

        /// <summary>Per frame: the pouch's age, whether it opened and the time since it opened.</summary>
        void SetRevealTime(float pouchAge, bool opened, float sinceOpen);

        void HideReveal();

        bool RevealShown { get; }

        void Tick(float deltaTime);
    }

    /// <summary>T44A: the Albums entry of the refuge HUD (RefugeHud), a round button with ICO_Album.</summary>
    public interface IAlbumHud
    {
        void BindAlbums(IAlbumHudActions actions);

        /// <summary>Shown once the albums open (refuge level 12 and day 5); the dot while a sticker waits to be seen.</summary>
        void SetAlbums(bool visible, bool dot);

        bool AlbumsVisible { get; }

        /// <summary>Where gained stickers fly when the screen is closed (screen pixels).</summary>
        Vector2 AlbumsScreenPosition { get; }
    }

    public interface IAlbumHudActions
    {
        void OpenAlbums();
    }

    /// <summary>T44A's text keys (Editor AlbumTexts holds the shipped texts). Names: « {key}.name » of albums, pages, stickers.</summary>
    public static class AlbumText
    {
        public const string Title = "album.title";
        public const string Stars = "album.stars";
        public const string Choose = "album.choose";
        public const string ChooseConfirm = "album.choose.confirm";
        public const string New = "album.new";

        /// <summary>« Déjà collé : +{0} étoiles », counted (« .one », « .other »).</summary>
        public const string Duplicate = "album.duplicate";

        public const string PageHalf = "album.page.half";
        public const string PageComplete = "album.page.complete";
        public const string AlbumComplete = "album.album.complete";
        public const string Companion = "album.companion";
        public const string Odds = "album.info.odds";

        // The screen's own words.
        public const string Common = "album.rarity.common";
        public const string Rare = "album.rarity.rare";
        public const string Shiny = "album.rarity.shiny";
        public const string Soon = "album.soon";
        public const string Yes = "album.confirm.yes";
        public const string No = "album.confirm.no";

        /// <summary>« {n} à trouver », counted.</summary>
        public const string Missing = "album.choose.missing";

        public const string NotEnoughStars = "album.choose.not_enough";
        public const string ChooseHint = "album.choose.hint";
        public const string Locked = "album.locked";
        public const string Closed = "album.closed";

        public static string Rarity(StickerRarity rarity) => rarity == StickerRarity.Shiny ? Shiny : rarity == StickerRarity.Rare ? Rare : Common;

        /// <summary>The hint of an empty slot's source (section 1.13).</summary>
        public static string Hint(StickerHint hint) => hint switch
        {
            StickerHint.Activity => "album.hint.activity",
            StickerHint.Record => "album.hint.record",
            StickerHint.Challenge => "album.hint.challenge",
            StickerHint.Wish => "album.hint.wish",
            StickerHint.Expedition => "album.hint.expedition",
            StickerHint.Gift => "album.hint.gift",
            StickerHint.Event => "album.hint.event",
            StickerHint.Visitor => "album.hint.visitor",
            StickerHint.Garden => "album.hint.garden",
            StickerHint.Zone => "album.hint.zone",
            _ => "album.hint.world",
        };

        public static string Name(string key) => key + ".name";
    }
}
