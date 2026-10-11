using PocketWilds.Presentation;

namespace PocketWilds.Editor.UI
{
    /// <summary>
    /// T49 texts (EN, FR): « L'Éveil des Terriers » (its name and currency), the download page of a missing event group, the
    /// 8 accessories, the 7 outfits, the 13 decorations and the 4 gold ones, the monument, the hatch effect and the gold frame.
    /// The mole's and the event variants' names are the animal lane's; the album page, stickers, title, frame and pose names are
    /// in the generated tables. Writing Guide: short, calm, no pressure words; no em dash.
    /// </summary>
    public static class BurrowsTexts
    {
        public static readonly string[,] Texts =
        {
            { "event.burrows.name", "Burrow Awakening", "L'Éveil des Terriers" },
            { "event.burrows.currency", "Sprouts", "Pousses" },

            { EventText.DownloadSize, "Event download: {0} MB", "Téléchargement de l'événement : {0} Mo" },
            { EventText.Download, "Download", "Télécharger" },
            { EventText.Downloading, "The event is downloading.", "L'événement se télécharge." },
            { EventText.DownloadFailed, "The download did not finish. Try again later.", "Le téléchargement n'a pas abouti. Réessaie plus tard." },

            { "accessory.burrows.sprout_hat.name", "Sprout Hat", "Chapeau pousse" },
            { "accessory.burrows.seed_sack.name", "Seed Sack", "Sac à graines" },
            { "accessory.burrows.burrow_goggles.name", "Burrow Goggles", "Lunettes de terrier" },
            { "accessory.burrows.gardener_scarf.name", "Gardener's Scarf", "Foulard de jardinier" },
            { "accessory.burrows.miner_helmet.name", "Golden Miner Helmet", "Casque de mineur doré" },
            { "accessory.burrows.golden_shovel.name", "Golden Shovel", "Pelle dorée" },
            { "accessory.burrows.overalls_bib.name", "Overalls Bib", "Plastron de salopette" },
            { "accessory.burrows.overalls_straps.name", "Overalls Straps", "Bretelles de salopette" },

            { "outfit.burrows.01.name", "Sprout Hat", "Chapeau pousse" },
            { "outfit.burrows.02.name", "Seed Sack", "Sac à graines" },
            { "outfit.burrows.03.name", "Burrow Goggles", "Lunettes de terrier" },
            { "outfit.burrows.04.name", "Gardener's Scarf", "Foulard de jardinier" },
            { "outfit.burrows.05.name", "Golden Miner Helmet", "Casque de mineur doré" },
            { "outfit.burrows.06.name", "Golden Shovel", "Pelle dorée" },
            { "outfit.burrows.07.name", "Gardener's Overalls", "Salopette de jardinier" },

            { "decor.burrows.flowery_entrance.name", "Flowery Burrow Entrance", "Entrée de terrier fleurie" },
            { "decor.burrows.moss_mound.name", "Moss Mound", "Monticule de mousse" },
            { "decor.burrows.dew_mushrooms.name", "Dew Mushrooms", "Champignons de rosée" },
            { "decor.burrows.watering_can.name", "Watering Can", "Arrosoir" },
            { "decor.burrows.mini_wheelbarrow.name", "Mini Wheelbarrow", "Brouette miniature" },
            { "decor.burrows.seedling_pots.name", "Seedling Pots", "Pots de semis" },
            { "decor.burrows.burrow_sign.name", "Burrow Sign", "Panneau du terrier" },
            { "decor.burrows.earthworm_cushion.name", "Earthworm Cushion", "Coussin ver de terre" },
            { "decor.burrows.garden_shed.name", "Garden Shed", "Cabane de jardin" },
            { "decor.burrows.mini_greenhouse.name", "Mini Greenhouse", "Serre miniature" },
            { "decor.burrows.wooden_bench.name", "Wooden Bench", "Banc en bois" },
            { "decor.burrows.garden_fence.name", "Garden Fence", "Clôture de jardin" },
            { "decor.burrows.souvenir.name", "Burrow Awakening Keepsake", "Souvenir de l'Éveil des Terriers" },
            { "decor.burrows.flowery_entrance_gold.name", "Golden Burrow Entrance", "Entrée de terrier dorée" },
            { "decor.burrows.moss_mound_gold.name", "Golden Moss Mound", "Monticule de mousse doré" },
            { "decor.burrows.dew_mushrooms_gold.name", "Golden Dew Mushrooms", "Champignons de rosée dorés" },
            { "decor.burrows.watering_can_gold.name", "Golden Watering Can", "Arrosoir doré" },
            { "decor.burrows.root_burrow.name", "Great Root Burrow", "Grand terrier aux racines" },

            { "effect.burrows.sprout_burst.name", "Sprout Burst", "Éclosion de pousses" },
            { "frame.event_burrows_gold.name", "Golden Roots", "Racines dorées" },
            { "frame.event_burrows_gold.source", "Burrow Awakening, Gold book tier 20", "L'Éveil des Terriers, palier 20 du Carnet d'or" },
        };
    }
}
