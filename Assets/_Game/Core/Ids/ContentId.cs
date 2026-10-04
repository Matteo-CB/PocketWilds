#nullable enable
using System;
using System.Collections.Generic;

namespace PocketWilds.Core
{
    /// <summary>
    /// Stable compact content identifier. Source IDs are human readable ("species.rabbit"),
    /// runtime IDs are the 32-bit FNV-1a hash of that string. The hash never changes for a
    /// given source ID, so saves can store it directly. Removed IDs are tombstoned, never reused.
    /// </summary>
    public readonly struct ContentId : IEquatable<ContentId>, IComparable<ContentId>
    {
        public static readonly ContentId None = default;

        public readonly uint Value;

        public ContentId(uint value)
        {
            Value = value;
        }

        public bool IsValid => Value != 0;

        /// <summary>Hashes a source ID. Call at load/build time, not per frame.</summary>
        public static ContentId FromSource(string sourceId)
        {
            if (string.IsNullOrEmpty(sourceId))
                return None;

            var id = new ContentId(ContentIdHash.Compute(sourceId));
            ContentIdDebugNames.Register(id, sourceId);
            return id;
        }

        public bool Equals(ContentId other) => Value == other.Value;
        public override bool Equals(object? obj) => obj is ContentId other && other.Value == Value;
        public override int GetHashCode() => (int)Value;
        public int CompareTo(ContentId other) => Value.CompareTo(other.Value);

        public static bool operator ==(ContentId a, ContentId b) => a.Value == b.Value;
        public static bool operator !=(ContentId a, ContentId b) => a.Value != b.Value;

        /// <summary>Debug only. Allocates; never call in hot paths.</summary>
        public override string ToString()
        {
            if (!IsValid)
                return "none";
            return ContentIdDebugNames.TryGet(this, out var name) ? name : "cid:" + Value.ToString("x8");
        }
    }

    public static class ContentIdHash
    {
        private const uint OffsetBasis = 2166136261u;
        private const uint Prime = 16777619u;

        /// <summary>32-bit FNV-1a over the UTF-16 code units of the source ID.</summary>
        public static uint Compute(string sourceId)
        {
            uint hash = OffsetBasis;
            for (int i = 0; i < sourceId.Length; i++)
            {
                char c = sourceId[i];
                hash ^= (byte)(c & 0xFF);
                hash *= Prime;
                hash ^= (byte)(c >> 8);
                hash *= Prime;
            }
            return hash;
        }
    }

    /// <summary>Source ID format: lowercase dotted segments, e.g. "variant.rabbit.white".</summary>
    public static class ContentIdFormat
    {
        public static bool IsValid(string? sourceId)
        {
            if (string.IsNullOrEmpty(sourceId))
                return false;

            int segments = 1;
            bool segmentHasChars = false;
            for (int i = 0; i < sourceId!.Length; i++)
            {
                char c = sourceId[i];
                if (c == '.')
                {
                    if (!segmentHasChars)
                        return false;
                    segments++;
                    segmentHasChars = false;
                    continue;
                }

                bool ok = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_';
                if (!ok)
                    return false;
                segmentHasChars = true;
            }

            return segmentHasChars && segments >= 2;
        }
    }

    /// <summary>Reverse lookup for logs and tools. Compiled out of release players.</summary>
    public static class ContentIdDebugNames
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static readonly Dictionary<uint, string> Names = new Dictionary<uint, string>(1024);
        private static readonly object Gate = new object();
#endif

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void Register(ContentId id, string sourceId)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            lock (Gate)
            {
                Names[id.Value] = sourceId;
            }
#endif
        }

        public static bool TryGet(ContentId id, out string name)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            lock (Gate)
            {
                if (Names.TryGetValue(id.Value, out var found))
                {
                    name = found;
                    return true;
                }
            }
#endif
            name = string.Empty;
            return false;
        }
    }
}
