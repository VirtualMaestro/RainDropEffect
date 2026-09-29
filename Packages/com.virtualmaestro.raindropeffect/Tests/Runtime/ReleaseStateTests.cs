using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.TestTools;

namespace RainDropEffect.Tests
{
    /// <summary>
    /// Task 30's State and Ownership sections. These are behaviour checks, not pixel checks: what
    /// matters is that <c>Delay</c>, <c>Clear</c>, <c>Intensity</c>, time scale and the delta-time
    /// clamp do exactly what D12 and D13 say, and that nothing grows across a hundred cycles.
    /// </summary>
    public class ReleaseStateTests
    {
        const string State = "State";
        const string Ownership = "Ownership";

        GameObject host;
        RainProfile profile;
        RainEffect effect;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("ReleaseStateCamera", typeof(Camera));
            profile = RainTestProfiles.CreateBasic();
            effect = RainTestProfiles.CreateEffect(host, profile);
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;

            if (host != null) Object.DestroyImmediate(host);
            if (profile != null) Object.DestroyImmediate(profile);
        }

        // ------------------------------------------------------------------ State

        [UnityTest]
        public IEnumerator ClearDuringDelayNeverSpawns()
        {
            // D12: Clear() removes pending delays too, so a Play() that is still counting down is
            // cancelled rather than merely paused.
            SetDelay(2f);
            effect.Rebuild();
            yield return null;

            effect.Play();
            Tick(60);
            effect.Clear();
            Tick(180);

            effect.EmitForTest();
            Assert.AreEqual(0, effect.RenderData.Batches.Count,
                "Clear() during the delay must cancel the pending emission");

            ReleaseMatrix.Record(State, "Delay 2 s then Clear at 1 s never spawns",
                nameof(ClearDuringDelayNeverSpawns), "0 batches after 3 further seconds");
        }

        [UnityTest]
        public IEnumerator RestartDuringDelayIsIgnored()
        {
            // D12: Play() during Delay is ignored, so a button mashed twice does not double-start.
            SetDelay(2f);
            effect.Rebuild();
            yield return null;

            // Every tick is a real frame: RainEffect clamps any delta above 0.1 s, so a single
            // TickForTest(1.5f) would advance the delay by 0.1 s, not 1.5 s.
            effect.Play();
            Tick(30);
            effect.Play();
            Tick(30);

            effect.EmitForTest();
            Assert.AreEqual(0, effect.RenderData.Batches.Count,
                "one second into a two-second delay nothing may be visible");

            Tick(90);

            effect.EmitForTest();
            Assert.Greater(effect.RenderData.Batches.Count, 0, "after the delay the layer must emit");

            ReleaseMatrix.Record(State, "Play() during the delay is ignored",
                nameof(RestartDuringDelayIsIgnored), "nothing at 1 s, batches present after 2 s");
        }

        [UnityTest]
        public IEnumerator ZeroIntensityProducesNoBatches()
        {
            // NFR-02: nothing visible must cost nothing, which starts with producing no geometry.
            yield return null;

            effect.Play();
            for (var i = 0; i < 60; i++)
            {
                effect.TickForTest(1f / 60f);
            }

            effect.EmitForTest();
            Assert.Greater(effect.RenderData.Batches.Count, 0, "the effect must be visible first");

            // Intensity is read into the tick context, so it applies from the next tick on.
            effect.Intensity = 0f;
            effect.TickForTest(1f / 60f);
            effect.EmitForTest();
            Assert.AreEqual(0, effect.RenderData.Batches.Count, "Intensity 0 must produce no batches");

            ReleaseMatrix.Record(State, "Intensity 0 produces no passes",
                nameof(ZeroIntensityProducesNoBatches), "batch count drops to 0");
        }

