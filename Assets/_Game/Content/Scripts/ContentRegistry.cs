#nullable enable
using System;
using System.Collections.Generic;
using PocketWilds.Core;

namespace PocketWilds.Content
{
    public interface IContentDefinition
    {
        ContentId Id { get; }
    }

    /// <summary>
    /// Immutable lookup table for one definition type. Dense indexes are stable for a session only;
    /// saves and network payloads always use ContentId.
    /// </summary>
    public sealed class ContentTable<T> where T : class, IContentDefinition
    {
        private readonly T[] _items;
        private readonly Dictionary<ContentId, int> _indexById;

        internal ContentTable(List<T> items)
        {
            _items = items.ToArray();
            _indexById = new Dictionary<ContentId, int>(_items.Length);
            for (int i = 0; i < _items.Length; i++)
                _indexById.Add(_items[i].Id, i);
        }

        public static ContentTable<T> Empty { get; } = new ContentTable<T>(new List<T>());

        public int Count => _items.Length;

        public T this[int denseIndex] => _items[denseIndex];

        public bool TryGetIndex(ContentId id, out int denseIndex) => _indexById.TryGetValue(id, out denseIndex);

        public bool TryGet(ContentId id, out T definition)
        {
            if (_indexById.TryGetValue(id, out int index))
            {
                definition = _items[index];
                return true;
            }
            definition = null!;
            return false;
        }

        public T Get(ContentId id)
        {
            if (!_indexById.TryGetValue(id, out int index))
                throw new KeyNotFoundException("Unknown content id " + id);
            return _items[index];
        }
    }

    /// <summary>
    /// Runtime content registry built once at boot from validated definitions. O(1) lookups by ContentId.
    /// </summary>
    public sealed class ContentRegistry
    {
        public ContentRegistry(ContentTable<SpeciesDefinition> species, ContentTable<VariantDefinition> variants, ContentTable<EggDefinition> eggs,
            ContentTable<PaletteDefinition> palettes, ContentTable<BiomeDefinition> biomes)
            : this(species, variants, eggs, palettes, biomes, ShopContent.Empty)
        {
        }

        public ContentRegistry(ContentTable<SpeciesDefinition> species, ContentTable<VariantDefinition> variants, ContentTable<EggDefinition> eggs,
            ContentTable<PaletteDefinition> palettes, ContentTable<BiomeDefinition> biomes, ShopContent shop)
        {
            Species = species;
            Variants = variants;
            Eggs = eggs;
            Palettes = palettes;
            Biomes = biomes;
            Shop = shop;
        }

        public ContentRegistry(ContentTable<SpeciesDefinition> species, ContentTable<VariantDefinition> variants, ContentTable<EggDefinition> eggs,
            ContentTable<PaletteDefinition> palettes)
            : this(species, variants, eggs, palettes, ContentTable<BiomeDefinition>.Empty)
        {
        }

        public ContentRegistry(ContentTable<SpeciesDefinition> species, ContentTable<VariantDefinition> variants, ContentTable<EggDefinition> eggs)
            : this(species, variants, eggs, ContentTable<PaletteDefinition>.Empty)
        {
        }

        public ContentRegistry(ContentTable<SpeciesDefinition> species, ContentTable<VariantDefinition> variants)
            : this(species, variants, ContentTable<EggDefinition>.Empty)
        {
        }

        public ContentRegistry(ContentTable<SpeciesDefinition> species)
            : this(species, ContentTable<VariantDefinition>.Empty, ContentTable<EggDefinition>.Empty)
        {
        }

        public static ContentRegistry Empty { get; } = new ContentRegistry(ContentTable<SpeciesDefinition>.Empty);

        public ContentTable<SpeciesDefinition> Species { get; }
        public ContentTable<VariantDefinition> Variants { get; }
        public ContentTable<EggDefinition> Eggs { get; }

        /// <summary>World palettes (props, ground).</summary>
        public ContentTable<PaletteDefinition> Palettes { get; }

        /// <summary>Biomes in progression order. May be empty in tests; the Codex then uses one section.</summary>
        public ContentTable<BiomeDefinition> Biomes { get; }

        /// <summary>Store products, offers and grantable items (T28). Empty when no catalog ships.</summary>
        public ShopContent Shop { get; }
    }

