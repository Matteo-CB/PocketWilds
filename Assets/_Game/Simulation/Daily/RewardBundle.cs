#nullable enable
using System;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>A gift's size, shown by the color of the box's band (doc 01 Contenu, section 3.1). Stable values.</summary>
    public enum GiftSize : byte
    {
        Small = 0,
        Nice = 1,
        Big = 2,
    }

    /// <summary>
    /// Everything one reward of the rendez-vous can hold (doc 01 Contenu: daily gift, calendar box, welcome gift, wish,
    /// journal goal, return gift). Light is in minutes of the current production. Bond goes to an animal and is applied by
    /// the wish itself.
    /// </summary>
    public struct RewardBundle
    {
        public long Gems;
        public double LightMinutes;
        public int Tickets;
        public ContentId Treat;
        public int Treats;
        public ContentId SeedPlant;
        public int Seeds;
        public int Stickers;
        public int DiscoveryEggs;
        public ContentId Decoration;
        public int FlowerSugar;
        public GiftSize Size;

        /// <summary>A closed system's lot was replaced by its substitution (doc 01: « avant le bouton Jouer, … »).</summary>
        public bool Substituted;

        public bool IsEmpty =>
            Gems == 0 && LightMinutes <= 0.0 && Tickets == 0 && Treats == 0 && Seeds == 0 && Stickers == 0 && DiscoveryEggs == 0
            && Decoration == ContentId.None && FlowerSugar == 0;
    }

    /// <summary>Which later systems are open, for the substitutions, and the newest biome's treat.</summary>
    public readonly struct RewardContext
    {
        /// <summary>The Play button and its tickets exist (activities).</summary>
        public readonly bool PlayOpen;

        public readonly bool GardenOpen;
        public readonly bool AlbumsOpen;
        public readonly bool BakeryOpen;

        /// <summary>The treat of the most recent biome (treat lots).</summary>
        public readonly ContentId TreatOfNewestBiome;

        /// <summary>Light minutes one ticket is worth while the activities are closed.</summary>
        public readonly double TicketLightMinutes;

        public RewardContext(bool playOpen, bool gardenOpen, bool albumsOpen, bool bakeryOpen, ContentId treatOfNewestBiome,
            double ticketLightMinutes)
        {
            PlayOpen = playOpen;
            GardenOpen = gardenOpen;
            AlbumsOpen = albumsOpen;
            BakeryOpen = bakeryOpen;
            TreatOfNewestBiome = treatOfNewestBiome;
            TicketLightMinutes = ticketLightMinutes;
        }
    }

    /// <summary>A bundle once given: the bundle and the Light it was actually worth at that moment.</summary>
    public readonly struct GrantedRewards
    {
        public readonly RewardBundle Bundle;
        public readonly BigValue Light;

        public GrantedRewards(in RewardBundle bundle, BigValue light)
        {
            Bundle = bundle;
            Light = light;
        }
    }

    /// <summary>T37: where rewarded tickets go (the activity ticket pool, <see cref="TicketService"/>).</summary>
    public interface ITicketSink
    {
        void Grant(int count, TicketSource source);
    }

    /// <summary>
    /// Gives a bundle (doc 01 Contenu, section 0): Gems through the economy (with its reason, for analytics), Light as
    /// minutes of the current production (at least 1), Discovery eggs into the hatch stock, treats into the treat stock,
    /// tickets, seeds, stickers, flower sugar and decorations into the inventory. Asks for a priority save: a gift once
    /// opened is never rolled again.
    /// </summary>
    public sealed class RewardGranter
    {
        public static readonly ContentId FlowerSugar = ContentId.FromSource("ingredient.flower_sugar");

        private readonly PlayerState _state;
        private readonly EconomyService _economy;
        private readonly HatchService? _hatch;
        private readonly ISaveScheduler? _save;

        public RewardGranter(PlayerState state, EconomyService economy, HatchService? hatch, ISaveScheduler? save)
        {
            _state = state;
            _economy = economy;
            _hatch = hatch;
            _save = save;
        }

        /// <summary>T37: the activity ticket pool; null before it exists (tickets then wait in the inventory).</summary>
        public ITicketSink? Tickets { get; set; }

        /// <summary>Where a rendez-vous ticket comes from, for analytics 114 (doc 08).</summary>
        public static TicketSource TicketSourceOf(EconomyReason reason) => reason switch
        {
            EconomyReason.DailyGift => TicketSource.DailyGift,
            EconomyReason.Calendar => TicketSource.Calendar,
            EconomyReason.WeeklyJournal => TicketSource.WeeklyJournal,
            EconomyReason.WelcomeGift => TicketSource.WelcomeGift,
            EconomyReason.ReturnGift => TicketSource.ReturnGift,
            EconomyReason.Wish => TicketSource.Wish,
            EconomyReason.Event => TicketSource.Event,
            EconomyReason.Expedition => TicketSource.Expedition,
            _ => TicketSource.Debug,
        };

        public GrantedRewards Grant(in RewardBundle bundle, EconomyReason reason, DiscoverySource source)
        {
            if (bundle.Gems > 0)
                _economy.Grant(CurrencyId.Gems, BigValue.FromDouble(bundle.Gems), reason);
            var light = BigValue.Zero;
            if (bundle.LightMinutes > 0.0)
            {
                light = BigValue.Multiply(_economy.GetLightPerSecond(), bundle.LightMinutes * 60.0);
                if (light < BigValue.One)
                    light = BigValue.One;
                _economy.Grant(CurrencyId.Light, light, reason);
            }
            if (bundle.DiscoveryEggs > 0)
            {
                if (_hatch != null)
                    _hatch.GrantDiscoveryEggs(bundle.DiscoveryEggs, source);
                else
                    _state.World.DiscoveryEggs += bundle.DiscoveryEggs;
            }
            if (bundle.Treats > 0 && bundle.Treat != ContentId.None)
            {
                var treats = _state.Progression.Treats;
                treats.TryGetValue(bundle.Treat, out int owned);
                treats[bundle.Treat] = owned + bundle.Treats;
            }
            var inventory = _state.Inventory;
            if (bundle.Tickets > 0)
            {
                // T37: tickets go straight into the activity pool (with their source for analytics) once it exists.
                if (Tickets != null)
                    Tickets.Grant(bundle.Tickets, TicketSourceOf(reason));
                else
                    inventory.Tickets += bundle.Tickets;
            }
            inventory.PendingStickers += Math.Max(0, bundle.Stickers);
            if (bundle.Seeds > 0)
                inventory.Add(bundle.SeedPlant, bundle.Seeds);
            if (bundle.FlowerSugar > 0)
                inventory.Add(FlowerSugar, bundle.FlowerSugar);
            if (bundle.Decoration != ContentId.None)
                inventory.Add(bundle.Decoration, 1);
            _save?.RequestSave(SaveUrgency.Priority);
            return new GrantedRewards(bundle, light);
        }
    }
}
