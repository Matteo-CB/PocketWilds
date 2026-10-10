using System;
using System.Collections.Generic;
using PocketWilds.Core;
using UnityEngine;

namespace PocketWilds.Content
{
    /// <summary>
    /// Authoring asset for one species (DEF_Species_&lt;Species&gt;). Plain data only: no logic,
    /// never mutated at runtime. Converted into immutable runtime definitions at boot.
    /// </summary>
    [CreateAssetMenu(menuName = "Pocket Wilds/Species Definition", fileName = "DEF_Species_New")]
    public sealed class SpeciesDefinitionAsset : ScriptableObject
    {
        [Serializable]
        public sealed class VariantEntry
        {
            public string sourceId = "variant.species.color";
            public string displayKey = "variant.species.color.name";
            public RarityTier rarity = RarityTier.Common;
            public Color primary = new Color(0.8f, 0.7f, 0.6f);
            public Color secondary = new Color(0.95f, 0.8f, 0.8f);
            public Color belly = new Color(0.97f, 0.94f, 0.88f);
            public Color detail = new Color(0.95f, 0.6f, 0.65f);
            public Color eye = new Color(0.04f, 0.03f, 0.04f);
            public Color pattern = new Color(0.55f, 0.42f, 0.34f);
            public PatternChannel patternChannel = PatternChannel.None;
            [Range(0, 15)] public int patternStrength = 0;
            [Tooltip("Secret variant: cosmetic, never given by the variant pity.")]
            public bool secret;

            [Tooltip("T44C: a rare visitor's seasonal variant (secret for the draws, its own Codex page).")]
            public bool visitor;
        }

        [Tooltip("Stable source ID, e.g. species.rabbit. Never change it once shipped.")]
        public string sourceId = "species.new";
        public string displayKey = "species.new.name";
        public RigFamily rigFamily = RigFamily.SmallQuadruped;
        public string biomeId = "biome.grassland";
        [Tooltip("Light per second for one Common animal at mastery 0.")]
        public double baseProduction = 1.0;
        [Tooltip("Key of the view prefab in the animal visual catalog.")]
        public string visualKey = "PF_Animal_New";
        [Tooltip("Secret species: hidden until found, never given by the discovery pity.")]
        public bool secret;
        public List<VariantEntry> variants = new List<VariantEntry>();

        public SpeciesDefinition ToRuntime()
        {
            return new SpeciesDefinition(ContentId.FromSource(sourceId), displayKey, rigFamily,
                ContentId.FromSource(biomeId), baseProduction, visualKey, secret);
        }

        public VariantDefinition ToRuntime(VariantEntry entry)
        {
            var palette = new Color32[VariantDefinition.PaletteSize];
            palette[0] = entry.primary;
            palette[1] = entry.secondary;
            palette[2] = entry.belly;
            palette[3] = entry.detail;
            palette[4] = entry.eye;
            palette[5] = entry.pattern;
            palette[6] = entry.primary;
            palette[7] = entry.primary;
            return new VariantDefinition(ContentId.FromSource(entry.sourceId), ContentId.FromSource(sourceId), entry.displayKey,
                entry.rarity, palette, entry.patternChannel, (byte)Mathf.Clamp(entry.patternStrength, 0, 15), entry.secret, entry.visitor);
        }
    }
}
