#nullable enable
using System;
using System.Collections.Generic;
using PocketWilds.Content;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>One biome block of the Codex.</summary>
    public readonly struct CodexSection
    {
        public readonly ContentId Biome;

        /// <summary>Localization key of the biome name; null when the content defines no biome.</summary>
        public readonly string? DisplayKey;

        /// <summary>First species of the section, in Codex order.</summary>
        public readonly int Start;
        public readonly int Count;
        public readonly int Discovered;

        public CodexSection(ContentId biome, string? displayKey, int start, int count, int discovered)
        {
            Biome = biome;
            DisplayKey = displayKey;
            Start = start;
            Count = count;
            Discovered = discovered;
        }
    }

    public readonly struct CodexSpeciesEntry
    {
        public readonly ContentId Species;
        public readonly string DisplayKey;
        public readonly bool Discovered;

        /// <summary>Discovered (or one of its variants) and not looked at yet.</summary>
        public readonly bool IsNew;
        public readonly int Owned;
        public readonly int VariantsFound;
        public readonly int VariantsTotal;

        public CodexSpeciesEntry(ContentId species, string displayKey, bool discovered, bool isNew, int owned, int variantsFound, int variantsTotal)
        {
            Species = species;
            DisplayKey = displayKey;
            Discovered = discovered;
            IsNew = isNew;
            Owned = owned;
            VariantsFound = variantsFound;
            VariantsTotal = variantsTotal;
        }
    }

    public readonly struct CodexVariantEntry
    {
        public readonly ContentId Variant;
        public readonly string DisplayKey;
        public readonly RarityTier Rarity;
        public readonly bool Discovered;
        public readonly bool IsNew;
        public readonly int Owned;

        public CodexVariantEntry(ContentId variant, string displayKey, RarityTier rarity, bool discovered, bool isNew, int owned)
        {
            Variant = variant;
            DisplayKey = displayKey;
            Rarity = rarity;
            Discovered = discovered;
            IsNew = isNew;
            Owned = owned;
        }
    }

    /// <summary>
    /// Codex view model (doc 03 Codex, Writing Guide Codex order): species grouped by biome, variants by
    /// rarity, discovery and "New" state, owned counts. Layout tables are built once from content; owned
    /// counts are rebuilt in one pass over the collection only when it changed. Reads never allocate,
    /// so a list of thousands of entries can bind rows on demand.
    /// </summary>
    public sealed class CodexService
    {
        private readonly PlayerState _state;
        private readonly ContentRegistry _content;
        private readonly ISaveScheduler? _save;
        private readonly MasteryService? _mastery;

        private readonly CodexSection[] _sections;
        private readonly int[] _speciesOrder;     // Codex index -> species dense index
        private readonly int[] _variantStart;     // species dense index -> first slot in _variantOrder
        private readonly int[] _variantCount;
        private readonly int[] _variantOrder;     // variant dense indices, grouped by species, by rarity
        private readonly int[] _speciesOwned;
        private readonly int[] _variantOwned;
        private readonly int[] _variantsFound;
        private readonly int[] _sectionDiscovered;

        private uint _stampNextId = uint.MaxValue;
        private int _stampCount = -1;
        private int _stampDiscoveredVariants = -1;

        public CodexService(PlayerState state, ContentRegistry content, ISaveScheduler? save, MasteryService? mastery = null)
        {
            _mastery = mastery;
            _state = state;
            _content = content;
            _save = save;
            var species = content.Species;
            var variants = content.Variants;

            // Variants per species, rarity ascending then content order (stable).
            _variantStart = new int[species.Count];
            _variantCount = new int[species.Count];
            for (int v = 0; v < variants.Count; v++)
            {
                if (species.TryGetIndex(variants[v].Species, out int s))
                    _variantCount[s]++;
            }
            int cursor = 0;
            for (int s = 0; s < species.Count; s++)
            {
                _variantStart[s] = cursor;
                cursor += _variantCount[s];
            }
            _variantOrder = new int[cursor];
            var fill = new int[species.Count];
            for (int v = 0; v < variants.Count; v++)
            {
                if (!species.TryGetIndex(variants[v].Species, out int s))
                    continue;
                _variantOrder[_variantStart[s] + fill[s]++] = v;
            }
            for (int s = 0; s < species.Count; s++)
                Array.Sort(_variantOrder, _variantStart[s], _variantCount[s], new RarityOrder(variants));

            // Sections: biomes in progression order, then species without a known biome. Companions have their own tab (T47):
            // never in a section, so never in the completion.
            var order = new List<int>(species.Count);
            var sections = new List<CodexSection>();
            var placed = new bool[species.Count];
            for (int b = 0; b < content.Biomes.Count; b++)
            {
                var biome = content.Biomes[b];
                int start = order.Count;
                for (int s = 0; s < species.Count; s++)
                {
                    if (species[s].Biome == biome.Id && !species[s].IsCompanion)
                    {
                        order.Add(s);
                        placed[s] = true;
                    }
                }
                if (order.Count > start)
                    sections.Add(new CodexSection(biome.Id, biome.DisplayKey, start, order.Count - start, 0));
            }
            int rest = order.Count;
            for (int s = 0; s < species.Count; s++)
            {
                if (!placed[s] && !species[s].IsCompanion)
                    order.Add(s);
            }
            if (order.Count > rest)
                sections.Add(new CodexSection(default, null, rest, order.Count - rest, 0));

            _speciesOrder = order.ToArray();
            _sections = sections.ToArray();
            _sectionDiscovered = new int[_sections.Length];
            _speciesOwned = new int[species.Count];
            _variantOwned = new int[variants.Count];
            _variantsFound = new int[species.Count];
        }

        public int SectionCount => _sections.Length;
        public int SpeciesCount => _speciesOrder.Length;
        public int TotalSpecies => _speciesOrder.Length;
        public int DiscoveredSpecies => _state.Collection.DiscoveredSpecies.Count;

        /// <summary>O(1): seen ids are always a subset of discovered ids.</summary>
        public bool HasNew =>
            _state.Collection.DiscoveredSpecies.Count + _state.Collection.DiscoveredVariants.Count
            > _state.Codex.SeenSpecies.Count + _state.Codex.SeenVariants.Count;

        /// <summary>Rebuilds owned counts when the collection changed since the last call. O(animals).</summary>
        public void Refresh()
        {
            var collection = _state.Collection;
            if (collection.NextInstanceId == _stampNextId && collection.Count == _stampCount
                && collection.DiscoveredVariants.Count == _stampDiscoveredVariants)
                return;

            using (GameProfilerMarkers.CodexRefresh.Auto())
            {
                Array.Clear(_speciesOwned, 0, _speciesOwned.Length);
                Array.Clear(_variantOwned, 0, _variantOwned.Length);
                var species = _content.Species;
                var variants = _content.Variants;
                for (int i = 0; i < collection.Count; i++)
                {
                    ref readonly var animal = ref collection[i];
                    if (species.TryGetIndex(animal.Species, out int s))
                        _speciesOwned[s]++;
                    if (variants.TryGetIndex(animal.Variant, out int v))
                        _variantOwned[v]++;
                }

                for (int s = 0; s < species.Count; s++)
                {
                    int found = 0;
                    for (int k = 0; k < _variantCount[s]; k++)
                    {
                        if (collection.DiscoveredVariants.Contains(variants[_variantOrder[_variantStart[s] + k]].Id))
                            found++;
                    }
                    _variantsFound[s] = found;
                }

                for (int i = 0; i < _sections.Length; i++)
                {
                    int discovered = 0;
                    var section = _sections[i];
                    for (int k = 0; k < section.Count; k++)
                    {
                        if (collection.DiscoveredSpecies.Contains(species[_speciesOrder[section.Start + k]].Id))
                            discovered++;
                    }
                    _sectionDiscovered[i] = discovered;
                }

                _stampNextId = collection.NextInstanceId;
                _stampCount = collection.Count;
                _stampDiscoveredVariants = collection.DiscoveredVariants.Count;
            }
        }

        public CodexSection GetSection(int index)
        {
            var section = _sections[index];
            return new CodexSection(section.Biome, section.DisplayKey, section.Start, section.Count, _sectionDiscovered[index]);
        }

        public CodexSpeciesEntry GetSpecies(int codexIndex)
        {
            int s = _speciesOrder[codexIndex];
            var definition = _content.Species[s];
            var collection = _state.Collection;
            bool discovered = collection.DiscoveredSpecies.Contains(definition.Id);
            bool isNew = discovered && (!_state.Codex.SeenSpecies.Contains(definition.Id) || HasNewVariant(s));
            return new CodexSpeciesEntry(definition.Id, definition.DisplayKey, discovered, isNew, _speciesOwned[s],
                _variantsFound[s], _variantCount[s]);
        }

        /// <summary>Mastery of a species (level, XP in level, XP for next). Empty without a mastery service.</summary>
        public MasteryProgress GetMastery(int codexIndex)
        {
            var species = _content.Species[_speciesOrder[codexIndex]].Id;
            return _mastery != null ? _mastery.Get(species) : default;
        }

        public bool HasMastery => _mastery != null;

        public int GetVariantCount(int codexIndex) => _variantCount[_speciesOrder[codexIndex]];

        public CodexVariantEntry GetVariant(int codexIndex, int slot)
        {
            int s = _speciesOrder[codexIndex];
            if ((uint)slot >= (uint)_variantCount[s])
                throw new ArgumentOutOfRangeException(nameof(slot));
            int v = _variantOrder[_variantStart[s] + slot];
            var definition = _content.Variants[v];
            bool discovered = _state.Collection.DiscoveredVariants.Contains(definition.Id);
            bool isNew = discovered && !_state.Codex.SeenVariants.Contains(definition.Id);
            return new CodexVariantEntry(definition.Id, definition.DisplayKey, definition.Rarity, discovered, isNew, _variantOwned[v]);
        }

        /// <summary>Codex index of a species, or -1.</summary>
        public int IndexOf(ContentId species)
        {
            if (!_content.Species.TryGetIndex(species, out int s))
                return -1;
            return Array.IndexOf(_speciesOrder, s);
        }

        /// <summary>T36: a species page was opened (the « look at {species} in the Codex » wish).</summary>
        public event Action<ContentId>? SpeciesPageOpened;

        /// <summary>The species sheet was opened: its discoveries stop being "New".</summary>
        public void MarkSeen(int codexIndex)
        {
            int s = _speciesOrder[codexIndex];
            var codex = _state.Codex;
            var collection = _state.Collection;
            bool changed = false;
            var id = _content.Species[s].Id;
            SpeciesPageOpened?.Invoke(id);
            if (collection.DiscoveredSpecies.Contains(id))
                changed |= codex.SeenSpecies.Add(id);
            for (int k = 0; k < _variantCount[s]; k++)
            {
                var variant = _content.Variants[_variantOrder[_variantStart[s] + k]].Id;
                if (collection.DiscoveredVariants.Contains(variant))
                    changed |= codex.SeenVariants.Add(variant);
            }
            if (changed)
                _save?.RequestSave(SaveUrgency.Normal);
        }

        private bool HasNewVariant(int s)
        {
            var collection = _state.Collection;
            var seen = _state.Codex.SeenVariants;
            for (int k = 0; k < _variantCount[s]; k++)
            {
                var variant = _content.Variants[_variantOrder[_variantStart[s] + k]].Id;
                if (collection.DiscoveredVariants.Contains(variant) && !seen.Contains(variant))
                    return true;
            }
            return false;
        }

        private sealed class RarityOrder : IComparer<int>
        {
            private readonly ContentTable<VariantDefinition> _variants;

            public RarityOrder(ContentTable<VariantDefinition> variants) => _variants = variants;

            public int Compare(int a, int b)
            {
                int byRarity = _variants[a].Rarity.CompareTo(_variants[b].Rarity);
                return byRarity != 0 ? byRarity : a.CompareTo(b);
            }
        }
    }
}
