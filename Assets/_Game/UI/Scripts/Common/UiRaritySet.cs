using System;
using PocketWilds.Content;
using PocketWilds.Simulation;
using UnityEngine;

namespace PocketWilds.UI
{
    /// <summary>
    /// DEF_UiRarity (T34A): rarity frames and badges (white, tinted by the rarity token; each with its own motif so
    /// rarity never depends on color alone), trait badges (own colors, symbols) and the favorite hearts.
    /// Sprites come from SourceArt/UI/Tools/pw_ui_sprites_t34a.py.
    /// </summary>
    [CreateAssetMenu(menuName = "Pocket Wilds/UI/Rarity Set", fileName = "DEF_UiRarity")]
    public sealed class UiRaritySet : ScriptableObject
    {
        /// <summary>Common, Uncommon, Rare, Epic, Legendary, Fabulous, Secret (same order as the rarity tokens).</summary>
        public Sprite[] frames = Array.Empty<Sprite>();
        public Sprite[] badges = Array.Empty<Sprite>();

        /// <summary>Tiny, Giant, Shiny, Golden, Albino, Melanistic (AnimalTrait bit order).</summary>
        public Sprite[] traits = Array.Empty<Sprite>();

        public Sprite favoriteOn;
        public Sprite favoriteOff;

        public Sprite Frame(RarityTier rarity) => Pick(frames, (int)rarity);

        public Sprite Badge(RarityTier rarity) => Pick(badges, (int)rarity);

        /// <summary>Badge of one trait flag (lowest set bit), null when unknown.</summary>
        public Sprite Trait(AnimalTrait trait)
        {
            int bits = (int)trait;
            if (bits == 0)
                return null;
            int index = 0;
            while ((bits & 1) == 0)
            {
                bits >>= 1;
                index++;
            }
            return Pick(traits, index);
        }

        private static Sprite Pick(Sprite[] sprites, int index) =>
            sprites != null && index >= 0 && index < sprites.Length ? sprites[index] : null;
    }
}
