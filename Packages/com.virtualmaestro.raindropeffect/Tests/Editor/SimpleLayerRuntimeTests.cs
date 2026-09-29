using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace RainDropEffect.Tests
{
    /// <summary>
    /// The Simple family: capacity, lifetime, playback transitions, determinism, and the drift sign
    /// that decides whether drops fall or rise.
    /// </summary>
    public class SimpleLayerRuntimeTests
    {
        const string ShaderSetPath =
            "Packages/com.virtualmaestro.raindropeffect/Runtime/Shaders/RainShaderSet.asset";

        readonly List<LayerRuntime> created = new List<LayerRuntime>();

        RainShaderSet shaders;
        RainRandom rng;

        [SetUp]
        public void SetUp()
        {
            shaders = AssetDatabase.LoadAssetAtPath<RainShaderSet>(ShaderSetPath);
            Assert.IsNotNull(shaders, "the shipped RainShaderSet is missing");
            rng = new RainRandom(7);
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

        SimpleLayerRuntime Create(SimpleLayerSettings settings, RainQuality quality = RainQuality.High)
        {
            var runtime = new SimpleLayerRuntime(settings, shaders, quality);
            created.Add(runtime);
            return runtime;
        }

        static SimpleLayerSettings Settings()
        {
            return new SimpleLayerSettings
            {
                Name = "test",
                Duration = 1f,
                EmissionRateRange = new Vector2Int(60, 60),
                MaxCount = 5,
                LifetimeRange = new Vector2(0.5f, 0.5f)
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
        public void SpawnsUpToCapacityAndNoMore()
        {
            var runtime = Create(Settings());
            runtime.Play(ref rng);

            var ctx = Context(1f / 60f);
            for (var i = 0; i < 100; i++)
            {
                runtime.Tick(ctx, ref rng);
                Assert.LessOrEqual(runtime.ActiveCount, 5, $"capacity exceeded at tick {i}");
            }

            Assert.Greater(runtime.ActiveCount, 0, "nothing spawned at all");
        }

        [Test]
        public void DropsExpireByLifetime()
        {
            var runtime = Create(Settings());
            runtime.Play(ref rng);

            var ctx = Context(1f / 60f);
            for (var i = 0; i < 30; i++)
            {
                runtime.Tick(ctx, ref rng);
            }

            Assert.Greater(runtime.ActiveCount, 0);

            runtime.Stop();
            for (var i = 0; i < 40; i++)
            {
                runtime.Tick(ctx, ref rng);
            }

            Assert.AreEqual(0, runtime.ActiveCount, "drops outlived their 0.5 s lifetime");
        }

        [Test]
        public void StopDrainsThenIdle()
        {
            var runtime = Create(Settings());
            runtime.Play(ref rng);

            var ctx = Context(1f / 60f);
            for (var i = 0; i < 30; i++)
            {
                runtime.Tick(ctx, ref rng);
            }

            runtime.Stop();
            Assert.IsFalse(runtime.IsEmitting, "Stop must end emission at once");

            for (var i = 0; i < 60; i++)
            {
                runtime.Tick(ctx, ref rng);
            }

            Assert.IsFalse(runtime.IsPlaying, "the layer must go idle once the last drop expires");
        }

        [Test]
        public void ClearRemovesEverything()
        {
            var runtime = Create(Settings());
            runtime.Play(ref rng);

            var ctx = Context(1f / 60f);
            for (var i = 0; i < 30; i++)
            {
                runtime.Tick(ctx, ref rng);
            }

            runtime.Clear();
            runtime.Emit(ctx);

            Assert.AreEqual(0, runtime.ActiveCount);
            Assert.IsFalse(runtime.IsPlaying);
            Assert.AreEqual(0, runtime.Mesh.VertexCount);
        }

        [Test]
        public void DeterministicWithSeed()
        {
            var a = Create(Settings());
            var b = Create(Settings());

            var rngA = new RainRandom(7);
            var rngB = new RainRandom(7);
            a.Play(ref rngA);
            b.Play(ref rngB);

            var ctx = Context(1f / 60f);
            for (var i = 0; i < 60; i++)
            {
                a.Tick(ctx, ref rngA);
                b.Tick(ctx, ref rngB);
            }

            Assert.AreEqual(a.ActiveCount, b.ActiveCount);
            for (var slot = 0; slot < a.Capacity; slot++)
            {
                Assert.AreEqual(a.DropActive(slot), b.DropActive(slot), $"slot {slot} differs in activity");
                if (a.DropActive(slot))
                {
                    Assert.AreEqual(a.DropPosition(slot), b.DropPosition(slot), $"slot {slot} differs in position");
                }
            }
        }

        [Test]
        public void MotionFollowsGravityAndDeltaTime()
        {
            // The same 0.5 s of simulated time in two different step sizes must move a drop the
            // same distance; anything else means the motion is per-frame rather than per-second.
            var coarse = Fall(1f / 30f, 15);
            var fine = Fall(1f / 120f, 60);

            Assert.AreEqual(-0.5f, coarse, 1e-3f);
            Assert.AreEqual(-0.5f, fine, 1e-3f);
        }

        [Test]
        public void PositiveCurveDriftsAgainstGravity()
        {
            // Pins the legacy sign: a positive drift curve moves the drop away from gravity.
            Assert.Greater(Fall(1f / 60f, 30, driftValue: 1f), 0f);
        }

        /// <summary>Runs one drop with a fixed drift and returns how far it moved on y.</summary>
        float Fall(float dt, int steps, float driftValue = -1f)
        {
            var settings = Settings();
            settings.MaxCount = 1;
            settings.EmissionRateRange = new Vector2Int(1000, 1000);
            settings.LifetimeRange = new Vector2(100f, 100f);
            settings.DriftSpeed = 1f;
            settings.DriftOverLifetime = AnimationCurve.Constant(0f, 1f, driftValue);

            var runtime = Create(settings);
            var local = new RainRandom(3);
            runtime.Play(ref local);

            var ctx = Context(dt);
            runtime.Tick(ctx, ref local);
            Assert.AreEqual(1, runtime.ActiveCount, "the test needs exactly one drop");
            var start = runtime.DropPosition(0);

            for (var i = 0; i < steps; i++)
            {
                runtime.Tick(ctx, ref local);
            }

            return runtime.DropPosition(0).y - start.y;
        }

        [Test]
        public void EmitProducesFourVerticesPerActiveDrop()
        {
            var runtime = Create(Settings());
            runtime.Play(ref rng);

            var ctx = Context(1f / 60f);
            for (var i = 0; i < 20; i++)
            {
                runtime.Tick(ctx, ref rng);
            }

            runtime.Emit(ctx);

            Assert.AreEqual(4 * runtime.ActiveCount, runtime.Mesh.VertexCount);
        }

        [Test]
        public void IntensityZeroEmitsNothing()
        {
            var runtime = Create(Settings());
            runtime.Play(ref rng);

            var ctx = Context(1f / 60f);
            for (var i = 0; i < 20; i++)
            {
                runtime.Tick(ctx, ref rng);
            }

            var zero = Context(1f / 60f);
            zero.Intensity = 0f;
            runtime.Emit(zero);

            Assert.AreEqual(0, runtime.Mesh.VertexCount);
            Assert.IsFalse(runtime.Visible);
        }

        [Test]
        public void QualityCapsTheDropCount()
        {
            var settings = Settings();
            settings.MaxCount = 256;

            var low = Create(settings, RainQuality.Low);

            Assert.AreEqual(RainQualityCaps.MaxDrops(RainQuality.Low), low.Capacity);
        }
    }
}
