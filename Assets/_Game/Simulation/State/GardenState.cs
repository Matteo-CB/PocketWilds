#nullable enable
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>
    /// T43 « Jardin » (doc 01 Contenu, sections 8 and 9): everything the garden remembers. Plain data, fixed-size arrays
    /// allocated once. Growth times are absolute UTC times (unix ms): plants grow while the game is closed, a ripe plant waits
    /// for ever, nothing is lost by a late return. Every random part of a harvest (the seed given back, « Pousse heureuse »)
    /// is drawn at planting and kept in its plot. Products and seeds live in the <see cref="InventoryState"/>; dyes and the
    /// dye palettes bought live here (« l'inventaire du Jardin », section 9.1).
    /// </summary>
    public sealed class GardenState
    {
        /// <summary>Plots along the row: 3 free, the Gems plot, the Club plot.</summary>
        public const int MaxPlots = 5;

        /// <summary>The 25 dyes of section 9.2, by catalogue index.</summary>
        public const int DyeCount = 25;

        /// <summary>The Gems plot was bought (400 Gems, permanent).</summary>
        public bool PlotBought;

        /// <summary>The first visit's tutorial was shown (the paw on a bare bed, the Sweet Clover packet, the can).</summary>
        public bool FirstVisitDone;

        /// <summary>Garden points earned since install (the level is read from them, table 8.3).</summary>
        public int Points;

        /// <summary>Seeds planted since install (each sprout's seed is derived from it: an app kill never rerolls).</summary>
        public int Planted;

        /// <summary>Harvests since install.</summary>
        public int Harvested;

        /// <summary>Day key of the speed-ups counter, and the speed-ups of that day (3 a day at most).</summary>
        public int SpeedUpDayKey;

        public int SpeedUpsToday;

        /// <summary>The challenge being played (0 to 9), −1 for none; one at a time.</summary>
        public sbyte Challenge = -1;

        /// <summary>Harvests counted toward it and their harvest stars added up.</summary>
        public int ChallengeHarvests;

        public int ChallengeStars;

        /// <summary>When the current run of the challenge began (its first counted harvest; 0: none yet).</summary>
        public long ChallengeRunStartUnixMs;

        /// <summary>Plants (catalogue index bits) or biomes counted in the run (« 3 plantes différentes », « Panier à gâteaux »).</summary>
        public uint ChallengeMask;

        public GardenPlotRecord[] Plots = new GardenPlotRecord[MaxPlots];

        /// <summary>Dyes owned, by catalogue index (section 9.1: without limit).</summary>
        public int[] Dyes = new int[DyeCount];

        /// <summary>Palettes bought, one bit per palette (25 single then 6 duo); permanent.</summary>
        public uint Palettes;

        /// <summary>A sprout is in a plot (growing or ripe).</summary>
        public bool AnyActive
        {
            get
            {
                for (int p = 0; p < Plots.Length; p++)
                    if (Plots[p].Active)
                        return true;
                return false;
            }
        }

        public void ClearChallenge()
        {
            Challenge = -1;
            ResetChallengeRun();
        }

        public void ResetChallengeRun()
        {
            ChallengeHarvests = 0;
            ChallengeStars = 0;
            ChallengeRunStartUnixMs = 0;
            ChallengeMask = 0;
        }
    }

    /// <summary>
    /// One plot's sprout (doc 01 Contenu, section 8.1): the plant, its planting and ripening times (UTC unix ms), the growth
    /// it had at planting, watered or not, the seed and the draws made at planting, and the help of its neighbours. An empty
    /// record is a bare plot.
    /// </summary>
    public struct GardenPlotRecord
    {
        public bool Active;
        public ContentId Plant;
        public long PlantedUnixMs;

        /// <summary>Ripe from this time (watering and a speed-up bring it closer; never before the planting).</summary>
        public long RipeUnixMs;

        /// <summary>The growth at planting in ms (events and the Essence tree applied): watering takes 10 % of it.</summary>
        public long GrowMs;

        public bool Watered;
        public uint Seed;

        /// <summary>Seeds of the same plant given back at the harvest (drawn at planting).</summary>
        public byte SeedsBack;

        /// <summary>« Pousse heureuse » (garden level 7, drawn at planting): the products doubled.</summary>
        public bool Happy;

        /// <summary>
        /// The neighbours' help already credited (ms): the time a finished crop of another species on the left (plot − 1) or
        /// right (plot + 1) grew during this sprout's growth. The neighbour's current crop is added when read, so the third
        /// star is exact for any number of crops beside it, offline too.
        /// </summary>
        public long HelpLeftMs;

        public long HelpRightMs;

        /// <summary>A finished flower crop of a neighbour grew beside it (challenge 02 « chacune à côté d'une fleur »).</summary>
        public bool FlowerBeside;
    }
}
