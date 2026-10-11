using System;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Simulation;

namespace PocketWilds.Presentation
{
    /// <summary>T47 outfit picker and Codex companions: text keys (FR and EN in Editor/UI/OutfitTexts.cs).</summary>
    public static class OutfitText
    {
        public const string SheetButton = "outfit.button";
        public const string Title = "outfit.title";
        public const string SlotHeadTop = "outfit.slot.headtop";
        public const string SlotNeck = "outfit.slot.neck";
        public const string SlotBack = "outfit.slot.back";
        public const string SlotTailBase = "outfit.slot.tailbase";
        public const string SlotLocked = "outfit.slot.locked";
        public const string SlotHidden = "outfit.slot.hidden";
        public const string Remove = "outfit.remove";
        public const string Empty = "outfit.empty";
        public const string ToShop = "outfit.to_shop";
        public const string PalettePrefix = "outfit.palette.";

        public const string CodexSpeciesTab = "codex.tab.species";
        public const string CodexCompanionsTab = "codex.companions.title";
        public const string CodexLooks = "codex.companions.looks";
        public const string CodexRewardLook = "codex.companions.reward.look";
        public const string CodexRewardAccessory = "codex.companions.reward.accessory";
        public const string CodexRewardPose = "codex.companions.reward.pose";
        public const string CodexRewardTitle = "codex.companions.reward.title";
        public const string CodexBonusPrefix = "codex.companions.bonus.";

        public static string SlotKey(AccessorySocket socket) => socket switch
        {
            AccessorySocket.HeadTop => SlotHeadTop,
            AccessorySocket.Neck => SlotNeck,
            AccessorySocket.Back => SlotBack,
            _ => SlotTailBase,
        };

        public static string PaletteKey(string palette) => PalettePrefix + palette;

        public static string RewardKindKey(CompanionReward reward) => reward switch
        {
            CompanionReward.Look => CodexRewardLook,
            CompanionReward.Accessory => CodexRewardAccessory,
            CompanionReward.Pose => CodexRewardPose,
            _ => CodexRewardTitle,
        };

        /// <summary>The biome bonus line of a companion (« Lumière +5 % pour les animaux de la Prairie »), one key per companion.</summary>
        public static string BonusKey(string companionName) => CodexBonusPrefix + companionName;
    }

    /// <summary>How a slot of the outfit picker looks. Hidden and Locked share the calm locked look, with their own line.</summary>
    public enum OutfitSlotState : byte
    {
        /// <summary>The species hides this socket (the hamster's tail).</summary>
        Hidden = 0,

        /// <summary>Nothing owned fits here yet.</summary>
        Locked = 1,

        /// <summary>Owned accessories fit, nothing worn.</summary>
        Open = 2,

        /// <summary>An accessory is worn.</summary>
        Worn = 3,
    }

    public readonly struct OutfitSlot
    {
        public OutfitSlot(AccessorySocket socket, OutfitSlotState state, ContentId worn, int palette, int choices)
        {
            Socket = socket;
            State = state;
            Worn = worn;
            Palette = palette;
            Choices = choices;
        }

        public AccessorySocket Socket { get; }
        public OutfitSlotState State { get; }
        public ContentId Worn { get; }
        public int Palette { get; }

        /// <summary>Owned accessories that fit this slot on this animal.</summary>
        public int Choices { get; }
        public bool Usable => State == OutfitSlotState.Open || State == OutfitSlotState.Worn;
    }

    /// <summary>One owned accessory that fits the chosen slot.</summary>
    public readonly struct OutfitChoice
    {
        public OutfitChoice(ContentId accessory, string nameKey, string iconKey, string[] palettes, bool worn)
        {
            Accessory = accessory;
            NameKey = nameKey;
            IconKey = iconKey;
            Palettes = palettes;
            Worn = worn;
        }

        public ContentId Accessory { get; }
        public string NameKey { get; }
        public string IconKey { get; }
        public string[] Palettes { get; }
        public bool Worn { get; }
    }

    /// <summary>
    /// T47 outfit picker model: the four slots (HeadTop, Neck, Back, TailBase) of one animal with their state, and the owned
    /// accessories that fit each. Built on open and after each change (never per frame); reads only.
    /// </summary>
    public sealed class OutfitPicker
    {
        public const int MaxChoices = 8;

        /// <summary>The slots the picker shows, in order (ears are not offered yet).</summary>
        public static readonly AccessorySocket[] Sockets = { AccessorySocket.HeadTop, AccessorySocket.Neck, AccessorySocket.Back, AccessorySocket.TailBase };

