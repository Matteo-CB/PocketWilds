#nullable enable
using System;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Simulation;

namespace PocketWilds.Services.Save
{
    /// <summary>Hides the on-disk format from the rest of the game.</summary>
    public interface ISaveSerializer
    {
        int CurrentSchemaVersion { get; }

        /// <summary>Writes a complete file (header + payload) into <paramref name="output"/>.</summary>
        void Serialize(PlayerState state, ByteBufferWriter output);

        /// <summary>Validates and splits a file into raw sections. Throws SaveCorruptException.</summary>
        SaveDocument ReadDocument(byte[] data, int length);

        /// <summary>Builds typed state from a document already migrated to the current schema.</summary>
        PlayerState Deserialize(SaveDocument document);
    }

    public sealed class SaveCorruptException : Exception
    {
        public SaveCorruptException(string message) : base(message)
        {
        }
    }

    public static class SaveSchema
    {
        /// <summary>Bump on any persisted data change, with a migration and updated golden saves.</summary>
        /// <summary>2: Offline section (rate at save, time high-water mark). 3: Codex section (seen ids). 4: Pity counters.</summary>
        public const int CurrentVersion = 4;
    }

    /// <summary>
    /// Compact binary sections. Each section carries its own version so readers can evolve
    /// independently. Missing sections load as defaults; unknown sections are ignored.
    /// </summary>
    public sealed class BinarySaveSerializer : ISaveSerializer
    {
        private const ushort MetaVersion = 1;
        private const ushort WalletVersion = 1;
        private const ushort WorldVersion = 1;
        private const ushort CollectionVersion = 1;
        private const ushort NamesVersion = 1;
        private const ushort MasteryVersion = 1;
        private const ushort SettingsVersion = 1;
        private const ushort EntitlementsVersion = 1;
        private const ushort OfflineVersion = 1;
        private const ushort CodexVersion = 1;
        private const ushort PityVersion = 1;

        public int CurrentSchemaVersion => SaveSchema.CurrentVersion;

        // ---------------- Write ----------------

