#nullable enable
using System;
using System.Collections.Generic;

namespace PocketWilds.Services.Save
{
    /// <summary>One schema step. Each migration only knows its two versions; never patch old formats elsewhere.</summary>
    public interface ISaveMigration
    {
        int FromVersion { get; }
        int ToVersion { get; }
        void Apply(SaveDocument document);
    }

    public sealed class SaveMigrationException : Exception
    {
        public SaveMigrationException(string message) : base(message)
        {
        }
    }

    /// <summary>Applies migrations in a strict chain from the document version to the current schema.</summary>
    public sealed class SaveMigrationPipeline
    {
        private readonly Dictionary<int, ISaveMigration> _byFromVersion = new Dictionary<int, ISaveMigration>();

        public SaveMigrationPipeline(int currentSchemaVersion, IEnumerable<ISaveMigration> migrations)
        {
            CurrentSchemaVersion = currentSchemaVersion;
            foreach (var migration in migrations)
            {
                if (migration.ToVersion != migration.FromVersion + 1)
                    throw new ArgumentException($"Migration {migration.FromVersion}->{migration.ToVersion} must advance exactly one version.");
                if (migration.ToVersion > currentSchemaVersion)
                    throw new ArgumentException($"Migration to {migration.ToVersion} is beyond schema {currentSchemaVersion}.");
                _byFromVersion.Add(migration.FromVersion, migration);
            }
        }

        public int CurrentSchemaVersion { get; }

        /// <summary>Returns the number of migrations applied. Throws if a step is missing.</summary>
        public int Migrate(SaveDocument document)
        {
            if (document.SchemaVersion > CurrentSchemaVersion)
                throw new SaveMigrationException($"Save schema {document.SchemaVersion} is newer than this build ({CurrentSchemaVersion}).");

            int applied = 0;
            while (document.SchemaVersion < CurrentSchemaVersion)
            {
                if (!_byFromVersion.TryGetValue(document.SchemaVersion, out var migration))
                    throw new SaveMigrationException($"No migration from schema {document.SchemaVersion}.");

                migration.Apply(document);
                document.SchemaVersion = migration.ToVersion;
                applied++;
            }
            return applied;
        }
    }
}
