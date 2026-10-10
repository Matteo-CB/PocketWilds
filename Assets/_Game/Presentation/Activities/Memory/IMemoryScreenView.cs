using PocketWilds.Simulation;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// T41: PF_UI_Memory's view (implemented in Game.UI by MemoryScreenView, built by MemoryPipeline): the cream tablecloth,
    /// the grid of pooled card views, the round glints and the hearts. The stage owns the game and its touches; the view
    /// owns the pictures and their motion. Built once, nothing instantiated after; no call allocates once
    /// <see cref="Bind"/> ran (portraits are borrowed from the loaded codex catalog, never copied into the screen's bundle).
    /// </summary>
    public interface IMemoryScreenView
    {
        /// <summary>The prefab carries every part (cards, effects); false: rebuild it with MemoryPipeline.</summary>
        bool Valid { get; }

        int Capacity { get; }

        /// <summary>The canvas draws.</summary>
        bool Shown { get; }

        bool ReduceMotion { get; set; }

        /// <summary>
        /// Once after loading: borrows the portraits (the codex thumbnails DEF_CodexVisuals already loaded by the scene) and
        /// the activity HUD's lent icons (a heart, a check) when the screen has none of its own. Allocates (a lookup).
        /// </summary>
        void Bind(IActivityHudView hud);

        /// <summary>The variant's coat colour baked by the pipeline (its palette's primary); false when the table lacks it.</summary>
        bool TryCoat(uint variant, out Color coat);

        /// <summary>A portrait (codex thumbnail) is known for the variant.</summary>
        bool HasPortrait(uint variant);

        void Show();

        void Hide();

        /// <summary>The grid's columns and rows (the cards beyond are hidden); places the pooled views in the safe area.</summary>
        void Layout(int columns, int rows);

        /// <summary>A card's content: its variant and species, portrait or pawprint (tinted <paramref name="coat"/>), gold edge, « ×2 ».</summary>
        void Deal(int card, uint variant, uint species, MemoryFace face, bool glow, bool isDouble, Color coat);

        /// <summary>The side a card should show; a change flips it (after <paramref name="delay"/> s), or at once.</summary>
        void SetUp(int card, bool up, bool instant, float delay);

        /// <summary>A matched card stays face up with its soft mark and dims very slightly.</summary>
        void SetMatched(int card, bool matched, bool instant);

        /// <summary>A mismatch: a gentle shake (the cards turn back on FlippedBack).</summary>
        void Shake(int card);

        /// <summary>The glow card's pair pulses gold for <paramref name="seconds"/>.</summary>
        void Pulse(int card, float seconds);

        /// <summary>Challenge 6: the card (already dealt its new content) glides from <paramref name="fromCard"/>'s place to its own.</summary>
        void GlideFrom(int card, int fromCard, float seconds);

        /// <summary>A match: a round glint on each card and a little heart rising (pooled).</summary>
        void Match(int card, int other);

        /// <summary>The card under a screen point (pixels), −1 for none (gaps included). Raycast free, no allocation.</summary>
        int HitTest(Vector2 screen);

        /// <summary>A card's centre on the screen (pixels).</summary>
        Vector2 ScreenPoint(int card);

        void SetPaused(bool paused);

        /// <summary>Per frame: flips, shakes, pulses, glides, glints, hearts. No allocation.</summary>
        void Tick(float deltaTime);

        // ---------------- read by tests and captures ----------------

        /// <summary>The card currently shows its face side (half way through a flip counts as the side it turns to).</summary>
        bool FaceShown(int card);

        /// <summary>A flip, a shake, a glide or a pulse is playing on the card.</summary>
        bool Animating(int card);

        bool Pulsing(int card);

        /// <summary>The sprite on the card's portrait (null on a pawprint card).</summary>
        Sprite PortraitShown(int card);

        /// <summary>The tint of the card's pawprint (clear on a portrait card).</summary>
        Color PawTint(int card);

        int GlintsShown { get; }

        int HeartsShown { get; }
    }
}
