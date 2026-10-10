#nullable enable
using System;

namespace PocketWilds.Simulation
{
    /// <summary>One saved journal page: the moment, its date, its image reference and capture flags, its variables' values.</summary>
    public struct JournalEntry
    {
        public const byte FlagCaptured = 1;

        /// <summary>Waiting for its capture (the capture layer takes it at a calm moment).</summary>
        public const byte FlagPending = 2;

        /// <summary>The capture was never made or was given back (the 300 cap, the queue of 8, a new device): the thumbnail stands in.</summary>
        public const byte FlagThumbnail = 4;

        public int Id;
        public short Moment;

        /// <summary>Local date, days since 2000-01-01.</summary>
        public int Day;

        public uint ImageRef;

        /// <summary>What makes a repeatable moment distinct (a species, a biome, a level…; 0: none).</summary>
        public uint Subject;

        public byte Flags;

        /// <summary>The values of the moment's variables, in the row's order: a literal (a custom name, a number) or a text key
        /// prefixed by <see cref="JournalService.KeyPrefix"/>.</summary>
        public string? Value0;

        public string? Value1;

        public bool Captured => (Flags & FlagCaptured) != 0;
        public bool Pending => (Flags & FlagPending) != 0;
    }

    /// <summary>
    /// T44C « Journal du refuge » (doc 01 Succès, section 6): the pages (oldest first, ids rising), the first-time moments
    /// already written, the subjects of the per-subject ones, the capture flags, the surprises seen, the unseen count. Plain
    /// data; arrays grow when a page is added (a rare event, never per frame). Saved in section 22 (schema 16). The online
    /// save keeps the pages, not the images: on a new device a missing capture falls back to the moment's thumbnail.
    /// </summary>
    public sealed class JournalState
    {
        /// <summary>The journal was set up once (the first egg written, or an older account's past moments marked done).</summary>
        public bool Initialized;

        public int NextId = 1;

        public JournalEntry[] Pages = new JournalEntry[32];
        public int PageCount;

        /// <summary>One bit per moment: a first-time moment already written (or marked done for an older account).</summary>
        public ulong FirstDone;

        /// <summary>(moment &lt;&lt; 32) | subject: per-subject moments already written.</summary>
        public ulong[] Subjects = new ulong[32];

        public int SubjectCount;

        /// <summary>New pages not looked at yet.</summary>
        public int Unseen;

        /// <summary>The garden's plants harvested (memory.garden_complete), one bit per plant of the catalog.</summary>
        public uint GardenPlants;

        /// <summary>The best Essence of a Renewal (memory.renewal_record).</summary>
        public long BestRenewalEssence;

        /// <summary>The world surprises seen (« Moments »): their keys, in the order seen.</summary>
        public string[] Surprises = new string[8];

        public int SurpriseCount;

        public bool IsFirstDone(int moment) => (FirstDone & (1UL << moment)) != 0;

        public void SetFirstDone(int moment) => FirstDone |= 1UL << moment;

        public bool HasSubject(int moment, uint subject)
        {
            ulong key = ((ulong)(uint)moment << 32) | subject;
            for (int i = 0; i < SubjectCount; i++)
                if (Subjects[i] == key)
                    return true;
            return false;
        }

        public void AddSubject(int moment, uint subject)
        {
            if (HasSubject(moment, subject))
                return;
            if (SubjectCount == Subjects.Length)
                Array.Resize(ref Subjects, Subjects.Length * 2);
            Subjects[SubjectCount++] = ((ulong)(uint)moment << 32) | subject;
        }

        public ref JournalEntry Append()
        {
            if (PageCount == Pages.Length)
                Array.Resize(ref Pages, Pages.Length * 2);
            ref var entry = ref Pages[PageCount++];
            entry = default;
            return ref entry;
        }

        public bool HasSurprise(string key)
        {
            for (int i = 0; i < SurpriseCount; i++)
                if (string.Equals(Surprises[i], key, StringComparison.Ordinal))
                    return true;
            return false;
        }

        public void AddSurprise(string key)
        {
            if (string.IsNullOrEmpty(key) || HasSurprise(key))
                return;
            if (SurpriseCount == Surprises.Length)
                Array.Resize(ref Surprises, Surprises.Length * 2);
            Surprises[SurpriseCount++] = key;
        }

        /// <summary>Back to a new account (tests, the debug panel).</summary>
        public void Clear()
        {
            Initialized = false;
            NextId = 1;
            Array.Clear(Pages, 0, Pages.Length);
            PageCount = 0;
            FirstDone = 0;
            SubjectCount = 0;
            Unseen = 0;
            GardenPlants = 0;
            BestRenewalEssence = 0;
            Array.Clear(Surprises, 0, Surprises.Length);
            SurpriseCount = 0;
        }
    }

    /// <summary>
    /// T44C « Visiteurs rares » (doc Rétention; doc Événements originaux, section 5): what the rare visitors remember. The
    /// schedule itself is never saved (a pure function of the day and the profile seed); only the contacts, the visitors kept,
    /// the visits greeted today and the guaranteed first visit are. Saved with the journal (section 22, schema 16).
    /// </summary>
    public sealed class VisitorState
    {
        public const int Visitors = 12;

        /// <summary>Times each visitor was touched (its 4th contact guarantees it joins).</summary>
        public byte[] Contacts = new byte[Visitors];

        /// <summary>One bit per visitor kept in the collection.</summary>
        public ushort JoinedMask;

        /// <summary>The local day (days since 2000-01-01) of <see cref="GreetedMask"/>, and its visits already greeted (bit per visit).</summary>
        public int GreetedDay;

        public byte GreetedMask;

        /// <summary>The local day the visitors opened (0: not yet). That day keeps one visit for the player (doc 05, SUR-06).</summary>
        public int OpenedDay;

        /// <summary>The guaranteed first visit's arrival (UTC unix ms; 0: none), placed when the visitors open after the day's visits.</summary>
        public long FirstVisitUnixMs;

        /// <summary>The visitor of the guaranteed first visit (index into <see cref="VisitorData"/>).</summary>
        public byte FirstVisitor;

        /// <summary>Every touch, for the account's life.</summary>
        public int Greetings;

        public bool Joined(int visitor) => (JoinedMask & (1 << visitor)) != 0;

        public int KeptCount
        {
            get
            {
                int n = 0;
                for (int v = JoinedMask; v != 0; v &= v - 1)
                    n++;
                return n;
            }
        }

        public void Clear()
        {
            Array.Clear(Contacts, 0, Contacts.Length);
            JoinedMask = 0;
            GreetedDay = 0;
            GreetedMask = 0;
            OpenedDay = 0;
            FirstVisitUnixMs = 0;
            FirstVisitor = 0;
            Greetings = 0;
        }
    }
}