        public void Serialize(PlayerState state, ByteBufferWriter output)
        {
            output.Reset();
            SaveFileFormat.WriteHeaderPlaceholder(output, SaveSchema.CurrentVersion, state.Meta.Revision);

            int mark = BeginSection(output, SaveSectionId.Meta, MetaVersion);
            WriteMeta(output, state.Meta);
            EndSection(output, mark);

            mark = BeginSection(output, SaveSectionId.Wallet, WalletVersion);
            output.WriteBigValue(state.Wallet.Light);
            output.WriteBigValue(state.Wallet.LifetimeLight);
            output.WriteInt64(state.Wallet.Essence);
            output.WriteInt64(state.Wallet.Gems);
            EndSection(output, mark);

            mark = BeginSection(output, SaveSectionId.WorldProgress, WorldVersion);
            WriteWorld(output, state.World);
            EndSection(output, mark);

            mark = BeginSection(output, SaveSectionId.Collection, CollectionVersion);
            WriteCollection(output, state.Collection);
            EndSection(output, mark);

            mark = BeginSection(output, SaveSectionId.AnimalNames, NamesVersion);
            output.WriteInt32(state.Collection.CustomNames.Count);
            foreach (var pair in state.Collection.CustomNames)
            {
                output.WriteUInt32(pair.Key);
                output.WriteString(pair.Value);
            }
            EndSection(output, mark);

            mark = BeginSection(output, SaveSectionId.Mastery, MasteryVersion);
            output.WriteInt32(state.Mastery.Species.Count);
            foreach (var pair in state.Mastery.Species)
            {
                output.WriteContentId(pair.Key);
                output.WriteInt32(pair.Value.Level);
                output.WriteInt32(pair.Value.Xp);
            }
            EndSection(output, mark);

            mark = BeginSection(output, SaveSectionId.Settings, SettingsVersion);
            output.WriteDouble(state.Settings.MusicVolume);
            output.WriteDouble(state.Settings.SfxVolume);
            output.WriteBool(state.Settings.HapticsEnabled);
            output.WriteBool(state.Settings.ReduceMotion);
            output.WriteString(state.Settings.LanguageCode);
            EndSection(output, mark);

            mark = BeginSection(output, SaveSectionId.Entitlements, EntitlementsVersion);
            output.WriteInt32(state.Entitlements.Owned.Count);
            foreach (var id in state.Entitlements.Owned)
                output.WriteContentId(id);
            output.WriteInt32(state.Entitlements.ProcessedTransactions.Count);
            foreach (var transaction in state.Entitlements.ProcessedTransactions)
                output.WriteString(transaction);
            EndSection(output, mark);

            mark = BeginSection(output, SaveSectionId.Offline, OfflineVersion);
            output.WriteBigValue(state.Offline.RateAtSave);
            output.WriteBool(state.Offline.HasRate);
            output.WriteInt64(state.Offline.HighWaterUtc.ToUnixTimeMilliseconds());
            EndSection(output, mark);

            mark = BeginSection(output, SaveSectionId.Codex, CodexVersion);
            output.WriteInt32(state.Codex.SeenSpecies.Count);
            foreach (var id in state.Codex.SeenSpecies)
                output.WriteContentId(id);
            output.WriteInt32(state.Codex.SeenVariants.Count);
            foreach (var id in state.Codex.SeenVariants)
                output.WriteContentId(id);
            EndSection(output, mark);

            mark = BeginSection(output, SaveSectionId.Pity, PityVersion);
            WriteCounters(output, state.Pity.NewSpeciesMisses);
            WriteCounters(output, state.Pity.VariantMisses);
            EndSection(output, mark);

            SaveFileFormat.FinishPayload(output);
        }

        private static int BeginSection(ByteBufferWriter output, SaveSectionId id, ushort version)
        {
            output.WriteUInt16((ushort)id);
            output.WriteUInt16(version);
            return output.ReserveInt32();
        }

        private static void EndSection(ByteBufferWriter output, int lengthPosition)
        {
            output.PatchInt32(lengthPosition, output.Length - lengthPosition - 4);
        }

        private static void WriteMeta(ByteBufferWriter output, SaveMeta meta)
        {
            WriteGuid(output, meta.ProfileId);
            output.WriteInt64(meta.CreatedUtc.ToUnixTimeMilliseconds());
            output.WriteInt64(meta.LastSaveUtc.ToUnixTimeMilliseconds());
            WriteGuid(output, meta.LastKnownSessionId);
            output.WriteInt64(meta.Revision);
            output.WriteString(meta.AppVersion);
            output.WriteDouble(meta.TotalPlaySeconds);
        }

        private static void WriteWorld(ByteBufferWriter output, WorldProgressState world)
        {
            output.WriteInt32(world.Biomes.Count);
            for (int i = 0; i < world.Biomes.Count; i++)
            {
                var biome = world.Biomes[i];
                output.WriteContentId(biome.Biome);
                output.WriteBool(biome.Unlocked);
                output.WriteInt32(biome.Expansions);
            }

            output.WriteInt32(world.EggPurchasesInCycle.Count);
            foreach (var pair in world.EggPurchasesInCycle)
            {
                output.WriteContentId(pair.Key);
                output.WriteInt32(pair.Value);
            }

            output.WriteInt32(world.RenewalCount);
        }

