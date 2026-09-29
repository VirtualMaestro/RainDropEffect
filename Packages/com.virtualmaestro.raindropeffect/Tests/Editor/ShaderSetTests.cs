using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace RainDropEffect.Tests
{
    /// <summary>
    /// The shader set is the package's only shader reference (no Shader.Find, no Resources.Load).
    /// These tests keep the asset, its passes and its texture properties in sync with Task 6.
    /// </summary>
    public class ShaderSetTests
    {
        const string SetPath = "Packages/com.virtualmaestro.raindropeffect/Runtime/Shaders/RainShaderSet.asset";

        [Test]
        public void ShaderSetAssetIsCompleteAndSupported()
        {
            var set = AssetDatabase.LoadAssetAtPath<RainShaderSet>(SetPath);
            Assert.IsNotNull(set, $"RainShaderSet asset missing at {SetPath}");
            Assert.IsNotNull(set.Lens, "RainShaderSet.Lens is not assigned");
            Assert.IsNotNull(set.Blur, "RainShaderSet.Blur is not assigned");
            Assert.IsTrue(set.Lens.isSupported, "Lens shader is not supported on this platform");
            Assert.IsTrue(set.Blur.isSupported, "Blur shader is not supported on this platform");
            Assert.AreEqual(2, set.Lens.passCount, "Lens shader must expose exactly LENS and OVERLAY");
        }

        [Test]
        public void LensShaderDeclaresLayerTextures()
        {
            var set = AssetDatabase.LoadAssetAtPath<RainShaderSet>(SetPath);
            Assert.IsNotNull(set);

            foreach (var property in new[] { "_NormalMap", "_OverlayTex", "_CoverageTex" })
            {
                Assert.GreaterOrEqual(set.Lens.FindPropertyIndex(property), 0,
                    $"Lens shader is missing property {property}");
            }
        }
    }
}
