using PocketWilds.Core;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>One pill under the hatch button: an offered egg, its colors and, for Discovery eggs, the stock.</summary>
    public readonly struct EggSlot
    {
        public readonly ContentId Egg;
        public readonly Color32 Shell;
        public readonly Color32 Spots;

        /// <summary>Eggs in stock (Discovery); negative when the egg is bought, not counted.</summary>
        public readonly int Stock;

        public EggSlot(ContentId egg, Color32 shell, Color32 spots, int stock)
        {
            Egg = egg;
            Shell = shell;
            Spots = spots;
            Stock = stock;
        }
    }

    /// <summary>A small portrait over the hatch button (Focus targets): a known color, or the silhouette of a species to find.</summary>
    public readonly struct PortraitSlot
    {
        public readonly ContentId Species;

        /// <summary>None: the species is not discovered yet, its silhouette shows.</summary>
        public readonly ContentId Variant;

        public PortraitSlot(ContentId species, ContentId variant)
        {
            Species = species;
            Variant = variant;
        }
    }

    /// <summary>
    /// T35B (doc 02 Œufs, sections 3 and 5): the egg choice around the hatch button. Implemented by the refuge HUD;
    /// Presentation never references UI types. Calls come when something changes, never every frame.
    /// </summary>
    public interface IEggChoiceHud
    {
        void BindEggs(IEggChoiceActions actions);

        /// <summary>The pills of the offered eggs (hidden with a single egg) and the chosen one.</summary>
        void SetEggSlots(EggSlot[] slots, int count, int selected);

        /// <summary>The chosen egg's name, shown for a moment after a change.</summary>
        void ShowEggName(string name);

        /// <summary>The × 1 / × 3 / × 10 pill (hidden until × 3 is unlocked).</summary>
        void SetHatchQuantity(int quantity, bool visible);

        /// <summary>The Focus egg's targets over the button; count 0 hides them.</summary>
        void SetFocusTargets(PortraitSlot[] portraits, int count);

        /// <summary>A word in the price pill instead of the Light price ("Free" for a Discovery egg); null: the price.</summary>
        void SetPriceText(string text);
    }

    /// <summary>Player intents of the egg choice.</summary>
    public interface IEggChoiceActions
    {
        void SelectEgg(int index);

        /// <summary>A swipe on the hatch button: the next (1) or previous (-1) egg.</summary>
        void StepEgg(int direction);

        void CycleHatchQuantity();
    }
}
