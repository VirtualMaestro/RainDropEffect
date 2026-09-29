using UnityEngine;

namespace RainDropEffect
{
    /// <summary>
    /// A baked scalar grid that tells a friction trail where it prefers to run: one byte per cell,
    /// higher means more attractive.
    ///
    /// The legacy version sampled a readable sRGB texture with <c>GetPixel</c> every frame, which
    /// forced the texture to stay uncompressed in memory. Baking it once removes the readable
    /// texture from the build and turns the sample into an array read.
    ///
    /// Rows are stored bottom-up, matching viewport v, and already carry the vertical flip the
    /// the original Built-in implementation's sampling implied.
    /// </summary>
    public sealed class FrictionField : ScriptableObject
    {
        public int Width;
        public int Height;

        [HideInInspector] public byte[] Values;

        /// <summary>Where the field came from; kept so a re-bake is reproducible.</summary>
        public string SourceTexturePath;

        public string SourceGuid;

        /// <summary>
        /// Samples the field at viewport coordinates (bottom-left origin). Coordinates outside the
        /// view clamp to the edge rather than wrapping, so a trail leaving the screen keeps a sane
        /// score instead of teleporting to the opposite side.
        /// </summary>
        public float Sample(float u, float v)
        {
            if (Values == null || Width <= 0 || Height <= 0)
            {
                return 0f;
            }

            var x = Mathf.Clamp((int)(u * Width), 0, Width - 1);
            var y = Mathf.Clamp((int)(v * Height), 0, Height - 1);
            return Values[y * Width + x] * (1f / 255f);
        }
    }
}
