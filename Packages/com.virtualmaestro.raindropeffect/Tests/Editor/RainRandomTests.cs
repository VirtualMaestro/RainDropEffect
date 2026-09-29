using NUnit.Framework;

namespace RainDropEffect.Tests
{
    /// <summary>
    /// The RNG is what makes a seeded run reproducible, which the whole visual comparison in
    /// Phase 7 rests on. These tests pin its contract, not its exact values.
    /// </summary>
    public class RainRandomTests
    {
        [Test]
        public void SameSeedSameSequence()
        {
            var a = new RainRandom(12345);
            var b = new RainRandom(12345);

            for (var i = 0; i < 1000; i++)
            {
                Assert.AreEqual(a.NextUInt(), b.NextUInt(), $"sequences diverged at draw {i}");
            }
        }

        [Test]
        public void DifferentSeedsDiverge()
        {
            var a = new RainRandom(1);
            var b = new RainRandom(2);

            var same = 0;
            for (var i = 0; i < 100; i++)
            {
                if (a.NextUInt() == b.NextUInt())
                {
                    same++;
                }
            }

            Assert.Less(same, 5, "two different seeds produced nearly the same sequence");
        }

        [Test]
        public void RangeIntExclusiveMax()
        {
            var rng = new RainRandom(7);
            var seen = new bool[3];

            for (var i = 0; i < 10000; i++)
            {
                var value = rng.Range(2, 5);
                Assert.GreaterOrEqual(value, 2);
                Assert.Less(value, 5, "the maximum must be exclusive");
                seen[value - 2] = true;
            }

            CollectionAssert.AreEqual(new[] { true, true, true }, seen, "not every value in [2,5) appeared");
        }

        [Test]
        public void RangeIntDegenerateReturnsMin()
        {
            var rng = new RainRandom(7);
            Assert.AreEqual(5, rng.Range(5, 5));
            Assert.AreEqual(5, rng.Range(5, 3));
        }

        [Test]
        public void NextFloatStaysInUnitInterval()
        {
            var rng = new RainRandom(99);
            for (var i = 0; i < 10000; i++)
            {
                var value = rng.NextFloat();
                Assert.GreaterOrEqual(value, 0f);
                Assert.Less(value, 1f);
            }
        }

        [Test]
        public void SeedZeroIsNotZeroState()
        {
            // Xorshift never leaves a zero state, so seed 0 must be remapped.
            var rng = new RainRandom(0);

            var nonZero = false;
            for (var i = 0; i < 10; i++)
            {
                if (rng.NextUInt() != 0)
                {
                    nonZero = true;
                }
            }

            Assert.IsTrue(nonZero, "seed 0 produced a dead sequence");
        }
    }
}
