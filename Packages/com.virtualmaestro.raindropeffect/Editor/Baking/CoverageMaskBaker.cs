using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RainDropEffect.Editor
{
    /// <summary>
    /// Bakes the explicit coverage channel the lens pass requires (decision D9).
    ///
    /// Coverage is never inferred from normal magnitude at runtime: a flat area of a drop quad
    /// would otherwise erase rain drawn underneath it. One mask is baked per overlay/normal pair
    /// and assigned by the converter (Task 26).
    /// </summary>
    public static class CoverageMaskBaker
    {
        public const string OutputDirectory =
            "Packages/com.virtualmaestro.raindropeffect/Runtime/Textures/Coverage";

        const string SourceRoot = "Packages/com.virtualmaestro.raindropeffect/Runtime/Textures/";

        /// <summary>
        /// Normal magnitude band. Below <see cref="NormalRampMin"/> a pixel is fully outside the
        /// drop, above <see cref="NormalRampMax"/> fully inside, and in between coverage rises
        /// smoothly.
        ///
        /// The shipped normal maps do not use a flat-blue background: unused area is pure black
        /// (measured on all ten pairs, flat-blue pixels 0.0%). "Carries normal data" is therefore
        /// the drop's silhouette, and a deviation-from-flat-blue test would mark every pixel.
        ///
        /// This used to be a single threshold at 0.02 producing 0 or 255. A binary mask is what made
        /// the drop read as a composited hard-edged object rather than as refraction: the lens pass
        /// blends with alpha = coverage * opacity, so a step in coverage is a visible cut-out.
        /// </summary>
        const float NormalRampMin = 0.01f;

        const float NormalRampMax = 0.25f;

        /// <summary>Overlay luminance band, same shape as the normal one.</summary>
        const float OverlayRampMin = 0.02f;

        const float OverlayRampMax = 0.30f;

        /// <summary>
        /// Softening radius, expressed as a gaussian sigma per pixel of the mask's edge length, so a
        /// 128 px source and a 1024 px one get the same softness in normalized units. The old bake
        /// applied one fixed 3x3 box pass, which on a 128 px source is a one-pixel edge.
        /// </summary>
        const float SoftenSigmaPerPixel = 1f / 96f;

        const float MinSoftenSigma = 1f;

        const float MaxSoftenSigma = 12f;

        /// <summary>The 10 shipped pairs, relative to <see cref="SourceRoot"/> without extension.</summary>
        static readonly string[] Pairs =
        {
            "Rain/rain_drop",
            "Rain/rain_frame",
            "Rain/flow",
            "Rain/bubble",
            "Rain/wash",
            "Rain/rain_white",
            "Frozen/frozenfill",
            "Frozen/frozenframe",
            "Blood/bloodframe",
            "Blood/bloodsplatter"
        };

        [MenuItem("Rain Drop Effect/Bake Coverage Masks")]
        public static void BakeAll()
        {
            Directory.CreateDirectory(Path.GetFullPath(OutputDirectory));

            foreach (var pair in Pairs)
            {
                var overlayPath = SourceRoot + pair + ".tga";
                var normalPath = SourceRoot + pair + "_normal.tga";

                var overlay = AssetDatabase.LoadAssetAtPath<Texture2D>(overlayPath);
                var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);

                if (overlay == null)
                {
                    RainLog.Error($"coverage bake skipped: missing {overlayPath}");
                    continue;
                }

                if (normal == null)
                {
                    RainLog.Error($"coverage bake skipped: missing {normalPath}");
                    continue;
                }

                var name = Path.GetFileNameWithoutExtension(overlayPath);
                Bake(normal, overlay, $"{OutputDirectory}/{name}_coverage.png");
            }

            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Bakes one mask and writes it as a SingleChannel texture asset.
        /// Deterministic: identical inputs produce byte-identical output.
        /// </summary>
        public static Texture2D Bake(Texture2D normal, Texture2D overlay, string outputAssetPath, int maxSize = 1024)
        {
            if (normal == null || overlay == null)
            {
                RainLog.Error($"coverage bake skipped: null input for {outputAssetPath}");
                return null;
            }

            var size = Mathf.Min(maxSize, Mathf.Max(
                Mathf.Max(normal.width, normal.height),
                Mathf.Max(overlay.width, overlay.height)));

            // Reimporting replaces the texture objects, so work from paths from here on.
            var normalPath = AssetDatabase.GetAssetPath(normal);
            var overlayPath = AssetDatabase.GetAssetPath(overlay);

            byte[] mask;
            using (new ReadablePixels(normalPath))
            using (new ReadablePixels(overlayPath))
            {
                var normalPixels = ReadPixels(normalPath, out var normalWidth, out var normalHeight);
                var overlayPixels = ReadPixels(overlayPath, out var overlayWidth, out var overlayHeight);
                mask = BakeInMemory(
                    Resample(normalPixels, normalWidth, normalHeight, size, size),
                    Resample(overlayPixels, overlayWidth, overlayHeight, size, size),
                    size, size);
            }

            long sum = 0;
            var touched = 0;
            for (var i = 0; i < mask.Length; i++)
            {
                sum += mask[i];
                if (mask[i] > 0)
                {
                    touched++;
                }
            }

            // Mean coverage is the honest number (the mask is soft at the edges); the "any" figure
            // shows how far dilation and blur reach.
            var covered = 100f * sum / (255f * mask.Length);
            var any = 100f * touched / mask.Length;

            var name = Path.GetFileNameWithoutExtension(outputAssetPath);
            RainLog.Verbose($"coverage {name}: {covered:0.0}% covered, {size}x{size}");
            Debug.Log($"[RainCoverage] {name}: {covered:0.0}% covered (any {any:0.0}%), {size}x{size}, " +
                      $"normal ramp {NormalRampMin}..{NormalRampMax}, overlay ramp {OverlayRampMin}..{OverlayRampMax}, " +
                      $"gaussian sigma {SigmaFor(size, size):0.00} px, no dilation");

            WritePng(mask, size, outputAssetPath);
            ApplyMaskImportSettings(outputAssetPath);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(outputAssetPath);
        }

        /// <summary>
        /// The pure part: coverage as a continuous ramp over normal magnitude and overlay
        /// luminance, softened by a separable gaussian. Exposed separately from asset I/O so it can
        /// be tested without importer round-trips, and deterministic so the bake is reproducible.
        ///
        /// There is deliberately no dilation step any more. With a hard threshold, growing the
        /// silhouette 2 px hid the stair-stepping; with a ramp it only pushes coverage outside the
        /// artwork, which is the halo the drop was wearing.
        /// </summary>
        public static byte[] BakeInMemory(Color32[] normal, Color32[] overlay, int width, int height)
        {
            if (normal == null || overlay == null)
            {
                throw new ArgumentNullException(normal == null ? nameof(normal) : nameof(overlay));
            }

            if (normal.Length < width * height || overlay.Length < width * height)
            {
                throw new ArgumentException("Pixel arrays are smaller than width * height.");
            }

            var coverage = new float[width * height];
            for (var i = 0; i < coverage.Length; i++)
            {
                var n = normal[i];
                var magnitude = Mathf.Max(n.r, Mathf.Max(n.g, n.b)) / 255f;

                var o = overlay[i];
                var luminance = Mathf.Max(o.r, Mathf.Max(o.g, o.b)) / 255f;

                coverage[i] = Mathf.Max(
                    Ramp(NormalRampMin, NormalRampMax, magnitude),
                    Ramp(OverlayRampMin, OverlayRampMax, luminance));
            }

            coverage = Soften(coverage, width, height, SigmaFor(width, height));

            var mask = new byte[coverage.Length];
            for (var i = 0; i < mask.Length; i++)
            {
                mask[i] = (byte)Mathf.Clamp(Mathf.RoundToInt(coverage[i] * 255f), 0, 255);
            }

            return mask;
        }

        /// <summary>The gaussian sigma a mask of this size is softened with, in pixels.</summary>
        public static float SigmaFor(int width, int height)
        {
            return Mathf.Clamp(Mathf.Max(width, height) * SoftenSigmaPerPixel, MinSoftenSigma, MaxSoftenSigma);
        }

        /// <summary>
        /// The Hermite ramp (the shader smoothstep, not Mathf.SmoothStep, which interpolates between
        /// two values rather than mapping an edge band to 0..1).
        /// </summary>
        static float Ramp(float edge0, float edge1, float x)
        {
            var t = Mathf.Clamp01((x - edge0) / Mathf.Max(1e-6f, edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// Separable gaussian, clamped at the border. Truncated at three sigma, which is what keeps
        /// a mask's far corners at exactly zero — the guard the baker tests assert on.
        /// </summary>
        static float[] Soften(float[] source, int width, int height, float sigma)
        {
            var radius = Mathf.Max(1, Mathf.CeilToInt(3f * sigma));
            var kernel = new float[radius * 2 + 1];
            var total = 0f;
            for (var i = 0; i < kernel.Length; i++)
            {
                var d = i - radius;
                kernel[i] = Mathf.Exp(-(d * d) / (2f * sigma * sigma));
                total += kernel[i];
            }

            for (var i = 0; i < kernel.Length; i++)
            {
                kernel[i] /= total;
            }

            var horizontal = new float[source.Length];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var sum = 0f;
                    for (var k = -radius; k <= radius; k++)
                    {
                        var sx = Mathf.Clamp(x + k, 0, width - 1);
                        sum += source[y * width + sx] * kernel[k + radius];
                    }

                    horizontal[y * width + x] = sum;
                }
            }

            var result = new float[source.Length];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var sum = 0f;
                    for (var k = -radius; k <= radius; k++)
                    {
                        var sy = Mathf.Clamp(y + k, 0, height - 1);
                        sum += horizontal[sy * width + x] * kernel[k + radius];
                    }

                    result[y * width + x] = sum;
                }
            }

            return result;
        }

        /// <summary>Box-average resample. Returns the input array unchanged when sizes already match.</summary>
        static Color32[] Resample(Color32[] source, int width, int height, int targetWidth, int targetHeight)
        {
            if (width == targetWidth && height == targetHeight)
            {
                return source;
            }

            var result = new Color32[targetWidth * targetHeight];
            for (var y = 0; y < targetHeight; y++)
            {
                var y0 = y * height / targetHeight;
                var y1 = Mathf.Max(y0 + 1, (y + 1) * height / targetHeight);

                for (var x = 0; x < targetWidth; x++)
                {
                    var x0 = x * width / targetWidth;
                    var x1 = Mathf.Max(x0 + 1, (x + 1) * width / targetWidth);

                    int r = 0, g = 0, b = 0, a = 0, count = 0;
                    for (var sy = y0; sy < y1; sy++)
                    {
                        for (var sx = x0; sx < x1; sx++)
                        {
                            var c = source[sy * width + sx];
                            r += c.r;
                            g += c.g;
                            b += c.b;
                            a += c.a;
                            count++;
                        }
                    }

                    result[y * targetWidth + x] = new Color32(
                        (byte)(r / count), (byte)(g / count), (byte)(b / count), (byte)(a / count));
                }
            }

            return result;
        }

        static Color32[] ReadPixels(string assetPath, out int width, out int height)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            width = texture.width;
            height = texture.height;
            return texture.GetPixels32();
        }

        static void WritePng(byte[] mask, int size, string outputAssetPath)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            try
            {
                var pixels = new Color32[mask.Length];
                for (var i = 0; i < mask.Length; i++)
                {
                    // Replicated into rgb so the asset preview is readable; only R is imported.
                    pixels[i] = new Color32(mask[i], mask[i], mask[i], 255);
                }

                texture.SetPixels32(pixels);
                texture.Apply(false, false);

                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputAssetPath)));
                File.WriteAllBytes(Path.GetFullPath(outputAssetPath), texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }

            AssetDatabase.ImportAsset(outputAssetPath, ImportAssetOptions.ForceUpdate);
        }

        static void ApplyMaskImportSettings(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                RainLog.Error($"coverage mask has no texture importer: {assetPath}");
                return;
            }

            // singleChannelComponent lives on TextureImporterSettings, not on the importer itself.
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.textureType = TextureImporterType.SingleChannel;
            settings.singleChannelComponent = TextureImporterSingleChannelComponent.Red;
            importer.SetTextureSettings(settings);

            importer.sRGBTexture = false;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
        }

        /// <summary>
        /// Makes a texture readable (uncompressed, Default type) for the duration of a bake and
        /// always restores the original importer settings, even on failure.
        /// </summary>
        sealed class ReadablePixels : IDisposable
        {
            readonly TextureImporter importer;
            readonly string path;
            readonly bool readable;
            readonly TextureImporterType type;
            readonly TextureImporterCompression compression;
            readonly bool srgb;

            public ReadablePixels(string assetPath)
            {
                path = assetPath;
                importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    return;
                }

                readable = importer.isReadable;
                type = importer.textureType;
                compression = importer.textureCompression;
                srgb = importer.sRGBTexture;

                importer.isReadable = true;
                importer.textureType = TextureImporterType.Default;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.sRGBTexture = false;
                importer.SaveAndReimport();
            }

            public void Dispose()
            {
                if (importer == null)
                {
                    return;
                }

                try
                {
                    importer.isReadable = readable;
                    importer.textureType = type;
                    importer.textureCompression = compression;
                    importer.sRGBTexture = srgb;
                    importer.SaveAndReimport();
                }
                catch (Exception e)
                {
                    RainLog.Error($"coverage bake could not restore importer settings for {path}: {e.Message}");
                }
            }
        }
    }
}
