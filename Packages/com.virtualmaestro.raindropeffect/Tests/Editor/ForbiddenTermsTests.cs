using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace RainDropEffect.Tests
{
    /// <summary>
    /// Nothing shipped may carry a Built-in Render Pipeline construct. This package is URP-only, and
    /// a stray <c>GrabPass</c> or <c>CGPROGRAM</c> compiles fine in the editor while failing on a
    /// target that never sees Built-in. A grep is easy to forget in a later change, so it is a test.
    ///
    /// <c>Shader.Find</c> is here for a different reason: shaders are bound through the serialized
    /// <c>RainShaderSet</c> asset so they survive a player build, and a name lookup would silently
    /// undo that.
    /// </summary>
    public class ForbiddenTermsTests
    {
        const string PackageRoot = "Packages/com.virtualmaestro.raindropeffect";

        static readonly string[] ForbiddenTerms =
        {
            "GrabPass",
            "_GrabTexture",
            "_BackgroundTexture",
            "UnityCG",
            "CGPROGRAM",
            "Shader.Find"
        };

        [Test]
        public void RuntimeContainsNoLegacyTerms()
        {
            AssertClean(Path.Combine(PackageRoot, "Runtime"));
        }

        [Test]
        public void SamplesContainNoLegacyTerms()
        {
            AssertClean(Path.Combine(PackageRoot, "Samples~"));
        }

        static void AssertClean(string root)
        {
            Assert.IsTrue(Directory.Exists(root), $"Missing folder: {root}");

            var hits = new List<string>();

            foreach (var file in SourceFiles(root))
            {
                var text = File.ReadAllText(file);

                foreach (var term in ForbiddenTerms)
                {
                    if (text.Contains(term))
                    {
                        hits.Add($"{file}: {term}");
                    }
                }
            }

            Assert.IsEmpty(hits, "Legacy terms found:\n" + string.Join("\n", hits));
        }

        static IEnumerable<string> SourceFiles(string root)
        {
            return new[] { "*.cs", "*.shader", "*.hlsl" }
                .SelectMany(pattern => Directory.GetFiles(root, pattern, SearchOption.AllDirectories));
        }
    }
}
