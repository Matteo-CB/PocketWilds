using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using PocketWilds.Content;
using PocketWilds.Core;

namespace PocketWilds.Tests.EditMode
{
    internal static class TestUtil
    {
        public static readonly DateTimeOffset T0 = new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

        public static ContentRegistry ThreeSpecies()
        {
            var builder = new ContentRegistryBuilder();
            AddSpecies(builder, "species.rabbit", 1.0);
            AddSpecies(builder, "species.squirrel", 1.5);
            AddSpecies(builder, "species.hedgehog", 2.0);
            return builder.Build();
        }

        public static void AddSpecies(ContentRegistryBuilder builder, string sourceId, double baseProduction)
        {
            builder.AddSpecies(
                new SpeciesDefinition(ContentId.FromSource(sourceId), sourceId + ".name", RigFamily.SmallQuadruped,
                    ContentId.FromSource("biome.grassland"), baseProduction, "PF_Animal_Test"),
                sourceId);
        }

        public static string NewTempDirectory()
        {
            string path = Path.Combine(Path.GetTempPath(), "PocketWildsTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        public static void DeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
            }
            catch (IOException)
            {
            }
        }

        /// <summary>Lets the editor loop pump continuations, then rethrows any task failure.</summary>
        public static IEnumerator Await(Task task, double timeoutSeconds = 10.0)
        {
            var start = DateTime.UtcNow;
            while (!task.IsCompleted)
            {
                if ((DateTime.UtcNow - start).TotalSeconds > timeoutSeconds)
                    throw new TimeoutException("Task did not complete in time.");
                yield return null;
            }
            if (task.IsFaulted)
                throw task.Exception.GetBaseException();
        }
    }
}
