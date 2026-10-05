#nullable enable
using System.Collections.Generic;
using PocketWilds.Core;

namespace PocketWilds.Services.Save
{
    /// <summary>Stable section ids. Append only; a removed id is never reused.</summary>
    public enum SaveSectionId : ushort
    {
        Meta = 1,
        Wallet = 2,
        WorldProgress = 3,
        Collection = 4,
        Mastery = 5,
        Settings = 6,
        Entitlements = 7,
        AnimalNames = 8,
        Offline = 9,
        Codex = 10,
        Pity = 11,
        Shop = 12,
        Analytics = 13,
    }

    public sealed class SaveSection
    {
        public SaveSection(SaveSectionId id, ushort version, byte[] data)
        {
            Id = id;
            Version = version;
            Data = data;
        }

        public SaveSectionId Id { get; }
        public ushort Version { get; }
        public byte[] Data { get; }

        public ByteBufferReader CreateReader() => new ByteBufferReader(Data, 0, Data.Length);
    }

    /// <summary>
    /// Raw, migration-friendly view of a save: schema version plus untyped sections.
    /// Migrations rewrite sections here before the typed PlayerState is built.
    /// </summary>
    public sealed class SaveDocument
    {
        private readonly Dictionary<SaveSectionId, SaveSection> _sections = new Dictionary<SaveSectionId, SaveSection>();

        public SaveDocument(int schemaVersion, long revision)
        {
            SchemaVersion = schemaVersion;
            Revision = revision;
        }

        public int SchemaVersion { get; set; }
        public long Revision { get; }

        public IEnumerable<SaveSection> Sections => _sections.Values;

        public bool TryGetSection(SaveSectionId id, out SaveSection section) => _sections.TryGetValue(id, out section!);

        public void SetSection(SaveSection section) => _sections[section.Id] = section;

        public bool RemoveSection(SaveSectionId id) => _sections.Remove(id);
    }
}
