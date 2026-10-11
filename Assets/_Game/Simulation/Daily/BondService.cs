#nullable enable
using System;
using PocketWilds.Content;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>
    /// The bond of the refuge's animals (doc « 02 — Renouveau, Essence, maîtrises et liens », section 4.1; doc 01 Contenu,
    /// section 10.1; doc 08 econ.bond.*): 0 to 1 000 points per animal, never lowered.
    /// <list type="bullet">
    /// <item>A touch gives 1 point, counted at most 5 times a day per animal and 30 times a day in all (a touch past the
    /// caps still makes the animal react, it only gives nothing).</item>
    /// <item>A treat gives its points (10 to 18 for the 13 biome treats, by their place in <see cref="ProgressionConfig.Treats"/>;
    /// 20 for the Seed Cookie, 40 for the Golden Treat), +50 % rounded for the family the treat prefers. No daily cap: the
    /// treats owned are the only limit.</item>
    /// <item>A wish gives 15, through <see cref="IBondSink"/> (the wish service applies it).</item>
    /// <item>Essence: Comfort adds 10 % per rank for every animal, Best Friends 10 % more per rank for favorites.</item>
    /// </list>
    /// Every change is written back with <see cref="CollectionState.Update"/> and reported to
    /// <see cref="EconomyService.OnBondChanged"/>, so the production stays exact. Plain methods the facade calls; nothing
    /// allocates (the touch counters reuse one dictionary, cleared when the day moves forward).
    /// </summary>
    public sealed class BondService : IBondSink
    {
        /// <summary>Points of the 13 biome treats, in the order of <see cref="ProgressionConfig.Treats"/> (doc 01, section 10.2).</summary>
        private static readonly int[] BiomeTreatPoints = { 10, 10, 10, 12, 12, 12, 14, 14, 14, 16, 16, 16, 18 };

        /// <summary>Treats with no preferred family that one animal eats (doc 01, section 10.2; never sold, later sources).</summary>
        public static readonly ContentId SeedCookie = ContentId.FromSource("treat.seed_cookie");

        public static readonly ContentId GoldenTreat = ContentId.FromSource("treat.golden_treat");

        private const int SeedCookiePoints = 20;
        private const int GoldenTreatPoints = 40;

        /// <summary>The treat's preferred family eats it for +50 % (doc 01, section 10.1).</summary>
        private const double PreferredFamilyMultiplier = 1.5;

        private readonly DailyContext _ctx;

        public BondService(DailyContext ctx)
        {
            _ctx = ctx;
        }

        /// <summary>T47: the companions' own bond track (XP per source, 10 counted touches a day). Null: no companion system.</summary>
        public ICompanionBondSink? Companions { get; set; }

        /// <summary>An animal was touched; counted: inside the daily caps (wishes and the journal count only those).</summary>
        public event Action<uint, bool>? Touched;

        /// <summary>A treat was eaten: the animal, the treat and the bond points really added.</summary>
        public event Action<uint, ContentId, int>? TreatGiven;

        /// <summary>Bond points really added to an animal (after Essence and the cap), whatever the source.</summary>
        public event Action<uint, int>? BondGained;

        /// <summary>Touches counted today in all.</summary>
        public int TouchesToday
        {
            get
            {
                RollDay();
                return _ctx.State.DailyVisit.TouchesToday;
            }
        }

        /// <summary>
        /// The day moved forward (<see cref="DailyContext.TodayKey"/>): the touch caps start again. A key that went back
        /// (a time-zone change westward) keeps today's counters. Also done lazily by every touch.
        /// </summary>
        public void OnDayChanged() => RollDay();

        /// <summary>
        /// A touch on an animal (doc 02 Renouveau, section 4.1): +<see cref="DailyVisitConfig.BondTouch"/> while the animal has
        /// fewer than <see cref="DailyVisitConfig.BondTouchCapAnimal"/> counted touches today and the refuge fewer than
        /// <see cref="DailyVisitConfig.BondTouchCapTotal"/>. Returns whether it counted.
        /// </summary>
        public bool TryTouch(uint animal)
        {
            if (!_ctx.State.Collection.TryGetIndex(animal, out _))
                return false;
            RollDay();
            var visit = _ctx.State.DailyVisit;
            var config = _ctx.Config;
            bool counted = false;
            if (visit.TouchesToday < config.BondTouchCapTotal)
            {
                visit.TouchesByAnimal.TryGetValue(animal, out byte mine);
                int capAnimal = Companions != null && Companions.IsCompanion(animal) ? CompanionService.TouchesPerDay : config.BondTouchCapAnimal;
                if (mine < capAnimal && mine < byte.MaxValue)
                {
                    visit.TouchesByAnimal[animal] = (byte)(mine + 1);
                    visit.TouchesToday++;
                    counted = true;
                }
            }
            Touched?.Invoke(animal, counted);
            if (counted)
            {
                if (config.BondTouch > 0)
                    AddBond(animal, config.BondTouch, BondSource.Touch);
                else
                    _ctx.RequestSave(SaveUrgency.Normal);
            }
            return counted;
        }

        /// <summary>
        /// Gives the animal one of the treats owned (the sheet's « Friandise » button, doc 01, section 10.1): a biome treat
        /// its family prefers first, else any biome treat; among those, the most plentiful, then the one worth most. The
        /// treat is used even when the bond is already full (the animal still eats it). False when none is owned.
        /// </summary>
        public bool TryGiveTreat(uint animal, out ContentId treat, out int points)
        {
            treat = BestTreatFor(animal);
            points = 0;
            return treat.IsValid && Feed(animal, treat, out points);
        }

        /// <summary>Gives one chosen treat (dragged from the quick inventory). False when not owned or not one animal's treat.</summary>
        public bool TryGiveTreat(uint animal, ContentId treat, out int points)
        {
            points = 0;
            return treat.IsValid && Feed(animal, treat, out points);
        }

        /// <summary>The treat <see cref="TryGiveTreat(uint, out ContentId, out int)"/> would give (None: no treat owned).</summary>
        public ContentId BestTreatFor(uint animal)
        {
            if (!_ctx.State.Collection.TryGet(animal, out var instance))
                return ContentId.None;
            bool known = _ctx.Content.Species.TryGet(instance.Species, out var species);
            var treats = _ctx.ProgressionConfig.Treats;
            var owned = _ctx.State.Progression.Treats;
            int best = -1;
            bool bestPreferred = false;
            int bestCount = 0;
            int bestPoints = 0;
            for (int i = 0; i < treats.Length; i++)
            {
                if (!owned.TryGetValue(treats[i].Id, out int count) || count <= 0)
                    continue;
                bool preferred = known && treats[i].PreferredFamily == species.RigFamily;
                int points = BiomePoints(i);
                bool better = best < 0
                    || (preferred && !bestPreferred)
                    || (preferred == bestPreferred && (count > bestCount || (count == bestCount && points > bestPoints)));
                if (!better)
                    continue;
                best = i;
                bestPreferred = preferred;
                bestCount = count;
                bestPoints = points;
            }
            return best < 0 ? ContentId.None : treats[best].Id;
        }

        /// <summary>
        /// Bond points a treat gives this animal before Essence: the treat's points, × 1.5 rounded for its preferred family.
        /// 0 for an unknown animal or a treat no single animal eats (the Treat Basket feeds a whole biome, later task).
        /// </summary>
        public int TreatPoints(uint animal, ContentId treat)
        {
            if (!_ctx.State.Collection.TryGet(animal, out var instance))
                return 0;
            return TreatPoints(instance, treat);
        }

        /// <summary>The treat is one of the 13 biome treats and this animal's family prefers it (+50 %).</summary>
        public bool IsPreferred(uint animal, ContentId treat)
        {
            if (!_ctx.State.Collection.TryGet(animal, out var instance) || !_ctx.Content.Species.TryGet(instance.Species, out var species))
                return false;
            var treats = _ctx.ProgressionConfig.Treats;
            for (int i = 0; i < treats.Length; i++)
                if (treats[i].Id == treat)
                    return species.RigFamily == treats[i].PreferredFamily;
            return false;
        }

        /// <summary>T48B the Week-end des Câlins (events.weekend.bond_multiplier, set by <see cref="WeekendService"/>): every source × this; 1 otherwise.</summary>
        public double WeekendMultiplier { get; set; } = 1.0;

        /// <summary>
        /// Adds bond points (doc 02 Renouveau, section 4.1): × (1 + Comfort), + Best Friends for a favorite, × the Week-end des
        /// Câlins, rounded; capped
        /// at <see cref="DailyVisitConfig.BondMax"/>, never lowered. Returns the points really added.
        /// </summary>
        public int AddBond(uint animal, int points, BondSource source)
        {
            if (points <= 0)
                return 0;
            var collection = _ctx.State.Collection;
            if (!collection.TryGet(animal, out var instance))
                return 0;
            // A companion's bond XP has its own 25 levels: it keeps counting once the bond points are full.
            Companions?.OnBondEvent(animal, source);

            double multiplier = 1.0;
            var essence = _ctx.Progression?.Essence;
            if (essence != null)
            {
                multiplier += essence.Sum(EssenceEffect.BondGain);
                if (instance.IsFavorite)
                    multiplier += essence.Sum(EssenceEffect.FavoriteBond);
            }
            multiplier *= Math.Max(1.0, WeekendMultiplier);
            double scaled = Math.Round(points * Math.Max(0.0, multiplier), MidpointRounding.AwayFromZero);

            int max = Math.Min(_ctx.Config.BondMax, ushort.MaxValue);
            ushort oldBond = instance.BondPoints;
            if (oldBond >= max || scaled <= 0.0)
                return 0;
            int next = scaled >= max - oldBond ? max : oldBond + (int)scaled;
            int added = next - oldBond;
            instance.BondPoints = (ushort)next;
            collection.Update(instance);
            _ctx.Economy.OnBondChanged(instance, oldBond);
            BondGained?.Invoke(animal, added);
            _ctx.RequestSave(SaveUrgency.Normal);
            return added;
        }

        private bool Feed(uint animal, ContentId treat, out int points)
        {
            points = 0;
            if (!_ctx.State.Collection.TryGet(animal, out var instance))
                return false;
            int basePoints = TreatPoints(instance, treat);
            if (basePoints <= 0)
                return false;
            var owned = _ctx.State.Progression.Treats;
            if (!owned.TryGetValue(treat, out int count) || count <= 0)
                return false;
            if (count == 1)
                owned.Remove(treat);
            else
                owned[treat] = count - 1;
            points = AddBond(animal, basePoints, BondSource.Treat);
            TreatGiven?.Invoke(animal, treat, points);
            _ctx.RequestSave(SaveUrgency.Normal);
            return true;
        }

        private int TreatPoints(in AnimalInstance instance, ContentId treat)
        {
            var treats = _ctx.ProgressionConfig.Treats;
            for (int i = 0; i < treats.Length; i++)
            {
                if (treats[i].Id != treat)
                    continue;
                int points = BiomePoints(i);
                bool preferred = _ctx.Content.Species.TryGet(instance.Species, out var species) && species.RigFamily == treats[i].PreferredFamily;
                return preferred ? (int)Math.Round(points * PreferredFamilyMultiplier, MidpointRounding.AwayFromZero) : points;
            }
            if (treat == SeedCookie)
                return SeedCookiePoints;
            if (treat == GoldenTreat)
                return GoldenTreatPoints;
            return 0;
        }

        private static int BiomePoints(int treatIndex) =>
            treatIndex < BiomeTreatPoints.Length ? BiomeTreatPoints[treatIndex] : BiomeTreatPoints[BiomeTreatPoints.Length - 1];

        private void RollDay()
        {
            var visit = _ctx.State.DailyVisit;
            int today = _ctx.TodayKey;
            if (today <= visit.TouchDayKey)
                return;
            visit.TouchDayKey = today;
            visit.TouchesToday = 0;
            visit.TouchesByAnimal.Clear();
        }
    }
}
