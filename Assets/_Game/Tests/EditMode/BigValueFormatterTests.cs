using System;
using System.Diagnostics;
using NUnit.Framework;
using PocketWilds.Core;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace PocketWilds.Tests.EditMode
{
    public class BigValueFormatterTests
    {
        private static string Format(BigValue value, NumberFormatSettings settings)
        {
            var buffer = new char[BigValueFormatter.MaxLength];
            int length = BigValueFormatter.Format(value, buffer, 0, settings);
            return new string(buffer, 0, length);
        }

        [TestCase(0.0, "0")]
        [TestCase(0.04, "0")]
        [TestCase(0.15, "0.1")]
        [TestCase(1.25, "1.2")]
        [TestCase(4.5, "4.5")]
        [TestCase(10.0, "10")]
        [TestCase(99.99, "99.9")]
        [TestCase(100.0, "100")]
        [TestCase(999.9, "999")]
        [TestCase(1250.0, "1,250")]
        [TestCase(9999.0, "9,999")]
        [TestCase(10000.0, "10K")]
        [TestCase(12400.0, "12.4K")]
        [TestCase(24800.0, "24.8K")]
        [TestCase(124000.0, "124K")]
        [TestCase(999999.0, "999K")]
        [TestCase(1e6, "1M")]
        [TestCase(2.1e6, "2.1M")]
        [TestCase(3.75e9, "3.75B")]
        [TestCase(1e12, "1T")]
        [TestCase(1e15, "1aa")]
        [TestCase(1.5e18, "1.5ab")]
        [TestCase(1e90, "1az")]
        [TestCase(1e93, "1ba")]
        [TestCase(-1250.0, "-1,250")]
        [TestCase(-12400.0, "-12.4K")]
        [TestCase(-0.01, "0")]
        public void Formats_PlayerFacing(double value, string expected)
        {
            Assert.AreEqual(expected, Format(BigValue.FromDouble(value), NumberFormatSettings.Invariant));
        }

        [Test]
        public void Formats_LastLetterTier_ThenScientific()
        {
            Assert.AreEqual("1zz", Format(BigValue.FromMantissaExponent(1, 2040), NumberFormatSettings.Invariant));
            Assert.AreEqual("1.23e2043", Format(BigValue.FromMantissaExponent(1.23, 2043), NumberFormatSettings.Invariant));
            Assert.AreEqual("9.99e1000000000", Format(BigValue.MaxValue, NumberFormatSettings.Invariant));
        }

        [Test]
        public void Formats_WithLocaleSeparators()
        {
            var french = new NumberFormatSettings(',', ' ');
            Assert.AreEqual("1 250", Format(BigValue.FromDouble(1250), french));
            Assert.AreEqual("12,4K", Format(BigValue.FromDouble(12400), french));
            Assert.AreEqual("4,5", Format(BigValue.FromDouble(4.5), french));
        }

        [Test]
        public void RejectsTooSmallBuffer()
        {
            Assert.Throws<ArgumentException>(() => BigValueFormatter.Format(BigValue.One, new char[4], 0, NumberFormatSettings.Invariant));
        }

        [Test]
        public void Format_100k_DoesNotAllocate_AfterWarmup()
        {
            var buffer = new char[BigValueFormatter.MaxLength];
            var values = new BigValue[64];
            for (int i = 0; i < values.Length; i++)
                values[i] = BigValue.FromMantissaExponent(1.0 + i * 0.137, i * 7 - 20);

            BigValueFormatter.Format(values[0], buffer, 0, NumberFormatSettings.Invariant);

            int total = 0;
            Assert.That(() =>
            {
                for (int i = 0; i < 100000; i++)
                    total += BigValueFormatter.Format(values[i & 63], buffer, 0, NumberFormatSettings.Invariant);
            }, Is.Not.AllocatingGCMemory());
            Assert.Greater(total, 0);
        }

        [Test]
        public void Benchmark_100kFormats()
        {
            var buffer = new char[BigValueFormatter.MaxLength];
            var value = BigValue.FromDouble(12345678.9);
            var watch = Stopwatch.StartNew();
            for (int i = 0; i < 100000; i++)
                BigValueFormatter.Format(value, buffer, 0, NumberFormatSettings.Invariant);
            watch.Stop();
            UnityEngine.Debug.Log($"[Benchmark] BigValueFormatter 100k formats: {watch.Elapsed.TotalMilliseconds:F2} ms");
        }
    }
}
