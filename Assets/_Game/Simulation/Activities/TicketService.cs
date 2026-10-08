#nullable enable
using System;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>
    /// Activity tickets (doc « Mini-jeux &amp; activités » section 2; doc 01 Contenu section 0 « Billet »; doc 02 Gemmes; doc 08
    /// activities.tickets_*, gems.price.tickets_pack, gems.tickets_pack_*):
    /// <list type="bullet">
    /// <item>a reserve of 5 free tickets (7 with the Wild Club, +1 with the Essence node Extra Ticket), +1 every 120 minutes,
    /// computed from a UTC anchor, so time away refills too; the part of an interval already waited is kept;</item>
    /// <item>free tickets above the reserve (gifts, ads, events, Circles, journal…) are kept up to 10 and simply stop the
    /// refill until the count is back under the reserve (doc 08: « aucun billet retiré »); more than that waits in the
    /// inventory stock and joins as soon as there is room (nothing is ever lost);</item>
    /// <item>tickets bought with Gems (3 for 30 Gems, twice a day) are counted apart and used after the free ones: a game
    /// played with one draws nothing at random (doc 01, section 12.0, decision of 7 October);</item>
    /// <item>the T36 stock <see cref="InventoryState.Tickets"/> (tickets won before the Play button, and any ticket a
    /// rendez-vous gift still puts there) is absorbed into the free pool at each <see cref="Refresh"/>.</item>
    /// </list>
    /// The first use fills the reserve (doc 01 A15: the Play button shows 5). Plain methods; nothing allocates.
    /// </summary>
    public sealed class TicketService : ITicketSink
    {
        private readonly ActivityContext _ctx;

        public TicketService(ActivityContext ctx)
        {
            _ctx = ctx;
        }

        /// <summary>Tickets received (analytics 114 TicketsGain): source, amount, balance after. The refill is not reported here.</summary>
        public event Action<TicketSource, int, int>? TicketsGained;

        /// <summary>Tickets of the automatic refill (counted by economy_flow, reason « Recharge de billets »): count, balance after.</summary>
        public event Action<int, int>? Refilled;

        /// <summary>The count or the time to the next ticket changed (labels refresh on it).</summary>
        public event Action? Changed;

        private ActivityState S => _ctx.Activities;
        private ActivityConfig Config => _ctx.Config;

        /// <summary>Tickets owned: free and bought.</summary>
        public int Count => S.FreeTickets + S.PaidTickets;

        public int Free => S.FreeTickets;
        public int Paid => S.PaidTickets;

        /// <summary>The reserve the refill fills: 5, 7 with the Club, +1 per Extra Ticket rank (doc 08 offline.extra_ticket).</summary>
        public int Max => Config.Reserve(Config.ClubActive) + (int)Math.Round(_ctx.Essence(EssenceEffect.ExtraTicket), MidpointRounding.AwayFromZero);

        /// <summary>Free tickets a gift can raise the pool to (10, or the reserve when higher).</summary>
        public int GiftCap => Math.Max(Config.TicketsGiftCap, Max);

        /// <summary>The refill waits: the free tickets reach the reserve.</summary>
        public bool Full => S.FreeTickets >= Max;

        /// <summary>Milliseconds before the next free ticket (0 when the reserve is full). Reads the clock; no allocation.</summary>
        public long MillisecondsToNext
        {
            get
            {
                if (Full)
                    return 0;
                long left = S.RefillAnchorUnixMs + Config.RefillMilliseconds - _ctx.NowUnixMs;
                return left < 0 ? 0 : Math.Min(left, Config.RefillMilliseconds);
            }
        }

        /// <summary>Minutes before the next free ticket, rounded up (0 when full). Shown as real time, never a pressure.</summary>
        public int MinutesToNext => (int)((MillisecondsToNext + 59_999) / 60_000);

        public double SecondsToNext => MillisecondsToNext / 1000.0;

        /// <summary>Ticket packs still for sale today.</summary>
        public int PacksLeftToday
        {
            get
            {
                int bought = S.PackDayKey == _ctx.TodayKey ? S.PacksToday : 0;
                return Math.Max(0, Config.PacksPerDay - bought);
            }
        }

        public int PackPrice => Math.Max(0, Config.PackPrice);
        public int PackSize => Math.Max(1, Config.PackSize);

        /// <summary>A pack is for sale today and affordable.</summary>
        public bool CanBuyPack => PacksLeftToday > 0 && _ctx.Economy.CanSpend(CurrencyId.Gems, BigValue.FromDouble(PackPrice));

        /// <summary>
        /// Starts the pool on first use, absorbs the inventory stock and refills from the clock. Called once a second by
        /// <see cref="ActivityServices.Tick"/>, on resume and before every spend or grant.
        /// </summary>
        public void Refresh()
        {
            bool changed = Start();
            changed |= Absorb() > 0;
            changed |= Refill() > 0;
            if (changed)
                Changed?.Invoke();
        }

        /// <summary>
        /// Spends one ticket for a rewarded game: a free one first, else a bought one (<paramref name="paid"/>). Leaving a
        /// full reserve starts a new interval now.
        /// </summary>
        public bool TrySpend(out bool paid)
        {
            Refresh();
            paid = false;
            var s = S;
            if (s.FreeTickets > 0)
            {
                if (s.FreeTickets >= Max)
                    s.RefillAnchorUnixMs = _ctx.NowUnixMs;
                s.FreeTickets--;
            }
            else if (s.PaidTickets > 0)
            {
                s.PaidTickets--;
                paid = true;
            }
            else
            {
                return false;
            }
            Changed?.Invoke();
            _ctx.RequestSave(SaveUrgency.Priority);
            return true;
        }

        /// <summary>Gives back a ticket spent by a game quit at once (the runner's kind rule). Not a gain for analytics.</summary>
        public void Refund(bool paid)
        {
            if (paid)
                S.PaidTickets++;
            else
                S.FreeTickets++;
            Changed?.Invoke();
            _ctx.RequestSave(SaveUrgency.Priority);
        }

        /// <summary>
        /// Free tickets from a gift, an ad, an event, a Circle, the journal… They may pass the reserve up to
        /// <see cref="GiftCap"/>; the rest waits in the inventory stock and joins when there is room. Reported whole.
        /// </summary>
        public void Grant(int count, TicketSource source)
        {
            if (count <= 0)
                return;
            Refresh();
            var s = S;
            int room = Math.Max(0, GiftCap - s.FreeTickets);
            int now = Math.Min(room, count);
            if (now > 0)
            {
                if (s.FreeTickets < Max && s.FreeTickets + now >= Max)
                    s.RefillAnchorUnixMs = _ctx.NowUnixMs;
                s.FreeTickets += now;
            }
            if (count > now)
                _ctx.State.Inventory.Tickets += count - now;
            TicketsGained?.Invoke(source, count, Count);
            Changed?.Invoke();
            _ctx.RequestSave(SaveUrgency.Priority);
        }

        /// <summary>
        /// Buys a pack (doc 02 Gemmes: 3 tickets for 30 Gems, twice a day at most) through the economy with
        /// <see cref="EconomyReason.TicketPack"/>. Bought tickets are counted apart (no paid randomness).
        /// </summary>
        public bool TryBuyPack()
        {
            Refresh();
            if (PacksLeftToday <= 0)
                return false;
            if (!_ctx.Economy.Spend(CurrencyId.Gems, BigValue.FromDouble(PackPrice), EconomyReason.TicketPack))
                return false;
            var s = S;
            int today = _ctx.TodayKey;
            if (s.PackDayKey != today)
            {
                s.PackDayKey = today;
                s.PacksToday = 0;
            }
            s.PacksToday++;
            s.PaidTickets += PackSize;
            TicketsGained?.Invoke(TicketSource.Gems, PackSize, Count);
            Changed?.Invoke();
            _ctx.RequestSave(SaveUrgency.Priority);
            return true;
        }

        // ------------------------------------------------------------------------------------------- internals

        private bool Start()
        {
            var s = S;
            if (s.TicketsStarted)
                return false;
            s.TicketsStarted = true;
            s.FreeTickets = Math.Max(s.FreeTickets, Max);
            s.RefillAnchorUnixMs = _ctx.NowUnixMs;
            _ctx.RequestSave(SaveUrgency.Normal);
            return true;
        }

        /// <summary>Moves the inventory stock into the free pool, up to the gift cap.</summary>
        private int Absorb()
        {
            var inventory = _ctx.State.Inventory;
            if (inventory.Tickets <= 0)
                return 0;
            var s = S;
            int moved = Math.Min(inventory.Tickets, Math.Max(0, GiftCap - s.FreeTickets));
            if (moved <= 0)
                return 0;
            if (s.FreeTickets < Max && s.FreeTickets + moved >= Max)
                s.RefillAnchorUnixMs = _ctx.NowUnixMs;
            inventory.Tickets -= moved;
            s.FreeTickets += moved;
            _ctx.RequestSave(SaveUrgency.Normal);
            return moved;
        }

        /// <summary>
        /// +1 free ticket per full interval since the anchor, up to the reserve. The anchor moves by the intervals used,
        /// so the time already waited toward the next ticket is kept; while the reserve is full the anchor follows the
        /// clock (the next interval starts when a ticket is spent).
        /// </summary>
        private int Refill()
        {
            var s = S;
            long now = _ctx.NowUnixMs;
            int max = Max;
            if (s.FreeTickets >= max)
            {
                s.RefillAnchorUnixMs = now;
                return 0;
            }
            long elapsed = now - s.RefillAnchorUnixMs;
            long interval = Config.RefillMilliseconds;
            if (elapsed < interval)
                return 0;
            long ticks = elapsed / interval;
            int add = (int)Math.Min(ticks, max - s.FreeTickets);
            s.FreeTickets += add;
            if (s.FreeTickets >= max)
                s.RefillAnchorUnixMs = now;
            else
                s.RefillAnchorUnixMs += ticks * interval;
            Refilled?.Invoke(add, Count);
            _ctx.RequestSave(SaveUrgency.Normal);
            return add;
        }
    }
}