        private static void WriteCollection(ByteBufferWriter output, CollectionState collection)
        {
            output.WriteUInt32(collection.NextInstanceId);
            output.WriteInt32(collection.Count);
            for (int i = 0; i < collection.Count; i++)
            {
                ref readonly var a = ref collection[i];
                output.WriteUInt32(a.InstanceId);
                output.WriteContentId(a.Species);
                output.WriteContentId(a.Variant);
                output.WriteUInt32(a.Seed);
                output.WriteUInt32(a.CreatedUtcSeconds);
                output.WriteUInt32(a.TraitBits);
                output.WriteUInt16(a.BondPoints);
                output.WriteByte((byte)a.Rarity);
                output.WriteByte((byte)a.Flags);
            }

            output.WriteInt32(collection.DiscoveredSpecies.Count);
            foreach (var id in collection.DiscoveredSpecies)
                output.WriteContentId(id);

            output.WriteInt32(collection.DiscoveredVariants.Count);
            foreach (var id in collection.DiscoveredVariants)
                output.WriteContentId(id);
        }

        private static void WriteGuid(ByteBufferWriter output, Guid guid)
        {
            Span<byte> bytes = stackalloc byte[16];
            guid.TryWriteBytes(bytes);
            output.WriteBytes(bytes);
        }

        // ---------------- Read ----------------

        public SaveDocument ReadDocument(byte[] data, int length)
        {
            if (!SaveFileFormat.TryValidate(data, length, out var header, out var error))
                throw new SaveCorruptException(error);

            var document = new SaveDocument(header.SchemaVersion, header.Revision);
            try
            {
                var reader = new ByteBufferReader(data, SaveFileFormat.HeaderSize, header.PayloadLength);
                while (!reader.IsAtEnd)
                {
                    var id = (SaveSectionId)reader.ReadUInt16();
                    ushort version = reader.ReadUInt16();
                    int sectionLength = reader.ReadInt32();
                    if (sectionLength < 0)
                        throw new SaveCorruptException("Negative section length.");
                    var bytes = reader.ReadBytes(sectionLength).ToArray();
                    document.SetSection(new SaveSection(id, version, bytes));
                }
            }
            catch (BinaryFormatException ex)
            {
                throw new SaveCorruptException("Section table damaged: " + ex.Message);
            }

            return document;
        }

        public PlayerState Deserialize(SaveDocument document)
        {
            if (document.SchemaVersion != SaveSchema.CurrentVersion)
                throw new InvalidOperationException("Migrate the document before deserializing.");

            var state = new PlayerState();
            try
            {
                if (document.TryGetSection(SaveSectionId.Meta, out var meta))
                    ReadMeta(meta, state.Meta);
                if (document.TryGetSection(SaveSectionId.Wallet, out var wallet))
                    ReadWallet(wallet, state.Wallet);
                if (document.TryGetSection(SaveSectionId.WorldProgress, out var world))
                    ReadWorld(world, state.World);
                if (document.TryGetSection(SaveSectionId.Collection, out var collection))
                    ReadCollection(collection, state.Collection);
                if (document.TryGetSection(SaveSectionId.AnimalNames, out var names))
                    ReadNames(names, state.Collection);
                if (document.TryGetSection(SaveSectionId.Mastery, out var mastery))
                    ReadMastery(mastery, state.Mastery);
                if (document.TryGetSection(SaveSectionId.Settings, out var settings))
                    ReadSettings(settings, state.Settings);
                if (document.TryGetSection(SaveSectionId.Entitlements, out var entitlements))
                    ReadEntitlements(entitlements, state.Entitlements);
                if (document.TryGetSection(SaveSectionId.Offline, out var offline))
                    ReadOffline(offline, state.Offline);
                if (document.TryGetSection(SaveSectionId.Pity, out var pity))
                    ReadPity(pity, state.Pity);
                if (document.TryGetSection(SaveSectionId.Codex, out var codex))
                    ReadCodex(codex, state.Codex);
                else
                {
                    // Saves older than the Codex: everything found so far counts as seen (no flood of "New").
                    state.Codex.SeenSpecies.UnionWith(state.Collection.DiscoveredSpecies);
                    state.Codex.SeenVariants.UnionWith(state.Collection.DiscoveredVariants);
                }
            }
            catch (BinaryFormatException ex)
            {
                throw new SaveCorruptException("Section content damaged: " + ex.Message);
            }

            // The header revision is authoritative; it decides which file is newest.
            state.Meta.Revision = Math.Max(state.Meta.Revision, document.Revision);
            return state;
        }

