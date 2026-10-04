using System;
using NUnit.Framework;
using PocketWilds.Core;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace PocketWilds.Tests.EditMode
{
    public class ObjectPoolTests
    {
        private sealed class Item : IPoolable
        {
            public int Acquired;
            public int Released;
            public bool Destroyed;
            public void OnAcquire() => Acquired++;
            public void OnRelease() => Released++;
        }

        private int _created;

        private ObjectPool<Item> NewPool(int soft, int hard) =>
            new ObjectPool<Item>(() => { _created++; return new Item(); }, item => item.Destroyed = true, soft, hard);

        [SetUp]
        public void SetUp() => _created = 0;

        [Test]
        public void Warmup_ThenSteadyState_NeverCreates()
        {
            var pool = NewPool(16, 32);
            Assert.AreEqual(16, pool.Warmup(16));
            Assert.AreEqual(16, _created);

            var held = new Item[16];
            for (int cycle = 0; cycle < 1000; cycle++)
            {
                for (int i = 0; i < 16; i++)
                    Assert.IsTrue(pool.TryAcquire(out held[i]));
                for (int i = 0; i < 16; i++)
                    pool.Release(held[i]);
            }

            Assert.AreEqual(16, _created);
            Assert.AreEqual(0, pool.Stats.Destroyed);
            Assert.AreEqual(16, pool.Stats.PeakActive);
        }

        [Test]
        public void SteadyState_DoesNotAllocate()
        {
            var pool = NewPool(8, 8);
            pool.Warmup(8);
            var held = new Item[8];
            Assert.That(() =>
            {
                for (int cycle = 0; cycle < 1000; cycle++)
                {
                    for (int i = 0; i < 8; i++)
                        pool.TryAcquire(out held[i]);
                    for (int i = 0; i < 8; i++)
                        pool.Release(held[i]);
                }
            }, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void HardCap_FailsCleanly()
        {
            var pool = NewPool(2, 3);
            Assert.IsTrue(pool.TryAcquire(out _));
            Assert.IsTrue(pool.TryAcquire(out _));
            Assert.IsTrue(pool.TryAcquire(out _));
            Assert.IsFalse(pool.TryAcquire(out var none));
            Assert.IsNull(none);
            Assert.AreEqual(3, _created);
            Assert.AreEqual(4, pool.Stats.Misses);
        }

        [Test]
        public void AboveSoftCapacity_IsDestroyedLater_OrRevived()
        {
            var pool = NewPool(2, 6);
            var items = new Item[5];
            for (int i = 0; i < 5; i++)
                pool.TryAcquire(out items[i]);
            for (int i = 0; i < 5; i++)
                pool.Release(items[i]);

            Assert.AreEqual(2, pool.Stats.Inactive);
            Assert.AreEqual(3, pool.Stats.PendingDestroy);

            // Demand comes back before trimming: pending objects are reused, nothing new is created.
            for (int i = 0; i < 3; i++)
                pool.TryAcquire(out items[i]);
            Assert.AreEqual(5, _created);
            Assert.AreEqual(2, pool.Stats.PendingDestroy);

            Assert.AreEqual(1, pool.TrimPending(1));
            Assert.AreEqual(1, pool.TrimPending(10));
            Assert.AreEqual(2, pool.Stats.Destroyed);
        }

        [Test]
        public void Callbacks_AndMisuse()
        {
            var pool = NewPool(4, 4);
            pool.TryAcquire(out var item);
            pool.Release(item);
            Assert.AreEqual(1, item.Acquired);
            Assert.AreEqual(1, item.Released);
            Assert.Throws<InvalidOperationException>(() => pool.Release(item));
            Assert.Throws<ArgumentOutOfRangeException>(() => NewPool(5, 4));
        }
    }
}
