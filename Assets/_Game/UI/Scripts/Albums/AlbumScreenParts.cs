using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PocketWilds.UI
{
    /// <summary>PF_UI_Album's parts (built by AlbumScreenPipeline, read by <see cref="AlbumScreenView"/>).</summary>
    [Serializable]
    public sealed class AlbumScreenParts
    {
        public Canvas canvas;
        public GameObject content;
        public RectTransform safeArea;

        /// <summary>Everything but the reveal layer (hidden while a reveal plays over the refuge).</summary>
        public GameObject screen;

        public Image background;

        [Header("Top bar")]
        public TMP_Text title;
        public Button close;
        public Image closeIcon;
        public Image starsChip;
        public Image starsIcon;
        public TMP_Text stars;
        public Button back;
        public Image backIcon;

        [Header("Shelf")]
        public GameObject shelf;
        public ScrollRect shelfScroll;
        public RectTransform shelfContent;
        public AlbumCoverParts[] covers = Array.Empty<AlbumCoverParts>();
        public Button choose;
        public Image chooseBack;
        public TMP_Text chooseLabel;
        public Image chooseStar;
        public TMP_Text choosePrice;

        [Header("Page")]
        public GameObject page;
        public TMP_Text pageAlbum;
        public TMP_Text pageTitle;
        public Button previous;
        public Image previousIcon;
        public Button next;
        public Image nextIcon;
        public Image barTrack;
        public Image barFill;
        public TMP_Text ratio;
        public Image halfIcon;
        public Image halfCheck;
        public Image fullIcon;
        public Image fullCheck;
        public RectTransform pageViewport;
        public RectTransform pagePanel;
        public CanvasGroup pageGroup;
        public AlbumSwipeArea swipe;
        public AlbumSlotParts[] slots = Array.Empty<AlbumSlotParts>();

        [Header("Choose list")]
        public GameObject chooseList;
        public TMP_Text chooseTitle;
        public ScrollRect chooseScroll;
        public RectTransform chooseContent;
        public AlbumChoiceRowParts[] rows = Array.Empty<AlbumChoiceRowParts>();

        [Header("Confirmation")]
        public GameObject confirm;
        public Button confirmDim;
        public Image confirmCard;
        public Image confirmArt;
        public StickerUv confirmArtUv;
        public TMP_Text confirmText;
        public Image confirmStar;
        public TMP_Text confirmPrice;
        public Button yes;
        public TMP_Text yesLabel;
        public Button no;
        public TMP_Text noLabel;

        [Header("Note")]
        public Image notePill;
        public CanvasGroup noteGroup;
        public TMP_Text noteLabel;

        [Header("Reveal")]
        public StickerRevealParts reveal = new StickerRevealParts();
    }

    [Serializable]
    public sealed class AlbumCoverParts
    {
        public RectTransform root;
        public Button button;
        public Image card;
        public Image art;

        /// <summary>The album's hand-drawn cover (UI_AlbumCover_*), shown in place of the kind icon when it exists.</summary>
        public Image coverArt;

        public Image kindIcon;
        public TMP_Text title;
        public Image barTrack;
        public Image barFill;
        public TMP_Text ratio;
        public Image lockIcon;
        public Image check;
        public Image dot;
    }

    [Serializable]
    public sealed class AlbumSlotParts
    {
        public RectTransform root;
        public Button button;

        /// <summary>The frame (white for Common, cream for Rare and Shiny) and the inner cream line of the double border.</summary>
        public Image frame;

        public Image inner;
        public Image art;
        public StickerUv artUv;
        public Image silhouette;
        public StickerUv silhouetteUv;
        public Image hintIcon;

        /// <summary>Top right: a small round dot (Rare) or a round pearl (Shiny) with its light.</summary>
        public Image sign;

        public Image signLight;
        public TMP_Text name;
        public TMP_Text rarity;
        public TMP_Text soon;
        public Image newPill;
        public TMP_Text newLabel;
        public Image priceChip;
        public Image priceStar;
        public TMP_Text price;
    }

    [Serializable]
    public sealed class AlbumChoiceRowParts
    {
        public RectTransform root;
        public Button button;
        public Image card;
        public TMP_Text title;
        public TMP_Text missing;
        public Image barTrack;
        public Image barFill;
    }

    [Serializable]
    public sealed class StickerRevealParts
    {
        public Canvas canvas;
        public RectTransform root;
        public Image dim;
        public Button tap;

        [Header("The cream paper pouch with its round tab")]
        public RectTransform pouch;
        public Image pouchBack;
        public RectTransform flap;
        public Image flapImage;
        public Image tab;

        [Header("The sticker")]
        public RectTransform card;
        public Image frame;
        public Image inner;
        public Image art;
        public StickerUv artUv;
        public Image silhouette;
        public StickerUv silhouetteUv;
        public Image cardBack;
        public Image cardBackPaw;
        public Image sign;
        public Image signLight;
        public TMP_Text name;
        public TMP_Text rarity;
        public Image newPill;
        public TMP_Text newLabel;
        public TMP_Text duplicate;
        public TMP_Text banner;

        [Header("H-VFX-038 and H-VFX-074: a soft ring, round confetti, pearly bubbles, the duplicate's stars")]
        public Image ring;
        public Image landRing;
        public Image[] confetti = Array.Empty<Image>();
        public Image[] bubbles = Array.Empty<Image>();
        public Image[] stars = Array.Empty<Image>();
    }
}
