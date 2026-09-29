using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RainDropEffect.Editor
{
    /// <summary>
    /// Inspector for <see cref="RainProfile"/>: one foldout per layer family, an explicit Add button
    /// per family, and a Sanitize button.
    ///
    /// The Add buttons exist because Unity's own array "+" zero-initializes a plain
    /// <c>[Serializable]</c> element — field initializers do not run — which produces a layer with
    /// no lifetime, no size and no emission that looks broken for no visible reason. Adding through
    /// <c>new</c> gives the declared defaults.
    /// </summary>
    [CustomEditor(typeof(RainProfile))]
    public sealed class RainProfileEditor : UnityEditor.Editor
    {
        const string TextureRoot = "Packages/com.virtualmaestro.raindropeffect/Runtime/Textures";

        static readonly string[] FamilyProperties =
        {
            "StaticLayers", "SimpleLayers", "FlowLayers", "FrictionLayers"
        };

        static readonly string[] FamilyLabels =
        {
            "Static Layers", "Simple Layers", "Flow Layers", "Friction Layers"
        };

        readonly bool[] expanded = { true, true, true, true };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("Version"));
            serializedObject.ApplyModifiedProperties();

            var profile = (RainProfile)target;

            for (var i = 0; i < FamilyProperties.Length; i++)
            {
                serializedObject.Update();
                var property = serializedObject.FindProperty(FamilyProperties[i]);

                expanded[i] = EditorGUILayout.Foldout(
                    expanded[i], $"{FamilyLabels[i]} ({property.arraySize})", true);

                if (expanded[i])
                {
                    EditorGUI.indentLevel++;
                    for (var e = 0; e < property.arraySize; e++)
                    {
                        EditorGUILayout.PropertyField(property.GetArrayElementAtIndex(e), true);
                    }

                    EditorGUI.indentLevel--;

                    if (GUILayout.Button($"Add {FamilyLabels[i].TrimEnd('s')}"))
                    {
                        AddLayer(profile, i);
                    }
                }

                serializedObject.ApplyModifiedProperties();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Total layers: {profile.LayerCount}");

            if (GUILayout.Button("Sanitize"))
            {
                Undo.RecordObject(profile, "Sanitize Rain Profile");
                profile.Sanitize();
                EditorUtility.SetDirty(profile);
            }
        }

        static void AddLayer(RainProfile profile, int family)
        {
            Undo.RecordObject(profile, "Add Rain Layer");

            switch (family)
            {
                case 0:
                    profile.StaticLayers = Append(profile.StaticLayers, new StaticLayerSettings { Name = "Static" });
                    break;
                case 1:
                    profile.SimpleLayers = Append(profile.SimpleLayers, new SimpleLayerSettings { Name = "Simple" });
                    break;
                case 2:
                    profile.FlowLayers = Append(profile.FlowLayers, new FlowLayerSettings { Name = "Flow" });
                    break;
                default:
                    profile.FrictionLayers =
                        Append(profile.FrictionLayers, new FrictionLayerSettings { Name = "Friction" });
                    break;
            }

            EditorUtility.SetDirty(profile);
        }

        static T[] Append<T>(T[] source, T value)
        {
            var result = new T[(source?.Length ?? 0) + 1];
            if (source != null)
            {
                Array.Copy(source, result, source.Length);
            }

            result[result.Length - 1] = value;
            return result;
        }

        [MenuItem("Assets/Create/Rain Drop Effect/Rain Profile (Basic Rain)", false, 82)]
        static void CreateBasicRainProfileMenu()
        {
            var folder = "Assets";
            foreach (var guid in Selection.assetGUIDs)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                folder = AssetDatabase.IsValidFolder(path) ? path : Path.GetDirectoryName(path).Replace('\\', '/');
                break;
            }

            var assetPath = AssetDatabase.GenerateUniqueAssetPath(folder + "/BasicRainProfile.asset");
            var profile = CreateBasicRainProfile(assetPath);
            Selection.activeObject = profile;
        }

        /// <summary>
        /// The shipped starting point: a rain frame that fades in, plus falling droplets. Also the
        /// asset the Basic sample uses, which is why the path is a parameter.
        /// </summary>
        internal static RainProfile CreateBasicRainProfile(string assetPath)
        {
            var profile = ScriptableObject.CreateInstance<RainProfile>();

            profile.StaticLayers = new[]
            {
                new StaticLayerSettings
                {
                    Name = "Rain Frame",
                    Depth = 0,
                    NormalMap = LoadTexture("Rain/rain_frame_normal.tga"),
                    OverlayTexture = LoadTexture("Rain/rain_frame.tga"),
                    CoverageMask = LoadTexture("Coverage/rain_frame_coverage.png"),
                    FullScreen = true,
                    Distortion = 20f,
                    Relief = 1f,
                    FadeTime = 2f
                }
            };

            profile.SimpleLayers = new[]
            {
                new SimpleLayerSettings
                {
                    Name = "Rain Drops",
                    Depth = 1,
                    NormalMap = LoadTexture("Rain/rain_drop_normal.tga"),
                    OverlayTexture = LoadTexture("Rain/rain_drop.tga"),
                    CoverageMask = LoadTexture("Coverage/rain_drop_coverage.png"),
                    MaxCount = 20,
                    Distortion = 50f,
                    Relief = 1.5f,

                    // Identical size, identical orientation and instant appearance read as a stamped
                    // pattern rather than as rain, which is what this preset shipped as in 2.0.0.
                    // Rain1 — converted from the original 1.x preset — is the reference for what
                    // plausible values look like: a real spread, well under a tenth of half-screen.
                    SizeMin = new Vector2(0.054f, 0.054f),
                    SizeMax = new Vector2(0.092f, 0.092f),

                    // One drop texture repeated at one angle is the single biggest contributor to
                    // the pattern reading.
                    AutoRotate = true,

                    // Arrive rather than appear: a short fade-in, then gone by the end of the life.
                    AlphaOverLifetime = new AnimationCurve(
                        new Keyframe(0f, 0f),
                        new Keyframe(0.18f, 1f),
                        new Keyframe(1f, 0f)),

                    // A drop swells slightly as it settles, then holds.
                    SizeOverLifetime = new AnimationCurve(
                        new Keyframe(0f, 0.8f),
                        new Keyframe(0.25f, 1f),
                        new Keyframe(1f, 1f)),

                    // Non-zero, and growing with age: a motionless drop reads as a decal.
                    DriftOverLifetime = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f)
                }
            };

            AssetDatabase.CreateAsset(profile, assetPath);
            AssetDatabase.SaveAssets();
            RainLog.Verbose($"created basic rain profile at {assetPath}");
            return profile;
        }

        static Texture2D LoadTexture(string relativePath)
        {
            var path = TextureRoot + "/" + relativePath;
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                RainLog.Warn($"basic profile: missing texture {path}");
            }

            return texture;
        }
    }
}