    /// <summary>Shop tables of the registry.</summary>
    public sealed class ShopContent
    {
        public ShopContent(ContentTable<ProductDefinition> products, ContentTable<OfferDefinition> offers, ContentTable<EntitlementDefinition> entitlements,
            int unlockMinAnimals)
        {
            Products = products;
            Offers = offers;
            Entitlements = entitlements;
            UnlockMinAnimals = unlockMinAnimals;
        }

        public static ShopContent Empty { get; } = new ShopContent(ContentTable<ProductDefinition>.Empty, ContentTable<OfferDefinition>.Empty,
            ContentTable<EntitlementDefinition>.Empty, int.MaxValue);

        public ContentTable<ProductDefinition> Products { get; }

        /// <summary>Offers by descending priority within each placement.</summary>
        public ContentTable<OfferDefinition> Offers { get; }

        public ContentTable<EntitlementDefinition> Entitlements { get; }

        /// <summary>Animals owned before the shop shortcut shows (int.MaxValue: no shop).</summary>
        public int UnlockMinAnimals { get; }
    }

    public enum ContentIssueSeverity
    {
        Warning,
        Error,
    }

    public readonly struct ContentIssue
    {
        public readonly ContentIssueSeverity Severity;
        public readonly string SourceId;
        public readonly string Message;

        public ContentIssue(ContentIssueSeverity severity, string sourceId, string message)
        {
            Severity = severity;
            SourceId = sourceId;
            Message = message;
        }

        public override string ToString() => $"{Severity} [{SourceId}] {Message}";
    }

    /// <summary>
    /// Collects definitions, rejects duplicates, hash collisions and tombstoned IDs,
    /// then builds the immutable registry. Errors must be fixed before a build ships.
    /// </summary>
    public sealed class ContentRegistryBuilder
    {
        private readonly List<SpeciesDefinition> _species = new List<SpeciesDefinition>();
        private readonly List<VariantDefinition> _variants = new List<VariantDefinition>();
        private readonly List<EggDefinition> _eggs = new List<EggDefinition>();
        private readonly List<PaletteDefinition> _palettes = new List<PaletteDefinition>();
        private readonly List<BiomeDefinition> _biomes = new List<BiomeDefinition>();
        private readonly List<ProductDefinition> _products = new List<ProductDefinition>();
        private readonly List<OfferDefinition> _offers = new List<OfferDefinition>();
        private readonly List<EntitlementDefinition> _entitlements = new List<EntitlementDefinition>();
        private int _shopUnlockMinAnimals = int.MaxValue;
        private readonly Dictionary<uint, string> _seenHashes = new Dictionary<uint, string>();
        private readonly HashSet<uint> _tombstones = new HashSet<uint>();
        private readonly List<ContentIssue> _issues = new List<ContentIssue>();

        public IReadOnlyList<ContentIssue> Issues => _issues;

        public bool HasErrors
        {
            get
            {
                for (int i = 0; i < _issues.Count; i++)
                {
                    if (_issues[i].Severity == ContentIssueSeverity.Error)
                        return true;
                }
                return false;
            }
        }

        /// <summary>Retired IDs. A tombstoned ID can never be used again, for any content type.</summary>
        public void AddTombstone(string sourceId)
        {
            _tombstones.Add(ContentIdHash.Compute(sourceId));
        }

        public void AddSpecies(SpeciesDefinition definition, string sourceId)
        {
            if (TryClaim(definition.Id, sourceId))
                _species.Add(definition);
        }

        public void AddVariant(VariantDefinition definition, string sourceId)
        {
            if (definition.Palette == null || definition.Palette.Length != VariantDefinition.PaletteSize)
            {
                _issues.Add(new ContentIssue(ContentIssueSeverity.Error, sourceId, "Palette must have " + VariantDefinition.PaletteSize + " colors."));
                return;
            }
            if (TryClaim(definition.Id, sourceId))
                _variants.Add(definition);
        }