        private static void RequireVersion(SaveSection section, ushort maxSupported)
        {
            if (section.Version == 0 || section.Version > maxSupported)
                throw new SaveCorruptException($"Section {section.Id} version {section.Version} not supported.");
        }

        private static void ReadMeta(SaveSection section, SaveMeta meta)
        {
            RequireVersion(section, MetaVersion);
            var r = section.CreateReader();
            meta.ProfileId = ReadGuid(ref r);
            meta.CreatedUtc = DateTimeOffset.FromUnixTimeMilliseconds(r.ReadInt64());
            meta.LastSaveUtc = DateTimeOffset.FromUnixTimeMilliseconds(r.ReadInt64());
            meta.LastKnownSessionId = ReadGuid(ref r);
            meta.Revision = r.ReadInt64();
            meta.AppVersion = r.ReadString() ?? string.Empty;
            meta.TotalPlaySeconds = r.ReadDouble();
        }

        private static void WriteCounters(ByteBufferWriter output, System.Collections.Generic.Dictionary<ContentId, int> counters)
        {
            output.WriteInt32(counters.Count);
            foreach (var pair in counters)
            {
                output.WriteContentId(pair.Key);
                output.WriteInt32(pair.Value);
            }
        }

        private static void ReadPity(SaveSection section, PityState pity)
        {
            RequireVersion(section, PityVersion);
            var r = section.CreateReader();
            int eggs = ReadCount(ref r);
            for (int i = 0; i < eggs; i++)
            {
                var egg = r.ReadContentId();
                pity.NewSpeciesMisses[egg] = r.ReadInt32();
            }
            int species = ReadCount(ref r);
            for (int i = 0; i < species; i++)
            {
                var id = r.ReadContentId();
                pity.VariantMisses[id] = r.ReadInt32();
            }
        }

        private static void ReadCodex(SaveSection section, CodexState codex)
        {
            RequireVersion(section, CodexVersion);
            var r = section.CreateReader();
            int species = ReadCount(ref r);
            for (int i = 0; i < species; i++)
                codex.SeenSpecies.Add(r.ReadContentId());
            int variants = ReadCount(ref r);
            for (int i = 0; i < variants; i++)
                codex.SeenVariants.Add(r.ReadContentId());
        }

        private static void ReadOffline(SaveSection section, OfflineState offline)
        {
            RequireVersion(section, OfflineVersion);
            var r = section.CreateReader();
            offline.RateAtSave = r.ReadBigValue();
            offline.HasRate = r.ReadBool();
            offline.HighWaterUtc = DateTimeOffset.FromUnixTimeMilliseconds(r.ReadInt64());
        }

        private static void ReadWallet(SaveSection section, WalletState wallet)
        {
            RequireVersion(section, WalletVersion);
            var r = section.CreateReader();
            wallet.Light = r.ReadBigValue();
            wallet.LifetimeLight = r.ReadBigValue();
            wallet.Essence = r.ReadInt64();
            wallet.Gems = r.ReadInt64();
        }

        private static void ReadWorld(SaveSection section, WorldProgressState world)
        {
            RequireVersion(section, WorldVersion);
            var r = section.CreateReader();
            int biomeCount = ReadCount(ref r);
            for (int i = 0; i < biomeCount; i++)
            {
                world.Biomes.Add(new BiomeProgress
                {
                    Biome = r.ReadContentId(),
                    Unlocked = r.ReadBool(),
                    Expansions = r.ReadInt32(),
                });
            }

            int eggCount = ReadCount(ref r);
            for (int i = 0; i < eggCount; i++)
            {
                var egg = r.ReadContentId();
                world.EggPurchasesInCycle[egg] = r.ReadInt32();
            }

            world.RenewalCount = r.ReadInt32();
        }

