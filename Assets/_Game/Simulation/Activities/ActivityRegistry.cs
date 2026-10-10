#nullable enable

namespace PocketWilds.Simulation
{
    /// <summary>
    /// The activities shipped in this build register their rules here, once, at boot (doc « Mini-jeux &amp; activités »,
    /// section 5: each activity task adds its line and flips <see cref="ActivityDefinition.ShippedInThisBuild"/>). T37 shipped
    /// the framework alone; T38 ships « Toilettage » (S-ACT-001): its rules and its 3-star Clover Crunch; T39 ships
    /// « Cache-cache » (S-ACT-002): its rules and its 3-star Mushroom Biscuit; T40 ships « Pluie de noisettes » (S-ACT-003):
    /// its rules, its 3-star Toasted Hazelnut and its counted Hazelnut and Strawberry ingredients; T41 ships « Memory des
    /// empreintes » (S-ACT-004): its rules, its 3-star Seed Cookie and its 3-star seed behind the Garden. With one activity
    /// registered the Play button exists and rendez-vous tickets become real tickets.
    /// </summary>
    public static class ActivityRegistry
    {
        public static void RegisterShipped(ActivityServices services)
        {
            // T38 S-ACT-001 Toilettage: free mode, d1 / d5 / d10 parameters, 10 challenges; « Objet gagné : 1 Trèfle croquant
            // à 3 étoiles ».
            services.Rules.Register(GroomingRules.Create());
            services.Rewards.SetItemReward(ActivityId.Grooming, GroomingRules.ThreeStarTreat, GroomingRules.ThreeStarTreatCount,
                GroomingRules.ThreeStarTreatStars);

            // T39 S-ACT-002 Cache-cache: free mode, d1 / d5 / d10 parameters, 10 challenges; « Objet gagné : 1 Biscuit
            // champignon à 3 étoiles ».
            services.Rules.Register(HideSeekRules.Create());
            services.Rewards.SetItemReward(ActivityId.HideAndSeek, HideSeekRules.ThreeStarTreat, HideSeekRules.ThreeStarTreatCount,
                HideSeekRules.ThreeStarTreatStars);

            // T40 S-ACT-003 Pluie de noisettes: free mode, d1 / d5 / d10 parameters, 10 challenges; « Objets gagnés : 1 Noisette
            // par 10 noisettes attrapées, 3 au plus ; 1 Fraise par 10 baies, 2 au plus (Pâtisserie ouverte) ; 1 Noisette grillée
            // à 3 étoiles ».
            services.Rules.Register(NutDropRules.Create());
            services.Rewards.SetItemReward(ActivityId.NutRain, NutDropRules.ThreeStarTreat, NutDropRules.ThreeStarTreatCount,
                NutDropRules.ThreeStarTreatStars);
            services.Rewards.SetCountReward(ActivityId.NutRain, NutDropRules.HazelnutCount, NutDropRules.HazelnutIngredient,
                NutDropRules.HazelnutsPerIngredient, NutDropRules.HazelnutIngredientMax);
            services.Rewards.SetCountReward(ActivityId.NutRain, NutDropRules.BerryCount, NutDropRules.StrawberryIngredient,
                NutDropRules.BerriesPerStrawberry, NutDropRules.StrawberryMax, needsBakery: true);

            // T41 S-ACT-004 Memory des empreintes: free mode (thresholds and length per grid), stepped d1 to d10 parameters, 10
            // challenges; « Objets gagnés : à 3 étoiles, 1 Biscuit aux graines et 1 graine (Jardin ouvert) ».
            services.Rules.Register(MemoryRules.Create());
            services.Rewards.SetItemReward(ActivityId.PawMemory, MemoryRules.ThreeStarTreat, MemoryRules.ThreeStarTreatCount,
                MemoryRules.ThreeStarTreatStars);
            services.Rewards.SetCountReward(ActivityId.PawMemory, MemoryRules.SeedCount, MemoryRules.ThreeStarSeed,
                MemoryRules.SeedPer, MemoryRules.SeedMax, needsGarden: true, minStars: MemoryRules.SeedStars);
        }
    }
}
