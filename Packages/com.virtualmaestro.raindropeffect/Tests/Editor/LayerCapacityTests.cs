using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace RainDropEffect.Tests
{
    /// <summary>
    /// The mesh capacity a layer allocates is arithmetic that used to exist twice: once in each
    /// runtime's base constructor call, and once in <see cref="LayerRuntimeFactory"/>. Two copies of
    /// one formula drift, and this one did — the factory never learned about the per-trail head quad
    /// added in 2.0.1, and never applied the runtime's own clamps either.
    ///
    /// A wrong capacity does not throw. <c>RainBatchMesh.AddVertex</c> sets <c>Overflowed</c> and
    /// hands back a scratch vertex, so an undersized mesh silently drops geometry. These tests are
    /// the guard that keeps the factory and the runtimes agreeing.
    /// </summary>
    public class LayerCapacityTests
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

        T Track<T>(T runtime) where T : LayerRuntime
        {
            created.Add(runtime);
            return runtime;
        }

        static readonly RainQuality[] Tiers =
        {
            RainQuality.Low, RainQuality.Balanced, RainQuality.High
        };

        /// <summary>Includes values outside the runtimes' own clamps, which is where the two copies differed.</summary>
        static readonly int[] Counts = { 0, 1, 5, 30, 500 };

        static readonly int[] Points = { 2, 4, 40, 64, 400 };

        [Test]
        public void StaticCapacityMatchesWhatTheRuntimeAllocates()
        {
            foreach (var quality in Tiers)
            {
                var runtime = Track(new StaticLayerRuntime(
                    new StaticLayerSettings { Name = "static" }, shaders, quality));

                LayerRuntimeFactory.StaticCapacity(out var vertices, out var indices);

                Assert.AreEqual(runtime.Mesh.VertexCapacity, vertices, $"vertices at {quality}");
                Assert.AreEqual(runtime.Mesh.IndexCapacity, indices, $"indices at {quality}");
            }
        }

        [Test]
        public void DropCapacityMatchesWhatTheRuntimeAllocates()
        {
            foreach (var quality in Tiers)
            {
                foreach (var count in Counts)
                {
                    var settings = new SimpleLayerSettings { Name = "simple", MaxCount = count };
                    var runtime = Track(new SimpleLayerRuntime(settings, shaders, quality));

                    LayerRuntimeFactory.DropCapacity(count, quality, out var vertices, out var indices);

                    Assert.AreEqual(runtime.Mesh.VertexCapacity, vertices,
                        $"vertices at {quality}, MaxCount {count}");
                    Assert.AreEqual(runtime.Mesh.IndexCapacity, indices,
                        $"indices at {quality}, MaxCount {count}");
                }
            }
        }

        [Test]
        public void TrailCapacityMatchesWhatTheFlowRuntimeAllocates()
        {
            foreach (var quality in Tiers)
            {
                foreach (var count in Counts)
                {
                    foreach (var points in Points)
                    {
                        var settings = new FlowLayerSettings
                        {
                            Name = "flow", MaxCount = count, MaxPoints = points
                        };
                        var runtime = Track(new FlowLayerRuntime(settings, shaders, quality));

                        LayerRuntimeFactory.TrailCapacity(count, points, quality,
                            out var vertices, out var indices);

                        Assert.AreEqual(runtime.Mesh.VertexCapacity, vertices,
                            $"vertices at {quality}, MaxCount {count}, MaxPoints {points}");
                        Assert.AreEqual(runtime.Mesh.IndexCapacity, indices,
                            $"indices at {quality}, MaxCount {count}, MaxPoints {points}");
                    }
                }
            }
        }

        [Test]
        public void TrailCapacityMatchesWhatTheFrictionRuntimeAllocates()
        {
            foreach (var quality in Tiers)
            {
                foreach (var count in Counts)
                {
                    foreach (var points in Points)
                    {
                        var settings = new FrictionLayerSettings
                        {
                            Name = "friction", MaxCount = count, MaxPoints = points
                        };
                        var runtime = Track(new FrictionLayerRuntime(settings, shaders, quality));

                        LayerRuntimeFactory.TrailCapacity(count, points, quality,
                            out var vertices, out var indices);

                        Assert.AreEqual(runtime.Mesh.VertexCapacity, vertices,
                            $"vertices at {quality}, MaxCount {count}, MaxPoints {points}");
                        Assert.AreEqual(runtime.Mesh.IndexCapacity, indices,
                            $"indices at {quality}, MaxCount {count}, MaxPoints {points}");
                    }
                }
            }
        }

        /// <summary>
        /// The reason the arithmetic has to be right: a full ribbon plus its head quad must fit.
        /// The Flow side of this is covered in FlowLayerRuntimeTests; Friction shares the formula and
        /// the head quad, and had no equivalent guard.
        /// </summary>
        [Test]
        public void FrictionFullCapacityAtHighQualityDoesNotOverflow()
        {
            var settings = new FrictionLayerSettings
            {
                Name = "friction",
                Duration = 1f,
                EmissionRateRange = new Vector2Int(200, 200),
                MaxCount = 32,
                MaxPoints = 96,
                LifetimeRange = new Vector2(30f, 30f),
                AccelerationRange = new Vector2(2f, 2f),
                InitialVelocity = 0.5f,
                WidthRange = new Vector2(0.1f, 0.1f),
                PointSpacing = 0.004f,
                ScanRate = 150f,
                LateralSamples = 5,
                LateralStepFactor = 1f
            };

            var runtime = Track(new FrictionLayerRuntime(settings, shaders, RainQuality.High));
            Assert.AreEqual(32, runtime.Capacity);

            var rng = new RainRandom(6);
            runtime.Play(ref rng);

            var ctx = new RainTickContext
            {
                Dt = 1f / 60f,
                Down = Vector2.down,
                Wind = Vector2.zero,
                Intensity = 1f,
                Aspect = 16f / 9f,
                Quality = RainQuality.High
            };

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
