using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace RainDropEffect.Editor
{
    /// <summary>
    /// Checks that nothing the package ships points outside the package (Task 24).
    ///
    /// A profile that still references a texture under <c>Assets/</c> works perfectly in this
    /// project and breaks the moment a consumer installs the package, which is exactly the class of
    /// bug a clean-install gate cannot see: the asset resolves here because the host happens to
    /// have it.
    /// </summary>
    public static class ReferenceAudit
    {
        const string PackageRoot = "Packages/com.virtualmaestro.raindropeffect";
        const string OutputPath = "Logs/reference-audit.json";
        const string MissingScriptsPath = "Logs/missing-scripts.json";

        /// <summary>Prefixes a shipped asset is allowed to depend on.</summary>
        static readonly string[] AllowedPrefixes =
        {
            PackageRoot + "/",
            "Packages/com.unity.render-pipelines.",
            "Library/unity default resources",
            "Resources/unity_builtin_extra"
        };

        [MenuItem("Rain Drop Effect/Validate Package References")]
        public static void Validate()
        {
            var offenders = new List<(string Asset, string Dependency)>();
            var checkedAssets = 0;

            foreach (var asset in ShippedAssets())
            {
                checkedAssets++;
                foreach (var dependency in AssetDatabase.GetDependencies(asset, true))
                {
                    if (dependency == asset || IsAllowed(dependency))
                    {
                        continue;
                    }

                    offenders.Add((asset, dependency));
                    RainLog.Error($"reference audit: {asset} depends on {dependency}");
                }
            }

            Write(checkedAssets, offenders);

            var summary = $"reference audit: {checkedAssets} assets, {offenders.Count} external references";
            RainLog.Verbose(summary);
            Debug.Log($"[RainDropEffect] {summary} -> {OutputPath}");
        }

        /// <summary>
        /// Task 29 step 5: a prefab or scene that lost a script still opens, still saves, and
        /// silently drops whatever that component held. Deleting the legacy runtime is exactly the
        /// change that produces one, so the count is checked rather than eyeballed.
        ///
        /// <c>Samples~</c> is invisible to the Asset Database; run this while the folder is renamed
        /// to <c>Samples</c> during the Task 28 authoring session, or after importing the samples
        /// into <c>Assets/Samples/</c>.
        /// </summary>
        [MenuItem("Rain Drop Effect/Validate Missing Scripts")]
        public static void ValidateMissingScripts()
        {
            var offenders = new List<(string Asset, int Count)>();
            var checkedAssets = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets", PackageRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    continue;
                }

                checkedAssets++;
                var missing = CountMissing(prefab);
                if (missing > 0)
                {
                    offenders.Add((path, missing));
                    RainLog.Error($"missing scripts: {path} has {missing}");
                }
            }

            // A scene has to be opened to be inspected. Every scene under Assets and the package is
            // opened additively and closed again, because a scene that lost a script still opens
            // and still saves - it just drops what the component held.
            foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets", PackageRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                UnityEngine.SceneManagement.Scene scene;

                try
                {
                    scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                        path, UnityEditor.SceneManagement.OpenSceneMode.Additive);
                }
                catch (System.Exception e)
                {
                    offenders.Add((path, -1));
                    RainLog.Error($"missing scripts: {path} could not be opened: {e.Message}");
                    continue;
                }

                checkedAssets++;
                var missing = 0;
                foreach (var root in scene.GetRootGameObjects())
                {
                    missing += CountMissing(root);
                }

                if (missing > 0)
                {
                    offenders.Add((path, missing));
                    RainLog.Error($"missing scripts: {path} has {missing}");
                }

                UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
            }

            WriteMissingScripts(checkedAssets, offenders);

            var summary = $"missing-script audit: {checkedAssets} assets, {offenders.Count} with missing scripts";
            RainLog.Verbose(summary);
            Debug.Log($"[RainDropEffect] {summary} -> {MissingScriptsPath}");
        }

        static int CountMissing(GameObject root)
        {
            var count = UnityEditor.GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root);

            foreach (Transform child in root.transform)
            {
                count += CountMissing(child.gameObject);
            }

            return count;
        }

        static void WriteMissingScripts(int checkedAssets, List<(string Asset, int Count)> offenders)
        {
            var json = new StringBuilder();
            json.Append("{\n");
            json.Append($"  \"checkedAssets\": {checkedAssets},\n");
            json.Append($"  \"missingScripts\": {offenders.Count},\n");
            json.Append("  \"offenders\": [\n");

            for (var i = 0; i < offenders.Count; i++)
            {
                json.Append($"    {{ \"asset\": \"{offenders[i].Asset}\", \"count\": {offenders[i].Count} }}");
                json.Append(i < offenders.Count - 1 ? ",\n" : "\n");
            }

            json.Append("  ]\n}\n");

            Directory.CreateDirectory("Logs");
            File.WriteAllText(MissingScriptsPath, json.ToString());
        }

        static bool IsAllowed(string path)
        {
            foreach (var prefix in AllowedPrefixes)
            {
                if (path.StartsWith(prefix, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Everything the package ships that can hold a reference. <c>Samples~</c> is invisible to
        /// the Asset Database, so it is audited during the rename workflow (Task 28) rather than here.
        /// </summary>
        static IEnumerable<string> ShippedAssets()
        {
            var folders = new List<string>();
            foreach (var folder in new[] { "/Runtime/Profiles", "/Runtime/Data", "/Runtime/Shaders", "/Runtime/Textures" })
            {
                if (AssetDatabase.IsValidFolder(PackageRoot + folder))
                {
                    folders.Add(PackageRoot + folder);
                }
            }

            if (folders.Count == 0)
            {
                yield break;
            }

            foreach (var guid in AssetDatabase.FindAssets(string.Empty, folders.ToArray()))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!AssetDatabase.IsValidFolder(path))
                {
                    yield return path;
                }
            }
        }

        static void Write(int checkedAssets, List<(string Asset, string Dependency)> offenders)
        {
            var json = new StringBuilder();
            json.Append("{\n");
            json.Append($"  \"checkedAssets\": {checkedAssets},\n");
            json.Append($"  \"externalReferences\": {offenders.Count},\n");

            // The release gate greps for the literal '"external": []', so the flat list of offending
            // dependency paths is written as well as the asset/dependency pairs below.
            var external = new List<string>();
            foreach (var offender in offenders)
            {
                if (!external.Contains(offender.Dependency))
                {
                    external.Add(offender.Dependency);
                }
            }

            json.Append($"  \"external\": [{string.Join(", ", external.ConvertAll(p => $"\"{p}\""))}],\n");
            json.Append("  \"offenders\": [\n");

            for (var i = 0; i < offenders.Count; i++)
            {
                json.Append($"    {{ \"asset\": \"{offenders[i].Asset}\", \"dependency\": \"{offenders[i].Dependency}\" }}");
                json.Append(i < offenders.Count - 1 ? ",\n" : "\n");
            }

            json.Append("  ]\n}\n");

            Directory.CreateDirectory("Logs");
            File.WriteAllText(OutputPath, json.ToString());
        }
    }
}
