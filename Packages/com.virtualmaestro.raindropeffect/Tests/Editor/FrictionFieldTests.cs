using NUnit.Framework;
using RainDropEffect.Editor;
using UnityEngine;

namespace RainDropEffect.Tests
{
    /// <summary>
    /// The baked friction field and the bake that produces it. The vertical flip is the part worth
    /// pinning: it is invisible in code and inverts every trail path if it is wrong.
    /// </summary>
    public class FrictionFieldTests
    {
        FrictionField field;

        [TearDown]
        public void TearDown()
        {
            if (field != null)
            {
                Object.DestroyImmediate(field);
                field = null;
            }
        }

        FrictionField Make(int width, int height, byte[] values)
        {
            field = ScriptableObject.CreateInstance<FrictionField>();
            field.Width = width;
            field.Height = height;
            field.Values = values;
            return field;
        }

        [Test]
        public void SampleUsesBottomUpRows()
        {
            // Row 0 is the bottom of the viewport; row 1 the top.
            var f = Make(2, 2, new byte[] { 0, 0, 255, 255 });

            Assert.AreEqual(1f, f.Sample(0.25f, 0.75f), 1e-4f, "the top row should be bright");
            Assert.AreEqual(0f, f.Sample(0.25f, 0.25f), 1e-4f, "the bottom row should be dark");
        }

        [Test]
        public void SampleClampsOutsideRange()
        {
            var f = Make(2, 2, new byte[] { 0, 0, 255, 255 });

            Assert.AreEqual(0f, f.Sample(-5f, -9f), 1e-4f);
            Assert.AreEqual(1f, f.Sample(9f, 5f), 1e-4f);
        }

        [Test]
        public void SampleWithoutValuesIsZero()
        {
            var f = Make(0, 0, null);

            Assert.AreEqual(0f, f.Sample(0.5f, 0.5f));
        }

        [Test]
        public void BakeUsesGrayscaleWeights()
        {
            // Pure red has a luminance of 0.299, so the friction score is 1 - 0.299.
            var values = FrictionFieldBaker.Bake(new[] { new Color32(255, 0, 0, 255) }, 1, 1);

            Assert.AreEqual(179, values[0], 1);
        }

        [Test]
        public void BakeInvertsBrightness()
        {
            var values = FrictionFieldBaker.Bake(
                new[] { new Color32(255, 255, 255, 255), new Color32(0, 0, 0, 255) }, 2, 1);

            Assert.AreEqual(0, values[0], "white is the least attractive");
            Assert.AreEqual(255, values[1], "black is the most attractive");
        }

        [Test]
        public void BakeFlipsRows()
        {
            // GetPixels32 returns row 0 at the bottom of the texture. The bake flips, so that black
            // bottom texture row has to come out at the TOP of the field (y = 1), and the white top
            // texture row at the bottom (y = 0).
            var pixels = new[]
            {
                new Color32(0, 0, 0, 255), new Color32(0, 0, 0, 255),
                new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255)
            };

            var values = FrictionFieldBaker.Bake(pixels, 2, 2);

            Assert.AreEqual(0, values[0], "the white texture row must land at the field's bottom row");
            Assert.AreEqual(255, values[2], "the black texture row must land at the field's top row");
        }

        [Test]
        public void ShippedFieldIsBaked()
        {
            var shipped = UnityEditor.AssetDatabase.LoadAssetAtPath<FrictionField>(
                "Packages/com.virtualmaestro.raindropeffect/Runtime/Data/FrictionField_friction_map.asset");

            Assert.IsNotNull(shipped, "the shipped friction field has not been baked");
            Assert.AreEqual(512, shipped.Width);
            Assert.AreEqual(512, shipped.Height);
            Assert.AreEqual(512 * 512, shipped.Values.Length);
        }
    }
}
