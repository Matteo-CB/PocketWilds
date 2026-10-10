#nullable enable
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    // T43 « Jardin » (doc « Mini-jeux & activités », S-ACT-006; doc 01 Contenu, sections 8 and 9 and S-ACT-006): the
    // contract between the garden (simulation: plots, sprouts, watering, the neighbours' help, harvests, garden points and
    // levels, dyes and dye palettes, the garden challenges) and its presentation (the garden set with its beds and plants,
    // the garden overlay). Growth times are absolute UTC times saved in the GardenState: plants grow while the game is
    // closed, a ripe plant waits for ever, a clock set back never makes a growth longer than its duration. Every random part
    // of a harvest (the seed given back, « Pousse heureuse ») is drawn when the seed is planted. Frozen for T43: the
    // simulation implements it, the presentation only uses it.

    /// <summary>What a plot shows.</summary>
    public enum GardenPlotState : byte
    {
        /// <summary>Not owned yet: 400 Gems (the fourth plot) or the Wild Club (the fifth).</summary>
        Locked = 0,

        /// <summary>Owned and bare: « Planter ».</summary>
        Empty = 1,

        /// <summary>A sprout grows (stage 1 or 2): « Prêt dans {time} ».</summary>
        Growing = 2,

        /// <summary>Stage 3, ripe, waiting as long as it takes: « Prêt à récolter ».</summary>
        Ripe = 3,
    }

    /// <summary>How a locked plot opens.</summary>
    public enum GardenPlotUnlock : byte
    {
        None = 0,
        Gems = 1,
        Club = 2,
    }

    public enum GardenPlantResult : byte
    {
        Planted = 0,
        NotOpen = 1,
        PlotNotEmpty = 2,

        /// <summary>No seed of that plant in the inventory.</summary>
        NoSeed = 3,

        /// <summary>The plant is not open yet (a higher garden level, or its biome is closed).</summary>
        PlantLocked = 4,
    }

    /// <summary>What a plant gives (challenge 02 « 3 récoltes de fruits, chacune à côté d'une fleur »).</summary>
    public enum GardenPlantKind : byte
    {
        Flower = 0,
        Fruit = 1,
        Vegetable = 2,
        Grain = 3,
    }

    /// <summary>One of the 25 plants (doc 01 table 8.2).</summary>
    public readonly struct GardenPlant
    {
        /// <summary>ContentId of « plant.sweet_clover »; also the id of its seed in the inventory.</summary>
        public readonly ContentId Id;

        /// <summary>The runtime id; its name text is « {Key}.name ».</summary>
        public readonly string Key;

        public readonly GardenPlantKind Kind;

        /// <summary>The garden level that opens it (1 to 7), or 0 for a biome plant (it opens with its biome).</summary>
        public readonly int Level;

        /// <summary>The biome of a biome plant (biome.grassland for the Sweet Pea), ContentId.None for a starter plant.</summary>
        public readonly ContentId Biome;

        /// <summary>The growth time in seconds (30 min to 8 h) before watering and events.</summary>
        public readonly double GrowSeconds;

        /// <summary>The main product and its count at 1 star (« 3 fleurs de trèfle »); +1 at 3 stars.</summary>
        public readonly ContentId Main;

        public readonly int MainCount;

        /// <summary>The second product (« 1 Sucre de fleurs »), ContentId.None when none.</summary>
        public readonly ContentId Side;

        public readonly int SideCount;

        /// <summary>Its dye (dye.clover_green) and how many a harvest gives once dyes are open (2 for the Meadow Indigo).</summary>
        public readonly ContentId Dye;

        public readonly int DyeCount;

        public GardenPlant(ContentId id, string key, GardenPlantKind kind, int level, ContentId biome, double growSeconds,
            ContentId main, int mainCount, ContentId side, int sideCount, ContentId dye, int dyeCount)
        {
            Id = id;
            Key = key;
            Kind = kind;
            Level = level;
            Biome = biome;
            GrowSeconds = growSeconds;
            Main = main;
            MainCount = mainCount;
            Side = side;
            SideCount = sideCount;
            Dye = dye;
            DyeCount = dyeCount;
        }
    }

    /// <summary>A harvest, handed out once by <see cref="IGarden.TryHarvest"/>: what was granted and why.</summary>
    public struct GardenHarvest
    {
        public int Plot;
        public ContentId Plant;

        /// <summary>1: picked; 2: watered; 3: watered and a neighbour of another species grew beside it half the time.</summary>
        public byte Stars;

        public bool Watered;

        /// <summary>« Les voisines s'entraident »: the neighbour condition of the third star.</summary>
        public bool Helped;

        public ContentId Main;
        public int MainCount;
        public ContentId Side;
        public int SideCount;

        /// <summary>The dye given (dyes open), ContentId.None otherwise.</summary>
        public ContentId Dye;

        public int Dyes;

        /// <summary>Seeds of the same plant given back (drawn at planting; +1 at 3 stars from garden level 10).</summary>
        public int SeedsBack;

        /// <summary>« Pousse heureuse » (garden level 7, drawn at planting): the products doubled.</summary>
        public bool Happy;

        /// <summary>Garden points of this harvest and the levels before and after.</summary>
        public int Points;

        public int LevelBefore;
        public int LevelAfter;
    }

    /// <summary>
    /// The garden as its presentation sees it. Reads never allocate; the overlay polls them (growth is computed from the
    /// clock on each read). Plots are numbered 0 to <see cref="PlotCount"/> − 1 along the row (plot i and i + 1 are
    /// neighbours): three owned, then the Gems plot, then the Club plot.
    /// </summary>
    public interface IGarden
    {
        /// <summary>Open: refuge level 8 and day 3 (doc 01 section 8.1).</summary>
        bool IsOpen { get; }

        /// <summary>Plots shown, owned or not (5).</summary>
        int PlotCount { get; }

        GardenPlotState StateOf(int plot);

        GardenPlotUnlock UnlockOf(int plot);

        /// <summary>The Gems plot's price (400).</summary>
        long PlotPrice { get; }

        bool TryBuyPlot();

        // ---------------- a plot's sprout ----------------

        ContentId PlantOf(int plot);

        /// <summary>1 (0 to 40 % of the growth), 2 (40 to 100 %), 3 (ripe); 0 when empty.</summary>
        int StageOf(int plot);

        /// <summary>The growth from 0 to 1 (1 when ripe).</summary>
        float GrowthOf(int plot);

        /// <summary>Seconds until ripe (0 when ripe), never more than <see cref="TotalSecondsOf"/>.</summary>
        double SecondsLeft(int plot);

        double TotalSecondsOf(int plot);

        bool Watered(int plot);

        /// <summary>The neighbour condition of the third star is met so far (the overlay shows « Les voisines s'entraident »).</summary>
        bool Helped(int plot);

        /// <summary>Plots ripe (the hub card, « Tout récolter »).</summary>
        int RipeCount { get; }

        /// <summary>The soonest ripening among the growing plots, in seconds (−1: none growing).</summary>
        double SoonestRipe { get; }

        // ---------------- plants and seeds ----------------

        int PlantCount { get; }

        GardenPlant PlantAt(int index);

        bool TryGetPlant(ContentId plant, out GardenPlant info);

        /// <summary>Open now (its garden level reached, or its biome open).</summary>
        bool IsPlantOpen(ContentId plant);

        /// <summary>Seeds of a plant in the inventory.</summary>
        int SeedsOf(ContentId plant);

        /// <summary>A seed becomes a sprout: its harvest's draws happen now (saved).</summary>
        GardenPlantResult Plant(int plot, ContentId plant);

        /// <summary>Waters a growing sprout once (−10 % of its total growth); false when empty, ripe or already watered.</summary>
        bool Water(int plot);

        // ---------------- finish now (doc Gemmes: 1 Gem per 10 minutes left, 3 at least, 3 a day) ----------------

        int SpeedUpCost(int plot);

        int SpeedUpsLeftToday { get; }

        /// <summary>Spends the Gems and ripens the sprout now; draws nothing.</summary>
        bool TrySpeedUp(int plot);

        // ---------------- the harvest ----------------

        /// <summary>
        /// Picks a ripe plant: the products, the dye, the seeds back and the garden points are granted, the plot is bare.
        /// False when the plot is not <see cref="GardenPlotState.Ripe"/>.
        /// </summary>
        bool TryHarvest(int plot, out GardenHarvest harvest);

        // ---------------- garden level (doc 01 table 8.3) ----------------

        int Level { get; }

        /// <summary>Points inside the level and the points the next level needs (0 at level 10).</summary>
        int PointsInLevel { get; }

        int PointsForNext { get; }

        /// <summary>Dyes are open (garden level 4).</summary>
        bool DyesOpen { get; }

        // ---------------- challenges (doc 01 S-ACT-006 « Défis »: stars from the harvest stars added up) ----------------

        /// <summary>10.</summary>
        int ChallengeCount { get; }

        /// <summary>The challenge being played (−1: none); one at a time.</summary>
        int ActiveChallenge { get; }

        /// <summary>Its condition can be met now (« Teintures ouvertes », the plants it names open).</summary>
        bool IsChallengeOpen(int challenge);

        /// <summary>Starts a challenge: the next harvests count toward it.</summary>
        bool StartChallenge(int challenge);

        void StopChallenge();

        /// <summary>Harvests counted and harvests needed, harvest stars added up so far.</summary>
        int ChallengeHarvests { get; }

        int ChallengeHarvestsNeeded(int challenge);

        int ChallengeStarsSoFar { get; }

        /// <summary>Harvest stars needed for 1, 2 and 3 challenge stars (<paramref name="stars"/> 1 to 3).</summary>
        int ChallengeThreshold(int challenge, int stars);

        /// <summary>The best challenge stars reached (saved with the activities' challenge stars).</summary>
        byte ChallengeStars(int challenge);

        /// <summary>Raised on the main thread when a plot changes state (planted, a stage passed, ripe, harvested, bought).</summary>
        event System.Action<int>? PlotChanged;
    }

    /// <summary>
    /// The dyes (doc 01 section 9): kept in the garden inventory without limit; a palette costs 5 dyes of its colour, a duo
    /// palette 3 of each of its two; a palette bought is permanent. The outfits' dye tab (T47) shows and applies them.
    /// </summary>
    public interface IGardenDyes
    {
        int DyeCount { get; }

        ContentId DyeAt(int index);

        /// <summary>Dyes of a colour owned.</summary>
        int Owned(ContentId dye);

        /// <summary>The 25 single palettes then the 6 duo palettes (31).</summary>
        int PaletteCount { get; }

        ContentId PaletteAt(int index);

        bool OwnsPalette(ContentId palette);

        bool CanBuyPalette(ContentId palette);

        bool TryBuyPalette(ContentId palette);
    }
}
