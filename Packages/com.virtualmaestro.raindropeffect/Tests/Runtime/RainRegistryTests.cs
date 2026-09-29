using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace RainDropEffect.Tests
{
    /// <summary>
    /// The render pass finds effects through the static camera registry; these tests pin the
    /// registration contract from Task 5 (registered while enabled, ordered by component order).
    /// </summary>
    public class RainRegistryTests
    {
        readonly List<GameObject> spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in spawned)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }

            spawned.Clear();
        }

        GameObject NewCameraObject(string name)
        {
            var go = new GameObject(name, typeof(Camera));
            spawned.Add(go);
            return go;
        }

        [Test]
        public void EnabledEffectIsRegisteredForItsCamera()
        {
            var go = NewCameraObject("RainCam");
            var camera = go.GetComponent<Camera>();
            go.AddComponent<RainEffect>();

            Assert.IsTrue(RainEffect.TryGet(camera, out var effects));
            Assert.AreEqual(1, effects.Count);
        }

        [Test]
        public void DisabledEffectIsUnregistered()
        {
            var go = NewCameraObject("RainCam");
            var camera = go.GetComponent<Camera>();
            var effect = go.AddComponent<RainEffect>();

            effect.enabled = false;

            Assert.IsFalse(RainEffect.TryGet(camera, out _));
        }

        [Test]
        public void MultipleEffectsKeepComponentOrder()
        {
            var go = NewCameraObject("RainCam");
            var camera = go.GetComponent<Camera>();
            var first = go.AddComponent<RainEffect>();
            var second = go.AddComponent<RainEffect>();

            Assert.IsTrue(RainEffect.TryGet(camera, out var effects));
            Assert.AreEqual(2, effects.Count);
            Assert.AreSame(first, effects[0]);
            Assert.AreSame(second, effects[1]);
        }
    }
}
