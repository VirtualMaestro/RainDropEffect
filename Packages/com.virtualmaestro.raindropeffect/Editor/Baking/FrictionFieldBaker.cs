using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RainDropEffect.Editor
{
    /// <summary>
    /// Bakes a friction texture into a <see cref="FrictionField"/> asset (Task 21).
    ///
    /// Doing this at import time is the whole point: the runtime then needs no readable, uncompressed
    /// copy of the texture in memory, and the per-frame cost drops from <c>GetPixel</c> to an array
    /// read.
    /// </summary>
    public static class FrictionFieldBaker
    {
        public const string OutputDirectory =
            "Packages/com.virtualmaestro.raindropeffect/Runtime/Data";

        const string ShippedTexture =
            "Packages/com.virtualmaestro.raindropeffect/Runtime/Textures/Rain/friction_map.tga";

        [MenuItem("Rain Drop Effect/Bake Friction Field")]
        public static void BakeShipped()
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(ShippedTexture);
            if (texture == null)
            {
                RainLog.Error($"friction bake: missing {ShippedTexture}");
                return;
            }

            var output = $"{OutputDirectory}/FrictionField_{Path.GetFileNameWithoutExtension(ShippedTexture)}.asset";
            var field = Bake(texture, output);

            if (field != null)
            {
                // The runtime samples the baked asset, never the texture.
                SetImporter(ShippedTexture, readable: false, mipmaps: false);
            }
        }

        /// <summary>
        /// Reads <paramref name="source"/> and writes a field asset at
        /// <paramref name="outputAssetPath"/>. Deterministic and idempotent: the same texture always
        /// produces the same bytes.
        /// </summary>
        public static FrictionField Bake(Texture2D source, string outputAssetPath)
        {
            if (source == null)
            {
                RainLog.Error("friction bake: source texture is null");
                return null;
            }

            var sourcePath = AssetDatabase.GetAssetPath(source);
            Color32[] pixels;
            int width;
            int height;

            using (new ReadablePixels(sourcePath))
            {
                var readable = AssetDatabase.LoadAssetAtPath<Texture2D>(sourcePath);
                width = readable.width;
                height = readable.height;
                pixels = readable.GetPixels32();
            }

            var values = Bake(pixels, width, height);

            Directory.CreateDirectory(Path.GetFullPath(Path.GetDirectoryName(outputAssetPath)));

            var field = AssetDatabase.LoadAssetAtPath<FrictionField>(outputAssetPath);
            var created = field == null;
            if (created)
            {
                field = ScriptableObject.CreateInstance<FrictionField>();
            }

            field.Width = width;
            field.Height = height;
            field.Values = values;
            field.SourceTexturePath = sourcePath;
            field.SourceGuid = AssetDatabase.AssetPathToGUID(sourcePath);

            if (created)
            {
                AssetDatabase.CreateAsset(field, outputAssetPath);
            }
            else
            {
                EditorUtility.SetDirty(field);
            }

            AssetDatabase.SaveAssets();

            RainLog.Verbose($"friction field baked: {width}x{height} from {sourcePath}");
            Debug.Log($"[RainDropEffect] friction field {width}x{height} ({values.Length} bytes) -> {outputAssetPath}");
            return field;
        }

        /// <summary>
        /// The pure part: grayscale, invert, flip rows. Exposed separately from asset I/O so it can
        /// be tested without importer round-trips.
        /// </summary>
        public static byte[] Bake(Color32[] pixels, int width, int height)
        {
            var values = new byte[width * height];

            for (var y = 0; y < height; y++)
            {
                // Legacy sampled row H - 1 - y for viewport row y, so the bake carries the flip and
                // the runtime sample stays a plain lookup.
                var sourceRow = (height - 1 - y) * width;
                var targetRow = y * width;

                for (var x = 0; x < width; x++)
                {
                    var c = pixels[sourceRow + x];

                    // Unity's Color.grayscale weights, on the raw bytes the legacy GetPixel returned.
                    var gray = (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) / 255f;
                    values[targetRow + x] = (byte)Mathf.Clamp(Mathf.RoundToInt(255f * (1f - gray)), 0, 255);
                }
            }

            return values;
        }

        static void SetImporter(string assetPath, bool readable, bool mipmaps)
        {
            if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer)
            {
                RainLog.Error($"friction bake: no texture importer for {assetPath}");
                return;
            }

            importer.isReadable = readable;
            importer.mipmapEnabled = mipmaps;
            importer.SaveAndReimport();
            RainLog.Verbose($"friction source importer: readable={readable}, mipmaps={mipmaps}");
        }

        /// <summary>
        /// Makes a texture readable for the duration of a bake and always restores the original
        /// importer settings, even on failure.
        /// </summary>
        sealed class ReadablePixels : IDisposable
        {
            readonly TextureImporter importer;
            readonly string path;
            readonly bool readable;
            readonly TextureImporterType type;
            readonly TextureImporterCompression compression;

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

                importer.isReadable = true;
                importer.textureType = TextureImporterType.Default;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
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
                    importer.SaveAndReimport();
                }
                catch (Exception e)
                {
                    RainLog.Error($"friction bake could not restore importer settings for {path}: {e.Message}");
                }
            }
        }
    }
}
