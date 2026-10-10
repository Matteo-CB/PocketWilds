using System;
using PocketWilds.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PocketWilds.UI
{
    /// <summary>PF_UI_Achievements' parts (built by ProfileScreenPipeline, read by <see cref="ProfileScreenView"/>).</summary>
    [Serializable]
    public sealed class ProfileScreenParts
    {
        public Canvas canvas;
        public GameObject content;
        public RectTransform safeArea;
        public Image background;

        [Header("Top bar")]
        public TMP_Text title;
        public Button close;
        public Image closeIcon;

        [Header("Card")]
        public Image card;
        public ProfileFrameParts cardFrame = new ProfileFrameParts();
        public TMP_Text cardTitle;
        public TMP_Text cardFrameName;

        [Header("Tabs")]
        public ProfileTabParts[] tabs = Array.Empty<ProfileTabParts>();

        [Header("Succès")]
        public GameObject achievements;
        public ScrollRect chipScroll;
        public RectTransform chipContent;
        public ProfileChipParts[] chips = Array.Empty<ProfileChipParts>();
        public ScrollRect badgeScroll;
        public RectTransform badgeContent;
        public ProfileBadgeParts[] badges = Array.Empty<ProfileBadgeParts>();

        [Header("Titres and Poses")]
        public GameObject rowsPage;
        public TMP_Text photoSoon;
        public RectTransform rowViewport;
        public ScrollRect rowScroll;
        public RectTransform rowContent;
        public ProfileRowParts[] rows = Array.Empty<ProfileRowParts>();

        [Header("Cadres")]
        public GameObject framesPage;
        public ScrollRect frameScroll;
        public RectTransform frameContent;
        public ProfileFrameCellParts[] frames = Array.Empty<ProfileFrameCellParts>();

        [Header("Note")]
        public Image notePill;
        public CanvasGroup noteGroup;
        public TMP_Text noteLabel;
    }

    /// <summary>A portrait in its frame: the cream disc, the portrait, the rim and the ring (one white ring sprite tinted), the
    /// inner ring of the Double style and the 6 motif points (<see cref="FramePoint"/> order).</summary>
    [Serializable]
    public sealed class ProfileFrameParts
    {
        public RectTransform root;
        public Image disc;
        public Image portrait;
        public Image rim;
        public Image ring;
        public Image inner;
        public Image[] motifs = Array.Empty<Image>();
    }

    [Serializable]
    public sealed class ProfileTabParts
    {
        public Button button;
        public Image back;
        public TMP_Text label;
        public Image dot;
    }

    [Serializable]
    public sealed class ProfileChipParts
    {
        public RectTransform root;
        public Button button;
        public Image back;
        public Image icon;
        public TMP_Text label;
        public Image dot;
    }

    [Serializable]
    public sealed class ProfileBadgeParts
    {
        public RectTransform root;
        public Button button;
        public Image card;

        /// <summary>The tier's shared shape (Locked, Bronze, Silver, Gold, Star) with the category's icon over it.</summary>
        public Image shape;

        public Image icon;

        /// <summary>Four small pips: the tiers reached (never colour alone).</summary>
        public Image[] pips = Array.Empty<Image>();

        public TMP_Text name;
        public TMP_Text goal;
        public Image barTrack;
        public Image barFill;
        public TMP_Text toGo;
        public Button claim;
        public Image claimBack;
        public TMP_Text claimLabel;
        public Image claimGem;
        public TMP_Text claimGems;
    }

    [Serializable]
    public sealed class ProfileRowParts
    {
        public RectTransform root;
        public Button button;
        public Image card;
        public Image icon;
        public Image lockIcon;
        public TMP_Text name;
        public TMP_Text source;
        public Image check;
        public Image newPill;
        public TMP_Text newLabel;
    }

    [Serializable]
    public sealed class ProfileFrameCellParts
    {
        public RectTransform root;
        public Button button;
        public Image card;
        public ProfileFrameParts frame = new ProfileFrameParts();
        public TMP_Text name;
        public Image lockIcon;
        public Image check;
        public Image newPill;
        public TMP_Text newLabel;
    }

    /// <summary>
    /// One profile frame as the screen draws it (baked by ProfileScreenPipeline from agent BA's Editor FrameArt table): the
    /// ring and rim colours, the Double style's inner ring (alpha 0: none) and the motif sprite and tint of each of the 6
    /// points (null: no motif there). The motif sprites live in the screen's bundle (Atlas_Frames).
    /// </summary>
    [Serializable]
    public sealed class ProfileFrameStyle
    {
        public string key;
        public Color ring = Color.white;
        public Color rim = Color.white;
        public Color inner = new Color(1f, 1f, 1f, 0f);
        public Sprite[] motifs = new Sprite[ProfileLayout.MotifPoints];
        public Color[] tints = new Color[ProfileLayout.MotifPoints];
    }
}