        private static void ReadCollection(SaveSection section, CollectionState collection)
        {
            RequireVersion(section, CollectionVersion);
            var r = section.CreateReader();
            uint nextId = r.ReadUInt32();
            int count = ReadCount(ref r);
            collection.EnsureCapacity(count + 16);
            for (int i = 0; i < count; i++)
            {
                var animal = new AnimalInstance
                {
                    InstanceId = r.ReadUInt32(),
                    Species = r.ReadContentId(),
                    Variant = r.ReadContentId(),
                    Seed = r.ReadUInt32(),
                    CreatedUtcSeconds = r.ReadUInt32(),
                    TraitBits = r.ReadUInt32(),
                    BondPoints = r.ReadUInt16(),
                    Rarity = (RarityTier)r.ReadByte(),
                    Flags = (AnimalFlags)r.ReadByte(),
                };
                collection.Restore(animal);
            }
            collection.SetNextInstanceId(nextId);

            int speciesCount = ReadCount(ref r);
            for (int i = 0; i < speciesCount; i++)
                collection.DiscoveredSpecies.Add(r.ReadContentId());

            int variantCount = ReadCount(ref r);
            for (int i = 0; i < variantCount; i++)
                collection.DiscoveredVariants.Add(r.ReadContentId());
        }

        private static void ReadNames(SaveSection section, CollectionState collection)
        {
            RequireVersion(section, NamesVersion);
            var r = section.CreateReader();
            int count = ReadCount(ref r);
            for (int i = 0; i < count; i++)
            {
                uint id = r.ReadUInt32();
                string? name = r.ReadString();
                if (!string.IsNullOrEmpty(name) && collection.TryGetIndex(id, out _))
                    collection.CustomNames[id] = name!;
            }
        }

        private static void ReadMastery(SaveSection section, MasteryState mastery)
        {
            RequireVersion(section, MasteryVersion);
            var r = section.CreateReader();
            int count = ReadCount(ref r);
            for (int i = 0; i < count; i++)
            {
                var species = r.ReadContentId();
                mastery.Species[species] = new SpeciesMastery { Level = r.ReadInt32(), Xp = r.ReadInt32() };
            }
        }

        private static void ReadSettings(SaveSection section, SettingsState settings)
        {
            RequireVersion(section, SettingsVersion);
            var r = section.CreateReader();
            settings.MusicVolume = Clamp01((float)r.ReadDouble());
            settings.SfxVolume = Clamp01((float)r.ReadDouble());
            settings.HapticsEnabled = r.ReadBool();
            settings.ReduceMotion = r.ReadBool();
            settings.LanguageCode = r.ReadString() ?? string.Empty;
        }

        private static void ReadEntitlements(SaveSection section, EntitlementState entitlements)
        {
            RequireVersion(section, EntitlementsVersion);
            var r = section.CreateReader();
            int owned = ReadCount(ref r);
            for (int i = 0; i < owned; i++)
                entitlements.Owned.Add(r.ReadContentId());
            int transactions = ReadCount(ref r);
            for (int i = 0; i < transactions; i++)
            {
                var transaction = r.ReadString();
                if (!string.IsNullOrEmpty(transaction))
                    entitlements.ProcessedTransactions.Add(transaction!);
            }
        }

        private static int ReadCount(ref ByteBufferReader reader)
        {
            int count = reader.ReadInt32();
            if (count < 0 || count > reader.Remaining)
                throw new SaveCorruptException("Invalid element count " + count + ".");
            return count;
        }

        private static Guid ReadGuid(ref ByteBufferReader reader) => new Guid(reader.ReadBytes(16));

        private static float Clamp01(float value) => value < 0f ? 0f : (value > 1f ? 1f : value);
    }
}
