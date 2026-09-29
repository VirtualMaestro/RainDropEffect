using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace RainDropEffect.Tests
{
    /// <summary>
    /// The Flow family: bounded trail count, determinism under a seed, and motion that does not
    /// depend on the frame rate — the property the closed-form fall exists to guarantee.
    /// </summary>
    public class FlowLayerRuntimeTests
    {
        const string ShaderSetPath =
            "Packages/com.virtualmaestro.raindropeffect/Runtime/Shaders/RainShaderSet.asset";

        readonly List<LayerRuntime> created = new List<LayerRuntime>();

        RainShaderSet shaders;

        [SetUp]
        public void SetUp()
        {
            shaders = AssetDatabase.LoadAssetAtPath<RainShaderSet>(ShaderSetPath);
            Assert.IsNotNull(shaders, "the shipped RainShaderSet is missing");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var runtime in created)
            {
                runtime.Dispose();
            }

            created.Clear();
        }

        FlowLayerRuntime Create(FlowLayerSettings settings, RainQuality quality = RainQuality.High)
        {
            var runtime = new FlowLayerRuntime(settings, shaders, quality);
            created.Add(runtime);
            return runtime;
        }

        static FlowLayerSettings Settings(float fluctuation = 5f)
        {
            return new FlowLayerSettings
            {
                Name = "flow",
                Duration = 1f,
                EmissionRateRange = new Vector2Int(10, 10),
                MaxCount = 10,
                LifetimeRange = new Vector2(3f, 3f),
                AccelerationRange = new Vector2(0.2f, 0.2f),
                FluctuationRateRange = new Vector2(fluctuation, fluctuation),
                WidthRange = new Vector2(0.1f, 0.1f),
                PointSpacing = 0.01f,
                MaxPoints = 64
            };
        }

        static RainTickContext Context(float dt)
        {
            return new RainTickContext
            {
                Dt = dt,
                Down = Vector2.down,
                Wind = Vector2.zero,
                Intensity = 1f,
                Aspect = 16f / 9f,
                Quality = RainQuality.High
            };
        }

        [Test]
        public void DeterministicWithSeed()
        {
            var a = Create(Settings());
            var b = Create(Settings());

            var rngA = new RainRandom(21);
            var rngB = new RainRandom(21);
            a.Play(ref rngA);
            b.Play(ref rngB);

            var ctx = Context(1f / 60f);
            for (var i = 0; i < 120; i++)
            {
                a.Tick(ctx, ref rngA);
                b.Tick(ctx, ref rngB);
            }

            Assert.AreEqual(a.ActiveCount, b.ActiveCount);
            for (var slot = 0; slot < a.Capacity; slot++)
            {
                Assert.AreEqual(a.TrailActive(slot), b.TrailActive(slot), $"slot {slot} differs in activity");
                if (a.TrailActive(slot))
                {
                    Assert.AreEqual(a.TrailPosition(slot), b.TrailPosition(slot), $"slot {slot} differs in position");
                    Assert.AreEqual(a.TrailPointCount(slot), b.TrailPointCount(slot), $"slot {slot} differs in points");
                }
            }
        }

        [Test]
        public void TrailCountNeverExceedsCap()
        {
            var settings = Settings();
            settings.MaxCount = 100;

            var runtime = Create(settings, RainQuality.Low);
            Assert.AreEqual(RainQualityCaps.MaxTrails(RainQuality.Low), runtime.Capacity);

            var rng = new RainRandom(3);
            runtime.Play(ref rng);

            var ctx = Context(1f / 60f);
            for (var i = 0; i < 300; i++)
            {
                runtime.Tick(ctx, ref rng);
                Assert.LessOrEqual(runtime.ActiveCount, runtime.Capacity);
            }
        }

        [Test]
        public void FrameRateIndependence()
        {
            // Fluctuation off: the sideways wobble consumes RNG at a rate that depends on the step
            // size, and this test is about the vertical motion.
            var heads = new float[3];
            var steps = new[] { 1f / 30f, 1f / 60f, 1f / 120f };

            for (var k = 0; k < steps.Length; k++)
            {
                var runtime = Create(Settings(fluctuation: 0f));
                var rng = new RainRandom(5);
                runtime.Play(ref rng);

                var ctx = Context(steps[k]);
                var count = Mathf.RoundToInt(1f / steps[k]);
                for (var i = 0; i < count; i++)
                {
                    runtime.Tick(ctx, ref rng);
                }

                heads[k] = runtime.TrailPosition(0).y;
            }

            Assert.AreEqual(heads[1], heads[0], 0.005f, "30 Hz and 60 Hz disagree on where the trail is");
            Assert.AreEqual(heads[1], heads[2], 0.005f, "120 Hz and 60 Hz disagree on where the trail is");
        }

        [Test]
        public void StopDrainsToIdle()
        {
            var runtime = Create(Settings());
            var rng = new RainRandom(9);
            runtime.Play(ref rng);

            var ctx = Context(1f / 60f);
            for (var i = 0; i < 60; i++)
            {
                runtime.Tick(ctx, ref rng);
            }

            runtime.Stop();
            Assert.IsFalse(runtime.IsEmitting);

            for (var i = 0; i < 300; i++)
            {
                runtime.Tick(ctx, ref rng);
            }

            Assert.IsFalse(runtime.IsPlaying, "trails must finish their 3 s lifetime and stop");
        }

        [Test]
        public void ClearEmptiesBuffers()
        {
            var runtime = Create(Settings());
            var rng = new RainRandom(9);
            runtime.Play(ref rng);

            var ctx = Context(1f / 60f);
            for (var i = 0; i < 60; i++)
            {
                runtime.Tick(ctx, ref rng);
            }

            runtime.Clear();
            runtime.Emit(ctx);

            Assert.AreEqual(0, runtime.ActiveCount);
            for (var slot = 0; slot < runtime.Capacity; slot++)
            {
                Assert.AreEqual(0, runtime.TrailPointCount(slot), $"slot {slot} still holds points");
            }

            Assert.AreEqual(0, runtime.Mesh.VertexCount);
        }

        [Test]
        public void FluctuationMeanMatchesRate()
        {
            var rng = new RainRandom(17);
            var sum = 0f;
            const int samples = 2000;

            for (var i = 0; i < samples; i++)
            {
                sum += FlowLayerRuntime.NextFluctDelay(5f, ref rng);
            }

            // Exponential with rate 5 has a mean of 0.2 s.
            Assert.AreEqual(0.2f, sum / samples, 0.02f);
        }

        [Test]
        public void ZeroFluctuationRateNeverReRolls()
        {
            var rng = new RainRandom(17);
            Assert.IsTrue(float.IsPositiveInfinity(FlowLayerRuntime.NextFluctDelay(0f, ref rng)));
        }

        /// <summary>
        /// The wobble must WANDER, not track the spawn column.
        ///
        /// Before the fix the offset was an absolute, re-roll-reset ease from <c>Start</c>:
        /// <c>LateralT</c> only reached <c>0.01 * Smooth * tau ≈ 0.01</c> per interval, so the offset
        /// was pinned at about <c>0.01 * Amplitude = 0.001</c> and returned toward zero on every
        /// re-roll. Integrating it instead makes successive intervals compound into a random walk,
        /// which at Smooth 5 / Amplitude 0.1 / rate 5 lands an order of magnitude further out over a
        /// 3 s life.
        /// </summary>
        [Test]
        public void LateralOffsetAccumulatesAndPicksBothSides()
        {
            const int seeds = 20;
            const float boundedCeiling = 0.001f;

            var finals = new List<float>(seeds);
            var ctx = Context(1f / 60f);

            for (var seed = 1; seed <= seeds; seed++)
            {
                var settings = Settings();
                settings.MaxCount = 1;

                var runtime = Create(settings);
                var rng = new RainRandom(seed);
                runtime.Play(ref rng);

                // 170 ticks is 2.83 s: the trail spawns at 0.1 s and lives 3 s, so it is still on
                // its first life and has never been reset.
                for (var i = 0; i < 170; i++)
                {
                    runtime.Tick(ctx, ref rng);
                }

                Assert.IsTrue(runtime.TrailActive(0), $"seed {seed} lost its trail before the measurement");
                finals.Add(runtime.TrailLateral(0));
            }

            var mean = 0f;
            foreach (var f in finals)
            {
                mean += Mathf.Abs(f);
            }

            mean /= finals.Count;

            Assert.Greater(mean, 5f * boundedCeiling,
                $"mean |Lateral| {mean:F5} is still near the bounded ceiling {boundedCeiling:F5} — the offset is not accumulating");

            var positive = 0;
            var negative = 0;
            foreach (var f in finals)
            {
                if (f > 0f)
                {
                    positive++;
                }
                else if (f < 0f)
                {
                    negative++;
                }
            }

            Assert.Greater(positive, 0, "no trail ever drifted right — this is a track, not a wander");
            Assert.Greater(negative, 0, "no trail ever drifted left — this is a track, not a wander");
        }

        /// <summary>Two vertices per ribbon point, plus one four-vertex head quad per drawn trail.</summary>
        [Test]
        public void EmitProducesRibbonPlusOneHeadQuadPerTrail()
        {
            var runtime = Create(Settings());
            var rng = new RainRandom(4);
            runtime.Play(ref rng);

            var ctx = Context(1f / 60f);
            for (var i = 0; i < 60; i++)
            {
                runtime.Tick(ctx, ref rng);
            }

            runtime.Emit(ctx);

            var points = 0;
            var ribbons = 0;
            for (var slot = 0; slot < runtime.Capacity; slot++)
            {
                if (runtime.TrailActive(slot) && runtime.TrailPointCount(slot) >= 2)
                {
                    points += runtime.TrailPointCount(slot);
                    ribbons++;
                }
            }

            Assert.AreEqual(2 * points + TrailBuffer.HeadVertices * ribbons, runtime.Mesh.VertexCount);
            Assert.IsFalse(runtime.Mesh.Overflowed, "the mesh was sized from the same caps");
        }

        /// <summary>
        /// The head quad grew the mesh formula. An arithmetic slip there does not throw — the batch
        /// mesh sets <c>Overflowed</c> and hands back a scratch vertex — so the worst case has to be
        /// asserted: every trail alive at once, each holding a full point ring, at High quality.
        /// </summary>
        [Test]
        public void FullCapacityAtHighQualityDoesNotOverflow()
        {
            var settings = Settings();
            settings.MaxCount = 32;
            settings.MaxPoints = 96;
            settings.EmissionRateRange = new Vector2Int(200, 200);
            settings.LifetimeRange = new Vector2(30f, 30f);
            settings.PointSpacing = 0.004f;
            settings.AccelerationRange = new Vector2(2f, 2f);

            var runtime = Create(settings, RainQuality.High);
            Assert.AreEqual(32, runtime.Capacity);

            var rng = new RainRandom(6);
            runtime.Play(ref rng);

            var ctx = Context(1f / 60f);
            for (var i = 0; i < 600; i++)
            {
                runtime.Tick(ctx, ref rng);
            }

            Assert.AreEqual(runtime.Capacity, runtime.ActiveCount, "the layer never filled up");
            for (var slot = 0; slot < runtime.Capacity; slot++)
            {
                Assert.AreEqual(96, runtime.TrailPointCount(slot), $"slot {slot} did not fill its ring");
            }

            runtime.Emit(ctx);

            Assert.IsFalse(runtime.Mesh.Overflowed, "the mesh formula does not cover the head quads");
            Assert.AreEqual(2 * 96 * 32 + TrailBuffer.HeadVertices * 32, runtime.Mesh.VertexCount);
        }
    }
}
