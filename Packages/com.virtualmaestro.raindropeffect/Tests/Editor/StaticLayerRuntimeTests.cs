using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace RainDropEffect.Tests
{
    /// <summary>
    /// The Static family: a single quad whose whole state is where it is in its fade, plus the
    /// sized-quad placement formula the migration matrix specifies.
    /// </summary>
    public class StaticLayerRuntimeTests
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
            rng = new RainRandom(1);
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

        StaticLayerRuntime Create(StaticLayerSettings settings)
        {
            var runtime = new StaticLayerRuntime(settings, shaders, RainQuality.High);
            created.Add(runtime);
            return runtime;
        }

        static RainTickContext Context(float dt, float aspect = 1f)
        {
            return new RainTickContext
            {
                Dt = dt,
                Down = Vector2.down,
                Intensity = 1f,
                Aspect = aspect,
                Quality = RainQuality.High
            };
        }

        /// <summary>Opacity is written into vertex z by <c>AddQuad</c>.</summary>
        static float Opacity(RainBatchMesh mesh) => mesh.GetVertex(0).Position.z;

        [Test]
        public void FadesInOverFadeTime()
        {
            var runtime = Create(new StaticLayerSettings { Name = "fade", FadeTime = 1f });
            runtime.Play(ref rng);

            var ctx = Context(0.1f);
            for (var i = 0; i < 5; i++)
            {
                runtime.Tick(ctx, ref rng);
            }

            runtime.Emit(ctx);
            var halfway = Opacity(runtime.Mesh);
            Assert.That(halfway, Is.InRange(0.3f, 0.7f), "the fade should be around half after half the fade time");

            for (var i = 0; i < 10; i++)
            {
                runtime.Tick(ctx, ref rng);
            }

            runtime.Emit(ctx);
            Assert.AreEqual(1f, Opacity(runtime.Mesh), 1e-3f, "the fade should be complete");
        }

        [Test]
        public void StopFadesOutThenInvisible()
        {
            var runtime = Create(new StaticLayerSettings { Name = "fade", FadeTime = 1f });
            runtime.Play(ref rng);

            var ctx = Context(0.1f);
            for (var i = 0; i < 15; i++)
            {
                runtime.Tick(ctx, ref rng);
            }

            runtime.Stop();
            runtime.Tick(ctx, ref rng);
            runtime.Emit(ctx);
            Assert.IsTrue(runtime.Visible, "a stopped layer keeps drawing while it fades out");

            for (var i = 0; i < 15; i++)
            {
                runtime.Tick(ctx, ref rng);
            }

            runtime.Emit(ctx);
            Assert.IsFalse(runtime.Visible, "the layer must disappear once the fade reaches zero");
            Assert.IsFalse(runtime.IsPlaying);
        }

        [Test]
        public void ClearIsImmediate()
        {
            var runtime = Create(new StaticLayerSettings { Name = "fade", FadeTime = 5f });
            runtime.Play(ref rng);

            var ctx = Context(0.1f);
            for (var i = 0; i < 30; i++)
            {
                runtime.Tick(ctx, ref rng);
            }

            runtime.Clear();
            runtime.Emit(ctx);

            Assert.IsFalse(runtime.Visible);
            Assert.IsFalse(runtime.IsPlaying);
            Assert.AreEqual(0, runtime.Mesh.VertexCount);
        }

        [Test]
        public void FadeTimeZeroIsInstant()
        {
            var runtime = Create(new StaticLayerSettings { Name = "instant", FadeTime = 0f });

            var ctx = Context(1f / 60f);
            runtime.Tick(ctx, ref rng);
            runtime.Emit(ctx);
            Assert.IsFalse(runtime.Visible, "nothing should draw before Play");

            runtime.Play(ref rng);
            runtime.Tick(ctx, ref rng);
            runtime.Emit(ctx);
            Assert.IsTrue(runtime.Visible);
            Assert.AreEqual(1f, Opacity(runtime.Mesh), 1e-3f);

            runtime.Stop();
            runtime.Tick(ctx, ref rng);
            runtime.Emit(ctx);
            Assert.IsFalse(runtime.Visible, "with no fade time the layer must vanish at once");
        }

        [Test]
        public void SizedQuadCenterFormula()
        {
            var runtime = Create(new StaticLayerSettings
            {
                Name = "sized",
                FadeTime = 0f,
                FullScreen = false,
                Size = new Vector2(0.5f, 0.5f),
                Offset = new Vector2(0.25f, -0.5f)
            });

            runtime.Play(ref rng);

            var ctx = Context(1f / 60f, aspect: 2f);
            runtime.Tick(ctx, ref rng);
            runtime.Emit(ctx);

            // Centroid of the four corners: (-2 * 0.25 * 2, -2 * -0.5) = (-1, 1).
            var centroid = Vector2.zero;
            for (var i = 0; i < 4; i++)
            {
                var p = runtime.Mesh.GetVertex(i).Position;
                centroid += new Vector2(p.x, p.y);
            }

            centroid /= 4f;

            Assert.AreEqual(-1f, centroid.x, 1e-4f);
            Assert.AreEqual(1f, centroid.y, 1e-4f);
        }

        [Test]
        public void FullScreenQuadCoversTheView()
        {
            var runtime = Create(new StaticLayerSettings { Name = "full", FadeTime = 0f, FullScreen = true });
            runtime.Play(ref rng);

            var ctx = Context(1f / 60f, aspect: 2f);
            runtime.Tick(ctx, ref rng);
            runtime.Emit(ctx);

            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            for (var i = 0; i < 4; i++)
            {
                var p = runtime.Mesh.GetVertex(i).Position;
                min = Vector2.Min(min, new Vector2(p.x, p.y));
                max = Vector2.Max(max, new Vector2(p.x, p.y));
            }

            Assert.AreEqual(new Vector2(-2f, -1f), min);
            Assert.AreEqual(new Vector2(2f, 1f), max);
        }
    }
}
