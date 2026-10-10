#nullable enable
using System;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>A title, frame or pose earned (the gallery's « Nouveau », a toast).</summary>
    public delegate void ProfileItemHandler(ProfileItemKind kind, int index);

    /// <summary>
    /// T44B: the profile's 322 titles, 42 frames and 47 poses (doc 01 Succès, sections 3 to 5): owned, unseen, the title and
    /// frame shown. An item once earned is never lost. Every source unlocks through one method here:
    /// <list type="bullet">
    /// <item>the starting title and frame (<see cref="GiveStarting"/>, at creation or on an older account's first load);</item>
    /// <item>achievement Star tiers (<see cref="Grant(string)"/> from <see cref="AchievementService.TryClaim"/>);</item>
    /// <item>album pages (<see cref="OnAlbumTitle"/>, raised by <see cref="AlbumHooks.TitleEarned"/>);</item>
    /// <item>refuge levels, biome and species mastery levels (<see cref="OnRefugeLevel"/>, <see cref="OnBiomeMastery"/>,
    /// <see cref="OnSpeciesMastery"/>: every level at or below the one reached, so a backfill and a late content both work);</item>
    /// <item>hooks for the systems still to come: companions' bonds, Pass seasons, event paths and shared goals, the journal's
    /// moments, photo mode.</item>
    /// </list>
    /// Reads never allocate.
    /// </summary>
    public sealed class ProfileItemService : IProfileItems
    {
        private readonly AchievementState _state;
        private readonly ProfileItemInfo[][] _infos = new ProfileItemInfo[3][];

        public ProfileItemService(AchievementState state)
        {
            _state = state;
            for (int k = 0; k < 3; k++)
            {
                var kind = (ProfileItemKind)k;
                var infos = new ProfileItemInfo[ProfileData.Count(kind)];
                for (int i = 0; i < infos.Length; i++)
                {
                    ref readonly var row = ref ProfileData.Row(kind, i);
                    infos[i] = new ProfileItemInfo(i, row.Key, row.Order);
                }
                _infos[k] = infos;
            }
        }

        public AchievementState State => _state;
        public ISaveScheduler? Save { get; set; }

        public event Action? Changed;

        /// <summary>A title, frame or pose earned for the first time.</summary>
        public event ProfileItemHandler? Earned;

        /// <summary>The title or frame shown changed (analytics profile_title_selected / profile_frame_selected; −1: none).</summary>
        public event ProfileItemHandler? SelectionChanged;

        // ------------------------------------------------------------------------------------------- IProfileItems

        public int Count(ProfileItemKind kind) => _infos[(int)kind].Length;

        public ProfileItemInfo At(ProfileItemKind kind, int index) => _infos[(int)kind][index];

        public bool Owned(ProfileItemKind kind, int index)
        {
            if ((uint)index >= (uint)_infos[(int)kind].Length)
                return false;
            return kind == ProfileItemKind.Title ? AchievementState.Bit(_state.TitlesOwned, index)
                : kind == ProfileItemKind.Frame ? (_state.FramesOwned & (1UL << index)) != 0
                : (_state.PosesOwned & (1UL << index)) != 0;
        }

        public int Selected(ProfileItemKind kind) =>
            kind == ProfileItemKind.Title ? _state.SelectedTitle : kind == ProfileItemKind.Frame ? _state.SelectedFrame : -1;

        /// <summary>Shows an owned title or frame, or none with −1. Poses are not shown (photo mode uses them).</summary>
        public bool TrySelect(ProfileItemKind kind, int index)
        {
            if (kind == ProfileItemKind.Pose || index < -1 || (index >= 0 && !Owned(kind, index)))
                return false;
            if (Selected(kind) == index)
                return true;
            if (kind == ProfileItemKind.Title)
                _state.SelectedTitle = (short)index;
            else
                _state.SelectedFrame = (short)index;
            SelectionChanged?.Invoke(kind, index);
            Changed?.Invoke();
            Save?.RequestSave(SaveUrgency.Normal);
            return true;
        }

        public bool Unseen(ProfileItemKind kind, int index)
        {
            if ((uint)index >= (uint)_infos[(int)kind].Length)
                return false;
            return kind == ProfileItemKind.Title ? AchievementState.Bit(_state.TitlesUnseen, index)
                : kind == ProfileItemKind.Frame ? (_state.FramesUnseen & (1UL << index)) != 0
                : (_state.PosesUnseen & (1UL << index)) != 0;
        }

        public void MarkSeen(ProfileItemKind kind)
        {
            bool any;
            if (kind == ProfileItemKind.Title)
            {
                any = false;
                for (int i = 0; i < _state.TitlesUnseen.Length; i++)
                {
                    any |= _state.TitlesUnseen[i] != 0;
                    _state.TitlesUnseen[i] = 0;
                }
            }
            else if (kind == ProfileItemKind.Frame)
            {
                any = _state.FramesUnseen != 0;
                _state.FramesUnseen = 0;
            }
            else
            {
                any = _state.PosesUnseen != 0;
                _state.PosesUnseen = 0;
            }
            if (!any)
                return;
            Changed?.Invoke();
            Save?.RequestSave(SaveUrgency.Normal);
        }

        /// <summary>Items earned and not looked at yet, of one kind (the gallery tab's badge).</summary>
        public int UnseenCount(ProfileItemKind kind)
        {
            if (kind == ProfileItemKind.Frame)
                return PopCount(_state.FramesUnseen);
            if (kind == ProfileItemKind.Pose)
                return PopCount(_state.PosesUnseen);
            int n = 0;
            for (int i = 0; i < _state.TitlesUnseen.Length; i++)
                n += PopCount(_state.TitlesUnseen[i]);
            return n;
        }

        public int OwnedCount(ProfileItemKind kind)
        {
            if (kind == ProfileItemKind.Frame)
                return PopCount(_state.FramesOwned);
            if (kind == ProfileItemKind.Pose)
                return PopCount(_state.PosesOwned);
            int n = 0;
            for (int i = 0; i < _state.TitlesOwned.Length; i++)
                n += PopCount(_state.TitlesOwned[i]);
            return n;
        }

        // ------------------------------------------------------------------------------------------- granting

        /// <summary>Gives an item (owned and « Nouveau »); false when it was owned already or the index is unknown.</summary>
        public bool Grant(ProfileItemKind kind, int index)
        {
            if ((uint)index >= (uint)_infos[(int)kind].Length || Owned(kind, index))
                return false;
            if (kind == ProfileItemKind.Title)
            {
                AchievementState.SetBit(_state.TitlesOwned, index, true);
                AchievementState.SetBit(_state.TitlesUnseen, index, true);
            }
            else if (kind == ProfileItemKind.Frame)
            {
                _state.FramesOwned |= 1UL << index;
                _state.FramesUnseen |= 1UL << index;
            }
            else
            {
                _state.PosesOwned |= 1UL << index;
                _state.PosesUnseen |= 1UL << index;
            }
            Earned?.Invoke(kind, index);
            Changed?.Invoke();
            Save?.RequestSave(SaveUrgency.Priority);
            return true;
        }

        /// <summary>Gives an item by its key (title.…, frame.…, pose.…).</summary>
        public bool Grant(string key)
        {
            if (!TryKindOf(key, out var kind))
                return false;
            return Grant(kind, ProfileData.IndexOf(kind, key));
        }

        public static bool TryKindOf(string key, out ProfileItemKind kind)
        {
            kind = ProfileItemKind.Title;
            if (string.IsNullOrEmpty(key))
                return false;
            if (key.StartsWith("title.", StringComparison.Ordinal))
                return true;
            if (key.StartsWith("frame.", StringComparison.Ordinal))
            {
                kind = ProfileItemKind.Frame;
                return true;
            }
            if (key.StartsWith("pose.", StringComparison.Ordinal))
            {
                kind = ProfileItemKind.Pose;
                return true;
            }
            return false;
        }

        /// <summary>The starting title and frame, given once and shown (doc 01 Succès, sections 3 and 4).</summary>
        public void GiveStarting()
        {
            if (_state.StartGiven)
                return;
            _state.StartGiven = true;
            int title = ProfileData.IndexOf(ProfileItemKind.Title, ProfileData.StartingTitle);
            int frame = ProfileData.IndexOf(ProfileItemKind.Frame, ProfileData.StartingFrame);
            Grant(ProfileItemKind.Title, title);
            Grant(ProfileItemKind.Frame, frame);
            // A starting gift is not « Nouveau ».
            AchievementState.SetBit(_state.TitlesUnseen, title, false);
            _state.FramesUnseen &= ~(1UL << frame);
            if (_state.SelectedTitle < 0)
                _state.SelectedTitle = (short)title;
            if (_state.SelectedFrame < 0)
                _state.SelectedFrame = (short)frame;
            Changed?.Invoke();
            Save?.RequestSave(SaveUrgency.Priority);
        }

        // ------------------------------------------------------------------------------------------- sources

        /// <summary>Every item of a source whose subject matches (−1: any) and whose value is at or below the one reached.</summary>
        public int GrantSource(ProfileSource source, int subject, int reached)
        {
            int given = 0;
            for (int k = 0; k < 3; k++)
            {
                var kind = (ProfileItemKind)k;
                int n = _infos[k].Length;
                for (int i = 0; i < n; i++)
                {
                    ref readonly var row = ref ProfileData.Row(kind, i);
                    if (row.Source != source || (subject >= 0 && row.Subject != subject) || row.Value > reached)
                        continue;
                    if (Grant(kind, i))
                        given++;
                }
            }
            return given;
        }

        public int OnRefugeLevel(int level) => GrantSource(ProfileSource.RefugeLevel, -1, level);

        /// <summary>A biome's mastery level (titles at 10, 25, 50; the frame at 20). The biome index of <see cref="AlbumData"/>.</summary>
        public int OnBiomeMastery(int biome, int level) => biome < 0 ? 0 : GrantSource(ProfileSource.BiomeMastery, biome, level);

        /// <summary>A species' mastery level (titles at 30 and 100). Companions and unknown species have none.</summary>
        public int OnSpeciesMastery(ContentId species, int level)
        {
            int index = ProfileData.SpeciesIndex(species);
            return index < 0 ? 0 : GrantSource(ProfileSource.SpeciesMastery, index, level);
        }

        /// <summary>A companion's bond level (its pose at 20, its title at 25); A-SP-101 to 113.</summary>
        public int OnCompanionBond(int companion, int level) => GrantSource(ProfileSource.Companion, companion, level);

        /// <summary>A Refuge Pass season: its title at free level 40, its pose with Premium+.</summary>
        public int OnPassSeason(int season, int freeLevel, bool premiumPlus)
        {
            int given = 0;
            if (freeLevel >= 40)
                given += GrantKindSource(ProfileItemKind.Title, ProfileSource.PassSeason, season);
            if (premiumPlus)
                given += GrantKindSource(ProfileItemKind.Pose, ProfileSource.PassSeason, season);
            return given;
        }

        /// <summary>A big event's free path tier (its frame at 30); events 101 to 112.</summary>
        public int OnEventTrackTier(int eventNumber, int tier) => GrantSource(ProfileSource.EventTrack, eventNumber, tier);

        /// <summary>A big event's shared goal reached, or the personal goal standing in for it (its pose).</summary>
        public int OnEventGoal(int eventNumber) => GrantSource(ProfileSource.EventGoal, eventNumber, 0);

        /// <summary>Moments on the journal's Moments page (the title at 10, the frame at 25; T44C).</summary>
        public int OnJournalMoments(int moments) => GrantSource(ProfileSource.JournalMoments, -1, moments);

        /// <summary>Photo mode opened: the 8 base poses.</summary>
        public int OnPhotoModeUnlocked() => GrantSource(ProfileSource.PhotoMode, -1, 0);

        public int OnEventPage(int eventNumber) => GrantSource(ProfileSource.EventPage, eventNumber, int.MaxValue);

        public int OnActivityPage(int activity) => GrantSource(ProfileSource.ActivityPage, activity, int.MaxValue);

        /// <summary>
        /// A title raised by the albums (<see cref="AlbumHooks.TitleEarned"/>): title.event.101 to 112 (an event page),
        /// title.activity.001 to 018 (an activity page). A complete biome album's title.album.{biome} has no title in this
        /// catalogue (it gives its monument): false.
        /// </summary>
        public bool OnAlbumTitle(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;
            if (key.StartsWith("title.event.", StringComparison.Ordinal))
            {
                int n = ParseTail(key, 12);
                return n > 0 && OnEventPage(n) > 0;
            }
            if (key.StartsWith("title.activity.", StringComparison.Ordinal))
            {
                int n = ParseTail(key, 15);
                return n > 0 && OnActivityPage(n) > 0;
            }
            // A key of the catalogue itself (a later album content naming its title directly).
            return Grant(key);
        }

        private int GrantKindSource(ProfileItemKind kind, ProfileSource source, int subject)
        {
            int given = 0;
            int n = _infos[(int)kind].Length;
            for (int i = 0; i < n; i++)
            {
                ref readonly var row = ref ProfileData.Row(kind, i);
                if (row.Source == source && row.Subject == subject && Grant(kind, i))
                    given++;
            }
            return given;
        }

        private static int ParseTail(string key, int start)
        {
            int n = 0;
            if (start >= key.Length)
                return -1;
            for (int i = start; i < key.Length; i++)
            {
                char c = key[i];
                if (c < '0' || c > '9')
                    return -1;
                n = n * 10 + (c - '0');
            }
            return n;
        }

        private static int PopCount(ulong v)
        {
            int n = 0;
            for (; v != 0; v &= v - 1)
                n++;
            return n;
        }
    }
}
