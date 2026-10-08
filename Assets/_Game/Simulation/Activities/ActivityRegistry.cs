#nullable enable

namespace PocketWilds.Simulation
{
    /// <summary>
    /// The activities shipped in this build register their rules here, once, at boot (doc « Mini-jeux &amp; activités »,
    /// section 5: each activity task adds its line and flips <see cref="ActivityDefinition.ShippedInThisBuild"/>). T37 ships
    /// the framework only: nothing is registered, the Play button stays hidden and a rendez-vous ticket stays worth Light.
    /// </summary>
    public static class ActivityRegistry
    {
        public static void RegisterShipped(ActivityServices services)
        {
        }
    }
}
