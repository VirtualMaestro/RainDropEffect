using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RainDropEffect.Tests
{
    /// <summary>
    /// The playback contract and the promise that playing does not slowly leak meshes or materials.
    ///
    /// Each layer owns one mesh and one material, so a cycle that forgets to dispose shows up as a
    /// steadily growing object count rather than as a visible bug — which is why these count
    /// objects instead of looking at the screen.
    /// </summary>
    public class RainEffectLifecycleTests
    {
        GameObject host;
        RainProfile profile;
        RainEffect effect;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("RainLifecycleCamera", typeof(Camera));
            profile = RainTestProfiles.CreateBasic();
            effect = RainTestProfiles.CreateEffect(host, profile);
        }

        [TearDown]
        public void TearDown()
        {
            if (host != null)
            {
                Object.DestroyImmediate(host);
            }

            if (profile != null)
            {
                Object.DestroyImmediate(profile);
            }
        }

        static int MeshCount() => Resources.FindObjectsOfTypeAll<Mesh>().Length;

        static int MaterialCount() => Resources.FindObjectsOfTypeAll<Material>().Length;

        [UnityTest]
        public IEnumerator PlayStopClearCyclesDoNotLeak()
        {
            yield return null;

            var meshes = MeshCount();
            var materials = MaterialCount();

            for (var i = 0; i < 100; i++)
            {
                effect.Play();
                effect.TickForTest(1f / 60f);
                effect.EmitForTest();

                effect.Stop();
                effect.TickForTest(1f / 60f);
                effect.EmitForTest();

                effect.Clear();
            }

            LogAssert.NoUnexpectedReceived();
            Assert.AreEqual(meshes, MeshCount(), "meshes leaked across 100 play/stop/clear cycles");
            Assert.AreEqual(materials, MaterialCount(), "materials leaked across 100 play/stop/clear cycles");
        }

        [UnityTest]
        public IEnumerator RebuildReplacesResourcesWithoutGrowth()
        {
            yield return null;

            var meshes = MeshCount();
            var materials = MaterialCount();

            // Object.Destroy is deferred to the end of the frame in Play Mode, so the released
            // meshes only disappear from the count after a frame boundary.
            for (var i = 0; i < 20; i++)
            {
                effect.Rebuild();
                yield return null;
            }

            Assert.AreEqual(meshes, MeshCount(), "Rebuild grew the mesh count");
            Assert.AreEqual(materials, MaterialCount(), "Rebuild grew the material count");
        }

        [UnityTest]
        public IEnumerator DisableReleasesResources()
        {
            yield return null;

            effect.enabled = false;
            yield return null;

            var disabledMeshes = MeshCount();
            var disabledMaterials = MaterialCount();

            effect.enabled = true;
            yield return null;

            effect.enabled = false;
            yield return null;

            Assert.AreEqual(disabledMeshes, MeshCount(), "disabling did not release the layer meshes");
            Assert.AreEqual(disabledMaterials, MaterialCount(), "disabling did not release the layer materials");
            Assert.IsFalse(effect.IsPlaying);
        }

        [UnityTest]
        public IEnumerator AutoStartPlaysOnEnableInPlayMode()
        {
            // AutoStart is on by default for both layers of the test profile.
            effect.enabled = false;
            yield return null;

            effect.enabled = true;
            yield return null;

            Assert.IsTrue(effect.IsPlaying, "layers with AutoStart should play as soon as the component enables");
        }

        [UnityTest]
        public IEnumerator StopLetsDropsFinishAndClearDoesNot()
        {
            yield return null;

            effect.Play();
            for (var i = 0; i < 30; i++)
            {
                effect.TickForTest(1f / 60f);
            }

            effect.Stop();
            Assert.IsFalse(effect.IsEmitting, "Stop must end emission at once");
            Assert.IsTrue(effect.IsPlaying, "drops already on screen must finish");

            effect.Clear();
            Assert.IsFalse(effect.IsPlaying, "Clear must remove everything immediately");

            effect.EmitForTest();
            Assert.AreEqual(0, effect.RenderData.Batches.Count);
        }
    }
}
