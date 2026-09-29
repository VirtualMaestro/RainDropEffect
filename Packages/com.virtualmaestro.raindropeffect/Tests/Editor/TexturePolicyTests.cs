using System.IO;
using System.Linq;
using NUnit.Framework;
using RainDropEffect.Editor;
using UnityEditor;

namespace RainDropEffect.Tests
{
    /// <summary>
    /// The import policy for shipped textures, and the rule that the runtime never looks an asset up
    /// by name.
    ///
    /// A missing platform override is invisible in the editor and costs several megabytes per
    /// texture in a mobile player, which is precisely the kind of regression nobody notices until a
    /// build report lands.
    /// </summary>
    public class TexturePolicyTests
    {
        const string PackageRoot = "Packages/com.virtualmaestro.raindropeffect";

        [Test]
        public void NormalMapsUseTwoChannelCompressionOnStandalone()
        {
            foreach (var path in AssetMover.AllPackageTexturePaths())
            {
                var name = Path.GetFileNameWithoutExtension(path);
                if (!name.EndsWith("_normal") || name.StartsWith("rain_white"))
                {
                    continue;
                }

                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                var settings = importer.GetPlatformTextureSettings("Standalone");

                Assert.IsTrue(settings.overridden, $"{name} has no Standalone override");
                Assert.AreEqual(TextureImporterFormat.BC5, settings.format, $"{name} is not BC5 on Standalone");
            }
        }

        [Test]
        public void EveryShippedTextureUsesAstcOnAndroid()
        {
            foreach (var path in AssetMover.AllPackageTexturePaths())
            {
                var name = Path.GetFileNameWithoutExtension(path);
                if (name.StartsWith("rain_white") || name == "friction_map")
                {
                    continue;
                }

                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                var settings = importer.GetPlatformTextureSettings("Android");

                Assert.IsTrue(settings.overridden, $"{name} has no Android override");
                Assert.AreEqual(TextureImporterFormat.ASTC_6x6, settings.format, $"{name} is not ASTC 6x6 on Android");
                Assert.LessOrEqual(settings.maxTextureSize, 1024, $"{name} is larger than 1024 on Android");
            }
        }

        [Test]
        public void NormalMapsAreImportedAsNormalMaps()
        {
            foreach (var path in AssetMover.AllPackageTexturePaths())
            {
                if (!Path.GetFileNameWithoutExtension(path).EndsWith("_normal"))
                {
                    continue;
                }

                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.AreEqual(TextureImporterType.NormalMap, importer.textureType, $"wrong type for {path}");
            }
        }

        [Test]
        public void FrictionSourceIsNotReadable()
        {
            // Task 21 baked it into a FrictionField asset; keeping it readable would keep an
            // uncompressed copy in memory for nothing.
            var importer = (TextureImporter)AssetImporter.GetAtPath(
                PackageRoot + "/Runtime/Textures/Rain/friction_map.tga");

            Assert.IsFalse(importer.isReadable);
            Assert.IsFalse(importer.mipmapEnabled);
        }

        [Test]
        public void NoShaderFindOrResourcesLoadInRuntime()
        {
            var offenders = Directory
                .GetFiles(PackageRoot + "/Runtime", "*.cs", SearchOption.AllDirectories)
                .Where(file =>
                {
                    var text = File.ReadAllText(file);
                    return text.Contains("Shader.Find(") || text.Contains("Resources.Load(");
                })
                .ToArray();

            CollectionAssert.IsEmpty(offenders,
                "runtime code must reference shaders through the serialized RainShaderSet, not by name");
        }

        [Test]
        public void OnlyTheBlurKeywordIsMultiCompiled()
        {
            var declarations = Directory
                .GetFiles(PackageRoot + "/Runtime/Shaders", "*.shader", SearchOption.AllDirectories)
                .SelectMany(File.ReadAllLines)
                .Select(line => line.Trim())
                .Where(line => line.StartsWith("#pragma multi_compile") || line.StartsWith("#pragma shader_feature"))
                .ToArray();

            foreach (var declaration in declarations)
            {
                Assert.IsTrue(declaration.Contains("_RAIN_BLUR"),
                    $"unexpected variant declaration, every one of them costs build time: {declaration}");
            }
        }
    }
}
