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
            new Migration0005AddAnalytics(),
            new Migration0006AddProgression(),
            new Migration0007AddDiscoveryEggs(),
            new Migration0008AddRenewal(),
            new Migration0009AddDailyVisit(),
            new Migration0010AddActivities(),
            new Migration0011AddExpeditions(),
            new Migration0012AddGarden(),
            new Migration0013AddAlbums(),
            new Migration0014AddAchievements(),
            new Migration0015AddJournal(),
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

    /// <summary>
    /// Schema 5 to 6: adds the Analytics section (funnel milestones, session count). A file without it infers the
    /// milestones from the profile on load (BinarySaveSerializer): nothing to convert here.
    /// </summary>
    public sealed class Migration0005AddAnalytics : ISaveMigration
    {
        public int FromVersion => 5;
        public int ToVersion => 6;

        public void Apply(SaveDocument document)
        {
        }
    }

    /// <summary>
    /// Schema 6 to 7: zones, biome mastery and Light upgrades per biome (World section 2), and the Progression section
    /// (refuge level, Light decorations, treat stand). Nothing to convert: a World 1 section reads its zone count as
    /// the arrival zone plus that many zones opened, and the refuge level is computed from the profile on load.
    /// </summary>
    public sealed class Migration0006AddProgression : ISaveMigration
    {
        public int FromVersion => 6;
        public int ToVersion => 7;

        public void Apply(SaveDocument document)
        {
        }
    }

    /// <summary>
    /// Schema 7 to 8: the Discovery egg stock (World section 3, doc 02 Œufs, section 7). Nothing to convert: a World 2
    /// section reads as an empty stock.
    /// </summary>
    public sealed class Migration0007AddDiscoveryEggs : ISaveMigration
    {
        public int FromVersion => 7;
        public int ToVersion => 8;

        public void Apply(SaveDocument document)
        {
        }
    }

    /// <summary>
    /// Schema 8 to 9 (T35C): the Progression section gains the Renewal's memory and the Essence tree. Nothing to convert:
    /// a version 1 section reads as no Renewal yet and no node bought.
    /// </summary>
    public sealed class Migration0008AddRenewal : ISaveMigration
    {
        public int FromVersion => 8;
        public int ToVersion => 9;

        public void Apply(SaveDocument document)
        {
        }
    }

    /// <summary>
    /// Schema 9 to 10 (T36): the DailyVisit and Inventory sections. Nothing to convert: without a DailyVisit section the
    /// loader marks the welcome as done when the collection already holds 5 animals (doc 01 Contenu, section 1.1), and
    /// every rendez-vous starts on the next day check; an empty inventory holds nothing.
    /// </summary>
    public sealed class Migration0009AddDailyVisit : ISaveMigration
    {
        public int FromVersion => 9;
        public int ToVersion => 10;

        public void Apply(SaveDocument document)
        {
        }
    }

    /// <summary>
    /// Schema 10 to 11 (T37): the Activities section. Nothing to convert: without it the first use fills the ticket reserve
    /// (5) and takes in the tickets the rendez-vous kept in the inventory; records, challenges and difficulty start fresh.
    /// </summary>
    public sealed class Migration0010AddActivities : ISaveMigration
    {
        public int FromVersion => 10;
        public int ToVersion => 11;

        public void Apply(SaveDocument document)
        {
        }
    }

    /// <summary>
    /// Schema 11 to 12 (T42): the Expeditions section. Nothing to convert: without it no team is away, the first expedition
    /// is still the 3-minute stroll, the Gems slot is not bought and the guarantee counter starts at zero.
    /// </summary>
    public sealed class Migration0011AddExpeditions : ISaveMigration
    {
        public int FromVersion => 11;
        public int ToVersion => 12;

        public void Apply(SaveDocument document)
        {
        }
    }

    /// <summary>
    /// Schema 12 to 13 (T43): the Garden section. Nothing to convert: without it every plot is bare, the Gems plot is not
    /// bought, the garden is at level 1 with no dye nor palette. Seeds and crops already sit in the inventory.
    /// </summary>
    public sealed class Migration0012AddGarden : ISaveMigration
    {
        public int FromVersion => 12;
        public int ToVersion => 13;

        public void Apply(SaveDocument document)
        {
        }
    }

    /// <summary>
    /// Schema 13 to 14 (T44A): the Albums section. Nothing to convert: without it no sticker is owned and the albums open
    /// at refuge level 12 and day 5 with their opening's progress; stickers kept in the inventory before become a sticker
    /// of choice each (10 album stars) when they open.
    /// </summary>
    public sealed class Migration0013AddAlbums : ISaveMigration
    {
        public int FromVersion => 13;
        public int ToVersion => 14;

        public void Apply(SaveDocument document)
        {
        }
    }

    /// <summary>
    /// Schema 14 to 15 (T44B): the Achievements section. Nothing to convert: without it every counter starts at zero, then
    /// the achievement hooks compute the ones that count a saved state (species, variants, levels, masteries, bonds,
    /// decorations, albums, refuge level, Renewals) from the save, unlock the titles and frames those states give, and give
    /// the starting title and frame (doc 01 Succès, section 1 « Ce qui compte »). Action counters begin with this version.
    /// </summary>
    public sealed class Migration0014AddAchievements : ISaveMigration
    {
        public int FromVersion => 14;
        public int ToVersion => 15;

        public void Apply(SaveDocument document)
        {
        }
    }

    /// <summary>
    /// Schema 15 to 16 (T44C): the Journal section (the refuge journal and the rare visitors). Nothing to convert: without it
    /// the journal hooks mark the account's past moments done without pages (first hatch, rarities, traits, names, bonds,
    /// species, first expedition, Renewal, harvests), so pages begin with what happens next; no visitor has been met yet.
    /// </summary>
    public sealed class Migration0015AddJournal : ISaveMigration
    {
        public int FromVersion => 15;
        public int ToVersion => 16;

        public void Apply(SaveDocument document)
        {
        }
    }
}
