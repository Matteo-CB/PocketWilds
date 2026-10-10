using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PocketWilds.UI
{
    /// <summary>PF_UI_Journal's parts (built by JournalScreenPipeline, read by <see cref="JournalScreenView"/>).</summary>
    [Serializable]
    public sealed class JournalScreenParts
    {
        public Canvas canvas;
        public GameObject content;
        public RectTransform safeArea;
        public Image background;

        [Header("Top bar")]
        public TMP_Text title;
        public Button close;
        public Image closeIcon;

        [Header("Tabs")]
        public JournalTabParts[] tabs = Array.Empty<JournalTabParts>();

        [Header("Timeline")]
        public GameObject pagesPage;
        public ScrollRect timelineScroll;
        public RectTransform timelineContent;
        public TMP_Text empty;
        public JournalCardParts[] cards = Array.Empty<JournalCardParts>();
        public JournalHeaderParts[] headers = Array.Empty<JournalHeaderParts>();

        [Header("Moments")]
        public GameObject momentsPage;
        public ScrollRect momentScroll;
        public RectTransform momentContent;
        public TMP_Text momentsEmpty;
        public JournalMomentParts[] moments = Array.Empty<JournalMomentParts>();
    }

    [Serializable]
    public sealed class JournalTabParts
    {
        public Button button;
        public Image back;
        public TMP_Text label;
    }

    /// <summary>A month of the timeline: its name and year over a soft rule.</summary>
    [Serializable]
    public sealed class JournalHeaderParts
    {
        public RectTransform root;
        public TMP_Text label;
        public Image rule;
    }

    /// <summary>A page: the card, the picture (a stored capture, or the moment's thumbnail), the date, the memory, the system's icon and « Nouveau ».</summary>
    [Serializable]
    public sealed class JournalCardParts
    {
        public RectTransform root;
        public Image card;
        public Image pictureBack;
        public RawImage picture;
        public Image thumb;
        public TMP_Text date;
        public TMP_Text text;
        public Image systemBack;
        public Image systemIcon;
        public Image newPill;
        public TMP_Text newLabel;
    }

    [Serializable]
    public sealed class JournalMomentParts
    {
        public RectTransform root;
        public Image card;
        public Image icon;
        public TMP_Text name;
    }
}