        public void AddEgg(EggDefinition definition, string sourceId)
        {
            if (definition.Palette == null || definition.Palette.Length != EggDefinition.PaletteSize)
            {
                _issues.Add(new ContentIssue(ContentIssueSeverity.Error, sourceId, "Palette must have " + EggDefinition.PaletteSize + " colors."));
                return;
            }
            if (definition.MaskStrengthR > 15 || definition.MaskStrengthG > 15 || definition.MaskStrengthB > 15)
            {
                _issues.Add(new ContentIssue(ContentIssueSeverity.Error, sourceId, "Mask strengths are 0 to 15."));
                return;
            }
            if (string.IsNullOrEmpty(definition.VisualKey))
            {
                _issues.Add(new ContentIssue(ContentIssueSeverity.Error, sourceId, "Egg needs a visual key."));
                return;
            }
            if (!(definition.BaseCost > 0.0) || !(definition.CostGrowth >= 1.0))
            {
                _issues.Add(new ContentIssue(ContentIssueSeverity.Error, sourceId, "Egg cost needs BaseCost > 0 and CostGrowth >= 1."));
                return;
            }
            if (definition.RarityWeights.Length != EggDefinition.RarityTierCount || !HasPositive(definition.RarityWeights))
            {
                _issues.Add(new ContentIssue(ContentIssueSeverity.Error, sourceId, "Egg needs " + EggDefinition.RarityTierCount + " rarity weights with a positive total."));
                return;
            }
            // A Discovery egg picks by its own rules (doc 02 Œufs, section 2.3): no pool, no starter.
            if (definition.Tier == EggTier.Discovery ? definition.Pool.Length > 0 || definition.StarterSpecies.IsValid : definition.Pool.Length == 0)
            {
                _issues.Add(new ContentIssue(ContentIssueSeverity.Error, sourceId,
                    definition.Tier == EggTier.Discovery ? "A Discovery egg has no pool and no starter." : "Egg pool is empty."));
                return;
            }
            if (definition.UnlockBiomeMastery < 0 || definition.FocusTargetCount < 0)
            {
                _issues.Add(new ContentIssue(ContentIssueSeverity.Error, sourceId, "Egg unlock mastery and Focus targets are 0 or more."));
                return;
            }
            // Doc 02 Œufs, section 7: every table sums to 100 %.
            double sum = 0.0;
            foreach (var weight in definition.RarityWeights)
                sum += weight;
            if (Math.Abs(sum - 100.0) > 0.01)
            {
                _issues.Add(new ContentIssue(ContentIssueSeverity.Error, sourceId, $"Egg rarity table sums to {sum:0.###} %, not 100 %."));
                return;
            }
            if (definition.PityHardLimit < 1)
            {
                _issues.Add(new ContentIssue(ContentIssueSeverity.Error, sourceId, "Egg pity hard limit must be at least 1."));
                return;
            }
            if (TryClaim(definition.Id, sourceId))
                _eggs.Add(definition);
        }

        public void AddPalette(PaletteDefinition definition, string sourceId)
        {
            if (definition.Colors == null || definition.Colors.Length != PaletteDefinition.PaletteSize)
            {
                _issues.Add(new ContentIssue(ContentIssueSeverity.Error, sourceId, "Palette must have " + PaletteDefinition.PaletteSize + " colors."));
                return;
            }
            if (TryClaim(definition.Id, sourceId))
                _palettes.Add(definition);
        }

        public void AddBiome(BiomeDefinition definition, string sourceId)
        {
            if (string.IsNullOrEmpty(definition.DisplayKey))
            {
                _issues.Add(new ContentIssue(ContentIssueSeverity.Error, sourceId, "Biome needs a display key."));
                return;
            }
            if (TryClaim(definition.Id, sourceId))
                _biomes.Add(definition);
        }

        public void SetShopUnlock(int minAnimals)
        {
            if (minAnimals < 0)
            {
                _issues.Add(new ContentIssue(ContentIssueSeverity.Error, "shop", "Shop unlock needs a non-negative animal count."));
                return;
            }
            _shopUnlockMinAnimals = minAnimals;
        }

        public void AddEntitlement(EntitlementDefinition definition, string sourceId)
        {
            if (string.IsNullOrEmpty(definition.DisplayKey) || string.IsNullOrEmpty(definition.IconKey))
            {
                _issues.Add(new ContentIssue(ContentIssueSeverity.Error, sourceId, "Entitlement needs a display key and an icon key."));
                return;
            }
            if (TryClaim(definition.Id, sourceId))
                _entitlements.Add(definition);
        }

