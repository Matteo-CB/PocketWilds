#nullable enable
using System;

namespace PocketWilds.Simulation
{
    /// <summary>
    /// T42: feeds the rendez-vous from the expeditions (doc 01 Contenu: WISH-041 « Lance une expédition », WISH-042 « … de 4 h
    /// ou plus », WISH-043 « Envoie {animal} en expédition », WISH-044 « Envoie une équipe de 3 animaux », WISH-045 « Ouvre une
    /// carte postale », WISH-046 « Pars vers {destination} »; JRN-024 « Fais {n} expéditions », JRN-025 « … de 8 h ou plus »,
    /// JRN-026 « Rapporte des cartes postales de {n} destinations », the stars for JRN-009 and a challenge passed for JRN-012).
    /// Listens to <see cref="ExpeditionService.Started"/> and <see cref="ExpeditionService.Welcomed"/>, then calls
    /// <see cref="DailyVisitServices.OnExpeditionStarted"/> and <see cref="DailyVisitServices.OnExpeditionWelcomed"/>. The
    /// bootstrap builds one after both, next to the <see cref="MemoryHooks"/>, and disposes it with them. Allocation-free.
    /// </summary>
    public sealed class ExpeditionHooks : IDisposable
    {
        private readonly ExpeditionService _expeditions;
        private readonly DailyVisitServices _daily;
        private readonly ExpeditionStartedHandler _onStarted;
        private readonly ExpeditionWelcomedHandler _onWelcomed;
        private bool _disposed;

        public ExpeditionHooks(ExpeditionService expeditions, DailyVisitServices daily)
        {
            _expeditions = expeditions;
            _daily = daily;
            _onStarted = OnStarted;
            _onWelcomed = OnWelcomed;
            expeditions.Started += _onStarted;
            expeditions.Welcomed += _onWelcomed;
        }

        /// <summary>Departures and welcomes seen since this object was built (tests, the debug panel).</summary>
        public int Departures { get; private set; }

        public int Welcomes { get; private set; }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _expeditions.Started -= _onStarted;
            _expeditions.Welcomed -= _onWelcomed;
        }

        private void OnStarted(in ExpeditionStartInfo info)
        {
            Departures++;
            _daily.OnExpeditionStarted(in info);
        }

        private void OnWelcomed(in ExpeditionWelcomeInfo info)
        {
            Welcomes++;
            _daily.OnExpeditionWelcomed(in info);
        }
    }
}
