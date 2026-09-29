using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace RainDropEffect.Tests
{
    /// <summary>
    /// Guards the U07 samples convention: Package Manager resolves a sample by the literal
    /// <c>samples[].path</c> string, so a renamed folder fails silently at import time instead of
    /// at build time. These tests turn that into a red test.
    /// </summary>
    public class SamplesLayoutTests
    {
        const string PackageRoot = "Packages/com.virtualmaestro.raindropeffect";

        [Test]
        public void EverySampleFolderExistsAndHasContent()
        {
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Path.Combine(PackageRoot, "package.json")));
            Assert.IsNotNull(manifest.samples, "package.json declares no samples.");
            Assert.IsNotEmpty(manifest.samples, "package.json declares no samples.");

            foreach (var sample in manifest.samples)
            {
                var folder = Path.Combine(PackageRoot, sample.path);
                Assert.IsTrue(Directory.Exists(folder), $"Missing sample folder for '{sample.displayName}': {folder}");

                var content = Directory.GetFiles(folder, "*", SearchOption.AllDirectories)
                    .Any(f => f.EndsWith(".unity") || Path.GetFileName(f) == "README.md");
                Assert.IsTrue(content, $"Sample folder has no scene and no README: {folder}");
            }
        }

        [Test]
        public void SamplesFolderIsHiddenFromTheAssetDatabase()
        {
            // A "Samples" folder without the tilde means the contributing workflow was left
            // half-done and the sample assets would ship as project assets.
            Assert.IsFalse(
                Directory.Exists(Path.Combine(PackageRoot, "Samples")),
                "Samples~ is still renamed to Samples; rename it back before committing.");
        }

        [Test]
        public void GallerySampleHasNoHostOverrides()
        {
            // The 1.x demo scripts reconfigured the whole game (resolution, orientation, frame
            // rate). A sample is example code, not a settings screen: it must leave those alone.
            var overrides = new[] { "SetResolution(", "Screen.orientation", "targetFrameRate" };

            var offenders = Directory
                .GetFiles(Path.Combine(PackageRoot, "Samples~"), "*.cs", SearchOption.AllDirectories)
                .Where(file => overrides.Any(term => File.ReadAllText(file).Contains(term)))
                .ToArray();

            Assert.IsEmpty(offenders, "Sample scripts override host settings: " + string.Join(", ", offenders));
        }

        // JsonUtility needs concrete serializable types; only the fields used here are declared.
        [System.Serializable]
        class Manifest
        {
            public Sample[] samples;
        }

        [System.Serializable]
        class Sample
        {
            public string displayName;
            public string path;
        }
    }
}
