using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace RainDropEffect.Tests
{
    /// <summary>
    /// NFR-02 as a test: after the first frame neither the tick nor the emit allocates managed
    /// memory. A per-frame allocation here would show up as periodic GC spikes in a shipped game,
    /// which is exactly the class of bug nobody attributes to a rain effect.
    /// </summary>
    public class RainEffectAllocationTests
    {
        GameObject host;
        RainProfile profile;
        RainEffect effect;

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

        IEnumerator Warm(int simpleCount)
        {
            host = new GameObject("RainAllocationCamera", typeof(Camera));
            profile = RainTestProfiles.CreateBasic(simpleCount);
            effect = RainTestProfiles.CreateEffect(host, profile);
            effect.Play();

            // The first frames build the drop array and grow the batch list; only the steady state
            // is expected to be allocation-free.
            for (var i = 0; i < 10; i++)
            {
                effect.TickForTest(1f / 60f);
                effect.EmitForTest();
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator TickDoesNotAllocateWithBasicProfile()
        {
            yield return Warm(20);

            Assert.That(() => effect.TickForTest(1f / 60f), Is.Not.AllocatingGCMemory());
        }

        [UnityTest]
        public IEnumerator EmitDoesNotAllocateWithBasicProfile()
        {
            yield return Warm(20);

            Assert.That(() => effect.EmitForTest(), Is.Not.AllocatingGCMemory());
        }

        [UnityTest]
        public IEnumerator TickDoesNotAllocateWithMaximumDrops()
        {
            yield return Warm(256);

            Assert.That(() => effect.TickForTest(1f / 60f), Is.Not.AllocatingGCMemory());
        }

        [UnityTest]
        public IEnumerator EmitDoesNotAllocateWithMaximumDrops()
        {
            yield return Warm(256);

            Assert.That(() => effect.EmitForTest(), Is.Not.AllocatingGCMemory());
        }
    }
}
