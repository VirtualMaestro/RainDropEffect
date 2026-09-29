using NUnit.Framework;
using RainDropEffect.Editor;
using UnityEditor;
using UnityEngine;

namespace RainDropEffect.Tests
{
    /// <summary>
    /// Coverage masks are the only thing that keeps a flat quad area from erasing rain drawn
    /// underneath it (decision D9), so the bake must be deterministic and must not leave the
    /// source textures' importer settings changed.
    /// </summary>
    public class CoverageMaskBakerTests
    {
        const int Size = 16;
        const string NormalAsset = "Packages/com.virtualmaestro.raindropeffect/Runtime/Textures/Rain/rain_drop_normal.tga";

        /// <summary>
        /// Matches the shipped textures: unused area is pure black, the drop carries normal data.
        /// </summary>
        static Color32[] NormalWithPatch()
        {
            var pixels = new Color32[Size * Size];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(0, 0, 0, 255);
            }

            for (var y = 6; y < 10; y++)
            {
                for (var x = 6; x < 10; x++)
                {
                    pixels[y * Size + x] = new Color32(200, 128, 255, 255);
                }
            }

            return pixels;
        }

        static Color32[] Black()
        {
            var pixels = new Color32[Size * Size];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(0, 0, 0, 255);
            }

            return pixels;
        }

        /// <summary>A normal map whose magnitude climbs linearly from left to right.</summary>
        static Color32[] NormalGradient()
        {
            var pixels = new Color32[Size * Size];
            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    var v = (byte)(x * 255 / (Size - 1));
                    pixels[y * Size + x] = new Color32(v, v, v, 255);
                }
            }

            return pixels;
        }

        [Test]
        public void BakeProducesMaskWhereNormalCarriesData()
        {
            var mask = CoverageMaskBaker.BakeInMemory(NormalWithPatch(), Black(), Size, Size);

            Assert.Greater(mask[8 * Size + 8], 0, "centre of the deviating patch must be covered");
            Assert.Greater(mask[5 * Size + 5], 0, "the gaussian must reach one pixel outside the patch");

            // These two are the guard on the softening radius: a gaussian wide enough to bleed into
            // the far corners of the mask is too wide, whatever it does to the silhouette edge.
            Assert.AreEqual(0, mask[0], "far corner must stay uncovered");
            Assert.AreEqual(0, mask[Size * Size - 1], "far corner must stay uncovered");
        }

        /// <summary>
        /// The point of the change: coverage is a ramp, not a threshold with a blur on top. A step
        /// function plus a 3x3 box gives one intermediate pixel; a ramp gives a gradient several
        /// byte steps deep, which is what stops the drop reading as a composited cut-out.
        /// </summary>
        [Test]
        public void CoverageIsAMonotonicRampNotAThreshold()
        {
            var mask = CoverageMaskBaker.BakeInMemory(NormalGradient(), Black(), Size, Size);

            var row = new int[Size];
            for (var x = 0; x < Size; x++)
            {
                row[x] = mask[8 * Size + x];
            }

            for (var x = 1; x < Size; x++)
            {
                Assert.GreaterOrEqual(row[x], row[x - 1],
                    $"coverage fell from {row[x - 1]} to {row[x]} at x={x}; the ramp must be monotonic");
            }

            var distinct = new System.Collections.Generic.HashSet<int>();
            foreach (var value in row)
            {
                if (value > 0 && value < 255)
                {
                    distinct.Add(value);
                }
            }

            Assert.Greater(distinct.Count, 1,
                $"the edge spans {distinct.Count} intermediate byte value(s) — that is a threshold, not a ramp");
            Assert.Less(row[0], 96, "the darkest column is barely covered");
            Assert.AreEqual(255, row[Size - 1], "the brightest column is fully inside the silhouette");
        }

        [Test]
        public void BakeIsDeterministic()
        {
            var first = CoverageMaskBaker.BakeInMemory(NormalWithPatch(), Black(), Size, Size);
            var second = CoverageMaskBaker.BakeInMemory(NormalWithPatch(), Black(), Size, Size);

            CollectionAssert.AreEqual(first, second);
        }

        [Test]
        public void ImporterSettingsRestored()
        {
            var importer = AssetImporter.GetAtPath(NormalAsset) as TextureImporter;
            Assert.IsNotNull(importer, $"{NormalAsset} has no texture importer");

            var readable = importer.isReadable;
            var type = importer.textureType;
            var compression = importer.textureCompression;
            var srgb = importer.sRGBTexture;

            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalAsset);
            var overlay = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.virtualmaestro.raindropeffect/Runtime/Textures/Rain/rain_drop.tga");

            // A scratch name, not the shipped mask: the bake is deterministic in content but the PNG
            // encoder is not byte-stable, so writing the real file left the working tree dirty after
            // every test run.
            var scratch = CoverageMaskBaker.OutputDirectory + "/__importer_settings_probe.png";
            CoverageMaskBaker.Bake(normal, overlay, scratch);
            AssetDatabase.DeleteAsset(scratch);

            var after = AssetImporter.GetAtPath(NormalAsset) as TextureImporter;
            Assert.IsNotNull(after);
            Assert.AreEqual(readable, after.isReadable);
            Assert.AreEqual(type, after.textureType);
            Assert.AreEqual(compression, after.textureCompression);
            Assert.AreEqual(srgb, after.sRGBTexture);
        }
    }
}
