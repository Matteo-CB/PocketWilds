using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// T42: how the postcard sets are named, where they live and how the team sits (doc 01 Contenu, section 7.1), shared by
    /// the postcard renderer and PostcardSetPipeline. One set per destination (Prés fleuris today: the team picnics at the
    /// foot of a hill covered with daisies, white butterflies around), each a small portrait scene far below the refuge with
    /// its own camera and light. A destination without a set of its own is drawn in the Prés fleuris set.
    /// </summary>
    public static class PostcardSetLayout
    {
        /// <summary>The destination of the only set of this build (and the fallback of every other one).</summary>
        public const string FlowerMeadows = "expedition.flower_meadows";

        public const string PrefabPrefix = "PF_Activity_Postcard_";
        public const string AddressPrefix = "activity.expeditions.postcard.";

        /// <summary>
        /// Where a set is built: 400 m under the refuge like the activity sets, 60 m further west than the Nut Drop set (no
        /// two sets meet, all stay far beyond the refuge camera's reach).
        /// </summary>
        public static readonly Vector3 SetPosition = new Vector3(-180f, -400f, 0f);

        /// <summary>The set camera's vertical field of view (a calm, slightly long lens).</summary>
        public const float CameraFov = 35f;

        public const float CameraNear = 0.3f;
        public const float CameraFar = 90f;

        /// <summary>The destination card art (the set without animals), landscape for the map's cards.</summary>
        public const int CardArtWidth = 512;

        public const int CardArtHeight = 320;

        /// <summary>The most animals of a team.</summary>
        public const int MaxTeam = 3;

        /// <summary>The seats of the three team sizes in one array: size 1 uses seat 0, size 2 seats 1 and 2, size 3 seats 3 to 5.</summary>
        public const int SeatCount = 6;

        /// <summary>Free space kept between two animals and between an animal and a prop (m): bodies never touch on a postcard.</summary>
        public const float SeatGap = 0.02f;

        /// <summary>The size gene's largest scale without the Giant trait (1 + 6 × 0.025): the seats are measured for it.</summary>
        public const float SeatScale = 1.15f;

        /// <summary>The happy animal's pose: this far into its Happy clip (the hop's lift).</summary>
        public const float HappyTime = 0.32f;

        /// <summary>The postcard's butterflies shown at most: the near ones that are clear, then far ones the seed picks.</summary>
        public const int ButterfliesShown = 5;

        public static int SeatIndex(int teamSize, int member) => teamSize * (teamSize - 1) / 2 + member;

        /// <summary>The runtime id's suffix (« expedition.flower_meadows » → « flower_meadows »).</summary>
        public static string Suffix(string destinationKey)
        {
            if (string.IsNullOrEmpty(destinationKey))
                return "flower_meadows";
            int dot = destinationKey.LastIndexOf('.');
            return dot >= 0 ? destinationKey.Substring(dot + 1) : destinationKey;
        }

        public static string SetAddress(string destinationKey) => AddressPrefix + Suffix(destinationKey);

        /// <summary>PF_Activity_Postcard_FlowerMeadows for « expedition.flower_meadows ».</summary>
        public static string PrefabName(string destinationKey)
        {
            string suffix = Suffix(destinationKey);
            var name = new System.Text.StringBuilder(PrefabPrefix, PrefabPrefix.Length + suffix.Length);
            bool upper = true;
            foreach (char c in suffix)
            {
                if (c == '_')
                {
                    upper = true;
                    continue;
                }
                name.Append(upper ? char.ToUpperInvariant(c) : c);
                upper = false;
            }
            return name.ToString();
        }
    }
}
