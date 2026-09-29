using System.IO;
using NUnit.Framework;
using RainDropEffect.Editor;
using UnityEditor;
using UnityEngine;

namespace RainDropEffect.Tests
{
    /// <summary>
    /// The two editor conveniences that decide whether a fresh component works at all: the shader
    /// set is found without the user knowing it exists, and the Basic Rain preset produces a profile
    /// with real defaults rather than a zeroed one.
    /// </summary>
    public class RainEffectEditorTests
    {
        const string TempFolder = "Assets/TempRainEditorTests";

        [Test]
        public void ShaderSetAutoAssignedOnReset()
        {
            var go = new GameObject("RainEditorTestCamera", typeof(Camera));
            try
            {
                var effect = go.AddComponent<RainEffect>();
                effect.EditorAutoAssignShaderSet();

                Assert.IsNotNull(effect.EditorShaderSet, "the shipped RainShaderSet was not found");
                Assert.IsNotNull(effect.EditorShaderSet.Lens, "the shader set has no lens shader");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void BasicProfileMenuCreatesTwoLayers()
        {
            if (!AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.CreateFolder("Assets", Path.GetFileName(TempFolder));
            }

            var path = TempFolder + "/BasicRainProfile.asset";
            try
            {
                var profile = RainProfileEditor.CreateBasicRainProfile(path);

                Assert.AreEqual(1, profile.StaticLayers.Length, "expected one static layer");
                Assert.AreEqual(1, profile.SimpleLayers.Length, "expected one simple layer");
                Assert.AreEqual(2, profile.LayerCount);

                // Defaults must survive asset creation; a zeroed layer would emit nothing.
                Assert.AreEqual(20, profile.SimpleLayers[0].MaxCount);
                Assert.AreEqual(new Vector2(0.6f, 1.4f), profile.SimpleLayers[0].LifetimeRange);
                Assert.AreEqual(2f, profile.StaticLayers[0].FadeTime);

                Assert.IsNotNull(profile.StaticLayers[0].NormalMap, "static layer has no normal map");
                Assert.IsNotNull(profile.SimpleLayers[0].CoverageMask, "simple layer has no coverage mask");

                // The exact defect the 2.0.0 preset shipped with: every drop the same size, at the
                // same angle, at full opacity from its first frame, never moving. Together that
                // reads as a stamped pattern rather than as rain, so each half is asserted here and
                // a future regeneration cannot quietly restore it.
                var drops = profile.SimpleLayers[0];

                Assert.AreNotEqual(drops.SizeMin, drops.SizeMax, "every drop would be the same size");
                Assert.Less(drops.SizeMax.x, 0.15f, "0.15 is 15% of half-screen — far too large for a droplet");
                Assert.IsTrue(drops.AutoRotate, "every drop would point the same way");

                Assert.Less(drops.AlphaOverLifetime.Evaluate(0f), 1f, "drops pop in instead of arriving");
                Assert.AreEqual(1f, drops.AlphaOverLifetime.Evaluate(0.18f), 1e-3f, "the fade-in never reaches full opacity");
                Assert.AreEqual(0f, drops.AlphaOverLifetime.Evaluate(1f), 1e-3f, "drops must be gone by the end of their life");

                Assert.Greater(drops.DriftOverLifetime.Evaluate(1f), 0f, "drops hang motionless");
                Assert.Greater(drops.DriftSpeed, 0f, "a drift curve with no speed still does not move");
            }
            finally
            {
                AssetDatabase.DeleteAsset(TempFolder);
            }
        }
    }
}
