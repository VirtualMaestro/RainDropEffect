using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace RainDropEffect.Tests
{
    /// <summary>
    /// Pins the two things every later phase depends on: the draw order of layers and the repairs
    /// <see cref="RainProfile.Sanitize"/> makes to authored data.
    /// </summary>
    public class RainProfileTests
    {
        RainProfile profile;
        readonly List<RainLayerSettings> ordered = new List<RainLayerSettings>();

        [SetUp]
        public void SetUp()
        {
            profile = ScriptableObject.CreateInstance<RainProfile>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(profile);
            ordered.Clear();
        }

        [Test]
        public void CollectOrderedSortsByDepthThenFamilyThenIndex()
        {
            profile.StaticLayers = new[]
            {
                new StaticLayerSettings { Name = "static-5", Depth = 5 },
                new StaticLayerSettings { Name = "static-1", Depth = 1 }
            };
            profile.SimpleLayers = new[] { new SimpleLayerSettings { Name = "simple-1", Depth = 1 } };
            profile.FlowLayers = new[] { new FlowLayerSettings { Name = "flow-1", Depth = 1 } };

            profile.CollectOrdered(ordered);

            CollectionAssert.AreEqual(
                new[] { "static-1", "simple-1", "flow-1", "static-5" },
                ordered.ConvertAll(l => l.Name));
        }

        [Test]
        public void CollectOrderedSkipsDisabled()
        {
            profile.SimpleLayers = new[]
            {
                new SimpleLayerSettings { Name = "on" },
                new SimpleLayerSettings { Name = "off", Enabled = false }
            };

            profile.CollectOrdered(ordered);

            Assert.AreEqual(1, ordered.Count);
            Assert.AreEqual("on", ordered[0].Name);
        }

        [Test]
        public void SanitizeSwapsInvertedRanges()
        {
            var simple = new SimpleLayerSettings
            {
                LifetimeRange = new Vector2(2f, 1f),
                EmissionRateRange = new Vector2Int(5, 2),
                SizeMin = new Vector2(0.3f, 0.1f),
                SizeMax = new Vector2(0.2f, 0.4f)
            };
            profile.SimpleLayers = new[] { simple };

            profile.Sanitize();

            Assert.AreEqual(new Vector2(1f, 2f), simple.LifetimeRange);
            Assert.AreEqual(new Vector2Int(2, 5), simple.EmissionRateRange);
            Assert.AreEqual(new Vector2(0.2f, 0.1f), simple.SizeMin);
            Assert.AreEqual(new Vector2(0.3f, 0.4f), simple.SizeMax);
        }

        [Test]
        public void SanitizeClampsMaxCount()
        {
            var simple = new SimpleLayerSettings { Name = "big", MaxCount = 300 };
            profile.SimpleLayers = new[] { simple };

            profile.Sanitize();

            Assert.AreEqual(256, simple.MaxCount);
        }
    }
}