        public void AddProduct(ProductDefinition definition, string sourceId)
        {
            if (string.IsNullOrEmpty(definition.StoreSku))
            {
                _issues.Add(new ContentIssue(ContentIssueSeverity.Error, sourceId, "Product needs a store SKU."));
                return;
            }
            if (definition.ReferencePriceCents <= 0)
            {
                _issues.Add(new ContentIssue(ContentIssueSeverity.Error, sourceId, "Product needs a positive reference price."));
                return;
            }
            if (definition.Gems < 0 || (definition.Gems == 0 && definition.Entitlements.Length == 0))
            {
                _issues.Add(new ContentIssue(ContentIssueSeverity.Error, sourceId, "Product must grant Gems or items, never a negative amount."));
                return;
            }
            if (TryClaim(definition.Id, sourceId))
                _products.Add(definition);
        }

        public void AddOffer(OfferDefinition definition, string sourceId)
        {
            if (definition.Product.IsValid == definition.IsGemPriced)
            {
                _issues.Add(new ContentIssue(ContentIssueSeverity.Error, sourceId, "Offer sells either a store product or items for Gems, not both."));
                return;
            }
            if (definition.IsGemPriced && definition.GemCostEntitlements.Length == 0)
            {
                _issues.Add(new ContentIssue(ContentIssueSeverity.Error, sourceId, "Gem-priced offer grants nothing."));
                return;
            }
            if (definition.PurchaseLimit < 0 || definition.CooldownSeconds < 0 || definition.MinAnimals < 0 || definition.MinDiscoveredSpecies < 0)
            {
                _issues.Add(new ContentIssue(ContentIssueSeverity.Error, sourceId, "Offer limits, cooldown and eligibility must not be negative."));
                return;
            }
            if (definition.HasEndDate && definition.EndUnixSeconds <= definition.StartUnixSeconds)
            {
                _issues.Add(new ContentIssue(ContentIssueSeverity.Error, sourceId, "Offer ends before it starts."));
                return;
            }
            if ((definition.Placement == OfferPlacement.ShopFeatured || definition.Placement == OfferPlacement.Moment) && string.IsNullOrEmpty(definition.DisplayKey))
            {
                _issues.Add(new ContentIssue(ContentIssueSeverity.Error, sourceId, "Featured and moment offers need a title key."));
                return;
            }
            if (string.IsNullOrEmpty(definition.ArtKey))
            {
                _issues.Add(new ContentIssue(ContentIssueSeverity.Error, sourceId, "Offer needs an art key."));
                return;
            }
            if (TryClaim(definition.Id, sourceId))
                _offers.Add(definition);
        }

        public ContentRegistry Build()
        {
            var speciesIds = new HashSet<ContentId>();
            foreach (var species in _species)
                speciesIds.Add(species.Id);
            foreach (var variant in _variants)
            {
                if (!speciesIds.Contains(variant.Species))
                    _issues.Add(new ContentIssue(ContentIssueSeverity.Error, variant.Id.ToString(), "Variant references an unknown species."));
            }
            foreach (var egg in _eggs)
            {
                foreach (var entry in egg.Pool)
                {
                    if (!speciesIds.Contains(entry.Species) || !(entry.Weight > 0.0))
                        _issues.Add(new ContentIssue(ContentIssueSeverity.Error, egg.Id.ToString(), "Egg pool entry needs a known species and a positive weight."));
                    // Doc 02 Œufs, section 7: every rarity of the table falls back to a normal variant (secret variants never
                    // enter the normal tables: HatchService leaves them out).
                    var species = _species.Find(s => s.Id == entry.Species);
                    if (species != null && !_variants.Exists(v => v.Species == entry.Species && !v.IsSecret))
                        _issues.Add(new ContentIssue(ContentIssueSeverity.Error, egg.Id.ToString(), "Every egg pool species needs a normal variant."));
                }
                if (egg.StarterSpecies.IsValid && Array.FindIndex(egg.Pool, e => e.Species == egg.StarterSpecies) < 0)
                    _issues.Add(new ContentIssue(ContentIssueSeverity.Error, egg.Id.ToString(), "Starter species must be in the egg pool."));
            }

            if (_biomes.Count > 0)
            {
                var biomeIds = new HashSet<ContentId>();
                foreach (var biome in _biomes)
                    biomeIds.Add(biome.Id);
                foreach (var species in _species)
                {
                    if (!biomeIds.Contains(species.Biome))
                        _issues.Add(new ContentIssue(ContentIssueSeverity.Warning, species.Id.ToString(), "Species biome is not defined: it gets no Codex section."));
                }
            }

            ValidateShop();

            if (HasErrors)
                throw new InvalidOperationException("Content has errors: " + string.Join("; ", _issues));
            _biomes.Sort((a, b) => a.Order.CompareTo(b.Order));
            // Stable order inside each placement: priority first, then id (deterministic across builds).
            _offers.Sort((a, b) => a.Placement != b.Placement ? a.Placement.CompareTo(b.Placement)
                : a.Priority != b.Priority ? b.Priority.CompareTo(a.Priority) : a.Id.CompareTo(b.Id));
            var shop = new ShopContent(new ContentTable<ProductDefinition>(_products), new ContentTable<OfferDefinition>(_offers),
                new ContentTable<EntitlementDefinition>(_entitlements), _shopUnlockMinAnimals);
            return new ContentRegistry(new ContentTable<SpeciesDefinition>(_species), new ContentTable<VariantDefinition>(_variants),
                new ContentTable<EggDefinition>(_eggs), new ContentTable<PaletteDefinition>(_palettes), new ContentTable<BiomeDefinition>(_biomes), shop);
        }

