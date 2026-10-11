#nullable enable
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>
    /// T49 hatch effects (O-HATCH-*, « Éclosion de pousses » first): owned entitlements (effect.&lt;event&gt;.&lt;name&gt;) that play on
    /// top of the normal hatch. The newest owned effect is used by default (receiving one selects it); the player may pick
    /// another owned one or turn hatch effects off. The choice is stored in <see cref="EntitlementState"/> (the profile's hatch
    /// area will show it later). Pure logic, no allocation after the static table.
    /// </summary>
    public static class HatchEffects
    {
        /// <summary>« Éclosion de pousses » (L'Éveil des Terriers, Carnet d'or tier 5): sprouts rise in a circle, round dew beads roll.</summary>
        public const string SproutBurst = "effect.burrows.sprout_burst";

        /// <summary>Every hatch effect in release order, append only: the index is the presentation slot, the last owned is the newest.</summary>
        public static readonly string[] Keys = { SproutBurst };

        private static readonly ContentId[] Ids = BuildIds();

        public static int Count => Ids.Length;

        public static ContentId Id(int index) => Ids[index];

        /// <summary>The slot of a hatch effect id, -1 for anything else.</summary>
        public static int IndexOf(ContentId id)
        {
            if (!id.IsValid)
                return -1;
            for (int i = 0; i < Ids.Length; i++)
                if (Ids[i] == id)
                    return i;
            return -1;
        }

        public static bool IsHatchEffect(ContentId id) => IndexOf(id) >= 0;

        /// <summary>
        /// The effect a hatch plays now (-1: none): nothing when the player turned effects off; the chosen effect while it is
        /// owned; else the newest owned one (latest in <see cref="Keys"/>).
        /// </summary>
        public static int Active(EntitlementState? entitlements)
        {
            if (entitlements == null || entitlements.HatchEffectOff)
                return -1;
            int chosen = IndexOf(entitlements.HatchEffect);
            if (chosen >= 0 && entitlements.Owned.Contains(Ids[chosen]))
                return chosen;
            for (int i = Ids.Length - 1; i >= 0; i--)
                if (entitlements.Owned.Contains(Ids[i]))
                    return i;
            return -1;
        }

        /// <summary>An effect was just received: it becomes the one used (the newest owned). An « off » choice stays off.</summary>
        public static void OnGranted(EntitlementState entitlements, ContentId id)
        {
            if (IsHatchEffect(id) && entitlements.Owned.Contains(id))
                entitlements.HatchEffect = id;
        }

        /// <summary>The player picks an owned effect (turns effects back on). False when the id is not an owned hatch effect.</summary>
        public static bool Select(EntitlementState entitlements, ContentId id)
        {
            if (!IsHatchEffect(id) || !entitlements.Owned.Contains(id))
                return false;
            entitlements.HatchEffect = id;
            entitlements.HatchEffectOff = false;
            return true;
        }

        /// <summary>The player turns hatch effects off (true) or back on (false, the chosen or newest owned one plays again).</summary>
        public static void SetOff(EntitlementState entitlements, bool off) => entitlements.HatchEffectOff = off;

        private static ContentId[] BuildIds()
        {
            var ids = new ContentId[Keys.Length];
            for (int i = 0; i < Keys.Length; i++)
                ids[i] = ContentId.FromSource(Keys[i]);
            return ids;
        }
    }
}