        [UnityTest]
        public IEnumerator TimeScaleZeroFreezesTheSimulation()
        {
            // D13: the simulation runs on scaled time, so timeScale is the pause switch and the
            // slow-motion knob a game already has.
            yield return null;

            effect.Play();
            for (var i = 0; i < 60; i++)
            {
                effect.TickForTest(1f / 60f);
            }

            var frozen = Sample();
            for (var i = 0; i < 30; i++)
            {
                effect.TickForTest(0f);
            }

            Assert.AreEqual(frozen, Sample(), "dt 0 must not advance the simulation");

            var before = Sample();
            effect.TickForTest(0.5f);
            Assert.AreNotEqual(before, Sample(), "a non-zero dt must advance it");

            ReleaseMatrix.Record(State, "timeScale 0 freezes, non-zero advances",
                nameof(TimeScaleZeroFreezesTheSimulation), "identical sample across 30 zero-length ticks");
        }

        [UnityTest]
        public IEnumerator LongFrameIsClampedToOneTenthOfASecond()
        {
            // D13: a tab that was in the background for three seconds must not deliver three
            // seconds of rain in one frame. RainEffect clamps dt to 0.1 s.
            yield return null;

            effect.Play();
            for (var i = 0; i < 60; i++)
            {
                effect.TickForTest(1f / 60f);
            }

            var clamped = Advance(3f);
            var reference = Advance(0.1f);

            Assert.AreEqual(reference, clamped,
                "a 3 s frame must advance the simulation by the same 0.1 s as a 0.1 s frame");

            ReleaseMatrix.Record(State, "Long frame clamped to 0.1 s",
                nameof(LongFrameIsClampedToOneTenthOfASecond),
                "a 3 s delta advances the state exactly as a 0.1 s delta does");
        }

        // ------------------------------------------------------------------ Ownership

        [UnityTest]
        public IEnumerator HundredPlayStopClearCyclesDoNotGrow()
        {
            yield return null;

            effect.Play();
            for (var i = 0; i < 30; i++)
            {
                effect.TickForTest(1f / 60f);
            }

            System.GC.Collect();
            var before = Profiler.GetTotalAllocatedMemoryLong();
            var meshesBefore = Resources.FindObjectsOfTypeAll<Mesh>().Length;

            for (var cycle = 0; cycle < 100; cycle++)
            {
                effect.Play();
                effect.TickForTest(1f / 60f);
                effect.Stop();
                effect.Clear();
            }

            System.GC.Collect();
            var after = Profiler.GetTotalAllocatedMemoryLong();
            var meshesAfter = Resources.FindObjectsOfTypeAll<Mesh>().Length;

            Assert.AreEqual(meshesBefore, meshesAfter, "a play/stop/clear cycle must create no mesh");
            Assert.Less(after - before, 1024 * 1024,
                $"100 cycles grew allocated memory by {after - before} B");

            ReleaseMatrix.Record(Ownership, "100 play/stop/clear cycles do not grow",
                nameof(HundredPlayStopClearCyclesDoNotGrow),
                $"meshes {meshesBefore} -> {meshesAfter}, allocated delta {after - before} B");
        }

        [UnityTest]
        public IEnumerator RepeatedRebuildDoesNotGrowResources()
        {
            yield return null;

            var meshesBefore = Resources.FindObjectsOfTypeAll<Mesh>().Length;
            var materialsBefore = Resources.FindObjectsOfTypeAll<Material>().Length;

            for (var i = 0; i < 20; i++)
            {
                effect.Quality = (RainQuality)(i % 3);
                effect.Rebuild();
                effect.TickForTest(1f / 60f);
            }

            Resources.UnloadUnusedAssets();
            yield return null;

            var meshesAfter = Resources.FindObjectsOfTypeAll<Mesh>().Length;
            var materialsAfter = Resources.FindObjectsOfTypeAll<Material>().Length;

            Assert.LessOrEqual(meshesAfter - meshesBefore, 1, $"meshes {meshesBefore} -> {meshesAfter}");
            Assert.LessOrEqual(materialsAfter - materialsBefore, 1,
                $"materials {materialsBefore} -> {materialsAfter}");

            ReleaseMatrix.Record(Ownership, "20 rebuilds across all quality tiers do not grow resources",
                nameof(RepeatedRebuildDoesNotGrowResources),
                $"meshes {meshesBefore} -> {meshesAfter}, materials {materialsBefore} -> {materialsAfter}");
        }

