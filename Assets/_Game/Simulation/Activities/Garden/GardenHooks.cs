#nullable enable
using System;

namespace PocketWilds.Simulation
{
    /// <summary>
    /// T43: feeds the rendez-vous from the garden (doc 01 Contenu: WISH-031 « Plante {n} graines », WISH-032 « Récolte {n}
    /// plantes », WISH-033 « Arrose {n} pousses », WISH-034 « Récolte {n} {crop} », WISH-035 « Apporte {n} {crop} à {animal} »,
    /// WISH-036 « Récolte une teinture »; JRN-027 « Récolte {n} plantes », JRN-028 « Arrose {n} pousses », JRN-029 « Récolte
    /// {n} plantes à 3 étoiles », JRN-030 « Récolte {n} teintures », JRN-041 « Gagne {n} points de Jardin », a garden
    /// challenge passed for JRN-012). Listens to <see cref="GardenService.Planted"/>, <see cref="GardenService.SproutWatered"/>
    /// and <see cref="GardenService.Harvested"/>, then calls <see cref="DailyVisitServices"/>. Building it marks the Garden as
    /// existing for the rendez-vous (<see cref="DailyVisitConfig.GardenAvailable"/>: the gift's seeds are given, not their
    /// Light). The bootstrap builds one after both, next to the <see cref="ExpeditionHooks"/>, and disposes it with them.
    /// Allocation-free.
    /// </summary>
    public sealed class GardenHooks : IDisposable
    {
        private readonly GardenService _garden;
        private readonly DailyVisitServices _daily;
        private readonly GardenPlantedHandler _onPlanted;
        private readonly Action<int> _onWatered;
        private readonly GardenHarvestedHandler _onHarvested;
        private bool _disposed;

        public GardenHooks(GardenService garden, DailyVisitServices daily)
        {
            _garden = garden;
            _daily = daily;
            if (ActivityCatalog.Get(ActivityId.Garden).ShippedInThisBuild)
                daily.Config.GardenAvailable = true;
            _onPlanted = OnPlanted;
            _onWatered = OnWatered;
            _onHarvested = OnHarvested;
            garden.Planted += _onPlanted;
            garden.SproutWatered += _onWatered;
            garden.Harvested += _onHarvested;
        }

        /// <summary>Seeds planted, sprouts watered and plants harvested since this object was built (tests, the debug panel).</summary>
        public int Plantings { get; private set; }

        public int Waterings { get; private set; }

        public int Harvests { get; private set; }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _garden.Planted -= _onPlanted;
            _garden.SproutWatered -= _onWatered;
            _garden.Harvested -= _onHarvested;
        }

        private void OnPlanted(in GardenPlantInfo info)
        {
            Plantings++;
            _daily.OnGardenPlanted(in info);
        }

        private void OnWatered(int plot)
        {
            Waterings++;
            _daily.OnGardenWatered(plot);
        }

        private void OnHarvested(in GardenHarvestInfo info)
        {
            Harvests++;
            _daily.OnGardenHarvested(in info);
        }
    }
}
