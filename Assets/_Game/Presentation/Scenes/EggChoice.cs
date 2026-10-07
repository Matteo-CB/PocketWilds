using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Simulation;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// T35B, doc 02 Œufs, sections 3 and 5: which eggs the pedestal offers (the Basic and Focus eggs of each open biome,
    /// the Discovery egg while the stock has one), the one chosen (the last one used, remembered on the device) and the
    /// hatch quantity (× 1, × 3 from refuge level 12, × 10 from level 25 after a Renewal, remembered). Plain data: the
    /// refuge controller asks it to refresh a few times a second and pushes changes to the HUD.
    /// </summary>
    public sealed class EggChoice
    {
        public const int MaxSlots = 4;
        public const string SelectedKey = "pw.egg.selected";
        public const string QuantityKey = "pw.hatch.qty";

        private readonly GameContext _context;
        private readonly ContentId _fallback;
        private readonly ContentId[] _offered = new ContentId[MaxSlots];
        private ContentId _remembered;
        private int _wanted;

        public EggChoice(GameContext context, ContentId fallback)
        {
            _context = context;
            _fallback = fallback;
            Selected = fallback;
            _remembered = new ContentId((uint)PlayerPrefs.GetInt(SelectedKey, (int)fallback.Value));
            _wanted = Mathf.Max(1, PlayerPrefs.GetInt(QuantityKey, 1));
            Quantity = 1;
            Refresh();
        }

        public ContentId Selected { get; private set; }
        public int Count { get; private set; }
        public ContentId this[int index] => _offered[index];

        /// <summary>Index of the chosen egg in the offered list.</summary>
        public int SelectedIndex
        {
            get
            {
                for (int i = 0; i < Count; i++)
                    if (_offered[i] == Selected)
                        return i;
                return 0;
            }
        }

        /// <summary>The quantity the player chose, within what is unlocked (1, 3 or 10).</summary>
        public int Quantity { get; private set; }

        /// <summary>The largest quantity unlocked now.</summary>
        public int MaxQuantity
        {
            get
            {
                var refuge = _context.Progression?.Refuge;
                if (refuge == null)
                    return 1;
                return refuge.IsFeatureUnlocked(RefugeFeature.HatchTen) ? 10 : refuge.IsFeatureUnlocked(RefugeFeature.HatchThree) ? 3 : 1;
            }
        }

        /// <summary>
        /// How many eggs the next press hatches: the chosen quantity, at most the Discovery stock for a Discovery egg (the
        /// whole stock opens by 3 or by 10), and a single egg for the free tutorial egg.
        /// </summary>
        public int NextCount
        {
            get
            {
                var hatch = _context.Hatch;
                if (hatch.IsNextHatchFree)
                    return 1;
                if (Selected == hatch.DiscoveryEgg)
                    return Mathf.Clamp(hatch.DiscoveryEggs, 1, Quantity);
                return Quantity;
            }
        }

        /// <summary>
        /// Rebuilds the offered list and keeps the choice valid. True when the list, the choice or the quantity changed
        /// (the HUD then redraws; nothing allocates).
        /// </summary>
        public bool Refresh()
        {
            var hatch = _context.Hatch;
            var eggs = _context.Content.Eggs;
            var store = _context.Progression?.Store;
            bool changed = false;
            int count = 0;
            for (int i = 0; i < eggs.Count && count < MaxSlots; i++)
            {
                var egg = eggs[i];
                if (egg.Tier == EggTier.Event || egg.Tier == EggTier.Fabulous)
                    continue;
                if (egg.Tier != EggTier.Discovery && store != null && !store.IsUnlocked(egg.Biome))
                    continue;
                if (!hatch.IsOffered(egg.Id))
                    continue;
                if (_offered[count] != egg.Id)
                {
                    _offered[count] = egg.Id;
                    changed = true;
                }
                count++;
            }
            if (count != Count)
            {
                Count = count;
                changed = true;
            }

            // The remembered egg comes back as soon as it is offered again (a Discovery egg won, the Focus egg unlocked).
            var wanted = Contains(_remembered) ? _remembered : Contains(Selected) ? Selected : Count > 0 ? _offered[0] : _fallback;
            if (wanted != Selected)
            {
                Selected = wanted;
                changed = true;
            }

            int quantity = Mathf.Min(_wanted, MaxQuantity);
            quantity = quantity >= 10 ? 10 : quantity >= 3 ? 3 : 1;
            if (quantity != Quantity)
            {
                Quantity = quantity;
                changed = true;
            }
            return changed;
        }

        public bool Select(int index)
        {
            if (index < 0 || index >= Count || _offered[index] == Selected)
                return false;
            Selected = _offered[index];
            Remember(Selected);
            return true;
        }

        /// <summary>The next or previous offered egg (a swipe on the hatch button), wrapping around.</summary>
        public bool Step(int direction)
        {
            if (Count < 2)
                return false;
            int index = (SelectedIndex + (direction >= 0 ? 1 : Count - 1)) % Count;
            return Select(index);
        }

        /// <summary>× 1, then × 3, then × 10, among the unlocked ones, then back to × 1.</summary>
        public bool CycleQuantity()
        {
            int max = MaxQuantity;
            if (max <= 1)
                return false;
            int next = Quantity == 1 ? 3 : Quantity == 3 && max >= 10 ? 10 : 1;
            _wanted = next;
            Quantity = next;
            PlayerPrefs.SetInt(QuantityKey, next);
            return true;
        }

        private void Remember(ContentId egg)
        {
            _remembered = egg;
            PlayerPrefs.SetInt(SelectedKey, (int)egg.Value);
        }

        private bool Contains(ContentId egg)
        {
            for (int i = 0; i < Count; i++)
                if (_offered[i] == egg)
                    return true;
            return false;
        }
    }
}
