#nullable enable
using PocketWilds.Content;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>
    /// T44B: the achievements that count a saved state (species, variants, levels, masteries, bonds, decorations, albums;
    /// doc 01 Succès, section 1 « Ce qui compte ») computed from the save, and the profile items those states unlock. Raised,
    /// never lowered: run at load (the backfill of an older account) and again by <see cref="AchievementHooks"/> when the
    /// state behind a group changes. Buffers are allocated once; a recount never allocates.
    /// </summary>
    public sealed class AchievementRecount
    {
        public const int BondLevel1 = 100;
        public const int BondLevel3 = 450;
        public const int BondLevel5 = 1000;

        /// <summary>Non-secret variants of the 6 species of a biome that complete its Codex (6 × 9).</summary>
        public const int BiomeCodexVariants = 54;

        private const int Biomes = AlbumState.Biomes;

        private readonly AchievementService _achievements;
        private readonly ProfileItemService? _items;
        private readonly PlayerState _state;
        private readonly ContentRegistry? _content;
        private readonly int[] _biomeVariants = new int[Biomes];
        private readonly int[] _biomePlainVariants = new int[Biomes];
        private readonly int[] _biomeBonds = new int[Biomes];
        private readonly int[] _speciesPlainVariants = new int[ProfileData.SpeciesCount];
        private readonly int[] _ownedOnPage = new int[AlbumState.Pages];

        public AchievementRecount(AchievementService achievements, ProfileItemService? items, PlayerState state, ContentRegistry? content)
        {
            _achievements = achievements;
            _items = items;
            _state = state;
            _content = content;
        }

        /// <summary>Every group (the backfill).</summary>
        public void All()
        {
            Collection();
            Bonds();
            Mastery();
            World();
            Activities();
            Daily();
            ExpeditionsAndGarden();
            Albums();
            Decorations();
            Renewal();
            RepairExtras();
        }

        // ------------------------------------------------------------------------------------------- collection

        /// <summary>Species, variants (all, secret, per biome), full base sets, biome Codex, Codex share, names, favorites.</summary>
        public void Collection()
        {
            var c = _state.Collection;
            var a = _achievements;
            a.Raise(AchievementCounter.SpeciesDiscovered, c.DiscoveredSpecies.Count);
            a.Raise(AchievementCounter.VariantsDiscovered, c.DiscoveredVariants.Count);
            a.Raise(AchievementCounter.AnimalsNamed, c.CustomNames.Count);

            int favorites = 0;
            for (int i = 0; i < c.Count; i++)
            {
                ref readonly var animal = ref c[i];
                if (animal.IsFavorite)
                    favorites++;
                int species = ProfileData.SpeciesIndex(animal.Species);
                if (species >= 0)
                    a.AddSpeciesTraits(species, (int)animal.TraitBits);
            }
            a.Raise(AchievementCounter.Favorites, favorites);

            if (_content == null)
                return;
            System.Array.Clear(_biomeVariants, 0, _biomeVariants.Length);
            System.Array.Clear(_biomePlainVariants, 0, _biomePlainVariants.Length);
            System.Array.Clear(_speciesPlainVariants, 0, _speciesPlainVariants.Length);
            int secret = 0, plain = 0;
            foreach (var id in c.DiscoveredVariants)
            {
                // T44C: a visitor's variant has its own Codex page; it never counts with the collection's variants.
                if (!_content.Variants.TryGet(id, out var variant) || variant.IsVisitor || variant.MasteryLevel > 0)
                    continue;
                if (variant.IsSecret)
                    secret++;
                else
                    plain++;
                int biome = BiomeOfSpecies(variant.Species);
                if (biome >= 0)
                {
                    _biomeVariants[biome]++;
                    if (!variant.IsSecret)
                        _biomePlainVariants[biome]++;
                }
                int species = ProfileData.SpeciesIndex(variant.Species);
                if (species >= 0 && !variant.IsSecret)
                    _speciesPlainVariants[species]++;
            }
            a.Raise(AchievementCounter.SecretVariants, secret);
            int fullSets = 0;
            for (int s = 0; s < _speciesPlainVariants.Length; s++)
                if (_speciesPlainVariants[s] >= 6)
                    fullSets++;
            a.Raise(AchievementCounter.BaseSetsComplete, fullSets);
            int codexBiomes = 0;
            for (int b = 0; b < Biomes; b++)
            {
                a.Raise(AchievementCounter.BiomeVariants, _biomeVariants[b], b);
                if (_biomePlainVariants[b] >= BiomeCodexVariants)
                    codexBiomes++;
            }
            a.Raise(AchievementCounter.BiomeCodexComplete, codexBiomes);

            // The Codex share: non-secret variants found over those the build holds.
            int total = 0;
            var all = _content.Variants;
            for (int i = 0; i < all.Count; i++)
                if (!all[i].IsSecret)
                    total++;
            if (total > 0)
                a.Raise(AchievementCounter.CodexPercent, (long)plain * 100 / total);
        }

        /// <summary>Animals at bond 1, 3 and 5, per biome at bond 3, event animals at bond 5, the bond points held.</summary>
        public void Bonds()
        {
            var c = _state.Collection;
            int b1 = 0, b3 = 0, b5 = 0, event5 = 0;
            long points = 0;
            System.Array.Clear(_biomeBonds, 0, _biomeBonds.Length);
            for (int i = 0; i < c.Count; i++)
            {
                ref readonly var animal = ref c[i];
                int bond = animal.BondPoints;
                points += bond;
                if (bond >= BondLevel1)
                    b1++;
                if (bond >= BondLevel3)
                {
                    b3++;
                    int biome = BiomeOfSpecies(animal.Species);
                    if (biome >= 0)
                        _biomeBonds[biome]++;
                }
                if (bond >= BondLevel5)
                {
                    b5++;
                    if (ProfileData.IsEventSpecies(animal.Species))
                        event5++;
                }
            }
            var a = _achievements;
            a.Raise(AchievementCounter.Bond1Animals, b1);
            a.Raise(AchievementCounter.Bond3Animals, b3);
            a.Raise(AchievementCounter.Bond5Animals, b5);
            a.Raise(AchievementCounter.EventAnimalsBond5, event5);
            // Points earned are at least the points held (an animal gives at most 1 000).
            a.Raise(AchievementCounter.BondPoints, points);
            for (int b = 0; b < Biomes; b++)
                a.Raise(AchievementCounter.BiomeBonds, _biomeBonds[b], b);
        }

        // ------------------------------------------------------------------------------------------- mastery and world

        /// <summary>Species mastery (levels 5, 10, 20, 30, milestones every 5 levels, total, event animals) and its titles.</summary>
        public void Mastery()
        {
            int m5 = 0, m10 = 0, m20 = 0, m30 = 0, event10 = 0;
            long milestones = 0, levels = 0;
            foreach (var pair in _state.Mastery.Species)
            {
                int level = pair.Value.Level;
                levels += level;
                milestones += level / 5;
                if (level >= 5)
                    m5++;
                if (level >= 10)
                {
                    m10++;
                    if (ProfileData.IsEventSpecies(pair.Key))
                        event10++;
                }
                if (level >= 20)
                    m20++;
                if (level >= 30)
                    m30++;
                _items?.OnSpeciesMastery(pair.Key, level);
            }
            var a = _achievements;
            a.Raise(AchievementCounter.SpeciesMastery5, m5);
            a.Raise(AchievementCounter.SpeciesMastery10, m10);
            a.Raise(AchievementCounter.SpeciesMastery20, m20);
            a.Raise(AchievementCounter.SpeciesMastery30, m30);
            a.Raise(AchievementCounter.MasteryMilestones, milestones);
            a.Raise(AchievementCounter.MasteryLevels, levels);
            a.Raise(AchievementCounter.EventSpeciesMastery10, event10);
        }

        /// <summary>Refuge level, biomes unlocked, zones open, biome mastery (12, 36 and its titles and frames), Light upgrades.</summary>
        public void World()
        {
            var a = _achievements;
            int level = _state.Progression.Refuge.Level;
            a.Raise(AchievementCounter.RefugeLevel, level);
            _items?.OnRefugeLevel(level);

            var biomes = _state.World.Biomes;
            int unlocked = 0, zones = 0, mastery12 = 0, mastery36 = 0;
            long light = 0;
            for (int i = 0; i < biomes.Count; i++)
            {
                var b = biomes[i];
                light += b.ProductionLevel + b.NestsLevel + b.CuriosityLevel;
                int index = AlbumData.BiomeIndex(b.Biome);
                if (b.MasteryLevel >= 12)
                    mastery12++;
                if (b.MasteryLevel >= 36)
                    mastery36++;
                if (index >= 0)
                    _items?.OnBiomeMastery(index, b.MasteryLevel);
                if (!b.Unlocked)
                    continue;
                unlocked++;
                // The arrival zone is open with its biome.
                zones += PopCount(b.ZonesOpened | 1u);
            }
            a.Raise(AchievementCounter.BiomesUnlocked, unlocked > 0 ? unlocked : 1);
            a.Raise(AchievementCounter.ZonesOpen, zones);
            a.Raise(AchievementCounter.BiomeMastery12, mastery12);
            a.Raise(AchievementCounter.BiomeMastery36, mastery36);
            // The upgrade levels held now: bought in this run, and at least that many over all runs.
            a.RaiseRunLightLevels(light);
            a.Raise(AchievementCounter.LightLevels, light);
        }

        // ------------------------------------------------------------------------------------------- systems

        /// <summary>Challenges at 3 stars, activities with all 10 at 3 stars, activities with a star.</summary>
        public void Activities()
        {
            var s = _state.Activities;
            var stars = s.ChallengeStars;
            int threeStars = 0, perfect = 0;
            int per = ActivityState.ChallengesPerActivity;
            for (int slot = 0; slot * per < stars.Length; slot++)
            {
                int full = 0;
                bool any = false;
                for (int i = 0; i < per && slot * per + i < stars.Length; i++)
                {
                    int v = stars[slot * per + i];
                    if (v >= 3)
                        full++;
                    any |= v > 0;
                }
                threeStars += full;
                if (full == per)
                    perfect++;
                if (any)
                    _achievements.AddDistinctBit(AchievementCounter.ActivitiesStarred, slot);
            }
            _achievements.Raise(AchievementCounter.ChallengesThreeStars, threeStars > s.ChallengesThreeStars ? threeStars : s.ChallengesThreeStars);
            _achievements.Raise(AchievementCounter.PerfectActivities, perfect);
        }

        /// <summary>Daily gifts opened, wishes granted, calendar boxes and calendars complete.</summary>
        public void Daily()
        {
            var d = _state.DailyVisit;
            var a = _achievements;
            a.Raise(AchievementCounter.DailyGifts, d.GiftsOpened);
            a.Raise(AchievementCounter.WishesGranted, d.WishesCompleted);
            a.Raise(AchievementCounter.CalendarDays, d.CalendarStamps);
            a.Raise(AchievementCounter.CalendarsComplete, d.CalendarStamps / 28);
        }

        /// <summary>Expeditions finished, 12 h ones, destinations; seeds planted, dyes, plants harvested.</summary>
        public void ExpeditionsAndGarden()
        {
            var a = _achievements;
            var albums = _state.Albums;
            a.Raise(AchievementCounter.ExpeditionsDone, _state.Expeditions.Completed);
            a.Raise(AchievementCounter.LongExpeditions, albums.LongExpeditions);
            for (int b = 0; b < Biomes; b++)
            {
                if ((albums.ExpeditionBiomes & (1 << b)) == 0)
                    continue;
                int d = ExpeditionCatalog.IndexOfBiome(AlbumData.BiomeId(b));
                if (d >= 0)
                    a.AddDistinctBit(AchievementCounter.Destinations, d);
            }
            a.Raise(AchievementCounter.SeedsPlanted, _state.Garden.Planted);
            a.Raise(AchievementCounter.DyesHarvested, albums.DyesObtained);
            for (int p = 0; p < 32; p++)
                if ((albums.PlantsHarvested & (1u << p)) != 0)
                    a.AddDistinctBit(AchievementCounter.PlantsHarvested, p);
        }

        /// <summary>Stickers owned, pages complete (all, event, season), biome albums complete, biome pages half full.</summary>
        public void Albums()
        {
            var s = _state.Albums;
            int owned = 0, complete = 0, events = 0, seasons = 0, half = 0, biomeAlbums = 0;
            for (int p = 0; p < AlbumState.Pages; p++)
            {
                int n = 0, first = p * AlbumData.StickersPerPage;
                for (int i = 0; i < AlbumData.StickersPerPage; i++)
                    if (s.IsOwned(first + i))
                        n++;
                _ownedOnPage[p] = n;
                owned += n;
                bool full = n >= AlbumData.StickersPerPage;
                var kind = AlbumData.Page(p).Kind;
                if (full)
                {
                    complete++;
                    if (kind == AlbumKind.Event)
                        events++;
                    else if (kind == AlbumKind.Season)
                        seasons++;
                }
                if (kind == AlbumKind.Biome && n * 2 >= AlbumData.StickersPerPage)
                    half++;
            }
            for (int b = 0; b < AlbumData.BiomeAlbums; b++)
            {
                int p1 = AlbumData.BiomePage(b, 1), p2 = AlbumData.BiomePage(b, 2);
                if (p1 >= 0 && p2 >= 0 && _ownedOnPage[p1] >= AlbumData.StickersPerPage && _ownedOnPage[p2] >= AlbumData.StickersPerPage)
                    biomeAlbums++;
            }
            var a = _achievements;
            a.Raise(AchievementCounter.StickersOwned, owned);
            a.Raise(AchievementCounter.PagesComplete, complete);
            a.Raise(AchievementCounter.EventPagesComplete, events);
            a.Raise(AchievementCounter.SeasonPagesComplete, seasons);
            a.Raise(AchievementCounter.BiomePagesHalf, half);
            a.Raise(AchievementCounter.BiomeAlbumsComplete, biomeAlbums);
        }

        /// <summary>Different decorations owned.</summary>
        public void Decorations() => _achievements.Raise(AchievementCounter.DecorationsOwned, _state.Progression.DecorationCopies.Count);

        /// <summary>Renewals, Essence upgrade levels, biomes at the last Renewal, the first Essence record.</summary>
        public void Renewal()
        {
            var a = _achievements;
            int renewals = _state.World.RenewalCount;
            a.Raise(AchievementCounter.Renewals, renewals);
            long ranks = 0;
            foreach (var pair in _state.Progression.EssenceRanks)
                ranks += pair.Value;
            a.Raise(AchievementCounter.EssenceLevels, ranks);
            a.Raise(AchievementCounter.BiomesAtRenewal, _state.Progression.Renewal.BiomesAtLast);
            // The first Renewal set the record (ACH-191 counts it).
            if (renewals > 0)
                a.Raise(AchievementCounter.EssenceRecords, 1);
        }

        /// <summary>Star extras of claimed Star tiers that are missing (an item list grown by an update): given again.</summary>
        public void RepairExtras()
        {
            if (_items == null)
                return;
            var state = _achievements.State;
            for (int i = 0; i < AchievementData.Count; i++)
            {
                if (state.Claimed[i] < (byte)AchievementTier.Star)
                    continue;
                string extra = AchievementData.Row(i).StarExtra;
                if (extra.Length > 0)
                    _items.Grant(extra);
            }
        }

        // ------------------------------------------------------------------------------------------- helpers

        /// <summary>The biome index of a species (−1: unknown species or a biome outside the 13).</summary>
        public int BiomeOfSpecies(ContentId species)
        {
            int index = ProfileData.SpeciesIndex(species);
            if (index >= 0 && index < ProfileData.FirstEventSpecies)
                return index / 6;
            if (_content != null && _content.Species.TryGet(species, out var definition))
                return AlbumData.BiomeIndex(definition.Biome);
            return -1;
        }

        private static int PopCount(uint v)
        {
            int n = 0;
            for (; v != 0; v &= v - 1)
                n++;
            return n;
        }
    }
}
