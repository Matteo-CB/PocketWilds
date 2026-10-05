#nullable enable

namespace PocketWilds.Services.Save
{
    /// <summary>Every shipped schema step, in order. The bootstrap and the tests build their pipeline here.</summary>
    public static class SaveMigrations
    {
        public static ISaveMigration[] All() => new ISaveMigration[]
        {
            new Migration0001AddOffline(),
            new Migration0002AddCodex(),
            new Migration0003AddPity(),
            new Migration0004AddShop(),
        };

        public static SaveMigrationPipeline CreatePipeline() => new SaveMigrationPipeline(SaveSchema.CurrentVersion, All());
    }

    /// <summary>
    /// Schema 1 to 2: adds the Offline section. Nothing to convert: a schema 1 file has no section, which
    /// loads as "rate unknown" (rebuilt from the collection) and no time high-water mark.
    /// </summary>
    public sealed class Migration0001AddOffline : ISaveMigration
    {
        public int FromVersion => 1;
        public int ToVersion => 2;

        public void Apply(SaveDocument document)
        {
        }
    }

    /// <summary>
    /// Schema 2 to 3: adds the Codex section. A file without it loads with every discovery marked as
    /// seen (BinarySaveSerializer), so old profiles do not open a Codex full of "New" badges.
    /// </summary>
    public sealed class Migration0002AddCodex : ISaveMigration
    {
        public int FromVersion => 2;
        public int ToVersion => 3;

        public void Apply(SaveDocument document)
        {
        }
    }

    /// <summary>
    /// Schema 3 to 4: adds the Pity section. A file without it starts every counter at zero: older
    /// profiles simply begin building their protection from now on.
    /// </summary>
    public sealed class Migration0003AddPity : ISaveMigration
    {
        public int FromVersion => 3;
        public int ToVersion => 4;

        public void Apply(SaveDocument document)
        {
        }
    }

    /// <summary>
    /// Schema 4 to 5: adds the Shop section (offer purchases, impressions, paid purchase count). A file
    /// without it starts with no shop history; Gems and owned items were already in Wallet and Entitlements.
    /// </summary>
    public sealed class Migration0004AddShop : ISaveMigration
    {
        public int FromVersion => 4;
        public int ToVersion => 5;

        public void Apply(SaveDocument document)
        {
        }
    }
}