        /// <summary>Cross references: every granted item and sold product exists, SKUs are unique.</summary>
        private void ValidateShop()
        {
            var entitlementIds = new HashSet<ContentId>();
            foreach (var e in _entitlements)
                entitlementIds.Add(e.Id);
            var products = new Dictionary<ContentId, ProductDefinition>();
            var skus = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in _products)
            {
                products[p.Id] = p;
                if (!skus.Add(p.StoreSku))
                    _issues.Add(new ContentIssue(ContentIssueSeverity.Error, p.Id.ToString(), "Store SKU " + p.StoreSku + " is used twice."));
                foreach (var e in p.Entitlements)
                {
                    if (!entitlementIds.Contains(e))
                        _issues.Add(new ContentIssue(ContentIssueSeverity.Error, p.Id.ToString(), "Product grants an unknown item."));
                }
                if (p.Kind == ProductKind.Consumable && p.Entitlements.Length > 0)
                    _issues.Add(new ContentIssue(ContentIssueSeverity.Error, p.Id.ToString(), "Permanent items must be sold as a non-consumable product."));
            }
            foreach (var o in _offers)
            {
                if (o.Product.IsValid)
                {
                    if (!products.TryGetValue(o.Product, out var product))
                        _issues.Add(new ContentIssue(ContentIssueSeverity.Error, o.Id.ToString(), "Offer sells an unknown product."));
                    else if (product.Kind == ProductKind.NonConsumable && o.PurchaseLimit != 1)
                        _issues.Add(new ContentIssue(ContentIssueSeverity.Error, o.Id.ToString(), "A one-time product needs a purchase limit of 1."));
                }
                foreach (var e in o.GemCostEntitlements)
                {
                    if (!entitlementIds.Contains(e))
                        _issues.Add(new ContentIssue(ContentIssueSeverity.Error, o.Id.ToString(), "Offer grants an unknown item."));
                }
            }
        }

        private static bool HasPositive(double[] values)
        {
            double total = 0.0;
            foreach (var v in values)
            {
                if (v < 0.0)
                    return false;
                total += v;
            }
            return total > 0.0;
        }

        private bool TryClaim(ContentId id, string sourceId)
        {
            if (!ContentIdFormat.IsValid(sourceId))
            {
                _issues.Add(new ContentIssue(ContentIssueSeverity.Error, sourceId, "Invalid ID format."));
                return false;
            }

            if (ContentIdHash.Compute(sourceId) != id.Value)
            {
                _issues.Add(new ContentIssue(ContentIssueSeverity.Error, sourceId, "ContentId does not match its source ID."));
                return false;
            }

            if (_tombstones.Contains(id.Value))
            {
                _issues.Add(new ContentIssue(ContentIssueSeverity.Error, sourceId, "ID is tombstoned and cannot be reused."));
                return false;
            }

            if (_seenHashes.TryGetValue(id.Value, out var existing))
            {
                string message = existing == sourceId
                    ? "Duplicate ID."
                    : "Hash collision with '" + existing + "'. Rename one of them.";
                _issues.Add(new ContentIssue(ContentIssueSeverity.Error, sourceId, message));
                return false;
            }

            _seenHashes.Add(id.Value, sourceId);
            return true;
        }
    }
}
