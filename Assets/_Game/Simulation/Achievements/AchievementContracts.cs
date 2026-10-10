#nullable enable

namespace PocketWilds.Simulation
{
    // T44B « Succès, titres, cadres, poses » (doc « 01 Succès, titres, cadres, poses et journal », sections 1 to 5): the
    // contract between the profile rewards (simulation: the 200 achievements with their counters and four tiers, the 322
    // titles, 42 profile frames and 47 photo poses with their sources) and their presentation (the achievements screen,
    // the profile card, the HUD badge). Frozen for T44B: the simulation implements it, the presentation only uses it.

    public enum AchievementTier : byte
    {
        None = 0,
        Bronze = 1,
        Silver = 2,
        Gold = 3,

        /// <summary>« Étoile »: a round five-point star badge, never a pointed sparkle.</summary>
        Star = 4,
    }

    /// <summary>The 13 categories (section 1 « Répartition »); each has its own icon on the shared badge shapes.</summary>
    public enum AchievementCategory : byte
    {
        Collection = 0,
        Biomes = 1,
        Mastery = 2,
        Hatching = 3,
        Activities = 4,
        Daily = 5,
        ExpeditionsGarden = 6,
        Albums = 7,
        Decoration = 8,
        Bonds = 9,
        Events = 10,
        Renewal = 11,
        Refuge = 12,
    }

    /// <summary>One achievement: its key (achievement.001; texts « {Key}.name » and « {Key}.goal » with {n}), category, thresholds.</summary>
    public readonly struct AchievementInfo
    {
        public readonly int Index;
        public readonly string Key;
        public readonly AchievementCategory Category;
        public readonly long Bronze;
        public readonly long Silver;
        public readonly long Gold;
        public readonly long Star;

        public AchievementInfo(int index, string key, AchievementCategory category, long bronze, long silver, long gold, long star)
        {
            Index = index;
            Key = key;
            Category = category;
            Bronze = bronze;
            Silver = silver;
            Gold = gold;
            Star = star;
        }

        public long Threshold(AchievementTier tier) =>
            tier == AchievementTier.Bronze ? Bronze : tier == AchievementTier.Silver ? Silver : tier == AchievementTier.Gold ? Gold : Star;
    }

    /// <summary>The achievements as their screen sees them. Reads never allocate.</summary>
    public interface IAchievements
    {
        int Count { get; }

        AchievementInfo At(int index);

        /// <summary>The counter's value now (cumulated since the account began; « best run » for the per-run ones).</summary>
        long Progress(int index);

        /// <summary>The highest tier reached and the highest tier claimed (rewards wait for ever until claimed).</summary>
        AchievementTier Reached(int index);

        AchievementTier Claimed(int index);

        /// <summary>Tiers reached and not claimed yet (the HUD badge).</summary>
        int Claimable { get; }

        /// <summary>Claims every reached tier of one achievement: Gems (5, 10, 20, 50) and the extras (decoration, title, frame, pose).</summary>
        bool TryClaim(int index);

        event System.Action? Changed;
    }

    /// <summary>What a title, frame or pose is (section 3, 4, 5).</summary>
    public readonly struct ProfileItemInfo
    {
        public readonly int Index;

        /// <summary>title.little_paw / frame.… / pose.…; its name text is « {Key}.name ».</summary>
        public readonly string Key;

        /// <summary>The source text key (« {Key}.source »: how to earn it).</summary>
        public readonly int Order;

        public ProfileItemInfo(int index, string key, int order)
        {
            Index = index;
            Key = key;
            Order = order;
        }
    }

    public enum ProfileItemKind : byte
    {
        Title = 0,
        Frame = 1,
        Pose = 2,
    }

    /// <summary>The profile's titles, frames and poses: owned ones, the one shown. Reads never allocate.</summary>
    public interface IProfileItems
    {
        int Count(ProfileItemKind kind);

        ProfileItemInfo At(ProfileItemKind kind, int index);

        bool Owned(ProfileItemKind kind, int index);

        /// <summary>The title and frame shown (−1: none); poses are not « shown », they are used in photo mode.</summary>
        int Selected(ProfileItemKind kind);

        bool TrySelect(ProfileItemKind kind, int index);

        /// <summary>Earned but not looked at yet (the gallery's « Nouveau »).</summary>
        bool Unseen(ProfileItemKind kind, int index);

        void MarkSeen(ProfileItemKind kind);

        event System.Action? Changed;
    }
}
