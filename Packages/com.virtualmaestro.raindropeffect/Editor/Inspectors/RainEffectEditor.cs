using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RainDropEffect.Editor
{
    /// <summary>
    /// Inspector for <see cref="RainEffect"/>: playback buttons, a live status line, and the two
    /// setup mistakes that make the effect silently draw nothing — no profile, and no renderer
    /// feature on the renderer this camera uses.
    /// </summary>
    [CustomEditor(typeof(RainEffect))]
    public sealed class RainEffectEditor : UnityEditor.Editor
    {
        public override bool RequiresConstantRepaint() => true;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var effect = (RainEffect)target;

            EditorGUILayout.Space();

            if (effect.Profile == null)
            {
                EditorGUILayout.HelpBox(
                    "No profile assigned. Create one with Assets > Create > Rain Drop Effect > Rain Profile "
                    + "(or the Basic Rain preset).",
                    MessageType.Warning);
            }

            DrawRendererFeatureSection();
            DrawBackbufferWarning(effect);
            DrawPlaybackSection(effect);

            EditorGUILayout.HelpBox(
                "Changing textures or layer counts in the profile requires Rebuild.",
                MessageType.None);
        }

        void DrawRendererFeatureSection()
        {
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null)
            {
                EditorGUILayout.HelpBox(
                    "The active render pipeline is not URP. Rain Drop Effect only renders under the "
                    + "Universal Render Pipeline.",
                    MessageType.Error);
                return;
            }

            var rendererData = FindDefaultRendererData(pipeline);
            if (rendererData == null)
            {
                EditorGUILayout.HelpBox(
                    "Could not read the default renderer from the active URP asset. Add "
                    + "'Rain Renderer Feature' to it manually.",
                    MessageType.Warning);
                return;
            }

            if (HasRainFeature(rendererData))
            {
                return;
            }

            EditorGUILayout.HelpBox(
                $"The renderer '{rendererData.name}' has no Rain Renderer Feature, so nothing is drawn.",
                MessageType.Warning);

            if (GUILayout.Button("Add Rain Renderer Feature"))
            {
                AddRainFeature(rendererData);
            }
        }

        void DrawBackbufferWarning(RainEffect effect)
        {
            var camera = effect.GetComponent<Camera>();
            var cameraData = camera != null ? camera.GetComponent<UniversalAdditionalCameraData>() : null;
            if (cameraData == null || cameraData.renderPostProcessing)
            {
                return;
            }

            EditorGUILayout.HelpBox(
                "Post-processing is off on this camera, so URP may render straight to the backbuffer and "
                + "skip the rain pass. Enable post-processing, or set Intermediate Texture = Always on the "
                + "renderer.",
                MessageType.Info);
        }

        void DrawPlaybackSection(RainEffect effect)
        {
            var layers = effect.State != null ? effect.State.Layers.Count : 0;
            EditorGUILayout.LabelField(
                $"Playing: {effect.IsPlaying}   Emitting: {effect.IsEmitting}   Layers: {layers}");

            // Outside Play Mode nothing ticks unless the component asks for an edit-mode preview.
            using (new EditorGUI.DisabledScope(!Application.isPlaying && !effect.PreviewInEditMode))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Play"))
                {
                    effect.Play();
                }

                if (GUILayout.Button("Stop"))
                {
                    effect.Stop();
                }

                if (GUILayout.Button("Clear"))
                {
                    effect.Clear();
                }

                if (GUILayout.Button("Rebuild"))
                {
                    effect.Rebuild();
                }
            }
        }

        /// <summary>
        /// Reads the default renderer through SerializedObject rather than the internal
        /// <c>rendererDataList</c> property, so a URP minor upgrade cannot break compilation.
        /// </summary>
        internal static ScriptableRendererData FindDefaultRendererData(UniversalRenderPipelineAsset pipeline)
        {
            var serialized = new SerializedObject(pipeline);
            var list = serialized.FindProperty("m_RendererDataList");
            if (list == null || !list.isArray || list.arraySize == 0)
            {
                return null;
            }

            var indexProperty = serialized.FindProperty("m_DefaultRendererIndex");
            var index = indexProperty != null ? Mathf.Clamp(indexProperty.intValue, 0, list.arraySize - 1) : 0;
            return list.GetArrayElementAtIndex(index).objectReferenceValue as ScriptableRendererData;
        }

        internal static bool HasRainFeature(ScriptableRendererData rendererData)
        {
            return rendererData.rendererFeatures.Any(f => f is RainRendererFeature);
        }

        /// <summary>
        /// Adds the feature to the host's renderer asset. Only ever called from the button: the
        /// package does not write into project assets on its own.
        /// </summary>
        internal static void AddRainFeature(ScriptableRendererData rendererData)
        {
            var feature = ScriptableObject.CreateInstance<RainRendererFeature>();
            feature.name = "RainRendererFeature";

            var serialized = new SerializedObject(rendererData);
            var features = serialized.FindProperty("m_RendererFeatures");
            var map = serialized.FindProperty("m_RendererFeatureMap");

            if (features == null || map == null)
            {
                Object.DestroyImmediate(feature);
                RainLog.Error(
                    "could not add renderer feature automatically; add 'Rain Renderer Feature' in the "
                    + "Universal Renderer asset");
                return;
            }

            AssetDatabase.AddObjectToAsset(feature, rendererData);

            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;

            // URP keys the map by the object's local file id, not by its name.
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(rendererData);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            RainLog.Verbose($"renderer feature added to {rendererData.name}");
        }
    }
}
