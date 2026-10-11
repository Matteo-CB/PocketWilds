using PocketWilds.Core;
using PocketWilds.Services.Feedback;
using PocketWilds.Simulation;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// T48B the season card and the weekend chip. The card « La saison {season} commence » shows once per new season (the service
    /// says when it is owed, never on the very first launch), only on a calm refuge: never over a hatch, a screen, the offline
    /// return or the first egg's hand; it waits until then. The chip sits in the Event button's slot while a weekend runs; its
    /// sheet tells the weekend's name, effect and time left. Both read the services once a second, never per frame.
    /// </summary>
    public sealed partial class RefugeController : IWeekendHudActions
    {
        private const float SeasonCheckSeconds = 1f;

        private float _nextSeasonCheck;
        private WeekendKind _weekendShown = (WeekendKind)255;
        private bool _weekendBound;

        /// <summary>Season cards shown since the scene started (tests).</summary>
        public int SeasonCardsShown { get; private set; }

        /// <summary>The weekend shown on the HUD (None when hidden).</summary>
        public WeekendKind WeekendShown => _weekendShown == (WeekendKind)255 ? WeekendKind.None : _weekendShown;

        /// <summary>Called by <see cref="Tick"/>: the chip, then the owed card. No allocation when nothing changes.</summary>
        private void TickSeasons(float time)
        {
            if (time < _nextSeasonCheck)
                return;
            _nextSeasonCheck = time + SeasonCheckSeconds;
            if (!_weekendBound && _hud is IWeekendHud bind)
            {
                bind.BindWeekend(this);
                _weekendBound = true;
            }
            RefreshWeekendHud();
            TryShowSeasonCard();
        }

        private void RefreshWeekendHud()
        {
            var weekends = _context.Weekends;
            var kind = weekends != null ? weekends.Active : WeekendKind.None;
            if (kind == _weekendShown)
                return;
            _weekendShown = kind;
            if (_hud is IWeekendHud hud)
                hud.SetWeekend(kind != WeekendKind.None, (int)kind);
            // On screen now: analytics weekend_seen once per weekend (the service keeps the once).
            if (kind != WeekendKind.None)
                weekends.MarkSeen();
        }

        /// <summary>Calm refuge only: the card waits for the end of a hatch, a screen, the offline count or the first egg's hand.</summary>
        private void TryShowSeasonCard()
        {
            var seasons = _context.Seasons;
            if (seasons == null || !seasons.CardPending || _modalView == null || ModalOpen || Hatching || TapHintShown || OfflineCollecting
                || _actorsHidden)
                return;
            var season = seasons.Current;
            if (season == RefugeSeason.None)
                return;
            var loc = _context.Localization;
            _modalView.Show(ModalKind.Info, loc.Get(SeasonText.CardTitle(season)), loc.Get(SeasonText.CardBody(season)),
                loc.Get(FeedbackText.ModalOkButton), null, this);
            seasons.MarkCardShown();
            SeasonCardsShown++;
            _context.Audio.Play(AudioEventId.PanelOpen, Vector3.zero, default);
        }

        // ---------------- IWeekendHudActions ----------------

        /// <summary>The weekend's sheet (the modal shell): its name, its effect line and the time left.</summary>
        public void OpenWeekend()
        {
            var weekends = _context.Weekends;
            if (weekends == null || weekends.Active == WeekendKind.None || _modalView == null || ModalOpen || Hatching)
                return;
            var kind = weekends.Active;
            var loc = _context.Localization;
            long left = weekends.EndsUnix - _context.Clock.UtcNow.ToUnixTimeSeconds();
            string body = loc.Get(SeasonText.WeekendEffect(kind)) + "\n" + loc.Get(SeasonText.WeekendEnds).Replace("{time}", TimeLeft(loc, left));
            _modalView.Show(ModalKind.Info, loc.Get(SeasonText.WeekendName(kind)), body, loc.Get(FeedbackText.ModalOkButton), null, this);
            weekends.MarkSeen();
            _context.Audio.Play(AudioEventId.PanelOpen, Vector3.zero, default);
            _context.Haptics.Play(HapticEventId.TapLight);
        }

        /// <summary>« 2 j 5 h », « 5 h 20 min », « 12 min » (never under one minute). Built on open only.</summary>
        public static string TimeLeft(ILocalization loc, long seconds)
        {
            long minutes = System.Math.Max(1L, (seconds + 59L) / 60L);
            long hours = minutes / 60L;
            if (hours >= 24L)
                return loc.Get(SeasonText.TimeDaysHours).Replace("{d}", (hours / 24L).ToString()).Replace("{h}", (hours % 24L).ToString());
            if (hours >= 1L)
                return loc.Get(SeasonText.TimeHoursMinutes).Replace("{h}", hours.ToString()).Replace("{m}", (minutes % 60L).ToString());
            return loc.Get(SeasonText.TimeMinutes).Replace("{m}", minutes.ToString());
        }
    }
}
