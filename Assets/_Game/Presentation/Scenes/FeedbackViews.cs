using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Simulation;

namespace PocketWilds.Presentation
{
    /// <summary>What the reveal banner says (texts already localized by the scene).</summary>
    public readonly struct RevealInfo
    {
        public RevealInfo(string title, string name, RarityTier rarity, HatchNews news)
        {
            Title = title;
            Name = name;
            Rarity = rarity;
            News = news;
        }

        /// <summary>"New species!", "New variant!" or the rarity name.</summary>
        public string Title { get; }

        /// <summary>Species name, or "variant species" for a new variant.</summary>
        public string Name { get; }
        public RarityTier Rarity { get; }
        public HatchNews News { get; }
    }

    /// <summary>Toast states (doc 06, SCREEN 027). Values are runtime only.</summary>
    public enum ToastKind : byte
    {
        Reward = 0,
        Discovery = 1,
        Unlock = 2,
        Visitor = 3,
        Expedition = 4,
        Warning = 5,
    }

    /// <summary>One animal as its quick sheet shows it (doc 06, SCREEN 005). Texts are localized by the scene.</summary>
    public readonly struct AnimalSheetData
    {
        public AnimalSheetData(uint instanceId, string name, string species, string variant, ContentId speciesId, ContentId variantId,
            RarityTier rarity, bool favorite, float bond, int bondLevel, uint traitBits)
        {
            InstanceId = instanceId;
            Name = name;
            Species = species;
            Variant = variant;
            SpeciesId = speciesId;
            VariantId = variantId;
            Rarity = rarity;
            Favorite = favorite;
            Bond = bond;
            BondLevel = bondLevel;
            TraitBits = traitBits;
        }

        public uint InstanceId { get; }
        public string Name { get; }
        public string Species { get; }
        public string Variant { get; }
        public ContentId SpeciesId { get; }
        public ContentId VariantId { get; }
        public RarityTier Rarity { get; }
        public bool Favorite { get; }

        /// <summary>Progress inside the current bond level, 0 to 1.</summary>
        public float Bond { get; }
        public int BondLevel { get; }
        public uint TraitBits { get; }
        public AnimalTrait Traits => AnimalGenome.Traits(TraitBits);
    }

    public interface IAnimalSheetActions
    {
        void ToggleFavorite(uint instanceId);

        /// <summary>The species page of the Codex (the animal's details and variants).</summary>
        void OpenSpeciesDetails(ContentId species);

        void AnimalSheetClosed();
    }

    /// <summary>PF_UI_AnimalQuickSheet (UI assembly).</summary>
    public interface IAnimalSheetView
    {
        bool IsOpen { get; }

        void Open(in AnimalSheetData data, ILocalization localization, IAnimalSheetActions actions);

        /// <summary>Favorite toggled while open: the heart changes at once.</summary>
        void Refresh(in AnimalSheetData data);

        void Close();

        void Tick(float unscaledDeltaTime);

        /// <summary>Builds the screen once at scene entry, invisibly (first open never hitches, T28 lesson).</summary>
        void Prewarm(ILocalization localization);
    }

    /// <summary>Modal shells (doc 06, SCREEN 025). One prefab, the kind sets icon and buttons.</summary>
    public enum ModalKind : byte
    {
        Info = 0,
        Confirm = 1,
        Warning = 2,
    }

    public interface IModalActions
    {
        void ModalClosed(bool confirmed);
    }

    /// <summary>PF_UI_Modal (UI assembly).</summary>
    public interface IModalView
    {
        bool IsOpen { get; }

        /// <param name="cancel">Null for one button (Info, Warning).</param>
        void Show(ModalKind kind, string title, string body, string confirm, string cancel, IModalActions actions);

        void Close();

        void Tick(float unscaledDeltaTime);

        /// <summary>Builds the shell once at scene entry, invisibly.</summary>
        void Prewarm(ILocalization localization);
    }
}
