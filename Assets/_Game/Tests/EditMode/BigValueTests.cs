using System;
using System.Diagnostics;
using NUnit.Framework;
using PocketWilds.Core;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace PocketWilds.Tests.EditMode
{
    public class BigValueTests
    {
        private static BigValue B(double mantissa, long exponent) => BigValue.FromMantissaExponent(mantissa, exponent);

        [Test]
        public void Zero_And_One()
        {
            Assert.IsTrue(BigValue.Zero.IsZero);
            Assert.AreEqual(0, BigValue.Zero.Sign);
            Assert.AreEqual(1.0, BigValue.One.Mantissa);
            Assert.AreEqual(0, BigValue.One.Exponent10);
            Assert.AreEqual(BigValue.Zero, BigValue.FromDouble(0.0));
            Assert.AreEqual(BigValue.Zero, BigValue.FromDouble(double.NaN));
        }

        [TestCase(12345.0, 1.2345, 4)]
        [TestCase(-0.5, -5.0, -1)]
        [TestCase(1e-300, 1.0, -300)]
        [TestCase(1e300, 1.0, 300)]
        [TestCase(9.999999, 9.999999, 0)]
        [TestCase(10.0, 1.0, 1)]
        public void FromDouble_Normalizes(double value, double mantissa, int exponent)
        {
            var v = BigValue.FromDouble(value);
            double abs = Math.Abs(v.Mantissa);
            Assert.IsTrue(abs >= 1.0 && abs < 10.0, "mantissa out of [1,10): " + v.Mantissa);
            // Values sitting exactly on a power of ten may land on either side by one ulp; compare values.
            Assert.IsTrue(BigValue.ApproximatelyEqual(B(mantissa, exponent), v, 1e-12), v.Mantissa + "e" + v.Exponent10);
        }

        [Test]
        public void Normalize_HandlesUnnormalizedInput()
        {
            var v = B(123.0, 5);
            Assert.AreEqual(7, v.Exponent10);
            Assert.AreEqual(1.23, v.Mantissa, 1e-12);
        }

        [Test]
        public void Range_1eMinus300_To_1e300_RoundTrips()
        {
            for (int e = -300; e <= 300; e += 7)
            {
                double value = 3.14159 * Math.Pow(10, e);
                double back = BigValue.FromDouble(value).ToDoubleClamped();
                Assert.AreEqual(value, back, Math.Abs(value) * 1e-12, "e=" + e);
            }
        }

        [Test]
        public void Add_SameOrder_And_HugeGap()
        {
            Assert.IsTrue(BigValue.ApproximatelyEqual(B(3, 5), B(1, 5) + B(2, 5), 1e-15));
            Assert.AreEqual(B(1, 300), B(1, 300) + BigValue.One);
            Assert.AreEqual(B(1, 300), BigValue.One + B(1, 300));
            Assert.IsTrue(BigValue.ApproximatelyEqual(B(1.1, 1), B(1, 1) + BigValue.One, 1e-15));
        }

        [Test]
        public void Subtract_NearCancellation_RenormalizesSmallResult()
        {
            var a = BigValue.FromDouble(100000.01);
            var b = BigValue.FromDouble(100000.0);
            var diff = a - b;
            Assert.AreEqual(-2, diff.Exponent10);
            Assert.AreEqual(1.0, diff.Mantissa, 1e-6);
            Assert.IsTrue((b - b).IsZero);
        }

        [Test]
        public void Multiply_BeyondDoubleRange()
        {
            var v = B(2, 1000) * B(3, 2000);
            Assert.AreEqual(3000, v.Exponent10);
            Assert.AreEqual(6.0, v.Mantissa, 1e-12);
            Assert.AreEqual(B(1.5, 1), BigValue.FromDouble(3) * 5.0);
        }

        [Test]
        public void Multiply_ExponentOverflow_Saturates_AndUnderflow_IsZero()
        {
            var big = B(5, BigValue.MaxExponent - 1);
            Assert.AreEqual(BigValue.MaxValue, big * B(5, 5));
            Assert.AreEqual(BigValue.Negate(BigValue.MaxValue), BigValue.Negate(big) * B(5, 5));
            var tiny = B(1, BigValue.MinExponent + 1);
            Assert.IsTrue((tiny * B(1, -10)).IsZero);
        }

        [Test]
        public void Divide_And_DivideByZero()
        {
            Assert.IsTrue(BigValue.ApproximatelyEqual(B(2, -3), B(6, 2) / B(3, 5), 1e-15));
            Assert.Throws<DivideByZeroException>(() => BigValue.Divide(BigValue.One, BigValue.Zero));
            Assert.IsTrue((BigValue.Zero / B(1, 5)).IsZero);
        }

        [Test]
        public void Compare_OrdersSignsExponentsAndMantissas()
        {
            Assert.Less(B(-5, 10).CompareTo(B(-5, 2)), 0);
            Assert.Less(B(-1, 0).CompareTo(BigValue.Zero), 0);
            Assert.Less(BigValue.Zero.CompareTo(B(1, -50)), 0);
            Assert.Less(B(9.9, 3).CompareTo(B(1.0, 4)), 0);
            Assert.Less(B(1.5, 4).CompareTo(B(2.5, 4)), 0);
            Assert.AreEqual(0, B(2.5, 4).CompareTo(B(2.5, 4)));
            Assert.IsTrue(B(1, 6) > B(9, 5));
            Assert.IsTrue(B(1, 6) >= B(1, 6));
        }

        [Test]
        public void Log10_And_PowPositive()
        {
            Assert.AreEqual(1234.0, BigValue.Log10(B(1, 1234)), 1e-9);
            Assert.AreEqual(double.NegativeInfinity, BigValue.Log10(BigValue.Zero));
            Assert.IsNaN(BigValue.Log10(B(-1, 0)));

            var pow = BigValue.PowPositive(BigValue.FromDouble(1.18), 100);
            Assert.AreEqual(15424131.905453218, pow.ToDoubleClamped(), 15424131.9 * 1e-9);

            Assert.AreEqual(BigValue.One, BigValue.PowPositive(BigValue.Zero, 0));
            Assert.IsTrue(BigValue.PowPositive(BigValue.Zero, 2).IsZero);
            Assert.Throws<ArgumentOutOfRangeException>(() => BigValue.PowPositive(BigValue.One, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => BigValue.PowPositive(B(-2, 0), 2));

            var huge = BigValue.PowPositive(B(1, 1000), 1000);
            Assert.AreEqual(1000000, huge.Exponent10);
        }

        [Test]
        public void ToDoubleClamped_Saturates()
        {
            Assert.AreEqual(double.MaxValue, B(5, 400).ToDoubleClamped());
            Assert.AreEqual(-double.MaxValue, B(-5, 400).ToDoubleClamped());
            Assert.AreEqual(0.0, B(5, -400).ToDoubleClamped());
            Assert.AreEqual(double.MaxValue, B(9, 308).ToDoubleClamped());
        }

        [Test]
        public void ClampMin_Max_Min()
        {
            Assert.AreEqual(BigValue.One, BigValue.ClampMin(BigValue.Zero, BigValue.One));
            Assert.AreEqual(B(5, 5), BigValue.Max(B(5, 5), B(4, 5)));
            Assert.AreEqual(B(4, 5), BigValue.Min(B(5, 5), B(4, 5)));
        }

        [Test]
        public void Arithmetic_DoesNotAllocate()
        {
            var acc = BigValue.One;
            var step = B(1.0001, 0);
            Assert.That(() =>
            {
                for (int i = 0; i < 10000; i++)
                {
                    acc = acc * step + BigValue.One;
                    acc = acc - B(1, -3);
                    if (acc > B(1, 50))
                        acc = acc / B(1, 40);
                }
            }, Is.Not.AllocatingGCMemory());
            Assert.IsTrue(acc.IsFinite);
        }

        [Test]
        public void Benchmark_100kOperations()
        {
            var acc = BigValue.One;
            var step = B(1.01, 0);
            var watch = Stopwatch.StartNew();
            for (int i = 0; i < 100000; i++)
                acc = BigValue.Add(BigValue.Multiply(acc, step), BigValue.One);
            watch.Stop();
            UnityEngine.Debug.Log($"[Benchmark] BigValue 100k multiply+add: {watch.Elapsed.TotalMilliseconds:F2} ms");
            Assert.IsTrue(acc.IsFinite);
        }
    }
}
