using PocketWilds.Content;
using PocketWilds.Core;

namespace PocketWilds.Presentation
{
    /// <summary>One shop card, ready to display. Built by <see cref="ShopPresenter"/> from the catalog and the store.</summary>
    public struct ShopCardModel
    {
        public ContentId Offer;
        public OfferPlacement Placement;

        /// <summary>Title localization key. Empty for Gem packs (the amount is the title).</summary>
        public string TitleKey;

        /// <summary>Gems the purchase gives (0 for items only).</summary>
        public long Gems;

        /// <summary>Extra Gems compared to the smallest pack, in percent (0: no chip).</summary>
        public int BonusPercent;

        /// <summary>First item granted: name key and icon key (empty when none).</summary>
        public string ItemKey;
        public string ItemIconKey;

        /// <summary>Card art: Gem pile tier or item icon.</summary>
        public string ArtKey;

        /// <summary>Localized store price. Null until the store answered (the button shows it is loading).</summary>
        public string Price;

        /// <summary>Gem price for premium offers (0 for store offers).</summary>
        public long GemCost;

        public OfferBadge Badge;
        public bool OneTime;
        public bool Owned;

        /// <summary>T47 pack: the body line key (« Hamster des champs + 500 Gemmes + Terrier douillet »), empty otherwise.</summary>
        public string BodyKey;

        /// <summary>T47 pack: the « Offre unique » badge key (shown while not owned), empty otherwise.</summary>
        public string OneTimeKey;

        /// <summary>T47 pack: « Restauré sur un nouvel appareil. » under the button.</summary>
        public bool RestoreNote;

        /// <summary>T47 pack: what it holds, as icons with labels (up to <see cref="MaxContents"/>, in order).</summary>
        public int ContentCount;
        public ShopContentItem Content0, Content1, Content2, Content3;

        public const int MaxContents = 4;

        public ShopContentItem GetContent(int index) => index switch
        {
            0 => Content0,
            1 => Content1,
            2 => Content2,
            _ => Content3,
        };

        public void AddContent(in ShopContentItem item)
        {
            switch (ContentCount)
            {
                case 0: Content0 = item; break;
                case 1: Content1 = item; break;
                case 2: Content2 = item; break;
                case 3: Content3 = item; break;
                default: return;
            }
            ContentCount++;
        }
    }

    /// <summary>One thing a pack holds: an icon and a name key, or an amount of Gems (« 500 Gemmes »).</summary>
    public struct ShopContentItem
    {
        public string IconKey;
        public string LabelKey;

        /// <summary>Gems (the label is « {0} Gemmes »); 0 for an item.</summary>
        public long Amount;
    }

    /// <summary>Feedback after a shop action. Views map each one to a short localized line.</summary>
    public enum ShopMessage : byte
    {
        Thanks = 0,
        Failed = 1,
        Pending = 2,
        Unavailable = 3,
        NotEnoughGems = 4,
        Restored = 5,
        NothingToRestore = 6,
    }

    /// <summary>What shop screens read. Implemented by <see cref="ShopPresenter"/>; screens never see the store.</summary>
    public interface IShopModel
    {
        long Gems { get; }

        /// <summary>A purchase or restore is running: buttons wait.</summary>
        bool Busy { get; }

        /// <summary>Offer of the purchase in progress (its card shows the wait), none otherwise.</summary>
        ContentId BusyOffer { get; }

        int FeaturedCount { get; }
        ShopCardModel GetFeatured(int index);

        int GemPackCount { get; }
        ShopCardModel GetGemPack(int index);
    }

    /// <summary>Player intents from shop screens. Offer ids only: no product, price or store type crosses this line.</summary>
    public interface IShopActions
    {
        void Buy(ContentId offer);
        void Restore();
        void CloseShop();
        void CloseOffer();
    }

    /// <summary>PF_UI_Shop (UI assembly). Opaque full screen: the world camera stops while it is open.</summary>
    public interface IShopView
    {
        bool IsOpen { get; }
        void Open(IShopModel model, ILocalization text, IShopActions actions);
        void Close();

        /// <summary>
        /// Builds layout, text meshes and price glyphs once, closed, at scene entry (behind the loading screen):
        /// the first real opening then costs like any other (T28: 36 to 82 ms otherwise on desktop).
        /// </summary>
        void Prewarm(IShopModel model, ILocalization text, IShopActions actions);

        /// <summary>The model changed (prices arrived, purchase finished): rebind the cards.</summary>
        void Refresh();

        void ShowMessage(ShopMessage message, long value);
        void Tick(float unscaledDeltaTime);
    }

    /// <summary>PF_UI_OfferModal (UI assembly): one offer over the refuge (Starter Pack after a happy hatch).</summary>
    public interface IOfferModalView
    {
        bool IsOpen { get; }
        void Open(ShopCardModel card, IShopModel model, ILocalization text, IShopActions actions);
        void Close();

        /// <summary>Same as <see cref="IShopView.Prewarm"/> for the modal.</summary>
        void Prewarm(ShopCardModel card, IShopModel model, ILocalization text, IShopActions actions);

        void Refresh(ShopCardModel card);
        void ShowMessage(ShopMessage message, long value);
        void Tick(float unscaledDeltaTime);
    }
}