        private readonly CosmeticService _cosmetics;
        private readonly OutfitSlot[] _slots = new OutfitSlot[4];
        private readonly OutfitChoice[] _choices = new OutfitChoice[4 * MaxChoices];
        private readonly int[] _choiceCounts = new int[4];
        private SpeciesDefinition _species;

        public OutfitPicker(CosmeticService cosmetics) => _cosmetics = cosmetics;

        public uint Animal { get; private set; }
        public ContentId Species => _species != null ? _species.Id : ContentId.None;

        /// <summary>At least one owned accessory fits this animal somewhere.</summary>
        public bool AnyOwned { get; private set; }

        /// <summary>The pack's offer can open from the picker (« Voir le pack »), set by the refuge.</summary>
        public bool ShopAvailable { get; set; }

        public OutfitSlot GetSlot(int index) => _slots[index];
        public int ChoiceCount(int slot) => _choiceCounts[slot];
        public OutfitChoice GetChoice(int slot, int index) => _choices[slot * MaxChoices + index];

        /// <summary>The first usable slot (the picker opens on it), else 0.</summary>
        public int FirstUsableSlot()
        {
            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i].Usable)
                    return i;
            return 0;
        }

        public void Bind(uint animal, SpeciesDefinition species)
        {
            Animal = animal;
            _species = species;
            Rebuild();
        }

        public void Rebuild()
        {
            AnyOwned = false;
            for (int s = 0; s < Sockets.Length; s++)
            {
                int count = 0;
                var socket = Sockets[s];
                var state = StateOf(_cosmetics, _species, Animal, socket, out _);
                if (state != OutfitSlotState.Hidden && _cosmetics != null)
                {
                    var accessories = _cosmetics.Accessories;
                    var worn = _cosmetics.GetEquipped(Animal, socket);
                    for (int i = 0; i < accessories.Count && count < MaxChoices; i++)
                    {
                        var accessory = accessories[i];
                        if (_cosmetics.CanEquip(Animal, accessory.Id, socket) != EquipResult.Ok)
                            continue;
                        _choices[s * MaxChoices + count] = new OutfitChoice(accessory.Id, accessory.DisplayKey, accessory.IconKey, accessory.Palettes,
                            !worn.IsEmpty && worn.Accessory == accessory.Id);
                        count++;
                    }
                }
                _choiceCounts[s] = count;
                var equipped = _cosmetics != null ? _cosmetics.GetEquipped(Animal, socket) : default;
                _slots[s] = new OutfitSlot(socket, state, equipped.Accessory, equipped.Palette, count);
                AnyOwned |= count > 0;
            }
        }

        /// <summary>
        /// The state of one slot: Hidden when the species hides the socket (or no species), Worn when something is worn, Open
        /// when owned accessories fit, Locked otherwise. <paramref name="choices"/>: the owned accessories that fit.
        /// </summary>
        public static OutfitSlotState StateOf(CosmeticService cosmetics, SpeciesDefinition species, uint animal, AccessorySocket socket, out int choices)
        {
            choices = 0;
            if (species == null || cosmetics == null || species.HidesSocket(socket))
                return OutfitSlotState.Hidden;
            var accessories = cosmetics.Accessories;
            for (int i = 0; i < accessories.Count; i++)
                if (cosmetics.CanEquip(animal, accessories[i].Id, socket) == EquipResult.Ok)
                    choices++;
            if (!cosmetics.GetEquipped(animal, socket).IsEmpty)
                return OutfitSlotState.Worn;
            return choices > 0 ? OutfitSlotState.Open : OutfitSlotState.Locked;
        }
    }

    /// <summary>One step of a companion's reward ladder (bond 5, 10, 15, 20, 25).</summary>
    public readonly struct CompanionRung
    {
        public CompanionRung(int level, CompanionReward reward, ContentId item, string nameKey, bool unlocked)
        {
            Level = level;
            Reward = reward;
            Item = item;
            NameKey = nameKey;
            Unlocked = unlocked;
        }

        public int Level { get; }
        public CompanionReward Reward { get; }

        /// <summary>The look (variant) or accessory given; none for the pose and the title.</summary>
        public ContentId Item { get; }
        public string NameKey { get; }
        public bool Unlocked { get; }
        public string KindKey => OutfitText.RewardKindKey(Reward);
    }

    /// <summary>
    /// T47 companion reward ladder (doc 02 Renouveau, section 5): 5 second look, 10 third look, 15 signature accessory, 20 photo pose,
    /// 25 profile title. Names come from the content (variants, accessory) and the profile rows (pose, title) of the companion's
    /// subject. Pure: no allocation when the array is given.
    /// </summary>
    public static class CompanionLadder
    {
        public const int Rungs = 5;
        public static readonly int[] Levels = { 5, 10, 15, 20, 25 };

        public static int Fill(CompanionRow row, int bondLevel, ContentRegistry content, CompanionRung[] into)
        {
            if (row == null || into == null || into.Length < Rungs)
                return 0;
            into[0] = new CompanionRung(5, CompanionReward.Look, row.Look5, VariantKey(content, row.Look5), bondLevel >= 5);
            into[1] = new CompanionRung(10, CompanionReward.Look, row.Look10, VariantKey(content, row.Look10), bondLevel >= 10);
            into[2] = new CompanionRung(15, CompanionReward.Accessory, row.Accessory15, AccessoryKey(content, row.Accessory15), bondLevel >= 15);
            into[3] = new CompanionRung(20, CompanionReward.Pose, ContentId.None, ProfileKey(ProfileItemKind.Pose, row.Subject, 20), bondLevel >= 20);
            into[4] = new CompanionRung(25, CompanionReward.Title, ContentId.None, ProfileKey(ProfileItemKind.Title, row.Subject, 25), bondLevel >= 25);
            return Rungs;
        }

        /// <summary>The companion's looks in order (base, bond 5, bond 10).</summary>
        public static ContentId Look(CompanionRow row, int index) => index == 0 ? row.BaseLook : index == 1 ? row.Look5 : row.Look10;

        private static string VariantKey(ContentRegistry content, ContentId variant) =>
            content != null && content.Variants.TryGet(variant, out var definition) ? definition.DisplayKey : string.Empty;

        private static string AccessoryKey(ContentRegistry content, ContentId accessory)
        {
            if (content == null)
                return string.Empty;
            if (content.Shop.Accessories.TryGet(accessory, out var definition))
                return definition.DisplayKey;
            return content.Shop.Entitlements.TryGet(accessory, out var item) ? item.DisplayKey : string.Empty;
        }

        /// <summary>The name key of the profile item a companion bond level gives (« pose.fill_cheeks.name » for subject 101 at 20).</summary>
        public static string ProfileKey(ProfileItemKind kind, int subject, int level)
        {
            int count = ProfileData.Count(kind);
            for (int i = 0; i < count; i++)
            {
                ref readonly var profile = ref ProfileData.Row(kind, i);
                if (profile.Source == ProfileSource.Companion && profile.Subject == subject && profile.Value == level)
                    return profile.Key + ".name";
            }
            return string.Empty;
        }
    }

    /// <summary>PF_UI_OutfitPicker (UI assembly): a bottom panel over the refuge; the camera keeps the animal in view above it.</summary>
    public interface IOutfitPickerView
    {
        bool IsOpen { get; }

        void Open(OutfitPicker model, string animalName, ILocalization text, IOutfitPickerActions actions);

        /// <summary>The model was rebuilt (an accessory put on or taken off, the pack bought): the panel binds again.</summary>
        void Refresh();

        void Close();

        void Tick(float unscaledDeltaTime);

        /// <summary>Builds the panel once at scene entry, invisibly.</summary>
        void Prewarm(ILocalization text);
    }

    public interface IOutfitPickerActions
    {
        void EquipAccessory(uint animal, ContentId accessory, AccessorySocket socket, int palette);

        void RemoveAccessory(uint animal, AccessorySocket socket);

        /// <summary>Nothing owned: the pack's offer (« Voir le pack »).</summary>
        void OpenPackOffer();

        void OutfitClosed();
    }

    /// <summary>T47: the « Tenue » button of the animal sheet (shown when something can be worn or the shop can offer the pack).</summary>
    public interface IAnimalSheetOutfitView
    {
        void SetOutfit(bool available);
    }

    public interface IAnimalSheetOutfitActions
    {
        void OpenOutfit(uint instanceId);
    }

    /// <summary>T47: the Codex's Companions tab reads the companions and the content (bound by the refuge before opening).</summary>
    public interface ICodexCompanionView
    {
        void BindCompanions(CompanionService companions, ContentRegistry content, PlayerState state);

        /// <summary>The Companions tab is showing (tests).</summary>
        bool CompanionsShown { get; }
    }

    /// <summary>T47 actions of the Companions tab.</summary>
    public interface ICodexCompanionActions
    {
        /// <summary>A silhouette tapped: the pack's offer over the Codex.</summary>
        void OpenCompanionOffer(ContentId companionItem);

        /// <summary>« Tenue » on a companion card: the Codex closes, the outfit picker opens on the companion.</summary>
        void OpenCompanionOutfit(ContentId species);

        /// <summary>A look chosen on the card. True when the companion now wears it.</summary>
        bool SelectCompanionLook(ContentId species, ContentId look);
    }
}
