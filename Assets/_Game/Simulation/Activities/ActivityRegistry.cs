#nullable enable

namespace PocketWilds.Simulation
{
    /// <summary>
    /// The activities shipped in this build register their rules here, once, at boot (doc « Mini-jeux &amp; activités »,
    /// section 5: each activity task adds its line and flips <see cref="ActivityDefinition.ShippedInThisBuild"/>). T37 shipped
    /// the framework alone; T38 ships « Toilettage » (S-ACT-001): its rules and its 3-star Clover Crunch. With one activity
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
        }
    }
}