        [UnityTest]
        public IEnumerator DisableAndDestroyReleaseTheRegistry()
        {
            yield return null;

            var camera = host.GetComponent<Camera>();
            Assert.IsTrue(RainEffect.TryGet(camera, out var registered) && registered.Count == 1);

            effect.enabled = false;
            yield return null;
            Assert.IsFalse(RainEffect.TryGet(camera, out var afterDisable) && afterDisable.Count > 0,
                "a disabled effect must leave the registry");

            effect.enabled = true;
            yield return null;
            Assert.IsTrue(RainEffect.TryGet(camera, out var afterEnable) && afterEnable.Count == 1,
                "re-enabling must register once, not twice");

            Object.DestroyImmediate(effect);
            effect = null;
            yield return null;
            Assert.IsFalse(RainEffect.TryGet(camera, out var afterDestroy) && afterDestroy.Count > 0,
                "a destroyed effect must leave the registry");

            ReleaseMatrix.Record(Ownership, "Disable and destroy release the camera registry",
                nameof(DisableAndDestroyReleaseTheRegistry), "registry count 1 -> 0 -> 1 -> 0");
        }

        [UnityTest]
        public IEnumerator ResizingTheTargetDoesNotGrowResources()
        {
            // A resized game view is a resized camera target; the effect must not react by
            // allocating a new mesh or material per size.
            yield return null;

            var camera = host.GetComponent<Camera>();
            effect.Play();

            var meshesBefore = Resources.FindObjectsOfTypeAll<Mesh>().Length;

            foreach (var size in new[] { 128, 256, 320, 200, 512 })
            {
                var rt = new RenderTexture(size, Mathf.Max(64, size / 2), 24);
                camera.targetTexture = rt;
                effect.TickForTest(1f / 60f);
                effect.EmitForTest();
                camera.targetTexture = null;
                rt.Release();
                Object.DestroyImmediate(rt);
            }

            var meshesAfter = Resources.FindObjectsOfTypeAll<Mesh>().Length;
            Assert.AreEqual(meshesBefore, meshesAfter, "resizing must not create meshes");

            ReleaseMatrix.Record(Ownership, "Five target resizes create no resources",
                nameof(ResizingTheTargetDoesNotGrowResources), $"meshes stayed at {meshesAfter}");
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>Advances the simulation by <paramref name="frames"/> frames at 60 Hz.</summary>
        void Tick(int frames)
        {
            for (var i = 0; i < frames; i++)
            {
                effect.TickForTest(1f / 60f);
            }
        }

        /// <summary>
        /// Puts the emitter behind a delay and disables the static layer, which has no Delay of its
        /// own and would otherwise be visible from frame one and mask what the test is watching.
        /// </summary>
        void SetDelay(float delay)
        {
            foreach (var layer in profile.SimpleLayers)
            {
                layer.Delay = delay;
                layer.AutoStart = false;
            }

            foreach (var layer in profile.StaticLayers)
            {
                layer.Enabled = false;
            }
        }

        /// <summary>
        /// A hash of every emitted vertex position. Batch and vertex counts are too coarse: a drop
        /// that moved half a screen without spawning or dying leaves both unchanged.
        /// </summary>
        int Sample()
        {
            effect.EmitForTest();
            var hash = 17;

            foreach (var batch in effect.RenderData.Batches)
            {
                if (batch.Mesh == null)
                {
                    continue;
                }

                foreach (var vertex in batch.Mesh.vertices)
                {
                    hash = hash * 31 + vertex.GetHashCode();
                }
            }

            return hash;
        }

        /// <summary>Advances a fresh copy of the state by <paramref name="dt"/> and samples it.</summary>
        int Advance(float dt)
        {
            effect.Seed = 4242;
            effect.Rebuild();
            effect.Play();

            for (var i = 0; i < 60; i++)
            {
                effect.TickForTest(1f / 60f);
            }

            effect.TickForTest(dt);
            return Sample();
        }
    }
}
