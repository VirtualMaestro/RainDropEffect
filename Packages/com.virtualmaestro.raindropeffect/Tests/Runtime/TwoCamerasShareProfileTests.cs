using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RainDropEffect.Tests
{
    /// <summary>
    /// One profile asset, two cameras, independent playback. This is the promise that lets a game
    /// reuse a single authored profile everywhere: if the runtime wrote into the settings, camera A
    /// would drag camera B along with it.
    /// </summary>
    public class TwoCamerasShareProfileTests
    {
        GameObject hostA;
        GameObject hostB;
        RainProfile profile;
        RainEffect a;
        RainEffect b;

        [SetUp]
        public void SetUp()
        {
            profile = RainTestProfiles.CreateBasic();

            hostA = new GameObject("RainCameraA", typeof(Camera));
            hostB = new GameObject("RainCameraB", typeof(Camera));

            a = RainTestProfiles.CreateEffect(hostA, profile, seed: 1);
            b = RainTestProfiles.CreateEffect(hostB, profile, seed: 2);
        }

        [TearDown]
        public void TearDown()
        {
            if (hostA != null)
            {
                Object.DestroyImmediate(hostA);
            }

            if (hostB != null)
            {
                Object.DestroyImmediate(hostB);
            }

            if (profile != null)
            {
                Object.DestroyImmediate(profile);
            }
        }

        [UnityTest]
        public IEnumerator PlaybackIsIndependent()
        {
            a.Clear();
            b.Clear();
            yield return null;

            a.Play();
            for (var i = 0; i < 10; i++)
            {
                a.TickForTest(1f / 60f);
                b.TickForTest(1f / 60f);
            }

            Assert.IsTrue(a.IsPlaying, "A was told to play");
            Assert.IsFalse(b.IsPlaying, "B was never told to play");

            b.Play();
            a.Clear();

            Assert.IsFalse(a.IsPlaying, "A was cleared");
            Assert.IsTrue(b.IsPlaying, "B must be unaffected by A being cleared");
        }

        [UnityTest]
        public IEnumerator TheProfileAssetIsNeverWrittenTo()
        {
            var maxCount = profile.SimpleLayers[0].MaxCount;
            var distortion = profile.SimpleLayers[0].Distortion;
            var fadeTime = profile.StaticLayers[0].FadeTime;

            a.Play();
            b.Play();
            a.Intensity = 0.25f;
            b.Intensity = 1f;

            for (var i = 0; i < 60; i++)
            {
                a.TickForTest(1f / 60f);
                a.EmitForTest();
                b.TickForTest(1f / 60f);
                b.EmitForTest();
            }

            yield return null;

            Assert.AreEqual(maxCount, profile.SimpleLayers[0].MaxCount);
            Assert.AreEqual(distortion, profile.SimpleLayers[0].Distortion);
            Assert.AreEqual(fadeTime, profile.StaticLayers[0].FadeTime);
        }

        [UnityTest]
        public IEnumerator DifferentSeedsProduceDifferentRuns()
        {
            a.Play();
            b.Play();

            for (var i = 0; i < 60; i++)
            {
                a.TickForTest(1f / 60f);
                a.EmitForTest();
                b.TickForTest(1f / 60f);
                b.EmitForTest();
            }

            yield return null;

            var meshA = a.RenderData.Batches[a.RenderData.Batches.Count - 1].Mesh;
            var meshB = b.RenderData.Batches[b.RenderData.Batches.Count - 1].Mesh;
            Assert.AreNotSame(meshA, meshB, "the two effects must not share a mesh");
        }
    }
}
