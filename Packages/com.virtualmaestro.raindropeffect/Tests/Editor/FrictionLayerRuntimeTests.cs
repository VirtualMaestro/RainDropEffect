using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace RainDropEffect.Tests
{
    /// <summary>
    /// The Friction family: fall speed, steering toward the field, and the two degenerate cases
    /// (no field, trail at the edge of the view) that used to throw or wrap in the legacy version.
    /// </summary>
    public class FrictionLayerRuntimeTests
    {
        const string ShaderSetPath =
            "Packages/com.virtualmaestro.raindropeffect/Runtime/Shaders/RainShaderSet.asset";

        readonly List<LayerRuntime> created = new List<LayerRuntime>();
        readonly List<Object> assets = new List<Object>();

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

            foreach (var asset in assets)
            {
                Object.DestroyImmediate(asset);
            }

            assets.Clear();
        }

        FrictionLayerRuntime Create(FrictionLayerSettings settings, RainQuality quality = RainQuality.High)
        {
            var runtime = new FrictionLayerRuntime(settings, shaders, quality);
            created.Add(runtime);
            return runtime;
        }

        static FrictionLayerSettings Settings()
        {
            return new FrictionLayerSettings
            {
                Name = "friction",
                Duration = 1f,
                EmissionRateRange = new Vector2Int(60, 60),
                MaxCount = 1,
                LifetimeRange = new Vector2(10f, 10f),
                AccelerationRange = new Vector2(0.1f, 0.1f),
                InitialVelocity = 0.5f,
                WidthRange = new Vector2(0.1f, 0.1f),
                PointSpacing = 0.01f,
                MaxPoints = 64,
                ScanRate = 150f,
                LateralSamples = 5,
                LateralStepFactor = 1f
            };
        }

        static RainTickContext Context(float dt, float aspect = 1f)
        {
            return new RainTickContext
            {
                Dt = dt,
                Down = Vector2.down,
                Wind = Vector2.zero,
                Intensity = 1f,
                Aspect = aspect,
                Quality = RainQuality.High
            };
        }

        /// <summary>
        /// A field that gets steadily more attractive toward the right.
        ///
        /// The resolution matters: the scan only looks a couple of thousandths of a view width
        /// sideways per step, so a coarse field would put every candidate in the same cell, they
        /// would all tie, and the drop would correctly refuse to steer.
        /// </summary>
        FrictionField GradientField()
        {
            const int size = 512;
            var values = new byte[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    values[y * size + x] = (byte)(x * 255 / (size - 1));
                }
            }

            var field = ScriptableObject.CreateInstance<FrictionField>();
            field.Width = size;
            field.Height = size;
            field.Values = values;
            assets.Add(field);
            return field;
        }

        [Test]
        public void IterClampMatchesLegacyRange()
        {
            Assert.AreEqual(2, FrictionLayerRuntime.IterFor(150f, 0.001f), "the scan never runs fewer than 2 steps");
            Assert.AreEqual(5, FrictionLayerRuntime.IterFor(150f, 1f), "the scan never runs more than 5 steps");
            Assert.AreEqual(3, FrictionLayerRuntime.IterFor(150f, 1f / 60f));
        }

        [Test]
        public void StraightFallWithoutField()
        {
            var runtime = Create(Settings());
            var rng = new RainRandom(2);
            runtime.Play(ref rng);

            var ctx = Context(1f / 60f);
            runtime.Tick(ctx, ref rng);
            var start = runtime.TrailPosition(0);

            for (var i = 0; i < 60; i++)
            {
                runtime.Tick(ctx, ref rng);
            }

            var end = runtime.TrailPosition(0);
            Assert.AreEqual(start.x, end.x, 1e-4f, "with no field there is nothing to steer toward");
            Assert.Less(end.y, start.y, "the trail must fall");
        }

        [Test]
        public void FallSpeedMatchesLegacyEquivalent()
        {
            var runtime = Create(Settings());
            var rng = new RainRandom(2);
            runtime.Play(ref rng);

            var ctx = Context(1f / 60f);
            runtime.Tick(ctx, ref rng);
            var start = runtime.TrailPosition(0).y;

            for (var i = 0; i < 60; i++)
            {
                runtime.Tick(ctx, ref rng);
            }

            // The tick sums (0.1 t^2 + 0.5 t) * dt over ages 1/60 .. 61/60, a right-hand Riemann sum
            // of the closed form over that window, so it lands a couple of percent above it.
            const float t = 61f / 60f;
            var expected = 0.1f / 3f * t * t * t + 0.25f * t * t;

            var fallen = start - runtime.TrailPosition(0).y;
            Assert.AreEqual(expected, fallen, expected * 0.05f);
        }

        [Test]
        public void MovesTowardHigherScore()
        {
            var settings = Settings();
            settings.Field = GradientField();
            settings.LateralStepFactor = 2f;

            var runtime = Create(settings);
            var rng = new RainRandom(2);
            runtime.Play(ref rng);

            var ctx = Context(1f / 60f);
            runtime.Tick(ctx, ref rng);

            var startU = runtime.TrailPosition(0).x / ctx.Aspect * 0.5f + 0.5f;

            for (var i = 0; i < 120; i++)
            {
                runtime.Tick(ctx, ref rng);
            }

            var endU = runtime.TrailPosition(0).x / ctx.Aspect * 0.5f + 0.5f;
            Assert.Greater(endU, startU, "the trail should drift toward the attractive column");
        }

        /// <summary>
        /// A field that gets steadily more attractive toward the LEFT — the mirror of
        /// <see cref="GradientField"/>. A gradient rather than the two-value left/right split it
        /// stands in for: a flat half would make every candidate tie, and a tie is now answered by a
        /// random pick, so a step field would prove nothing about steering.
        /// </summary>
        FrictionField LeftAttractiveField()
        {
            const int size = 512;
            var values = new byte[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    values[y * size + x] = (byte)((size - 1 - x) * 255 / (size - 1));
                }
            }

            var field = ScriptableObject.CreateInstance<FrictionField>();
            field.Width = size;
            field.Height = size;
            field.Values = values;
            assets.Add(field);
            return field;
        }

        /// <summary>
        /// Per-cell pseudo-noise at the same 512² resolution as the shipped
        /// <c>FrictionField_friction_map</c>, which is also per-pixel noise. Deterministic, so the
        /// drift band below is reproducible.
        /// </summary>
        FrictionField NoiseField()
        {
            const int size = 512;
            var values = new byte[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    unchecked
                    {
                        var h = (uint)(x * 374761393 + y * 668265263);
                        h ^= h >> 13;
                        h *= 1274126177u;
                        values[y * size + x] = (byte)((h ^ (h >> 16)) & 0xFF);
                    }
                }
            }

            var field = ScriptableObject.CreateInstance<FrictionField>();
            field.Width = size;
            field.Height = size;
            field.Values = values;
            assets.Add(field);
            return field;
        }

        /// <summary>Rain5's friction layer: the shipped profile the fan width was tuned against.</summary>
        static FrictionLayerSettings Rain5LikeSettings()
        {
            var settings = Settings();
            settings.LifetimeRange = new Vector2(2f, 2f);
            settings.AccelerationRange = new Vector2(0.13772935f, 0.24415655f);
            settings.InitialVelocity = 1.0392306f;
            settings.PointSpacing = 0.008f;
            return settings;
        }

        /// <summary>
        /// Median absolute lateral drift over one on-screen second, across <paramref name="seeds"/>
        /// independent trails.
        /// </summary>
        float MedianDrift(FrictionField field, float dt, int seeds)
        {
            var drifts = new List<float>(seeds);
            var ctx = Context(dt, 16f / 9f);
            var steps = Mathf.RoundToInt(1f / dt);

            for (var seed = 1; seed <= seeds; seed++)
            {
                var settings = Rain5LikeSettings();
                settings.Field = field;

                var runtime = Create(settings);
                var rng = new RainRandom(seed);
                runtime.Play(ref rng);

                // The emitter interval is one 60 Hz frame, so at 120 Hz the first tick spawns
                // nothing and slot 0 still reads as the zero vector. Wait for the trail before
                // taking the start position, or the "drift" is just the spawn abscissa.
                for (var i = 0; i < steps && !runtime.TrailActive(0); i++)
                {
                    runtime.Tick(ctx, ref rng);
                }

                Assert.IsTrue(runtime.TrailActive(0), $"seed {seed} never spawned a trail at dt={dt:F4}");
                var startX = runtime.TrailPosition(0).x;

                for (var i = 1; i < steps && runtime.TrailPosition(0).y > -1.2f; i++)
                {
                    runtime.Tick(ctx, ref rng);
                }

                drifts.Add(Mathf.Abs(runtime.TrailPosition(0).x - startX));
            }

            drifts.Sort();
            var mid = drifts.Count / 2;
            return drifts.Count % 2 == 1 ? drifts[mid] : 0.5f * (drifts[mid - 1] + drifts[mid]);
        }

        [Test]
        public void SteersLeftAtTheShippedFanWidth()
        {
            var settings = Settings();
            settings.Field = LeftAttractiveField();

            var runtime = Create(settings);
            var rng = new RainRandom(7);
            runtime.Play(ref rng);

            var ctx = Context(1f / 60f, 16f / 9f);
            runtime.Tick(ctx, ref rng);
            var startX = runtime.TrailPosition(0).x;

            for (var i = 0; i < 120; i++)
            {
                runtime.Tick(ctx, ref rng);
            }

            var endX = runtime.TrailPosition(0).x;

            // "Measurably", not "at all": the pre-fix fan was a third of a field texel wide, so
            // every candidate tied and the drop fell on a plumb line to within 1e-4.
            Assert.Less(endX, startX - 0.01f, "the trail should steer toward the attractive side");
        }

        /// <summary>
        /// The fan is a per-frame quantity, so the scan's reach per second still varies with the
        /// frame rate through the 2..5 iteration clamp. That residual spread is inherent to a random
        /// walk over a noise field; what must hold at every frame rate is that the trail meanders at
        /// all and never slides across the frame.
        ///
        /// Reference medians from a replica of this fixture (40 seeds, 16:9, Rain5 constants):
        /// <code>
        /// fan = distance * 1.0      (shipped)   30 fps 0.195   60 fps 0.060   120 fps 0.049
        /// fan = distance * 0.375                30 fps 0.042   60 fps 0.034   120 fps 0.015
        /// fan = stepLength * 0.375  (the 2.0.0 defect)  30 fps 0.024   60 fps 0.013   120 fps 0.012
        /// </code>
        /// A path-equality assertion is unsatisfiable here: the scan draws from rng per iteration
        /// per frame, so a different dt means a different draw count.
        /// </summary>
        [Test]
        public void LateralDriftStaysInBandAcrossFrameRates()
        {
            var field = NoiseField();

            foreach (var dt in new[] { 1f / 30f, 1f / 60f, 1f / 120f })
            {
                var median = MedianDrift(field, dt, 40);

                Assert.Greater(median, 0.015f,
                    $"at dt={dt:F4} the trail barely meanders — the fan is too narrow to see the field");
                Assert.Less(median, 0.5f,
                    $"at dt={dt:F4} the trail slides across the frame instead of wandering");
            }
        }

        [Test]
        public void EqualScoresTieBreakDeterministic()
        {
            var a = Create(Settings());
            var b = Create(Settings());

            var rngA = new RainRandom(13);
            var rngB = new RainRandom(13);
            a.Play(ref rngA);
            b.Play(ref rngB);

            var ctx = Context(1f / 60f);
            for (var i = 0; i < 120; i++)
            {
                a.Tick(ctx, ref rngA);
                b.Tick(ctx, ref rngB);
            }

            Assert.AreEqual(a.TrailPosition(0), b.TrailPosition(0), "a uniform field must still be reproducible");
        }

        [Test]
        public void BorderClampDoesNotThrow()
        {
            var settings = Settings();
            settings.Field = GradientField();
            settings.SpawnOffsetY = 1f;

            var runtime = Create(settings);
            var rng = new RainRandom(2);
            runtime.Play(ref rng);

            var ctx = Context(1f / 60f);
            Assert.DoesNotThrow(() =>
            {
                for (var i = 0; i < 120; i++)
                {
                    runtime.Tick(ctx, ref rng);
                    runtime.Emit(ctx);
                }
            });

            var pos = runtime.TrailPosition(0);
            Assert.IsFalse(float.IsNaN(pos.x) || float.IsNaN(pos.y), "the trail position went non-finite");
        }

        [Test]
        public void ClearEmptiesBuffers()
        {
            var runtime = Create(Settings());
            var rng = new RainRandom(2);
            runtime.Play(ref rng);

            var ctx = Context(1f / 60f);
            for (var i = 0; i < 60; i++)
            {
                runtime.Tick(ctx, ref rng);
            }

            runtime.Clear();
            runtime.Emit(ctx);

            Assert.AreEqual(0, runtime.ActiveCount);
            Assert.AreEqual(0, runtime.TrailPointCount(0));
            Assert.AreEqual(0, runtime.Mesh.VertexCount);
        }
    }
}
