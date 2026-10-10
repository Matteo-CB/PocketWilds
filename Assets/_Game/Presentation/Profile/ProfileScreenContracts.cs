using PocketWilds.Core;
using PocketWilds.Simulation;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>T44B: where the profile screen lives and its sizes (shared by the presenter, the view and the pipeline).</summary>
    public static class ProfileLayout
    {
        public const string ScreenAddress = "profile.screen";
        public const string ScreenPrefabName = "PF_UI_Achievements";
        public const string PrefabPath = "Assets/_Game/Prefabs/Profile/" + ScreenPrefabName + ".prefab";
        public const string Group = "Profile";

        /// <summary>The screen's own art (agent BA): the shared ring, the frame motifs, the four badge shapes.</summary>
        public const string ArtFolder = "Assets/_Game/Art/UI/Profile";

        public const string FramesFolder = ArtFolder + "/Frames";
        public const string BadgesFolder = ArtFolder + "/Badges";
        public const string FramesAtlasPath = ArtFolder + "/Atlas_Frames.spriteatlasv2";

        /// <summary>The one white SDF ring every frame tints (agent BA); the kit's soft ring stands in until it is drawn.</summary>
        public const string RingSpriteName = "UI_FrameRing";

        /// <summary>The 4 shared badge shapes (agent BA); a round kit shape (and the round five-point star) stand in.</summary>
        public static readonly string[] BadgeShapeNames = { "UI_Badge_Bronze", "UI_Badge_Silver", "UI_Badge_Gold", "UI_Badge_Star" };

        /// <summary>Over the hub (22), like the albums.</summary>
        public const int SortingOrder = 23;

        /// <summary>« Tout » then the 13 categories.</summary>
        public const int Chips = 14;

        public const int Categories = 13;

        /// <summary>The badge grid: 2 columns, 6 pooled rows (12 cells for 200 achievements, recycled while scrolling).</summary>
        public const int BadgeColumns = 2;

        public const int BadgePoolRows = 6;
        public const int BadgeCells = BadgeColumns * BadgePoolRows;

        /// <summary>The titles and poses list: one column, 12 pooled rows shared by both tabs.</summary>
        public const int RowCells = 12;

        /// <summary>The frames gallery: 3 columns, 6 pooled rows.</summary>
        public const int FrameColumns = 3;

        public const int FramePoolRows = 6;
        public const int FrameCells = FrameColumns * FramePoolRows;

        /// <summary>Section 4: 1 to 6 motifs on fixed points of the ring.</summary>
        public const int MotifPoints = 6;

        /// <summary>The claimed badge's little pop.</summary>
        public const float ClaimPopSeconds = 0.35f;

        /// <summary>Section 1 « Paliers et récompenses »: Gems for Bronze, Silver, Gold, Star.</summary>
        public static int Gems(AchievementTier tier) => tier switch
        {
            AchievementTier.Bronze => 5,
            AchievementTier.Silver => 10,
            AchievementTier.Gold => 20,
            AchievementTier.Star => 50,
            _ => 0,
        };

        /// <summary>The Gems of the tiers after <paramref name="claimed"/> up to <paramref name="reached"/>.</summary>
        public static int GemsBetween(AchievementTier claimed, AchievementTier reached)
        {
            int gems = 0;
            for (int t = (int)claimed + 1; t <= (int)reached; t++)
                gems += Gems((AchievementTier)t);
            return gems;
        }
    }

    /// <summary>Section 4: the ring's fixed points, in the order of the motif arrays.</summary>
    public enum FramePoint : byte
    {
        Top = 0,
        Bottom = 1,
        TopLeft = 2,
        TopRight = 3,
        BottomLeft = 4,
        BottomRight = 5,
    }

    public enum ProfileTab : byte
    {
        Achievements = 0,
        Titles = 1,
        Frames = 2,
        Poses = 3,
    }

    /// <summary>The three recycled lists (titles and poses share the row list).</summary>
    public enum ProfileList : byte
    {
        Badges = 0,
        Rows = 1,
        Frames = 2,
    }

    /// <summary>The profile screen's touches (raised by the view, handled by <see cref="ProfileScreenPresenter"/>).</summary>
    public interface IProfileScreenActions
    {
        void CloseScreen();

        void SelectTab(ProfileTab tab);

        /// <summary>0: « Tout »; 1 to 13: the category (chip - 1).</summary>
        void SelectCategory(int chip);

        /// <summary>A recycled cell now shows <paramref name="item"/> of <paramref name="list"/>: the presenter fills it.</summary>
        void FillCell(ProfileList list, int cell, int item);

        /// <summary>A badge: its goal; a title or a frame: selected when owned, its source when locked; a pose: « Mode photo bientôt ».</summary>
        void TapCell(ProfileList list, int item);

        /// <summary>The badge's claim button: every reached tier of the achievement shown at <paramref name="item"/>.</summary>
        void Claim(int item);
    }

    /// <summary>The profile card: the favorite animal's codex portrait in the selected frame, the selected title under it.</summary>
    public struct ProfileCardModel
    {
        public Sprite Portrait;

        /// <summary>The favorite animal's variant: the view finds its codex portrait when <see cref="Portrait"/> is null.</summary>
        public ContentId PortraitVariant;

        /// <summary>frame.… (null: the plain cream ring).</summary>
        public string FrameKey;

        public string Title;
        public string FrameName;
    }

    /// <summary>A filter chip: « Tout » or a category, with a dot while one of its tiers waits to be claimed.</summary>
    public struct ProfileChipModel
    {
        public bool Shown;
        public string Label;
        public bool All;
        public AchievementCategory Category;
        public bool Selected;
        public bool Dot;
    }

    /// <summary>One badge of the grid (section 1 « Affichage »).</summary>
    public struct AchievementCellModel
    {
        public bool Shown;
        public string Name;

        /// <summary>The goal line with {n}: the next tier's threshold (<see cref="Next"/>).</summary>
        public string Goal;

        public long Next;

        /// <summary>« Plus que {n} » toward the next tier.</summary>
        public long Left;

        public float Fill;
        public AchievementCategory Category;

        /// <summary>The shape shown: the highest tier reached (None: a soft locked Bronze).</summary>
        public AchievementTier Reached;

        public bool Claimable;

        /// <summary>The claim's Gems (every reached tier not claimed yet).</summary>
        public int Gems;

        /// <summary>The Star tier reached and claimed.</summary>
        public bool Complete;
    }

    /// <summary>A row of the titles or poses list.</summary>
    public struct ProfileRowModel
    {
        public bool Shown;
        public ProfileItemKind Kind;

        /// <summary>The « Aucun titre » row at the top of the titles.</summary>
        public bool None;

        public string Name;

        /// <summary>How to earn it (shown on every row; locked rows are soft).</summary>
        public string Source;

        public bool Owned;
        public bool Selected;
        public bool New;
    }

    /// <summary>A frame of the gallery around the portrait.</summary>
    public struct ProfileFrameCellModel
    {
        public bool Shown;
        public string FrameKey;
        public string Name;
        public bool Owned;
        public bool Selected;
        public bool New;
    }

    /// <summary>
    /// T44B: PF_UI_Achievements' view (implemented in Game.UI by ProfileScreenView, built by ProfileScreenPipeline): the profile
    /// card, four tabs (Succès with its category chips and a recycled badge grid, Titres, Cadres, Poses), a note pill. Built
    /// once with pooled cells (12 badges for 200 achievements, 12 rows, 18 frames): nothing is instantiated after, and no call
    /// allocates once <see cref="Bind"/> ran. The UI kit's sprites, the icons and the fonts are borrowed from the scene.
    /// </summary>
    public interface IProfileScreenView
    {
        bool Valid { get; }

        bool Shown { get; }

        bool ReduceMotion { get; set; }

        /// <summary>Once after loading: borrows fonts, kit sprites and icons; reads the frame styles. Allocates (lookups).</summary>
        void Bind(ILocalization text, IProfileScreenActions actions);

        void Show();

        void Hide();

        void SetTab(ProfileTab tab);

        ProfileTab TabShown { get; }

        void SetTabDots(bool achievements, bool titles, bool frames, bool poses);

        void SetCard(in ProfileCardModel model);

        void SetChip(int chip, in ProfileChipModel model);

        /// <summary>The list's length: its content is sized and the visible cells ask for their items (back to the top when <paramref name="toTop"/>).</summary>
        void SetListCount(ProfileList list, int count, bool toTop);

        /// <summary>The visible cells of the list ask for their items again.</summary>
        void RefreshList(ProfileList list);

        void SetBadge(int cell, in AchievementCellModel model);

        void SetRow(int cell, in ProfileRowModel model);

        void SetFrameCell(int cell, in ProfileFrameCellModel model);

        /// <summary>The cell showing <paramref name="item"/> (-1: scrolled away).</summary>
        int CellOf(ProfileList list, int item);

        /// <summary>The claimed badge pops (when it is on screen).</summary>
        void PlayClaim(int item);

        /// <summary>A short line in a soft pill at the bottom for 2 s (a source, a refusal, the Gems claimed).</summary>
        void ShowNote(string text);

        void Tick(float deltaTime);
    }

    /// <summary>T44B: the Succès entry of the refuge HUD (RefugeHud), a round button with ICO_Achievement above the Albums button.</summary>
    public interface IProfileHud
    {
        void BindProfile(IProfileHudActions actions);

        /// <summary>The dot while a tier waits to be claimed (<see cref="IAchievements.Claimable"/>).</summary>
        void SetProfile(bool visible, bool dot);

        bool ProfileVisible { get; }
    }

    public interface IProfileHudActions
    {
        void OpenProfile();
    }

    /// <summary>T44B's text keys (Editor ProfileScreenTexts holds the shipped texts). Names: « {key}.name », « {key}.goal », « {key}.source ».</summary>
    public static class ProfileText
    {
        public const string Title = "profile.title";
        public const string TabAchievements = "profile.tab.achievements";
        public const string TabTitles = "profile.tab.titles";
        public const string TabFrames = "profile.tab.frames";
        public const string TabPoses = "profile.tab.poses";
        public const string All = "profile.all";
        public const string Claim = "profile.claim";

        /// <summary>« Plus que {n} », counted.</summary>
        public const string ToGo = "profile.to_go";

        public const string Complete = "profile.complete";

        /// <summary>« +{n} Gemmes », counted.</summary>
        public const string Claimed = "profile.claimed";

        public const string New = "profile.new";
        public const string NoTitle = "profile.no_title";
        public const string NoTitleSource = "profile.no_title.source";
        public const string PlainFrame = "profile.frame.plain";
        public const string Locked = "profile.locked";
        public const string PhotoSoon = "profile.photo_soon";

        /// <summary>Section 5: a pose whose envelope does not fit (photo mode).</summary>
        public const string NoRoom = "profile.no_room";

        public static string Tab(ProfileTab tab) => tab switch
        {
            ProfileTab.Titles => TabTitles,
            ProfileTab.Frames => TabFrames,
            ProfileTab.Poses => TabPoses,
            _ => TabAchievements,
        };

        public static string Category(AchievementCategory category) => category switch
        {
            AchievementCategory.Collection => "profile.category.collection",
            AchievementCategory.Biomes => "profile.category.biomes",
            AchievementCategory.Mastery => "profile.category.mastery",
            AchievementCategory.Hatching => "profile.category.hatching",
            AchievementCategory.Activities => "profile.category.activities",
            AchievementCategory.Daily => "profile.category.daily",
            AchievementCategory.ExpeditionsGarden => "profile.category.expeditions_garden",
            AchievementCategory.Albums => "profile.category.albums",
            AchievementCategory.Decoration => "profile.category.decoration",
            AchievementCategory.Bonds => "profile.category.bonds",
            AchievementCategory.Events => "profile.category.events",
            AchievementCategory.Renewal => "profile.category.renewal",
            _ => "profile.category.refuge",
        };

        public static string Name(string key) => key + ".name";

        public static string Goal(string key) => key + ".goal";

        public static string Source(string key) => key + ".source";
    }
}
