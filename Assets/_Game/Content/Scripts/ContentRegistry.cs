#nullable enable
using System;
using System.Collections.Generic;
using PocketWilds.Core;

namespace PocketWilds.Content
{
    public interface IContentDefinition
    {
        ContentId Id { get; }
    }

    /// <summary>
    /// Immutable lookup table for one definition type. Dense indexes are stable for a session only;
    /// saves and network payloads always use ContentId.
    /// </summary>
    public sealed class ContentTable<T> where T : class, IContentDefinition
    {
        private readonly T[] _items;
        private readonly Dictionary<ContentId, int> _indexById;

        internal ContentTable(List<T> items)
        {
            _items = items.ToArray();
            _indexById = new Dictionary<ContentId, int>(_items.Length);
            for (int i = 0; i < _items.Length; i++)
                _indexById.Add(_items[i].Id, i);
        }

        public static ContentTable<T> Empty { get; } = new ContentTable<T>(new List<T>());

        public int Count => _items.Length;

        public T this[int denseIndex] => _items[denseIndex];

        public bool TryGetIndex(ContentId id, out int denseIndex) => _indexById.TryGetValue(id, out denseIndex);

        public bool TryGet(ContentId id, out T definition)
        {
            if (_indexById.TryGetValue(id, out int index))
            {
                definition = _items[index];
                return true;
            }
            definition = null!;
            return false;
        }

        public T Get(ContentId id)
        {
            if (!_indexById.TryGetValue(id, out int index))
                throw new KeyNotFoundException("Unknown content id " + id);
            return _items[index];
        }
    }

    /// <summary>
    /// Runtime content registry built once at boot from validated definitions. O(1) lookups by ContentId.
    /// </summary>
    public sealed class ContentRegistry
    {
        public ContentRegistry(ContentTable<SpeciesDefinition> species)
        {
            Species = species;
        }

        public static ContentRegistry Empty { get; } = new ContentRegistry(ContentTable<SpeciesDefinition>.Empty);

        public ContentTable<SpeciesDefinition> Species { get; }
    }

    public enum ContentIssueSeverity
    {
        Warning,
        Error,
    }

    public readonly struct ContentIssue
    {
        public readonly ContentIssueSeverity Severity;
        public readonly string SourceId;
        public readonly string Message;

        public ContentIssue(ContentIssueSeverity severity, string sourceId, string message)
        {
            Severity = severity;
            SourceId = sourceId;
            Message = message;
        }

        public override string ToString() => $"{Severity} [{SourceId}] {Message}";
    }

    /// <summary>
    /// Collects definitions, rejects duplicates, hash collisions and tombstoned IDs,
    /// then builds the immutable registry. Errors must be fixed before a build ships.
    /// </summary>
    public sealed class ContentRegistryBuilder
    {
        private readonly List<SpeciesDefinition> _species = new List<SpeciesDefinition>();
        private readonly Dictionary<uint, string> _seenHashes = new Dictionary<uint, string>();
        private readonly HashSet<uint> _tombstones = new HashSet<uint>();
        private readonly List<ContentIssue> _issues = new List<ContentIssue>();

        public IReadOnlyList<ContentIssue> Issues => _issues;

        public bool HasErrors
        {
            get
            {
                for (int i = 0; i < _issues.Count; i++)
                {
                    if (_issues[i].Severity == ContentIssueSeverity.Error)
                        return true;
                }
                return false;
            }
        }

        /// <summary>Retired IDs. A tombstoned ID can never be used again, for any content type.</summary>
        public void AddTombstone(string sourceId)
        {
            _tombstones.Add(ContentIdHash.Compute(sourceId));
        }

        public void AddSpecies(SpeciesDefinition definition, string sourceId)
        {
            if (TryClaim(definition.Id, sourceId))
                _species.Add(definition);
        }

        public ContentRegistry Build()
        {
            if (HasErrors)
                throw new InvalidOperationException("Content has errors: " + string.Join("; ", _issues));
            return new ContentRegistry(new ContentTable<SpeciesDefinition>(_species));
        }

        private bool TryClaim(ContentId id, string sourceId)
        {
            if (!ContentIdFormat.IsValid(sourceId))
            {
                _issues.Add(new ContentIssue(ContentIssueSeverity.Error, sourceId, "Invalid ID format."));
                return false;
            }

            if (ContentIdHash.Compute(sourceId) != id.Value)
            {
                _issues.Add(new ContentIssue(ContentIssueSeverity.Error, sourceId, "ContentId does not match its source ID."));
                return false;
            }

            if (_tombstones.Contains(id.Value))
            {
                _issues.Add(new ContentIssue(ContentIssueSeverity.Error, sourceId, "ID is tombstoned and cannot be reused."));
                return false;
            }

            if (_seenHashes.TryGetValue(id.Value, out var existing))
            {
                string message = existing == sourceId
                    ? "Duplicate ID."
                    : "Hash collision with '" + existing + "'. Rename one of them.";
                _issues.Add(new ContentIssue(ContentIssueSeverity.Error, sourceId, message));
                return false;
            }

            _seenHashes.Add(id.Value, sourceId);
            return true;
        }
    }
}
