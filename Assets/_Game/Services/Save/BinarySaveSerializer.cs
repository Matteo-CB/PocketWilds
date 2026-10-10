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
        /// <summary>
        /// 2: Offline section (rate at save, time high-water mark). 3: Codex section (seen ids). 4: Pity counters.
        /// 7: biome zones, mastery and upgrades (World 2), Progression section (refuge level, decorations, treats).
        /// 8: Discovery egg stock (World 3). 9: Renewal memory and Essence tree. 10: daily rendez-vous and inventory (T36).
        /// 11: activities (T37). 12: expeditions (T42). 13: garden (T43). 14: albums (T44A). 15: achievements and profile
        /// items (T44B). 16: the refuge journal and the rare visitors (T44C).
        /// </summary>
        public const int CurrentVersion = 16;
    }

    /// <summary>
    /// Compact binary sections. Each section carries its own version so readers can evolve
    /// independently. Missing sections load as defaults; unknown sections are ignored.
    /// </summary>
    public sealed class BinarySaveSerializer : ISaveSerializer
    {
        private const ushort MetaVersion = 1;
        private const ushort WalletVersion = 1;
        // 2: per biome, zones opened, mastery XP and level, upgrade levels. 3: Discovery egg stock.
        private const ushort WorldVersion = 3;
        private const ushort CollectionVersion = 1;
        private const ushort NamesVersion = 1;
        private const ushort MasteryVersion = 1;
        private const ushort SettingsVersion = 1;
        private const ushort EntitlementsVersion = 1;
        private const ushort OfflineVersion = 1;
        private const ushort CodexVersion = 1;
        private const ushort PityVersion = 1;
        private const ushort ShopVersion = 1;
        private const ushort AnalyticsVersion = 1;
        private const ushort ProgressionVersion = 2;
        private const ushort DailyVisitVersion = 1;
        private const ushort InventoryVersion = 1;
        private const ushort ActivitiesVersion = 1;
        private const ushort ExpeditionsVersion = 1;
        private const ushort GardenVersion = 1;
        private const ushort AlbumsVersion = 1;
        private const ushort AchievementsVersion = 1;
        private const ushort JournalVersion = 1;

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

            mark = BeginSection(output, SaveSectionId.Shop, ShopVersion);
            output.WriteInt32(state.Shop.PaidPurchases);
            output.WriteInt32(state.Shop.Offers.Count);
            foreach (var pair in state.Shop.Offers)
            {
                output.WriteContentId(pair.Key);
                output.WriteInt32(pair.Value.Purchases);
                output.WriteInt32(pair.Value.Impressions);
                output.WriteInt64(pair.Value.LastShownUnix);
            }
            EndSection(output, mark);

            mark = BeginSection(output, SaveSectionId.Analytics, AnalyticsVersion);
            output.WriteInt64((long)state.Analytics.Milestones);
            output.WriteInt32(state.Analytics.Sessions);
            EndSection(output, mark);

            mark = BeginSection(output, SaveSectionId.Progression, ProgressionVersion);
            WriteProgression(output, state.Progression);
            EndSection(output, mark);

            mark = BeginSection(output, SaveSectionId.DailyVisit, DailyVisitVersion);
            WriteDailyVisit(output, state.DailyVisit);
            EndSection(output, mark);

            mark = BeginSection(output, SaveSectionId.Inventory, InventoryVersion);
            WriteInventory(output, state.Inventory);
            EndSection(output, mark);

            mark = BeginSection(output, SaveSectionId.Activities, ActivitiesVersion);
            WriteActivities(output, state.Activities);
            EndSection(output, mark);

            mark = BeginSection(output, SaveSectionId.Expeditions, ExpeditionsVersion);
            WriteExpeditions(output, state.Expeditions);
            EndSection(output, mark);

            mark = BeginSection(output, SaveSectionId.Garden, GardenVersion);
            WriteGarden(output, state.Garden);
            EndSection(output, mark);

            mark = BeginSection(output, SaveSectionId.Albums, AlbumsVersion);
            WriteAlbums(output, state.Albums);
            EndSection(output, mark);

            mark = BeginSection(output, SaveSectionId.Achievements, AchievementsVersion);
            WriteAchievements(output, state.Achievements);
            EndSection(output, mark);

            mark = BeginSection(output, SaveSectionId.Journal, JournalVersion);
            WriteJournal(output, state.Journal, state.Visitors);
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
                output.WriteUInt32(biome.ZonesOpened);
                output.WriteInt64(biome.MasteryXp);
                output.WriteInt32(biome.MasteryLevel);
                output.WriteInt32(biome.ProductionLevel);
                output.WriteInt32(biome.NestsLevel);
                output.WriteInt32(biome.CuriosityLevel);
            }

            output.WriteInt32(world.EggPurchasesInCycle.Count);
            foreach (var pair in world.EggPurchasesInCycle)
            {
                output.WriteContentId(pair.Key);
                output.WriteInt32(pair.Value);
            }

            output.WriteInt32(world.RenewalCount);
            output.WriteInt32(world.DiscoveryEggs);
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
                if (document.TryGetSection(SaveSectionId.Shop, out var shop))
                    ReadShop(shop, state.Shop);
                if (document.TryGetSection(SaveSectionId.Analytics, out var analytics))
                {
                    RequireVersion(analytics, AnalyticsVersion);
                    var r = analytics.CreateReader();
                    state.Analytics.Milestones = (ulong)r.ReadInt64();
                    state.Analytics.Sessions = r.ReadInt32();
                }
                else
                {
                    // Saves older than schema 6: milestones already reached never fire again as "first".
                    AnalyticsState.InferFrom(state);
                }
                // Saves older than schema 7 have no Progression section: the refuge level is computed on load.
                if (document.TryGetSection(SaveSectionId.Progression, out var progression))
                    ReadProgression(progression, state.Progression);
                // Saves older than schema 10 have no rendez-vous: a profile that already holds 5 animals skips the welcome
                // (doc 01 Contenu, section 1.1); the first day check counts a login day.
                if (document.TryGetSection(SaveSectionId.DailyVisit, out var daily))
                    ReadDailyVisit(daily, state.DailyVisit);
                else
                    state.DailyVisit.OnboardingDone = state.Collection.Count >= 5;
                if (document.TryGetSection(SaveSectionId.Inventory, out var inventory))
                    ReadInventory(inventory, state.Inventory);
                // Saves older than schema 11 have no activities: the first use fills the ticket reserve and takes in the
                // tickets kept in the inventory (T37).
                if (document.TryGetSection(SaveSectionId.Activities, out var activities))
                    ReadActivities(activities, state.Activities);
                // Saves older than schema 12 have no expeditions: no team away, the first stroll still to come (T42).
                if (document.TryGetSection(SaveSectionId.Expeditions, out var expeditions))
                    ReadExpeditions(expeditions, state.Expeditions);
                // Saves older than schema 13 have no garden: every plot bare, level 1, no dye (T43).
                if (document.TryGetSection(SaveSectionId.Garden, out var garden))
                    ReadGarden(garden, state.Garden);
                if (document.TryGetSection(SaveSectionId.Albums, out var albums))
                    ReadAlbums(albums, state.Albums);
                // Saves older than schema 15 have no achievements: the hooks compute the state counters from the save
                // (species, levels, masteries, bonds, decorations, albums) and give the starting title and frame (T44B).
                if (document.TryGetSection(SaveSectionId.Achievements, out var achievements))
                    ReadAchievements(achievements, state.Achievements);
                // Saves older than schema 16 have no journal: the journal hooks mark the account's past moments done without
                // pages (no « first hatch » on an older account) and no visitor has been met yet (T44C).
                if (document.TryGetSection(SaveSectionId.Journal, out var journal))
                    ReadJournal(journal, state.Journal, state.Visitors);
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

        private static void WriteProgression(ByteBufferWriter output, ProgressionState progression)
        {
            var refuge = progression.Refuge;
            output.WriteInt64(refuge.Score);
            output.WriteInt32(refuge.Level);
            output.WriteInt32(refuge.LastRewardedLevel);
            output.WriteInt32(refuge.PendingEssenceFromLevels);
            WriteCounters(output, progression.DecorationCopies);
            var stand = progression.TreatStand;
            output.WriteInt32(stand.DayKey);
            output.WriteContentId(stand.Slot0);
            output.WriteContentId(stand.Slot1);
            output.WriteContentId(stand.Slot2);
            output.WriteInt32(stand.BoughtMask);
            WriteCounters(output, progression.Treats);
            // Version 2 (schema 9, T35C): the Renewal's memory and the Essence tree.
            var renewal = progression.Renewal;
            output.WriteInt64(renewal.EssenceFromLight);
            output.WriteInt64(renewal.EssenceEarned);
            output.WriteInt32(renewal.SpeciesAtLast);
            output.WriteInt32(renewal.BiomesAtLast);
            output.WriteInt32(renewal.MasteredAtLast);
            output.WriteInt32(renewal.AlbumPagesAtLast);
            WriteCounters(output, progression.EssenceRanks);
        }

        private static void ReadProgression(SaveSection section, ProgressionState progression)
        {
            RequireVersion(section, ProgressionVersion);
            var r = section.CreateReader();
            var refuge = progression.Refuge;
            refuge.Score = r.ReadInt64();
            refuge.Level = Math.Max(1, r.ReadInt32());
            refuge.LastRewardedLevel = Math.Max(1, r.ReadInt32());
            refuge.PendingEssenceFromLevels = r.ReadInt32();
            ReadCounters(ref r, progression.DecorationCopies);
            var stand = progression.TreatStand;
            stand.DayKey = r.ReadInt32();
            stand.Slot0 = r.ReadContentId();
            stand.Slot1 = r.ReadContentId();
            stand.Slot2 = r.ReadContentId();
            stand.BoughtMask = r.ReadInt32();
            ReadCounters(ref r, progression.Treats);
            if (section.Version < 2)
                return;
            var renewal = progression.Renewal;
            renewal.EssenceFromLight = Math.Max(0L, r.ReadInt64());
            renewal.EssenceEarned = Math.Max(0L, r.ReadInt64());
            renewal.SpeciesAtLast = Math.Max(0, r.ReadInt32());
            renewal.BiomesAtLast = Math.Max(0, r.ReadInt32());
            renewal.MasteredAtLast = Math.Max(0, r.ReadInt32());
            renewal.AlbumPagesAtLast = Math.Max(0, r.ReadInt32());
            ReadCounters(ref r, progression.EssenceRanks);
        }

        private static void WriteDailyVisit(ByteBufferWriter output, DailyVisitState d)
        {
            output.WriteInt32(d.LastDayKey);
            output.WriteInt32(d.LoginDays);
            output.WriteBool(d.OnboardingDone);
            output.WriteByte(d.TutorialStep);
            output.WriteByte(d.WelcomeClaimed);
            output.WriteInt32(d.WelcomeDayKey);
            output.WriteInt32(d.GiftDayKey);
            output.WriteInt32(d.GiftsOpened);
            output.WriteInt32(d.GiftsSinceBig);
            output.WriteByte(d.LastGiftLot);
            output.WriteByte(d.CalendarNext);
            output.WriteInt32(d.CalendarCycle);
            output.WriteInt32(d.CalendarDayKey);
            output.WriteInt32(d.CalendarStamps);

            output.WriteInt32(d.WishCount);
            for (int i = 0; i < d.WishCount; i++)
            {
                ref readonly var w = ref d.Wishes[i];
                output.WriteUInt16(w.Template);
                output.WriteUInt32(w.Animal);
                output.WriteContentId(w.Param);
                output.WriteByte(w.ParamKind);
                output.WriteByte(w.ParamExtra);
                output.WriteInt32(w.Target);
                output.WriteInt32(w.Progress);
                output.WriteInt32(w.DrawnDay);
                output.WriteByte(w.Flags);
            }
            output.WriteInt32(d.WishDrawDayKey);
            output.WriteInt32(d.SwapDayKey);
            output.WriteByte(d.SwapsUsed);
            output.WriteInt64(d.WishGemsTotal);
            output.WriteInt32(d.WishesCompleted);
            output.WriteInt32(d.HistoryCount);
            output.WriteInt32(d.HistoryNext);
            for (int i = 0; i < d.HistoryCount; i++)
            {
                output.WriteUInt16(d.History[i].Template);
                output.WriteInt32(d.History[i].DayKey);
            }
            output.WriteInt32(d.SystemUnlockDay.Length);
            for (int i = 0; i < d.SystemUnlockDay.Length; i++)
                output.WriteInt32(d.SystemUnlockDay[i]);
            output.WriteByte(d.TouchedCount);
            for (int i = 0; i < d.TouchedCount; i++)
                output.WriteUInt32(d.TouchedForWish[i]);

            output.WriteInt32(d.JournalStartKey);
            output.WriteInt32(d.JournalEndKey);
            output.WriteBool(d.FirstJournalDone);
            output.WriteByte(d.GoalCount);
            for (int i = 0; i < d.GoalCount; i++)
            {
                ref readonly var g = ref d.Goals[i];
                output.WriteUInt16(g.Goal);
                output.WriteByte(g.Slot);
                output.WriteInt32(g.Target);
                output.WriteInt32(g.Progress);
                output.WriteBool(g.Claimed);
            }
            output.WriteBool(d.JournalCompleteClaimed);
            output.WriteByte(d.PreviousGoalCount);
            for (int i = 0; i < d.PreviousGoalCount; i++)
                output.WriteUInt16(d.PreviousGoals[i]);
            output.WriteInt32(d.CarryGems);
            output.WriteInt32(d.CarryTickets);
            output.WriteInt32(d.CarryDiscovery);

            output.WriteInt32(d.LastReturnGiftDay);
            output.WriteInt32(d.LastBigReturnGiftDay);
            output.WriteByte(d.PendingReturnGift);
            output.WriteInt32(d.PendingReturnDays);

            output.WriteInt32(d.TouchDayKey);
            output.WriteInt32(d.TouchesToday);
            output.WriteInt32(d.TouchesByAnimal.Count);
            foreach (var pair in d.TouchesByAnimal)
            {
                output.WriteUInt32(pair.Key);
                output.WriteByte(pair.Value);
            }
        }

        private static void ReadDailyVisit(SaveSection section, DailyVisitState d)
        {
            RequireVersion(section, DailyVisitVersion);
            var r = section.CreateReader();
            d.LastDayKey = r.ReadInt32();
            d.LoginDays = Math.Max(0, r.ReadInt32());
            d.OnboardingDone = r.ReadBool();
            d.TutorialStep = r.ReadByte();
            d.WelcomeClaimed = (byte)Math.Min(3, (int)r.ReadByte());
            d.WelcomeDayKey = r.ReadInt32();
            d.GiftDayKey = r.ReadInt32();
            d.GiftsOpened = Math.Max(0, r.ReadInt32());
            d.GiftsSinceBig = Math.Max(0, r.ReadInt32());
            d.LastGiftLot = r.ReadByte();
            d.CalendarNext = (byte)(r.ReadByte() % 28);
            d.CalendarCycle = Math.Max(1, r.ReadInt32());
            d.CalendarDayKey = r.ReadInt32();
            d.CalendarStamps = Math.Max(0, r.ReadInt32());

            int wishes = ReadCount(ref r);
            d.WishCount = 0;
            for (int i = 0; i < wishes; i++)
            {
                var w = new WishRecord
                {
                    Template = r.ReadUInt16(),
                    Animal = r.ReadUInt32(),
                    Param = r.ReadContentId(),
                    ParamKind = r.ReadByte(),
                    ParamExtra = r.ReadByte(),
                    Target = Math.Max(1, r.ReadInt32()),
                    Progress = Math.Max(0, r.ReadInt32()),
                    DrawnDay = r.ReadInt32(),
                    Flags = r.ReadByte(),
                };
                if (d.WishCount < DailyVisitState.MaxWishStock)
                    d.Wishes[d.WishCount++] = w;
            }
            d.WishDrawDayKey = r.ReadInt32();
            d.SwapDayKey = r.ReadInt32();
            d.SwapsUsed = r.ReadByte();
            d.WishGemsTotal = Math.Max(0L, r.ReadInt64());
            d.WishesCompleted = Math.Max(0, r.ReadInt32());
            int history = ReadCount(ref r);
            d.HistoryNext = r.ReadInt32();
            d.HistoryCount = 0;
            for (int i = 0; i < history; i++)
            {
                var entry = new WishHistoryEntry { Template = r.ReadUInt16(), DayKey = r.ReadInt32() };
                if (d.HistoryCount < DailyVisitState.HistorySize)
                    d.History[d.HistoryCount++] = entry;
            }
            d.HistoryNext = d.HistoryCount == 0 ? 0 : ((d.HistoryNext % DailyVisitState.HistorySize) + DailyVisitState.HistorySize) % DailyVisitState.HistorySize;
            int systems = ReadCount(ref r);
            for (int i = 0; i < systems; i++)
            {
                int day = r.ReadInt32();
                if (i < d.SystemUnlockDay.Length)
                    d.SystemUnlockDay[i] = Math.Max(0, day);
            }
            int touched = r.ReadByte();
            d.TouchedCount = 0;
            for (int i = 0; i < touched; i++)
            {
                uint animal = r.ReadUInt32();
                if (d.TouchedCount < DailyVisitState.TouchedForWishSize)
                    d.TouchedForWish[d.TouchedCount++] = animal;
            }

            d.JournalStartKey = r.ReadInt32();
            d.JournalEndKey = r.ReadInt32();
            d.FirstJournalDone = r.ReadBool();
            int goals = r.ReadByte();
            d.GoalCount = 0;
            for (int i = 0; i < goals; i++)
            {
                var g = new JournalGoalRecord
                {
                    Goal = r.ReadUInt16(),
                    Slot = r.ReadByte(),
                    Target = Math.Max(1, r.ReadInt32()),
                    Progress = Math.Max(0, r.ReadInt32()),
                    Claimed = r.ReadBool(),
                };
                if (d.GoalCount < DailyVisitState.MaxJournalGoals)
                    d.Goals[d.GoalCount++] = g;
            }
            d.JournalCompleteClaimed = r.ReadBool();
            int previous = r.ReadByte();
            d.PreviousGoalCount = 0;
            for (int i = 0; i < previous; i++)
            {
                ushort goal = r.ReadUInt16();
                if (d.PreviousGoalCount < DailyVisitState.MaxJournalGoals)
                    d.PreviousGoals[d.PreviousGoalCount++] = goal;
            }
            d.CarryGems = Math.Max(0, r.ReadInt32());
            d.CarryTickets = Math.Max(0, r.ReadInt32());
            d.CarryDiscovery = Math.Max(0, r.ReadInt32());

            d.LastReturnGiftDay = r.ReadInt32();
            d.LastBigReturnGiftDay = r.ReadInt32();
            d.PendingReturnGift = (byte)Math.Min(2, (int)r.ReadByte());
            d.PendingReturnDays = Math.Max(0, r.ReadInt32());

            d.TouchDayKey = r.ReadInt32();
            d.TouchesToday = Math.Max(0, r.ReadInt32());
            int touches = ReadCount(ref r);
            d.TouchesByAnimal.Clear();
            for (int i = 0; i < touches; i++)
            {
                uint animal = r.ReadUInt32();
                d.TouchesByAnimal[animal] = r.ReadByte();
            }
        }

        private static void WriteInventory(ByteBufferWriter output, InventoryState inventory)
        {
            output.WriteInt32(inventory.Tickets);
            output.WriteInt32(inventory.PendingStickers);
            WriteCounters(output, inventory.Items);
        }

        private static void ReadInventory(SaveSection section, InventoryState inventory)
        {
            RequireVersion(section, InventoryVersion);
            var r = section.CreateReader();
            inventory.Tickets = Math.Max(0, r.ReadInt32());
            inventory.PendingStickers = Math.Max(0, r.ReadInt32());
            ReadCounters(ref r, inventory.Items);
        }

        /// <summary>
        /// T37: the activities. The 18 activity records are written with their count (the test activity's slot never is),
        /// then the stars and attempts of their 10 challenges.
        /// </summary>
        private static void WriteActivities(ByteBufferWriter output, ActivityState a)
        {
            output.WriteBool(a.TicketsStarted);
            output.WriteInt32(a.FreeTickets);
            output.WriteInt32(a.PaidTickets);
            output.WriteInt64(a.RefillAnchorUnixMs);
            output.WriteInt32(a.PackDayKey);
            output.WriteInt32(a.PacksToday);
            output.WriteInt32(a.GemsDayKey);
            output.WriteInt32(a.GemsToday);
            output.WriteInt32(a.ChallengesThreeStars);
            output.WriteInt32(a.ChallengeDecorations);
            output.WriteInt32(a.GamesStarted);
            const int count = ActivityCatalog.ActivityCount;
            const int challenges = ActivityState.ChallengesPerActivity;
            output.WriteInt32(count);
            for (int i = 0; i < count; i++)
            {
                ref var record = ref a.Records[i];
                output.WriteBool(record.Unlocked);
                output.WriteInt32(record.Best);
                output.WriteByte(record.BestStars);
                output.WriteByte(record.Difficulty);
                output.WriteByte(record.ZeroStarStreak);
                output.WriteByte(record.ThreeStarStreak);
                output.WriteInt32(record.GamesPlayed);
                output.WriteInt32(record.RewardedGames);
                output.WriteBool(record.FreeGameUsed);
                output.WriteInt32(record.ThreeStarGemsDayKey);
                output.WriteContentId(record.LastTheme);
                output.WriteInt32(record.PaidStickerPoints);
            }
            output.WriteInt32(challenges);
            for (int i = 0; i < count * challenges; i++)
                output.WriteByte(a.ChallengeStars[i]);
            for (int i = 0; i < count * challenges; i++)
                output.WriteUInt16(a.ChallengeAttempts[i]);
        }

        private static void ReadActivities(SaveSection section, ActivityState a)
        {
            RequireVersion(section, ActivitiesVersion);
            var r = section.CreateReader();
            a.TicketsStarted = r.ReadBool();
            a.FreeTickets = Math.Max(0, r.ReadInt32());
            a.PaidTickets = Math.Max(0, r.ReadInt32());
            a.RefillAnchorUnixMs = r.ReadInt64();
            a.PackDayKey = r.ReadInt32();
            a.PacksToday = Math.Max(0, r.ReadInt32());
            a.GemsDayKey = r.ReadInt32();
            a.GemsToday = Math.Max(0, r.ReadInt32());
            a.ChallengesThreeStars = Math.Max(0, r.ReadInt32());
            a.ChallengeDecorations = Math.Max(0, r.ReadInt32());
            a.GamesStarted = Math.Max(0, r.ReadInt32());
            int count = ReadCount(ref r);
            for (int i = 0; i < count; i++)
            {
                var record = new ActivityRecord
                {
                    Unlocked = r.ReadBool(),
                    Best = Math.Max(0, r.ReadInt32()),
                    BestStars = (byte)Math.Min(3, (int)r.ReadByte()),
                    Difficulty = (byte)Math.Min(10, (int)r.ReadByte()),
                    ZeroStarStreak = r.ReadByte(),
                    ThreeStarStreak = r.ReadByte(),
                    GamesPlayed = Math.Max(0, r.ReadInt32()),
                    RewardedGames = Math.Max(0, r.ReadInt32()),
                    FreeGameUsed = r.ReadBool(),
                    ThreeStarGemsDayKey = r.ReadInt32(),
                    LastTheme = r.ReadContentId(),
                    PaidStickerPoints = Math.Max(0, r.ReadInt32()),
                };
                // A newer build may know more activities: their records are read and dropped.
                if (i < ActivityCatalog.ActivityCount)
                    a.Records[i] = record;
            }
            int challenges = ReadCount(ref r);
            int cells = count * challenges;
            for (int i = 0; i < cells; i++)
            {
                byte stars = (byte)Math.Min(3, (int)r.ReadByte());
                int slot = i / Math.Max(1, challenges);
                int challenge = i % Math.Max(1, challenges);
                if (slot < ActivityCatalog.ActivityCount && challenge < ActivityState.ChallengesPerActivity)
                    a.ChallengeStars[ActivityState.ChallengeIndex(slot, challenge)] = stars;
            }
            for (int i = 0; i < cells; i++)
            {
                ushort attempts = r.ReadUInt16();
                int slot = i / Math.Max(1, challenges);
                int challenge = i % Math.Max(1, challenges);
                if (slot < ActivityCatalog.ActivityCount && challenge < ActivityState.ChallengesPerActivity)
                    a.ChallengeAttempts[ActivityState.ChallengeIndex(slot, challenge)] = attempts;
            }
        }

        /// <summary>
        /// T42: the expeditions. The slots are written with their count (each one's active flag, then its team, times, stars,
        /// challenge, seed and the loot drawn at departure), then the postcards kept, oldest first.
        /// </summary>
        private static void WriteExpeditions(ByteBufferWriter output, ExpeditionState e)
        {
            output.WriteBool(e.SlotBought);
            output.WriteBool(e.FirstStrollDone);
            output.WriteInt32(e.LongWithoutEgg);
            output.WriteInt32(e.Started);
            output.WriteInt32(e.Completed);
            output.WriteInt32(e.SpeedUpDayKey);
            output.WriteInt32(e.SpeedUpsToday);
            output.WriteInt32(e.NextPostcardId);
            output.WriteInt32(e.JournalWeekKey);
            output.WriteInt64((long)e.JournalPlacesMask);
            output.WriteInt32(e.Slots.Length);
            for (int s = 0; s < e.Slots.Length; s++)
            {
                ref readonly var slot = ref e.Slots[s];
                output.WriteBool(slot.Active);
                if (!slot.Active)
                    continue;
                output.WriteContentId(slot.Destination);
                output.WriteByte((byte)slot.Duration);
                output.WriteByte(slot.TeamSize);
                for (int i = 0; i < ExpeditionState.MaxTeam; i++)
                {
                    output.WriteUInt32(slot.AnimalAt(i));
                    output.WriteContentId(slot.VariantAt(i));
                    output.WriteContentId(slot.SpeciesAt(i));
                }
                output.WriteInt64(slot.StartUnixMs);
                output.WriteInt64(slot.ReturnUnixMs);
                output.WriteByte(slot.Stars);
                output.WriteByte(unchecked((byte)slot.Challenge));
                output.WriteBool(slot.FirstStroll);
                output.WriteUInt32(slot.Seed);
                WriteLoot(output, in slot.Loot);
            }
            int cards = Math.Min(e.PostcardCount, e.Postcards.Length);
            output.WriteInt32(cards);
            for (int i = 0; i < cards; i++)
            {
                ref readonly var card = ref e.Postcards[i];
                output.WriteInt32(card.Id);
                output.WriteContentId(card.Destination);
                output.WriteInt32(card.UnixDay);
                output.WriteBool(card.Rendered);
                output.WriteBool(card.FirstOfDestination);
            }
        }

        private static void WriteLoot(ByteBufferWriter output, in ExpeditionLoot loot)
        {
            output.WriteDouble(loot.LightMinutes);
            output.WriteContentId(loot.Treat);
            output.WriteInt32(loot.Treats);
            output.WriteInt32(loot.SeedKinds);
            for (int i = 0; i < 4; i++)
            {
                output.WriteContentId(loot.SeedAt(i));
                output.WriteInt32(loot.SeedsAt(i));
            }
            output.WriteInt32(loot.StickerChance);
            output.WriteInt32(loot.Stickers);
            output.WriteInt32(loot.DiscoveryChance);
            output.WriteInt32(loot.DiscoveryEggs);
            output.WriteInt32(loot.Tickets);
            output.WriteContentId(loot.Ingredient0);
            output.WriteInt32(loot.Ingredients0);
            output.WriteContentId(loot.Ingredient1);
            output.WriteInt32(loot.Ingredients1);
            output.WriteInt32(loot.BondPoints);
            output.WriteInt32(loot.MasteryXp);
            output.WriteInt32(loot.EventCurrency);
        }

        private static void ReadExpeditions(SaveSection section, ExpeditionState e)
        {
            RequireVersion(section, ExpeditionsVersion);
            var r = section.CreateReader();
            e.SlotBought = r.ReadBool();
            e.FirstStrollDone = r.ReadBool();
            e.LongWithoutEgg = Math.Max(0, r.ReadInt32());
            e.Started = Math.Max(0, r.ReadInt32());
            e.Completed = Math.Max(0, r.ReadInt32());
            e.SpeedUpDayKey = r.ReadInt32();
            e.SpeedUpsToday = Math.Max(0, r.ReadInt32());
            e.NextPostcardId = Math.Max(1, r.ReadInt32());
            e.JournalWeekKey = r.ReadInt32();
            e.JournalPlacesMask = (ulong)r.ReadInt64();
            int slots = ReadCount(ref r);
            for (int s = 0; s < slots; s++)
            {
                var slot = new ExpeditionSlotRecord { Active = r.ReadBool(), Challenge = -1 };
                if (slot.Active)
                {
                    slot.Destination = r.ReadContentId();
                    slot.Duration = (ExpeditionDuration)Math.Min((int)ExpeditionDuration.TwelveHours, (int)r.ReadByte());
                    slot.TeamSize = (byte)Math.Min(ExpeditionState.MaxTeam, (int)r.ReadByte());
                    for (int i = 0; i < ExpeditionState.MaxTeam; i++)
                    {
                        uint animal = r.ReadUInt32();
                        var variant = r.ReadContentId();
                        var species = r.ReadContentId();
                        slot.SetMember(i, animal, variant, species);
                    }
                    slot.StartUnixMs = r.ReadInt64();
                    slot.ReturnUnixMs = Math.Max(slot.StartUnixMs, r.ReadInt64());
                    slot.Stars = (byte)Math.Min(3, (int)r.ReadByte());
                    slot.Challenge = unchecked((sbyte)r.ReadByte());
                    slot.FirstStroll = r.ReadBool();
                    slot.Seed = r.ReadUInt32();
                    slot.Loot = ReadLoot(ref r);
                }
                // A newer build may own more slots: their teams are read and dropped.
                if (s < e.Slots.Length)
                    e.Slots[s] = slot;
            }
            int cards = ReadCount(ref r);
            e.PostcardCount = 0;
            for (int i = 0; i < cards; i++)
            {
                var card = new PostcardRecord
                {
                    Id = r.ReadInt32(),
                    Destination = r.ReadContentId(),
                    UnixDay = r.ReadInt32(),
                    Rendered = r.ReadBool(),
                    FirstOfDestination = r.ReadBool(),
                };
                if (e.PostcardCount < e.Postcards.Length)
                    e.Postcards[e.PostcardCount++] = card;
            }
        }

        private static ExpeditionLoot ReadLoot(ref ByteBufferReader r)
        {
            var loot = new ExpeditionLoot
            {
                LightMinutes = Math.Max(0.0, r.ReadDouble()),
                Treat = r.ReadContentId(),
                Treats = Math.Max(0, r.ReadInt32()),
                SeedKinds = Math.Max(0, Math.Min(4, r.ReadInt32())),
            };
            loot.Seed0 = r.ReadContentId();
            loot.Seeds0 = Math.Max(0, r.ReadInt32());
            loot.Seed1 = r.ReadContentId();
            loot.Seeds1 = Math.Max(0, r.ReadInt32());
            loot.Seed2 = r.ReadContentId();
            loot.Seeds2 = Math.Max(0, r.ReadInt32());
            loot.Seed3 = r.ReadContentId();
            loot.Seeds3 = Math.Max(0, r.ReadInt32());
            loot.StickerChance = Math.Max(0, r.ReadInt32());
            loot.Stickers = Math.Max(0, r.ReadInt32());
            loot.DiscoveryChance = Math.Max(0, r.ReadInt32());
            loot.DiscoveryEggs = Math.Max(0, r.ReadInt32());
            loot.Tickets = Math.Max(0, r.ReadInt32());
            loot.Ingredient0 = r.ReadContentId();
            loot.Ingredients0 = Math.Max(0, r.ReadInt32());
            loot.Ingredient1 = r.ReadContentId();
            loot.Ingredients1 = Math.Max(0, r.ReadInt32());
            loot.BondPoints = Math.Max(0, r.ReadInt32());
            loot.MasteryXp = Math.Max(0, r.ReadInt32());
            loot.EventCurrency = Math.Max(0, r.ReadInt32());
            return loot;
        }

        /// <summary>
        /// T43: the garden. The counters, the challenge run, the plots with their count (each one's active flag, then its plant,
        /// times, growth, watering, seed, the draws made at planting and the neighbours' help), the dyes with their count and
        /// the palettes bought.
        /// </summary>
        private static void WriteGarden(ByteBufferWriter output, GardenState g)
        {
            output.WriteBool(g.PlotBought);
            output.WriteBool(g.FirstVisitDone);
            output.WriteInt32(g.Points);
            output.WriteInt32(g.Planted);
            output.WriteInt32(g.Harvested);
            output.WriteInt32(g.SpeedUpDayKey);
            output.WriteInt32(g.SpeedUpsToday);
            output.WriteByte(unchecked((byte)g.Challenge));
            output.WriteInt32(g.ChallengeHarvests);
            output.WriteInt32(g.ChallengeStars);
            output.WriteInt64(g.ChallengeRunStartUnixMs);
            output.WriteUInt32(g.ChallengeMask);
            output.WriteInt32(g.Plots.Length);
            for (int p = 0; p < g.Plots.Length; p++)
            {
                ref readonly var plot = ref g.Plots[p];
                output.WriteBool(plot.Active);
                if (!plot.Active)
                    continue;
                output.WriteContentId(plot.Plant);
                output.WriteInt64(plot.PlantedUnixMs);
                output.WriteInt64(plot.RipeUnixMs);
                output.WriteInt64(plot.GrowMs);
                output.WriteBool(plot.Watered);
                output.WriteUInt32(plot.Seed);
                output.WriteByte(plot.SeedsBack);
                output.WriteBool(plot.Happy);
                output.WriteInt64(plot.HelpLeftMs);
                output.WriteInt64(plot.HelpRightMs);
                output.WriteBool(plot.FlowerBeside);
            }
            output.WriteInt32(g.Dyes.Length);
            for (int i = 0; i < g.Dyes.Length; i++)
                output.WriteInt32(g.Dyes[i]);
            output.WriteUInt32(g.Palettes);
        }

        private static void ReadGarden(SaveSection section, GardenState g)
        {
            RequireVersion(section, GardenVersion);
            var r = section.CreateReader();
            g.PlotBought = r.ReadBool();
            g.FirstVisitDone = r.ReadBool();
            g.Points = Math.Max(0, r.ReadInt32());
            g.Planted = Math.Max(0, r.ReadInt32());
            g.Harvested = Math.Max(0, r.ReadInt32());
            g.SpeedUpDayKey = r.ReadInt32();
            g.SpeedUpsToday = Math.Max(0, r.ReadInt32());
            int challenge = unchecked((sbyte)r.ReadByte());
            g.Challenge = (sbyte)(challenge >= 0 && challenge < GardenCatalog.ChallengeCount ? challenge : -1);
            g.ChallengeHarvests = Math.Max(0, r.ReadInt32());
            g.ChallengeStars = Math.Max(0, r.ReadInt32());
            g.ChallengeRunStartUnixMs = Math.Max(0L, r.ReadInt64());
            g.ChallengeMask = r.ReadUInt32();
            if (g.Challenge < 0)
                g.ResetChallengeRun();
            int plots = ReadCount(ref r);
            for (int p = 0; p < plots; p++)
            {
                var plot = new GardenPlotRecord { Active = r.ReadBool() };
                if (plot.Active)
                {
                    plot.Plant = r.ReadContentId();
                    plot.PlantedUnixMs = r.ReadInt64();
                    plot.RipeUnixMs = Math.Max(plot.PlantedUnixMs, r.ReadInt64());
                    plot.GrowMs = Math.Max(0L, r.ReadInt64());
                    plot.Watered = r.ReadBool();
                    plot.Seed = r.ReadUInt32();
                    plot.SeedsBack = (byte)Math.Min(1, (int)r.ReadByte());
                    plot.Happy = r.ReadBool();
                    plot.HelpLeftMs = Math.Max(0L, r.ReadInt64());
                    plot.HelpRightMs = Math.Max(0L, r.ReadInt64());
                    plot.FlowerBeside = r.ReadBool();
                }
                // A newer build may show more plots: their sprouts are read and dropped.
                if (p < g.Plots.Length)
                    g.Plots[p] = plot;
            }
            int dyes = ReadCount(ref r);
            for (int i = 0; i < dyes; i++)
            {
                int owned = Math.Max(0, r.ReadInt32());
                if (i < g.Dyes.Length)
                    g.Dyes[i] = owned;
            }
            g.Palettes = r.ReadUInt32() & ((1u << GardenCatalog.PaletteCount) - 1u);
        }

        /// <summary>T44A (doc 01 Albums, section 1.14): bit sets and counters at fixed sizes, each preceded by its length.</summary>
        private static void WriteAlbums(ByteBufferWriter output, AlbumState a)
        {
            output.WriteBool(a.Opened);
            WriteBytes(output, a.Owned);
            WriteBytes(output, a.Unseen);
            output.WriteInt32(a.Stars);
            output.WriteByte(a.DuplicateStreak);
            WriteBytes(output, a.ShinyDraws);
            WriteBytes(output, a.PaidPoints);
            output.WriteInt32(a.SignatureSeen);
            WriteBytes(output, a.PageRewards);
            output.WriteInt32(a.AlbumRewards);
            output.WriteInt64(unchecked((long)a.Seed));
            output.WriteInt64(unchecked((long)a.PagesOpen));
            output.WriteInt32(a.WishDayKey);
            output.WriteByte(a.WishesToday);
            WriteBytes(output, a.Records);
            output.WriteInt32(a.LongExpeditions);
            output.WriteInt32(a.ExpeditionBiomes);
            output.WriteUInt32(a.PlantsHarvested);
            output.WriteInt32(a.DyesObtained);
            output.WriteByte(a.GardenSpedUp);
        }

        private static void ReadAlbums(SaveSection section, AlbumState a)
        {
            RequireVersion(section, AlbumsVersion);
            var r = section.CreateReader();
            a.Opened = r.ReadBool();
            ReadBytes(ref r, a.Owned);
            ReadBytes(ref r, a.Unseen);
            a.Stars = Math.Max(0, r.ReadInt32());
            a.DuplicateStreak = r.ReadByte();
            ReadBytes(ref r, a.ShinyDraws);
            ReadBytes(ref r, a.PaidPoints);
            a.SignatureSeen = (ushort)(r.ReadInt32() & ((1 << AlbumState.Biomes) - 1));
            ReadBytes(ref r, a.PageRewards);
            a.AlbumRewards = (ushort)(r.ReadInt32() & 0xFFFF);
            a.Seed = unchecked((ulong)r.ReadInt64());
            a.PagesOpen = unchecked((ulong)r.ReadInt64()) & ((1UL << AlbumState.Pages) - 1UL);
            a.WishDayKey = r.ReadInt32();
            a.WishesToday = r.ReadByte();
            ReadBytes(ref r, a.Records);
            a.LongExpeditions = Math.Max(0, r.ReadInt32());
            a.ExpeditionBiomes = (ushort)(r.ReadInt32() & ((1 << AlbumState.Biomes) - 1));
            a.PlantsHarvested = r.ReadUInt32();
            a.DyesObtained = Math.Max(0, r.ReadInt32());
            a.GardenSpedUp = r.ReadByte();
            // Paid counters stay under 100 (a sticker is given at 100).
            for (int i = 0; i < a.PaidPoints.Length; i++)
                if (a.PaidPoints[i] >= 100)
                    a.PaidPoints[i] = 99;
        }

        /// <summary>
        /// T44B (doc 01 Succès): progress by achievement index, tiers reached and claimed, the run's and the day's counters,
        /// the distinct sets and lists, the profile items owned, unseen and shown. Every array is preceded by its length.
        /// </summary>
        private static void WriteAchievements(ByteBufferWriter output, AchievementState a)
        {
            output.WriteInt32(a.Progress.Length);
            for (int i = 0; i < a.Progress.Length; i++)
                output.WriteInt64(a.Progress[i]);
            WriteBytes(output, a.Reached);
            WriteBytes(output, a.Claimed);
            output.WriteInt64(a.RunHatches);
            output.WriteInt64(a.RunLightLevels);
            output.WriteBool(a.RunProductionBeaten);
            output.WriteInt64(a.BestRenewalEssence);
            output.WriteInt32(a.DayKey);
            output.WriteInt32(a.DayHatches);
            output.WriteInt32(a.DayWishes);
            output.WriteInt32(a.SeasonKey);
            output.WriteInt32(a.JournalWeek);
            output.WriteInt32(a.JournalGoalsCounted);
            output.WriteBool(a.JournalCompleteCounted);
            output.WriteUInt32(a.HatchBiomes);
            output.WriteUInt32(a.HatchRarities);
            output.WriteUInt32(a.ActivitiesStarred);
            output.WriteUInt32(a.Destinations);
            output.WriteUInt32(a.PlantsHarvested);
            output.WriteInt64(unchecked((long)a.PosesUsed));
            WriteBytes(output, a.SpeciesTraits);
            WriteIds(output, a.AnimalsPlayed, a.AnimalsPlayedCount);
            WriteIds(output, a.AnimalsWished, a.AnimalsWishedCount);
            WriteIds(output, a.AnimalsPetted, a.AnimalsPettedCount);
            WriteIds(output, a.Accessories, a.AccessoriesCount);
            output.WriteBool(a.StartGiven);
            WriteBytes(output, a.TitlesOwned);
            WriteBytes(output, a.TitlesUnseen);
            output.WriteInt64(unchecked((long)a.FramesOwned));
            output.WriteInt64(unchecked((long)a.FramesUnseen));
            output.WriteInt64(unchecked((long)a.PosesOwned));
            output.WriteInt64(unchecked((long)a.PosesUnseen));
            output.WriteInt32(a.SelectedTitle);
            output.WriteInt32(a.SelectedFrame);
        }

        private static void ReadAchievements(SaveSection section, AchievementState a)
        {
            RequireVersion(section, AchievementsVersion);
            var r = section.CreateReader();
            int count = ReadCount(ref r);
            for (int i = 0; i < count; i++)
            {
                long value = r.ReadInt64();
                // A newer build may hold more achievements: their progress is read and dropped.
                if (i < a.Progress.Length)
                    a.Progress[i] = Math.Max(0L, value);
            }
            ReadBytes(ref r, a.Reached);
            ReadBytes(ref r, a.Claimed);
            for (int i = 0; i < a.Reached.Length; i++)
            {
                if (a.Reached[i] > (byte)AchievementTier.Star)
                    a.Reached[i] = (byte)AchievementTier.Star;
                if (a.Claimed[i] > a.Reached[i])
                    a.Claimed[i] = a.Reached[i];
            }
            a.RunHatches = Math.Max(0L, r.ReadInt64());
            a.RunLightLevels = Math.Max(0L, r.ReadInt64());
            a.RunProductionBeaten = r.ReadBool();
            a.BestRenewalEssence = Math.Max(0L, r.ReadInt64());
            a.DayKey = r.ReadInt32();
            a.DayHatches = Math.Max(0, r.ReadInt32());
            a.DayWishes = Math.Max(0, r.ReadInt32());
            a.SeasonKey = r.ReadInt32();
            a.JournalWeek = r.ReadInt32();
            a.JournalGoalsCounted = Math.Max(0, r.ReadInt32());
            a.JournalCompleteCounted = r.ReadBool();
            a.HatchBiomes = r.ReadUInt32() & ((1u << AlbumState.Biomes) - 1u);
            a.HatchRarities = r.ReadUInt32() & 0x3Fu;
            a.ActivitiesStarred = r.ReadUInt32();
            a.Destinations = r.ReadUInt32();
            a.PlantsHarvested = r.ReadUInt32();
            a.PosesUsed = unchecked((ulong)r.ReadInt64()) & ((1UL << AchievementState.Poses) - 1UL);
            ReadBytes(ref r, a.SpeciesTraits);
            for (int i = 0; i < a.SpeciesTraits.Length; i++)
                a.SpeciesTraits[i] &= 0x3F;
            a.AnimalsPlayedCount = ReadIds(ref r, a.AnimalsPlayed);
            a.AnimalsWishedCount = ReadIds(ref r, a.AnimalsWished);
            a.AnimalsPettedCount = ReadIds(ref r, a.AnimalsPetted);
            a.AccessoriesCount = ReadIds(ref r, a.Accessories);
            a.StartGiven = r.ReadBool();
            ReadBytes(ref r, a.TitlesOwned);
            ReadBytes(ref r, a.TitlesUnseen);
            // The last byte holds bits past the 322 titles: cleared.
            int tail = AchievementState.Titles & 7;
            if (tail != 0)
            {
                byte keep = (byte)((1 << tail) - 1);
                a.TitlesOwned[AchievementState.TitleBytes - 1] &= keep;
                a.TitlesUnseen[AchievementState.TitleBytes - 1] &= keep;
            }
            const ulong frames = (1UL << AchievementState.Frames) - 1UL;
            const ulong poses = (1UL << AchievementState.Poses) - 1UL;
            a.FramesOwned = unchecked((ulong)r.ReadInt64()) & frames;
            a.FramesUnseen = unchecked((ulong)r.ReadInt64()) & frames;
            a.PosesOwned = unchecked((ulong)r.ReadInt64()) & poses;
            a.PosesUnseen = unchecked((ulong)r.ReadInt64()) & poses;
            int title = r.ReadInt32();
            int frame = r.ReadInt32();
            // A shown item must be owned (−1: none).
            a.SelectedTitle = (short)(title >= 0 && title < AchievementState.Titles && AchievementState.Bit(a.TitlesOwned, title) ? title : -1);
            a.SelectedFrame = (short)(frame >= 0 && frame < AchievementState.Frames && (a.FramesOwned & (1UL << frame)) != 0 ? frame : -1);
        }

        /// <summary>
        /// T44C (doc 01 Succès, section 6; doc Rétention): the journal's pages (id, moment, local day, image reference,
        /// subject, capture flags, the variables' values), the first times written, the per-subject moments, the unseen count,
        /// the garden plants and best Renewal, the surprises seen; then the visitors' contacts, kept ones, today's greetings
        /// and the guaranteed first visit. The schedule itself is never saved (a function of the day).
        /// </summary>
        private static void WriteJournal(ByteBufferWriter output, JournalState j, VisitorState v)
        {
            output.WriteBool(j.Initialized);
            output.WriteInt32(j.NextId);
            output.WriteInt32(j.PageCount);
            for (int i = 0; i < j.PageCount; i++)
            {
                ref readonly var p = ref j.Pages[i];
                output.WriteInt32(p.Id);
                output.WriteInt32(p.Moment);
                output.WriteInt32(p.Day);
                output.WriteUInt32(p.ImageRef);
                output.WriteUInt32(p.Subject);
                output.WriteByte(p.Flags);
                output.WriteString(p.Value0);
                output.WriteString(p.Value1);
            }
            output.WriteInt64(unchecked((long)j.FirstDone));
            output.WriteInt32(j.SubjectCount);
            for (int i = 0; i < j.SubjectCount; i++)
                output.WriteInt64(unchecked((long)j.Subjects[i]));
            output.WriteInt32(j.Unseen);
            output.WriteUInt32(j.GardenPlants);
            output.WriteInt64(j.BestRenewalEssence);
            output.WriteInt32(j.SurpriseCount);
            for (int i = 0; i < j.SurpriseCount; i++)
                output.WriteString(j.Surprises[i]);

            WriteBytes(output, v.Contacts);
            output.WriteUInt16(v.JoinedMask);
            output.WriteInt32(v.GreetedDay);
            output.WriteByte(v.GreetedMask);
            output.WriteInt32(v.OpenedDay);
            output.WriteInt64(v.FirstVisitUnixMs);
            output.WriteByte(v.FirstVisitor);
            output.WriteInt32(v.Greetings);
        }

        private static void ReadJournal(SaveSection section, JournalState j, VisitorState v)
        {
            RequireVersion(section, JournalVersion);
            var r = section.CreateReader();
            j.Clear();
            j.Initialized = r.ReadBool();
            j.NextId = Math.Max(1, r.ReadInt32());
            int pages = ReadCount(ref r);
            int lastId = 0;
            for (int i = 0; i < pages; i++)
            {
                int id = r.ReadInt32();
                int moment = r.ReadInt32();
                int day = r.ReadInt32();
                uint imageRef = r.ReadUInt32();
                uint subject = r.ReadUInt32();
                byte flags = r.ReadByte();
                string? value0 = r.ReadString();
                string? value1 = r.ReadString();
                // Ids rise with the pages (the screen and the captures find them by a binary search); unknown moments are dropped.
                if (id <= lastId || moment < 0 || moment >= JournalData.Count)
                    continue;
                lastId = id;
                ref var p = ref j.Append();
                p.Id = id;
                p.Moment = (short)moment;
                p.Day = day;
                p.ImageRef = imageRef;
                p.Subject = subject;
                p.Flags = (byte)(flags & (JournalEntry.FlagCaptured | JournalEntry.FlagPending | JournalEntry.FlagThumbnail));
                p.Value0 = value0;
                p.Value1 = value1;
            }
            if (j.NextId <= lastId)
                j.NextId = lastId + 1;
            j.FirstDone = unchecked((ulong)r.ReadInt64());
            int subjects = ReadCount(ref r);
            for (int i = 0; i < subjects; i++)
            {
                ulong key = unchecked((ulong)r.ReadInt64());
                j.AddSubject((int)(key >> 32), (uint)key);
            }
            j.Unseen = Math.Min(Math.Max(0, r.ReadInt32()), j.PageCount);
            j.GardenPlants = r.ReadUInt32();
            j.BestRenewalEssence = Math.Max(0L, r.ReadInt64());
            int surprises = ReadCount(ref r);
            for (int i = 0; i < surprises; i++)
            {
                var key = r.ReadString();
                if (!string.IsNullOrEmpty(key))
                    j.AddSurprise(key!);
            }

            v.Clear();
            ReadBytes(ref r, v.Contacts);
            v.JoinedMask = (ushort)(r.ReadUInt16() & ((1 << VisitorState.Visitors) - 1));
            v.GreetedDay = r.ReadInt32();
            v.GreetedMask = r.ReadByte();
            v.OpenedDay = Math.Max(0, r.ReadInt32());
            v.FirstVisitUnixMs = Math.Max(0L, r.ReadInt64());
            v.FirstVisitor = r.ReadByte();
            if (v.FirstVisitor >= VisitorState.Visitors)
                v.FirstVisitor = 0;
            v.Greetings = Math.Max(0, r.ReadInt32());
        }

        private static void WriteIds(ByteBufferWriter output, uint[] ids, int count)
        {
            if (count > ids.Length)
                count = ids.Length;
            output.WriteInt32(count);
            for (int i = 0; i < count; i++)
                output.WriteUInt32(ids[i]);
        }

        /// <summary>A length-prefixed id list into a fixed array (a longer one is read and its tail dropped); the ids kept.</summary>
        private static int ReadIds(ref ByteBufferReader r, uint[] into)
        {
            int count = ReadCount(ref r);
            int kept = 0;
            for (int i = 0; i < count; i++)
            {
                uint id = r.ReadUInt32();
                if (id != 0 && kept < into.Length)
                    into[kept++] = id;
            }
            return kept;
        }

        private static void WriteBytes(ByteBufferWriter output, byte[] bytes)
        {
            output.WriteInt32(bytes.Length);
            for (int i = 0; i < bytes.Length; i++)
                output.WriteByte(bytes[i]);
        }

        /// <summary>A length-prefixed byte run into a fixed array: a longer run (a newer build's) is read and its tail dropped.</summary>
        private static void ReadBytes(ref ByteBufferReader r, byte[] into)
        {
            int count = ReadCount(ref r);
            for (int i = 0; i < count; i++)
            {
                byte b = r.ReadByte();
                if (i < into.Length)
                    into[i] = b;
            }
        }

        private static void ReadCounters(ref ByteBufferReader r, System.Collections.Generic.Dictionary<ContentId, int> counters)
        {
            int count = ReadCount(ref r);
            for (int i = 0; i < count; i++)
            {
                var id = r.ReadContentId();
                counters[id] = r.ReadInt32();
            }
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

        private static void ReadShop(SaveSection section, ShopState shop)
        {
            RequireVersion(section, ShopVersion);
            var r = section.CreateReader();
            shop.PaidPurchases = r.ReadInt32();
            int offers = ReadCount(ref r);
            for (int i = 0; i < offers; i++)
            {
                var id = r.ReadContentId();
                shop.Offers[id] = new OfferRecord { Purchases = r.ReadInt32(), Impressions = r.ReadInt32(), LastShownUnix = r.ReadInt64() };
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
                var biome = new BiomeProgress
                {
                    Biome = r.ReadContentId(),
                    Unlocked = r.ReadBool(),
                    Expansions = r.ReadInt32(),
                };
                if (section.Version >= 2)
                {
                    biome.ZonesOpened = r.ReadUInt32();
                    biome.MasteryXp = r.ReadInt64();
                    biome.MasteryLevel = r.ReadInt32();
                    biome.ProductionLevel = r.ReadInt32();
                    biome.NestsLevel = r.ReadInt32();
                    biome.CuriosityLevel = r.ReadInt32();
                }
                else
                {
                    // World 1 only counted zones bought: the arrival zone (open with the biome) and the first ones.
                    biome.Expansions = Math.Max(0, Math.Min(biome.Expansions, 30));
                    biome.ZonesOpened = biome.Unlocked ? (2u << biome.Expansions) - 1u : 0u;
                }
                world.Biomes.Add(biome);
            }

            int eggCount = ReadCount(ref r);
            for (int i = 0; i < eggCount; i++)
            {
                var egg = r.ReadContentId();
                world.EggPurchasesInCycle[egg] = r.ReadInt32();
            }

            world.RenewalCount = r.ReadInt32();
            if (section.Version >= 3)
                world.DiscoveryEggs = Math.Max(0, r.ReadInt32());
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
