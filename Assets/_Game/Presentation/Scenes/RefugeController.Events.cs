using PocketWilds.Content;
using PocketWilds.Services.Feedback;
using PocketWilds.Simulation;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// T48 event screens: the event button on the HUD's right rail (shown during the announce and the run, its ring the tier
    /// progress, its dot a reward to claim), the event hub (screen 018, opaque: the world camera stops while it is open), the
    /// announce page (real start date, 3 headline rewards), the end summary card at the first open after an event's end, and
    /// « Voir le Carnet d'or », which closes the hub and opens the shop on its « Événement » section (never a popup). The
    /// service is read a few times a second at most, never per frame; the hub binds again after each action.
    /// </summary>
    public sealed partial class RefugeController : IEventHubActions, IEventHudActions
    {
        private const float EventCheckSeconds = 1f;

        private IEventHubView _eventView;
        private EventHubModel _eventModel;
        private float _nextEventCheck;
        private long _eventSignature = long.MinValue;
        private bool _eventHubWorldHidden;

        /// <summary>PF_UI_EventHub, set by the scene entry after construction (prewarmed at once).</summary>
        public IEventHubView EventView
        {
            get => _eventView;
            set
            {
                _eventView = value;
                _eventView?.Prewarm(_context.Localization);
                if (_hud is IEventHud hud)
                    hud.BindEvent(this);
                _nextEventCheck = 0f;
            }
        }

        public bool EventOpen => _eventView != null && _eventView.IsOpen;

        /// <summary>The hub's model (null until the first open), for tests.</summary>
        public EventHubModel EventModel => _eventModel;

        private EventHubModel EventsModel()
        {
            var events = _context.Events;
            if (events == null)
                return null;
            return _eventModel ??= new EventHubModel(events);
        }

        /// <summary>Called by <see cref="Tick"/>: the hub's motion, then a few checks a second (HUD button, open hub, summary).</summary>
        private void TickEvents(float time, float deltaTime)
        {
            _eventView?.Tick(deltaTime);
            if (time < _nextEventCheck)
                return;
            _nextEventCheck = time + EventCheckSeconds;
            var model = EventsModel();
            if (model == null)
                return;
            var events = model.Service;
            var phase = events.Phase;
            long signature = (long)phase | ((long)events.Points << 3) ^ ((long)events.Currency << 31) ^ ((long)events.Tier << 52)
                             ^ (events.Instance != null ? (long)events.Instance.ClaimedCount(false) << 20 ^ (long)events.Instance.ClaimedCount(true) << 40 : 0L)
                             ^ (events.GoldOwned ? 1L << 62 : 0L);
            if (signature != _eventSignature)
            {
                _eventSignature = signature;
                RefreshEventHud(model);
                if (EventOpen && !_eventView.SummaryOpen)
                    _eventView.Refresh();
            }
            TryShowEventSummary();
        }

        /// <summary>The rail button: visible during the announce and the run; the ring is the tier progress; the dot a reward to claim.</summary>
        private void RefreshEventHud(EventHubModel model)
        {
            if (!(_hud is IEventHud hud))
                return;
            var phase = model.Phase;
            bool visible = _eventView != null && (phase == EventPhase.Announce || phase == EventPhase.Running);
            bool running = phase == EventPhase.Running;
            hud.SetEvent(visible, running ? model.TierProgress : 0f, running && model.ClaimableCount > 0);
        }

        /// <summary>The first open after an event's end: its summary card, once no other screen covers the refuge.</summary>
        private void TryShowEventSummary()
        {
            var events = _context.Events;
            if (_eventView == null || events == null || ModalOpen || Hatching || !events.TryGetPendingSummary(out var summary))
                return;
            string name = _context.Content.Events.TryGet(summary.Event, out var definition) ? _context.Localization.Get(definition.NameKey) : string.Empty;
            _eventView.ShowSummary(summary, name, _context.Localization, this);
            if (_input != null)
                _input.Blocked = true;
            _context.Audio.Play(AudioEventId.PanelOpen, Vector3.zero, default);
        }

        // ---------------- IEventHudActions ----------------

        public void OpenEvent() => OpenEventHub(EventHubTab.Track);

        /// <summary>Opens the hub on a tab (tests and the HUD). Entering during the run joins the event.</summary>
        public void OpenEventHub(EventHubTab tab)
        {
            var model = EventsModel();
            if (_eventView == null || model == null || ModalOpen || Hatching)
                return;
            var events = model.Service;
            var phase = events.Phase;
            if (phase != EventPhase.Announce && phase != EventPhase.Running)
                return;
            events.MarkSeen();
            if (phase == EventPhase.Running && !events.IsJoined)
                events.Join();
            _eventView.Open(model, _context.Localization, this, tab);
            SetWorldVisible(false);
            _eventHubWorldHidden = true;
            _context.Audio.Play(AudioEventId.PanelOpen, Vector3.zero, default);
            _context.Haptics.Play(HapticEventId.TapLight);
        }

        // ---------------- IEventHubActions ----------------

        public void ClaimTier(int tier, bool gold)
        {
            var events = _context.Events;
            if (events == null)
                return;
            var status = events.Claim(tier, gold);
            if (status == EventClaimStatus.Claimed)
                EventSuccess(AudioEventId.Claim);
            else if (status == EventClaimStatus.NeedsGold)
            {
                OpenGoldBook();
                return;
            }
            else
                EventSoft();
            AfterEventAction();
        }

        public void ClaimAllTiers()
        {
            var events = _context.Events;
            if (events == null)
                return;
            if (events.ClaimAll() > 0)
                EventSuccess(AudioEventId.Claim);
            else
                EventSoft();
            AfterEventAction();
        }

        public void OpenEventEgg()
        {
            var events = _context.Events;
            if (events == null)
                return;
            var result = events.OpenEgg();
            if (result.Status == EventEggStatus.Opened)
                EventSuccess(AudioEventId.Claim);
            else
                EventSoft();
            _eventView?.ShowEggResult(result);
            AfterEventAction();
        }

        public void BuyEventItem(int index, int sticker)
        {
            var events = _context.Events;
            if (events == null)
                return;
            if (events.Buy(index, sticker) == EventBuyStatus.Bought)
                EventSuccess(AudioEventId.PurchaseComplete);
            else
                EventSoft();
            AfterEventAction();
        }

        public void BuyMemoryItem(int index)
        {
            var events = _context.Events;
            if (events == null)
                return;
            if (events.BuyMemory(index) == EventBuyStatus.Bought)
                EventSuccess(AudioEventId.PurchaseComplete);
            else
                EventSoft();
            AfterEventAction();
        }

        public void SwapQuest(int index)
        {
            var events = _context.Events;
            if (events == null)
                return;
            if (events.Swap(index))
                EventSuccess(AudioEventId.ToggleOn);
            else
                EventSoft();
            AfterEventAction();
        }

        public void OpenGoldBook()
        {
            _eventView?.Close();
            OpenShop();
        }

        public void SummarySeen(string instanceId)
        {
            _context.Events?.MarkSummarySeen(instanceId);
            if (_input != null && !_eventHubWorldHidden)
                _input.Blocked = false;
            _context.Audio.Play(AudioEventId.PanelClose, Vector3.zero, default);
        }

        public void EventHubClosed()
        {
            if (_eventHubWorldHidden)
            {
                _eventHubWorldHidden = false;
                SetWorldVisible(true);
            }
            _context.Audio.Play(AudioEventId.PanelClose, Vector3.zero, default);
            _nextEventCheck = 0f;
        }

        private void AfterEventAction()
        {
            _eventView?.Refresh();
            _nextEventCheck = 0f;
        }

        private void EventSuccess(AudioEventId sound)
        {
            _context.Audio.Play(sound, Vector3.zero, default);
            _context.Haptics.Play(HapticEventId.Confirm);
        }

        private void EventSoft()
        {
            _context.Audio.Play(AudioEventId.ErrorSoft, Vector3.zero, default);
            _context.Haptics.Play(HapticEventId.ErrorSoft);
        }
    }
}
