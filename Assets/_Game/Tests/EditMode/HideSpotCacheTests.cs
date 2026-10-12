using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PocketWilds.Editor.Activities;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// The hide-spot build's caches (HideSpotPipeline.Cache.cs): the start zone read back from the cache equals its focus
    /// measured again from scratch, bit for bit; the keys change with what they hold (a species' posed mesh, a decor, an
    /// obstacle) and only then; a cache file reads back what was written, and a damaged one reads as empty. The full check
    /// of every zone and focus is the menu « Verify Hide Spots Cache (T39) » (about a minute).
    /// </summary>
    public class HideSpotCacheTests
    {
        private static List<string> _findings;

        private static List<string> Findings(string kind)
        {
            if (_findings == null)
            {
                _findings = HideSpotPipeline.QuickCheck();
                foreach (var note in _findings.Where(f => f.StartsWith("note:")))
                    TestContext.WriteLine(note);
            }
            return _findings.Where(f => f.StartsWith(kind + ":")).ToList();
        }

        [Test]
        public void CachedZone_EqualsItsFocusMeasuredAgain()
        {
            var problems = Findings("cache");
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        [Test]
        public void CacheKeys_ChangeWithWhatTheyHold()
        {
            var problems = Findings("keys");
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        [Test]
        public void CacheFiles_ReadBackWhatWasWritten()
        {
            var problems = Findings("files");
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }
    }
}
