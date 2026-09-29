using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace RainDropEffect.Tests
{
    /// <summary>
    /// Release metadata gate (Task 32). A wrong version string or a stray dependency is invisible
    /// until a consumer resolves the package, at which point it is a support ticket rather than a
    /// build error.
    /// </summary>
    public class PackageMetadataTests
    {
        const string PackageRoot = "Packages/com.virtualmaestro.raindropeffect";

        static Manifest Read()
        {
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Path.Combine(PackageRoot, "package.json")));
            Assert.IsNotNull(manifest, "package.json did not parse.");
            return manifest;
        }

        [Test]
        public void VersionIsSemver()
        {
            Assert.IsTrue(
                Regex.IsMatch(Read().version, @"^\d+\.\d+\.\d+(-.+)?$"),
                $"version '{Read().version}' is not semver.");
        }

        [Test]
        public void UnityMinimumIs6000_3()
        {
            Assert.AreEqual("6000.3", Read().unity, "The declared minimum editor is not 6000.3 (decision D3).");
        }

        [Test]
        public void DependenciesContainOnlyUrp()
        {
            // JsonUtility cannot deserialize a dictionary, and the dependency block is two lines of
            // text; reading the raw keys is both simpler and stricter than a partial model.
            var text = File.ReadAllText(Path.Combine(PackageRoot, "package.json"));
            var block = Regex.Match(text, "\"dependencies\"\\s*:\\s*\\{(.*?)\\}", RegexOptions.Singleline);
            Assert.IsTrue(block.Success, "package.json has no dependencies block.");

            foreach (Match key in Regex.Matches(block.Groups[1].Value, "\"([^\"]+)\"\\s*:"))
            {
                Assert.AreEqual(
                    "com.unity.render-pipelines.universal",
                    key.Groups[1].Value,
                    "The runtime may depend on URP and nothing else.");
            }
        }

        [Test]
        public void SamplesPathsExist()
        {
            var manifest = Read();
            Assert.IsNotEmpty(manifest.samples, "package.json declares no samples.");

            foreach (var sample in manifest.samples)
            {
                Assert.IsTrue(
                    Directory.Exists(Path.Combine(PackageRoot, sample.path)),
                    $"Sample path does not exist: {sample.path}");
            }
        }

        [System.Serializable]
        class Manifest
        {
            public string version;
            public string unity;
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
