using NUnit.Framework;
using UnityEngine;

namespace RainDropEffect.Tests
{
    /// <summary>
    /// The emission clock, tested as a pure struct. Every family goes through it, so a change here
    /// changes the rate of every converted preset.
    /// </summary>
    public class EmitterStateTests
    {
        const int Slots = 100;

        RainRandom rng;

        [SetUp]
        public void SetUp()
        {
            rng = new RainRandom(42);
        }

        static SimpleLayerSettings Settings(float duration, int rateMin, int rateMax, float delay = 0f,
            bool playOnce = false)
        {
            return new SimpleLayerSettings
            {
                Duration = duration,
                EmissionRateRange = new Vector2Int(rateMin, rateMax),
                Delay = delay,
                PlayOnce = playOnce
            };
        }

        [Test]
        public void DelayThenEmitting()
        {
            var s = Settings(1f, 4, 5, delay: 0.5f);
            var emitter = new EmitterState();
            emitter.Play(s, ref rng);

            Assert.AreEqual(EmitterState.Phase.Delay, emitter.Current);
            Assert.AreEqual(0, emitter.Tick(0.25f, Slots, s, ref rng));
            Assert.AreEqual(EmitterState.Phase.Delay, emitter.Current);

            Assert.AreEqual(0, emitter.Tick(0.3f, Slots, s, ref rng));
            Assert.AreEqual(EmitterState.Phase.Emitting, emitter.Current);
        }

        [Test]
        public void PlayDuringDelayDoesNotRestart()
        {
            var s = Settings(1f, 4, 5, delay: 0.5f);
            var emitter = new EmitterState();
            emitter.Play(s, ref rng);

            emitter.Tick(0.45f, Slots, s, ref rng);

            // A second Play must not extend the delay that is already running.
            emitter.Play(s, ref rng);
            emitter.Tick(0.06f, Slots, s, ref rng);

            Assert.AreEqual(EmitterState.Phase.Emitting, emitter.Current);
        }

        [Test]
        public void OneShotDrainsAfterDuration()
        {
            var s = Settings(1f, 4, 5, playOnce: true);
            var emitter = new EmitterState();
            emitter.Play(s, ref rng);

            for (var i = 0; i < 21; i++)
            {
                emitter.Tick(0.05f, Slots, s, ref rng);
            }

            Assert.AreEqual(EmitterState.Phase.Draining, emitter.Current);
        }

        [Test]
        public void SpawnCountMatchesRate()
        {
            // Duration 1 s at 4-5 drops per second is an interval of 0.2-0.25 s.
            var s = Settings(1f, 4, 5);
            var emitter = new EmitterState();
            emitter.Play(s, ref rng);

            var total = 0;
            for (var i = 0; i < 10; i++)
            {
                total += emitter.Tick(0.1f, Slots, s, ref rng);
            }

            Assert.That(total, Is.InRange(3, 5), $"expected roughly 4 spawns in one second, got {total}");
        }

        [Test]
        public void LongFrameCatchesUp()
        {
            var s = Settings(1f, 4, 4);
            var emitter = new EmitterState();
            emitter.Play(s, ref rng);

            // 0.6 s at an interval of 0.25 s is two whole intervals.
            Assert.AreEqual(2, emitter.Tick(0.6f, Slots, s, ref rng));
        }

        [Test]
        public void FreeSlotsCapSpawns()
        {
            var s = Settings(1f, 4, 4);
            var emitter = new EmitterState();
            emitter.Play(s, ref rng);

            Assert.AreEqual(1, emitter.Tick(0.9f, 1, s, ref rng), "catch-up must not exceed the free slots");
        }

        [Test]
        public void ZeroRateNeverSpawns()
        {
            var s = Settings(1f, 0, 0);
            var emitter = new EmitterState();
            emitter.Play(s, ref rng);

            var total = 0;
            for (var i = 0; i < 100; i++)
            {
                total += emitter.Tick(0.1f, Slots, s, ref rng);
            }

            Assert.AreEqual(0, total);
        }

        [Test]
        public void ZeroDurationNeverSpawns()
        {
            var s = Settings(0f, 4, 5);
            var emitter = new EmitterState();
            emitter.Play(s, ref rng);

            var total = 0;
            for (var i = 0; i < 100; i++)
            {
                total += emitter.Tick(0.1f, Slots, s, ref rng);
            }

            Assert.AreEqual(0, total);
        }

        [Test]
        public void StopThenPlayResumes()
        {
            var s = Settings(1f, 4, 5);
            var emitter = new EmitterState();
            emitter.Play(s, ref rng);
            Assert.AreEqual(EmitterState.Phase.Emitting, emitter.Current);

            emitter.Stop();
            Assert.AreEqual(EmitterState.Phase.Draining, emitter.Current);

            emitter.Play(s, ref rng);
            Assert.AreEqual(EmitterState.Phase.Emitting, emitter.Current);
        }

        [Test]
        public void ClearReturnsToIdle()
        {
            var s = Settings(1f, 4, 5, delay: 1f);
            var emitter = new EmitterState();
            emitter.Play(s, ref rng);

            emitter.Clear();

            Assert.AreEqual(EmitterState.Phase.Idle, emitter.Current);
            Assert.AreEqual(0, emitter.Tick(10f, Slots, s, ref rng), "a cleared emitter must stay idle");
        }
    }
}
