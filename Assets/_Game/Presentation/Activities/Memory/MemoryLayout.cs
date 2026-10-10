using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// T41 « Memory des empreintes », the screen's numbers (doc 01 Contenu S-ACT-004; doc « Mini-jeux &amp; activités »
    /// sections 1 and 6): where its prefab lives and loads from, the grid's place in the portrait safe area (1080 × 2340
    /// reference units, the activity HUD's top card and its pills above it), the cards' shape and the timings of their
    /// motion. Shared by the stage, the screen view (Game.UI), the pipeline, the capture and the tests.
    /// </summary>
    public static class MemoryLayout
    {
        /// <summary>The Addressables group of the screen (loaded at the first game, released on leaving the activities).</summary>
        public const string Group = "Activity_Memory";

        public const string ScreenAddress = "activity.memory.screen";
        public const string ScreenPrefabName = "PF_UI_Memory";
        public const string PrefabPath = "Assets/_Game/Prefabs/Activities/" + ScreenPrefabName + ".prefab";

        /// <summary>Agent MA's sprites (cards, gold edge, double badge, tablecloth, the three pawprints).</summary>
        public const string SpriteFolder = "Assets/_Game/Art/UI/Activities/Memory";

        /// <summary>The screen's canvas: over the refuge HUD (10), under the activity HUD (20), the hub (22) and the result (30).</summary>
        public const int SortingOrder = 16;

        /// <summary>Pooled card views: the largest grid (5 × 4).</summary>
        public const int CardCapacity = 20;

        /// <summary>Pooled round glints (2 per match) and hearts (1 per match): a few matches in flight at once.</summary>
        public const int GlintCapacity = 8;

        public const int HeartCapacity = 4;

        // ---------------- the grid in the safe area (reference units) ----------------

        /// <summary>
        /// The grid starts under the activity HUD: its score card (28 to 208 under the safe area's top), the streak and
        /// « Entraînement » pills (to 310). The tip bubble (336 to 440) floats over the empty band a centred grid leaves.
        /// </summary>
        public const float GridTop = 330f;

        public const float GridBottom = 72f;
        public const float GridSide = 40f;

        /// <summary>The space between two cards.</summary>
        public const float Gap = 24f;

        /// <summary>A card's width over its height: about 3:4.</summary>
        public const float CardAspect = 0.75f;

        /// <summary>A 4 × 3 grid on a wide screen stays a comfortable size.</summary>
        public const float MaxCardWidth = 250f;

        // ---------------- motion (Reduce Motion: instant changes, no squash, no shake, no glide) ----------------

        /// <summary>A flip: a scale-X squash to 0 and back with a small lift.</summary>
        public const float FlipSeconds = 0.22f;

        public const float FlipLift = 14f;

        /// <summary>The preview flips the cards one after the other along the diagonals (seconds per step).</summary>
        public const float PreviewStagger = 0.025f;

        /// <summary>Challenge 6: two cards glide to each other's place.</summary>
        public const float SwapSeconds = 0.35f;

        /// <summary>The glow card's pair pulses gold this long (doc: « luit 1 s »).</summary>
        public const float GlowSeconds = 1f;

        /// <summary>A mismatch: a gentle shake, never a scolding.</summary>
        public const float ShakeSeconds = 0.32f;

        public const float ShakeAmplitude = 9f;

        /// <summary>A matched card dims very slightly (its colours times this), after its mark pops in.</summary>
        public const float MatchedDim = 0.9f;

        public const float MarkPopSeconds = 0.26f;

        /// <summary>A match's glints (round, warm) and its little heart rising.</summary>
        public const float GlintSeconds = 0.45f;

        public const float HeartSeconds = 0.85f;
        public const float HeartRise = 96f;

        // ---------------- the cards' faces ----------------

        /// <summary>The portrait inside the cream face (a share of the card's width on each side).</summary>
        public const float PortraitInset = 0.09f;

        /// <summary>
        /// The pawprint's size (a share of the card's width). Agent MA's paws are white with their own soft grey rim: the coat
        /// tint colours the pad and darkens the rim with it, so even a white coat reads on the cream face.
        /// </summary>
        public const float PawSize = 0.64f;

        /// <summary>A soft stamp shadow under the pawprint, this share of its size lower (the paw pressed into the card).</summary>
        public const float PawShadowDrop = 0.035f;

        /// <summary>The « ×2 » badge on the top right corner and the matched mark on the bottom right (shares of the width).</summary>
        public const float BadgeSize = 0.36f;

        public const float MarkSize = 0.28f;

        /// <summary>The badge's centre from the card's top right corner: inside the card, a small margin from its edges.</summary>
        public static Vector2 BadgeAt(float badge) => new Vector2(-(badge * 0.5f + 8f), -(badge * 0.5f + 8f));

        /// <summary>The matched mark's centre from the card's bottom right corner, inside the card.</summary>
        public static Vector2 MarkAt(float mark) => new Vector2(-(mark * 0.5f + 10f), mark * 0.5f + 10f);

        /// <summary>The gold edge's reach past the card (reference units): agent MA draws it on the card's own canvas, so none.</summary>
        public const float GlowOutset = 0f;

        /// <summary>Two variants of one species whose coats are closer than this (RGB distance) give pawprints too alike.</summary>
        public const float CoatAlike = 0.1f;

        /// <summary>
        /// The card size that fits <paramref name="columns"/> × <paramref name="rows"/> cards of <see cref="CardAspect"/> with
        /// <see cref="Gap"/> between them in a <paramref name="width"/> × <paramref name="height"/> area (no larger than
        /// <see cref="MaxCardWidth"/>).
        /// </summary>
        public static Vector2 Fit(int columns, int rows, float width, float height)
        {
            columns = Mathf.Max(1, columns);
            rows = Mathf.Max(1, rows);
            float byWidth = (width - (columns - 1) * Gap) / columns;
            float byHeight = (height - (rows - 1) * Gap) / rows * CardAspect;
            float cardWidth = Mathf.Max(8f, Mathf.Min(MaxCardWidth, Mathf.Min(byWidth, byHeight)));
            return new Vector2(cardWidth, cardWidth / CardAspect);
        }

        /// <summary>A card's centre from the grid's centre (cards numbered row by row from the top left).</summary>
        public static Vector2 SlotCenter(int card, int columns, int rows, Vector2 size)
        {
            columns = Mathf.Max(1, columns);
            rows = Mathf.Max(1, rows);
            int column = card % columns;
            int row = card / columns;
            float width = columns * size.x + (columns - 1) * Gap;
            float height = rows * size.y + (rows - 1) * Gap;
            return new Vector2(-width * 0.5f + size.x * 0.5f + column * (size.x + Gap), height * 0.5f - size.y * 0.5f - row * (size.y + Gap));
        }

        /// <summary>The whole grid's size for a card size.</summary>
        public static Vector2 GridSize(int columns, int rows, Vector2 size) =>
            new Vector2(columns * size.x + (columns - 1) * Gap, rows * size.y + (rows - 1) * Gap);

        // ---------------- coat colours (the pawprints' tint) ----------------

        /// <summary>A variant's coat colour for its pawprint: its palette's primary (the main coat), opaque.</summary>
        public static Color Coat(Color32 primary) => new Color(primary.r / 255f, primary.g / 255f, primary.b / 255f, 1f);

        /// <summary>
        /// The soft stamp shadow under a pawprint: a warm brown, a little stronger under a light coat (a white rabbit's paw
        /// lifts off the cream face).
        /// </summary>
        public static Color PawShadowColor(Color coat)
        {
            float luminance = 0.2126f * coat.r + 0.7152f * coat.g + 0.0722f * coat.b;
            return new Color(0.47f, 0.35f, 0.27f, 0.12f + Mathf.Clamp01((luminance - 0.5f) * 2f) * 0.16f);
        }

        /// <summary>How far apart two coats look (RGB distance, 0 to about 1.7).</summary>
        public static float CoatDistance(Color a, Color b)
        {
            float r = a.r - b.r, g = a.g - b.g, bl = a.b - b.b;
            return Mathf.Sqrt(r * r + g * g + bl * bl);
        }
    }
}
