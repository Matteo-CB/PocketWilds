using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PocketWilds.UI
{
    /// <summary>
    /// T43: every part of PF_UI_Garden, filled by GardenScreenPipeline (one serialized object, so the prefab builder sets the
    /// whole overlay in one call). Pooled views: 5 bed labels, 25 seed packets, 10 challenge cells, 6 product chips.
    /// </summary>
    [Serializable]
    public sealed class GardenScreenParts
    {
        [Serializable]
        public sealed class Bed
        {
            public RectTransform root;
            public CanvasGroup group;

            /// <summary>The label's pill (owned: « Planter », « Prêt dans 25 min », « Prêt à récolter »).</summary>
            public GameObject owned;

            public Image back;
            public Image outline;
            public TMP_Text state;
            public Image drop;
            public Image heart0;
            public Image heart1;

            /// <summary>The cover's sign on a locked bed: « Nouvelle parcelle » with its Gems, or « Avec le Wild Club ».</summary>
            public GameObject locked;

            public Image sign;
            public TMP_Text signLabel;
            public Image priceGem;
            public TMP_Text price;
        }

        [Serializable]
        public sealed class Packet
        {
            public RectTransform root;
            public Button button;
            public Image back;
            public Image icon;
            public Image lockIcon;
            public TMP_Text name;
            public TMP_Text count;
            public TMP_Text lockLabel;
        }

        [Serializable]
        public sealed class Challenge
        {
            public RectTransform root;
            public Button button;
            public Image back;
            public TMP_Text name;
            public TMP_Text goal;
            public TMP_Text progress;
            public Image[] stars = Array.Empty<Image>();
        }

        [Serializable]
        public sealed class Product
        {
            public RectTransform root;
            public CanvasGroup group;
            public Image back;
            public Image icon;
            public TMP_Text amount;
        }

        // ---------------- the overlay ----------------
        public Canvas canvas;
        public GameObject content;
        public RectTransform safeArea;

        /// <summary>The bed labels' layer (full screen: they follow the set camera's projection).</summary>
        public RectTransform labelLayer;

        public TMP_Text title;
        public Button close;
        public Image closeIcon;
        public Image gemsChip;
        public Image gemsIcon;
        public TMP_Text gems;

        public Image levelPill;
        public TMP_Text levelLabel;
        public Image levelTrack;
        public Image levelFill;

        public Bed[] beds = Array.Empty<Bed>();

        // ---------------- the bottom tools ----------------
        public Button can;
        public Image canBack;
        public Image canIcon;
        public Image canRing;

        public Button harvestAll;
        public Image harvestAllBack;
        public TMP_Text harvestAllLabel;

        public Button speedUp;
        public Image speedUpBack;
        public TMP_Text speedUpLabel;
        public Image speedUpGem;
        public TMP_Text speedUpCost;
        public TMP_Text speedUpsLeft;

        public TMP_Text challengesTitle;
        public ScrollRect challengeScroll;
        public RectTransform challengeContent;
        public Challenge[] challenges = Array.Empty<Challenge>();

        // ---------------- the seed sheet ----------------
        public RectTransform sheet;
        public CanvasGroup sheetGroup;
        public Image sheetBack;
        public TMP_Text sheetTitle;
        public Button sheetClose;
        public Image sheetCloseIcon;
        public ScrollRect packetScroll;
        public RectTransform packetContent;
        public Packet[] packets = Array.Empty<Packet>();

        // ---------------- the harvest's chips, the banner, the note and the paw ----------------
        public RectTransform productRow;
        public Product[] products = Array.Empty<Product>();

        public RectTransform banner;
        public CanvasGroup bannerGroup;
        public TMP_Text bannerTitle;
        public TMP_Text bannerLine;

        public RectTransform notePill;
        public CanvasGroup noteGroup;
        public TMP_Text noteLabel;
        public TapHint paw;
    }
}
