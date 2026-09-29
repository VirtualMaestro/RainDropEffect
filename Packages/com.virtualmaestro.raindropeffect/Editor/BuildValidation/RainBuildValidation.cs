using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace RainDropEffect.Editor
{
    /// <summary>
    /// Batch-mode entry points for the clean-install gate (Task 11), the benchmark builds (Task 25)
    /// and the release builds (Task 32).
    ///
    /// Every method is parameterless and driven by <see cref="EditorUserBuildSettings"/> so it can
    /// be called with <c>-executeMethod</c>; each exits with code 1 on a failed build so the calling
    /// script sees the failure without parsing the log.
    /// </summary>
    public static class RainBuildValidation
    {
        const string ValidationSceneFolder = "Assets/RainValidation";
        const string ValidationScene = ValidationSceneFolder + "/Validation.unity";
        const string OutputRoot = "Builds/Validation";
        const string PipelineAsset = "Assets/Settings/Rain-URP.asset";

        /// <summary>Project root (the folder that contains Assets/), usable from batch mode.</summary>
        static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        [MenuItem("Rain Drop Effect/Validation/Build Windows")]
        public static void BuildWindows()
        {
            Build(BuildTarget.StandaloneWindows64, BuildTargetGroup.Standalone,
                OutputRoot + "/Windows/RainValidation.exe", "Windows");
        }

        [MenuItem("Rain Drop Effect/Validation/Build WebGL")]
        public static void BuildWebGL()
        {
            Build(BuildTarget.WebGL, BuildTargetGroup.WebGL, OutputRoot + "/WebGL", "WebGL");
        }

        [MenuItem("Rain Drop Effect/Validation/Build Android")]
        public static void BuildAndroid()
        {
            EditorUserBuildSettings.buildAppBundle = false;
            Build(BuildTarget.Android, BuildTargetGroup.Android,
                OutputRoot + "/Android/RainValidation.apk", "Android");
        }

        /// <summary>
        /// Imports both samples into <c>Assets/Samples/</c> so the release checks can see them:
        /// <c>Samples~</c> is invisible to the Asset Database, and an unopened sample scene is an
        /// unchecked one. Pair with <see cref="RemoveImportedSamples"/>.
        /// </summary>
        [MenuItem("Rain Drop Effect/Validation/Import Samples")]
        public static void ImportSamples()
        {
            var samples = UnityEditor.PackageManager.UI.Sample
                .FindByPackage("com.virtualmaestro.raindropeffect", string.Empty).ToList();

            if (samples.Count == 0)
            {
                RainLog.Error("the package declares no samples");
                return;
            }

            foreach (var sample in samples)
            {
                var imported = sample.Import(
                    UnityEditor.PackageManager.UI.Sample.ImportOptions.OverridePreviousImports);
                Debug.Log($"[RainDropEffect] import '{sample.displayName}' -> {imported}");
            }

            AssetDatabase.Refresh();
        }

        [MenuItem("Rain Drop Effect/Validation/Remove Imported Samples")]
        public static void RemoveImportedSamples()
        {
            if (AssetDatabase.IsValidFolder("Assets/Samples"))
            {
                AssetDatabase.DeleteAsset("Assets/Samples");
            }

            AssetDatabase.Refresh();
            Debug.Log("[RainDropEffect] removed Assets/Samples");
        }

        /// <summary>
        /// Exports the package as a tarball into <c>Builds/Package/</c> (Task 32). The tarball, not
        /// the working tree, is what a consumer installs, so it is what the release gate tests.
        /// </summary>
        [MenuItem("Rain Drop Effect/Validation/Pack Package")]
        public static void PackPackage()
        {
            var output = Path.Combine(ProjectRoot, "Builds", "Package");
            Directory.CreateDirectory(output);

            var request = UnityEditor.PackageManager.Client.Pack(
                "Packages/com.virtualmaestro.raindropeffect", output);

            // Batch mode has no editor loop to pump the request; spinning is the documented way.
            while (!request.IsCompleted)
            {
                System.Threading.Thread.Sleep(100);
            }

            if (request.Status != UnityEditor.PackageManager.StatusCode.Success)
            {
                RainLog.Error($"pack failed: {request.Error?.message}");
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log($"[RainDropEffect] packed {request.Result.tarballPath}");
        }

        /// <summary>
        /// Assigns the shipped URP asset as the render pipeline for the default and every quality
        /// level. A fresh consumer project has no pipeline assigned, and URP shaders render magenta
        /// (or the build fails outright on WebGL) without it.
        /// </summary>
        [MenuItem("Rain Drop Effect/Validation/Configure URP")]
        public static void ConfigureUrp()
        {
            var asset = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(PipelineAsset);
            if (asset == null)
            {
                RainLog.Error($"clean-install: no pipeline asset at {PipelineAsset}");
                Debug.LogError($"[RainDropEffect] configure urp failed: no asset at {PipelineAsset}");
                EditorApplication.Exit(2);
                return;
            }

            GraphicsSettings.defaultRenderPipeline = asset;

            // Unity 6 only exposes the *current* level's override, so walk the levels.
            var currentLevel = QualitySettings.GetQualityLevel();
            for (var i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = asset;
            }

            QualitySettings.SetQualityLevel(currentLevel, false);

            WriteCaptureScript();

            AssetDatabase.SaveAssets();
            RainLog.Verbose($"clean-install: pipeline assigned to {QualitySettings.names.Length} quality levels");
            Debug.Log($"[RainDropEffect] configure urp result=ok levels={QualitySettings.names.Length}");
        }

        /// <summary>
        /// Drops a tiny capture behaviour into the consumer project so the built player can prove it
        /// actually renders rain, not merely that it linked.
        ///
        /// It is written here, in the first batch-mode run, because the build in the second run needs
        /// it already compiled: a script created and used in the same invocation never gets a domain
        /// reload.
        /// </summary>
        static void WriteCaptureScript()
        {
            if (!AssetDatabase.IsValidFolder(ValidationSceneFolder))
            {
                AssetDatabase.CreateFolder("Assets", "RainValidation");
            }

            var path = ValidationSceneFolder + "/RainValidationCapture.cs";
            if (File.Exists(path))
            {
                return;
            }

            File.WriteAllText(path, CaptureScriptSource);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            RainLog.Verbose($"clean-install: wrote {path}");
        }

        const string CaptureScriptSource = @"using UnityEngine;
using RainDropEffect;

/// <summary>
/// Generated by RainBuildValidation. Plays the rain, screenshots the result next to the player, and
/// quits, so a build can be checked without a human watching it.
/// </summary>
public class RainValidationCapture : MonoBehaviour
{
    const float CaptureAt = 2f;

    float elapsed;
    bool captured;

    void Start()
    {
        Application.targetFrameRate = 60;
        var effect = GetComponent<RainEffect>();
        if (effect != null)
        {
            effect.Play();
        }

        Debug.Log(""[RainValidation] started, effect="" + (effect != null));
    }

    void Update()
    {
        elapsed += Time.deltaTime;

        if (!captured && elapsed >= CaptureAt)
        {
            captured = true;
            var path = Application.dataPath + ""/../rain-validation.png"";
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log(""[RainValidation] captured "" + path);
        }
        else if (captured && elapsed >= CaptureAt + 1.5f)
        {
            Debug.Log(""[RainValidation] done"");
            Application.Quit();
        }
    }
}
";

        static void Build(BuildTarget target, BuildTargetGroup group, string relativeOutput, string label)
        {
            var scenes = EnabledScenes();
            var output = Path.Combine(ProjectRoot, relativeOutput);
            Directory.CreateDirectory(Path.GetDirectoryName(output));

            RainLog.Verbose($"clean-install: building {label} to {output} with {scenes.Length} scene(s)");

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                target = target,
                targetGroup = group,
                locationPathName = output,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            var errors = report.steps.SelectMany(s => s.messages)
                .Count(m => m.type == LogType.Error || m.type == LogType.Exception);

            Debug.Log($"[RainDropEffect] build {label} result={summary.result} size={summary.totalSize}");
            WriteReport(label, summary, errors);

            if (summary.result != BuildResult.Succeeded)
            {
                RainLog.Error($"clean-install: build {label} failed with {summary.totalErrors} error(s)");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// The enabled build scenes, or a generated one. A player needs at least one scene, and a
        /// fresh consumer project has none — the generated scene is also the smallest possible
        /// proof that RainEffect survives into the player.
        /// </summary>
        static string[] EnabledScenes()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length > 0)
            {
                return scenes;
            }

            if (!AssetDatabase.IsValidFolder(ValidationSceneFolder))
            {
                AssetDatabase.CreateFolder("Assets", "RainValidation");
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Main Camera", typeof(Camera));
            camera.tag = "MainCamera";

            var effect = camera.AddComponent<RainDropEffect.RainEffect>();
            effect.EditorAutoAssignShaderSet();
            effect.Profile = ValidationProfile();
            effect.Rebuild();

            // Written by ConfigureUrp in the previous batch-mode run, so it is compiled by now.
            var captureType = System.Type.GetType("RainValidationCapture, Assembly-CSharp");
            if (captureType != null)
            {
                camera.AddComponent(captureType);
            }
            else
            {
                RainLog.Warn("clean-install: RainValidationCapture is not compiled; the player will not screenshot");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ValidationScene);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ValidationScene, true) };
            RainLog.Verbose($"clean-install: generated {ValidationScene}");
            return new[] { ValidationScene };
        }

        /// <summary>The Basic Rain preset, created in the consumer project so the scene has something to show.</summary>
        static RainProfile ValidationProfile()
        {
            var path = ValidationSceneFolder + "/BasicRainProfile.asset";
            var existing = AssetDatabase.LoadAssetAtPath<RainProfile>(path);
            return existing != null ? existing : RainProfileEditor.CreateBasicRainProfile(path);
        }

        static void WriteReport(string label, BuildSummary summary, int errors)
        {
            var path = Path.Combine(ProjectRoot, OutputRoot, label + ".json");
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            var json =
                "{\n" +
                $"  \"result\": \"{summary.result}\",\n" +
                $"  \"totalSize\": {summary.totalSize},\n" +
                $"  \"totalTime\": \"{summary.totalTime}\",\n" +
                $"  \"errors\": {errors}\n" +
                "}\n";

            File.WriteAllText(path, json);
            RainLog.Verbose($"clean-install: wrote {path}");
        }
    }
}
