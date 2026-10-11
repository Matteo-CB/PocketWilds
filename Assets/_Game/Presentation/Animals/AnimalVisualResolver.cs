using System.Collections.Generic;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Simulation;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// Turns persistent animal data into an <see cref="AnimalVisualSnapshot"/>. Every variant is
    /// packed once at boot, so resolving is two dictionary reads and a hash: no allocation.
    /// An unknown or removed variant falls back to the first variant of its species, never to a
    /// missing animal.
    /// </summary>
    public sealed class AnimalVisualResolver
    {
        private const uint IdlePhaseSalt = 0x1D1E0001u;

        private readonly Dictionary<ContentId, uint> _visualByVariant;
        private readonly Dictionary<ContentId, uint> _defaultBySpecies;

        public AnimalVisualResolver(ContentRegistry registry, PaletteTable palettes)
        {
            var variants = registry.Variants;
            _visualByVariant = new Dictionary<ContentId, uint>(variants.Count);
            _defaultBySpecies = new Dictionary<ContentId, uint>(registry.Species.Count);
            for (int i = 0; i < variants.Count; i++)
            {
                var variant = variants[i];
                if (!palettes.TryGetRow(variant.Id, out int row))
                    continue;
                uint packed = AnimalVisualPacking.Pack(row, variant.PatternChannel, variant.PatternStrength, 0, variant.ModuleSlot);
                _visualByVariant.Add(variant.Id, packed);
                if (!_defaultBySpecies.ContainsKey(variant.Species))
                    _defaultBySpecies.Add(variant.Species, packed);
            }
        }

        /// <summary>Packed visual data for a variant, or the species default. False when the species has no palette.</summary>
        public bool TryGetVisualData(ContentId species, ContentId variant, out uint visualData)
        {
            if (_visualByVariant.TryGetValue(variant, out visualData))
                return true;
            return _defaultBySpecies.TryGetValue(species, out visualData);
        }

        public bool TryResolve(in AnimalInstance animal, Vector3 position, float yaw, out AnimalVisualSnapshot snapshot)
        {
            bool known = TryGetVisualData(animal.Species, animal.Variant, out uint visualData);
            // T26 genome: a carried pattern shows tone on tone on a palette without its own pattern.
            var carried = AnimalGenome.CarriedPattern(animal.TraitBits);
            if (carried != PatternChannel.None && AnimalVisualPacking.Pattern(visualData) == PatternChannel.None)
                visualData = AnimalVisualPacking.Pack(AnimalVisualPacking.PaletteRow(visualData), carried,
                    AnimalGenome.CarriedStrength(animal.TraitBits), AnimalVisualPacking.RareIntensity(visualData),
                    AnimalVisualPacking.ModuleSlot(visualData));
            // T34A SH_RareGlow: Legendary, Golden and Fabulous animals carry their rim glow level.
            int rare = RareGlow.ShaderLevel(animal.Rarity, animal.TraitBits);
            if (rare != AnimalVisualPacking.RareIntensity(visualData))
                visualData = AnimalVisualPacking.Pack(AnimalVisualPacking.PaletteRow(visualData), AnimalVisualPacking.Pattern(visualData),
                    AnimalVisualPacking.PatternStrength(visualData), rare, AnimalVisualPacking.ModuleSlot(visualData));
            snapshot = new AnimalVisualSnapshot
            {
                InstanceId = animal.InstanceId,
                Species = animal.Species,
                Position = position,
                Yaw = yaw,
                Scale = AnimalGenome.Scale(animal.TraitBits),
                VisualData = visualData,
                IdlePhase = IdlePhase(animal.Seed),
            };
            return known;
        }

        /// <summary>Stable idle start so neighbours never breathe in sync, and a view keeps its rhythm when reacquired.</summary>
        public static float IdlePhase(uint seed) => SeedHash.Unit(seed, IdlePhaseSalt);
    }
}
