using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PocketWilds.UI
{
    /// <summary>
    /// T42: every part of PF_UI_Expeditions, filled by ExpeditionPipeline (one serialized object, so the prefab builder sets
    /// the whole screen in one call). Pooled views: 4 slots, 16 destination cards, 40 animal cells, 4 duration chips, 7 loot
    /// items, 10 challenge cells, 4 postcards.
    /// </summary>
    [Serializable]
    public sealed class ExpeditionScreenParts
    {
        [Serializable]
        public sealed class Slot
        {
            public RectTransform root;
            public Button button;
            public Image frame;
            public Image glow;

            public GameObject empty;
            public TMP_Text emptyLabel;
            public Image plus;

            /// <summary>Away or back: the team's portraits, the destination, the stars.</summary>
            public GameObject team;

            public Image[] portraits = Array.Empty<Image>();
            public TMP_Text destination;
            public Image[] stars = Array.Empty<Image>();

            public GameObject away;
            public Image ringTrack;
            public Image ringFill;
            public TMP_Text timer;
            public Button speedUp;
            public Image speedUpBack;
            public TMP_Text speedUpLabel;
            public Image speedUpGem;
            public TMP_Text speedUpCost;
            public TMP_Text speedUpsLeft;

            public GameObject back;
            public Image backIcon;
            public TMP_Text backLabel;

            public GameObject locked;
            public Image lockIcon;
            public TMP_Text lockedLabel;
            public Image priceGem;
            public TMP_Text price;

            public Image[] dust = Array.Empty<Image>();
        }

        [Serializable]
        public sealed class Destination
        {
            public RectTransform root;
            public Button button;
            public Image frame;
            public Image art;
            public Image lockIcon;
            public TMP_Text name;
            public Image outline;
        }

        [Serializable]
        public sealed class Animal
        {
            public RectTransform root;
            public Button button;
            public Image disc;
            public Image portrait;
            public Image ring;
            public Image check;
            public Image leaf;
        }

        [Serializable]
        public sealed class Chip
        {
            public RectTransform root;
            public Button button;
            public Image back;
            public Image icon;
            public TMP_Text label;
        }

        [Serializable]
        public sealed class LootItem
        {
            public RectTransform root;
            public Image icon;
            public TMP_Text amount;
        }

        [Serializable]
        public sealed class Challenge
        {
            public RectTransform root;
            public Button button;
            public Image back;
            public TMP_Text name;
            public TMP_Text goal;
            public Image[] stars = Array.Empty<Image>();
        }

        [Serializable]
        public sealed class Postcard
        {
            public RectTransform root;
            public CanvasGroup group;
            public RectTransform card;
            public Image frame;
            public RawImage picture;
            public Image placeholder;
            public TMP_Text destination;
            public Image stamp;
            public Image postmark;
            public Image ring;
            public Image[] petals = Array.Empty<Image>();
        }

        [Serializable]
        public sealed class Reason
        {
            public RectTransform root;
            public Image icon;
            public TMP_Text label;
        }

        // ---------------- the screen ----------------
        public Canvas canvas;
        public GameObject content;
        public CanvasGroup contentGroup;
        public RectTransform safeArea;
        public Image background;

        public TMP_Text title;
        public Button close;
        public Image closeIcon;
        public Image gemsChip;
        public Image gemsIcon;
        public TMP_Text gems;

        public Image map;
        public ScrollRect mapScroll;
        public RectTransform mapContent;
        public Destination[] destinations = Array.Empty<Destination>();

        public Slot[] slots = Array.Empty<Slot>();

        public Button claimAll;
        public Image claimAllBack;
        public TMP_Text claimAllLabel;

        public TMP_Text challengesTitle;
        public ScrollRect challengeScroll;
        public RectTransform challengeContent;
        public Challenge[] challenges = Array.Empty<Challenge>();

        // ---------------- the planning sheet ----------------
        public RectTransform sheet;
        public CanvasGroup sheetGroup;
        public Image sheetBack;
        public TMP_Text sheetTitle;
        public TMP_Text sheetDestination;
        public Button sheetClose;
        public Image sheetCloseIcon;
        public ScrollRect animalScroll;
        public RectTransform animalContent;
        public Animal[] animals = Array.Empty<Animal>();
        public Button suggested;
        public Image suggestedBack;
        public TMP_Text suggestedLabel;
        public Image[] ratingStars = Array.Empty<Image>();
        public TMP_Text ratingLabel;
        public Reason[] reasons = Array.Empty<Reason>();
        public TMP_Text durationTitle;
        public Chip[] durations = Array.Empty<Chip>();
        public TMP_Text lootTitle;
        public LootItem[] loot = Array.Empty<LootItem>();
        public TMP_Text note;
        public Button go;
        public Image goBack;
        public TMP_Text goLabel;

        // ---------------- the welcome ----------------
        public Image dim;
        public RectTransform postcardLayer;
        public Postcard[] postcards = Array.Empty<Postcard>();

        // ---------------- the note and the paw ----------------
        public RectTransform notePill;
        public CanvasGroup noteGroup;
        public TMP_Text noteLabel;
        public TapHint paw;
    }
}
