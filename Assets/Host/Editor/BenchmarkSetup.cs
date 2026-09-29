using System;
using System.IO;
using System.Linq;
using RainDropEffect;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Host-only, reproducible setup for Task 25's benchmark: the three <c>Bench_*</c> profiles, the
/// <c>Assets/Host/Benchmark.unity</c> scene and a Development Windows build of it.
///
/// Everything is generated from code so a re-run produces the same scene: the benchmark is evidence
/// and must not depend on hand-placed props. The backdrop is built from primitives instead of the
/// demo still life because the demo folder is removed in Task 29.
/// </summary>
public static class BenchmarkSetup
{
    const string PackageRoot = "Packages/com.virtualmaestro.raindropeffect";
    const string ProfileDir = PackageRoot + "/Runtime/Profiles";
    const string TextureDir = PackageRoot + "/Runtime/Textures";
    const string HostDir = "Assets/Host";
    const string BenchmarkScene = HostDir + "/Benchmark.unity";
    const string SmokeScene = HostDir + "/Smoke.unity";

    /// <summary>The Basic sample profile lives in Samples~, which the AssetDatabase cannot see;
    /// the host smoke scene therefore uses a shipped Runtime profile.</summary>
    const string SmokeProfilePath = ProfileDir + "/Rain3.asset";

    [MenuItem("Rain Drop Effect/Benchmark/Create Benchmark Profiles")]
    public static void CreateProfiles()
    {
        CreateProfile("Bench_Sparse", BuildSparse);
        CreateProfile("Bench_Trails", BuildTrails);
        CreateProfile("Bench_Combined", BuildCombined);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[RainBenchmark] benchmark profiles written to " + ProfileDir);
    }

    [MenuItem("Rain Drop Effect/Benchmark/Create Benchmark Scene")]
    public static void CreateBenchmarkScene()
    {
        var profiles = new[] { "Bench_Sparse", "Bench_Trails", "Bench_Combined" }
            .Select(n => AssetDatabase.LoadAssetAtPath<RainProfile>($"{ProfileDir}/{n}.asset"))
            .Where(p => p != null)
            .ToArray();

        if (profiles.Length != 3)
        {
            Debug.LogError("[RainBenchmark] create the benchmark profiles first");
            return;
        }

        var scene = BuildScene(out var camera);

        var driver = camera.gameObject.AddComponent<BenchmarkDriver>();
        driver.Effect = camera.GetComponent<RainEffect>();
        driver.Profiles = profiles;
        driver.PlatformLabel = string.Empty;

        Directory.CreateDirectory(HostDir);
        EditorSceneManager.SaveScene(scene, BenchmarkScene);
        Debug.Log("[RainBenchmark] wrote " + BenchmarkScene);
    }

    /// <summary>
    /// Task 29 step 4: the host keeps one smoke scene (camera + effect + backdrop) after the demo
    /// scenes are deleted. Same generator, no driver, the Basic profile.
    /// </summary>
    [MenuItem("Rain Drop Effect/Benchmark/Create Host Smoke Scene")]
    public static void CreateSmokeScene()
    {
        var scene = BuildScene(out var camera);
        var effect = camera.GetComponent<RainEffect>();
        effect.Profile = AssetDatabase.LoadAssetAtPath<RainProfile>(SmokeProfilePath);
        effect.Quality = RainQuality.High;

        Directory.CreateDirectory(HostDir);
        EditorSceneManager.SaveScene(scene, SmokeScene);

        // The smoke scene replaces the deleted Demo1.unity as the host's only enabled build scene;
        // the benchmark scene stays listed but disabled, so a stray build does not measure instead
        // of smoking.
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(SmokeScene, true),
            new EditorBuildSettingsScene(BenchmarkScene, false)
        };

        Debug.Log("[RainBenchmark] wrote " + SmokeScene + " and set it as the only enabled build scene");
    }

    /// <summary>
    /// Development build of the benchmark scene only. Editor Play Mode cannot measure GPU time, so
    /// this is the only valid Windows measurement path (Task 25 step 3).
    /// </summary>
    [MenuItem("Rain Drop Effect/Benchmark/Build Windows Benchmark Player")]
    public static void BuildWindowsBenchmark()
    {
        Build(BenchmarkScene, "Builds/Benchmark/Windows/RainBenchmark.exe");
    }

    public static void BuildWindowsBenchmarkFromCommandLine()
    {
        PlayerSettings.enableFrameTimingStats = true;
        Build(BenchmarkScene, "Builds/Benchmark/Windows/RainBenchmark.exe");
    }

    /// <summary>
    /// WebGL2 benchmark build (Task 25 step 3, Task 31 step 2). GPU timings are unavailable in a
    /// browser, so this run reports frame time only — the driver says so in its JSON.
    /// </summary>
    [MenuItem("Rain Drop Effect/Benchmark/Build WebGL Benchmark Player")]
    public static void BuildWebGLBenchmark()
    {
        PlayerSettings.enableFrameTimingStats = true;

        // A compressed build needs server-side content negotiation; the local test server has none.
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;

        Build(BenchmarkScene, "Builds/Benchmark/WebGL", BuildTarget.WebGL, BuildTargetGroup.WebGL);
    }

    static void Build(string scene, string relativeOutput)
    {
        Build(scene, relativeOutput, BuildTarget.StandaloneWindows64, BuildTargetGroup.Standalone);
    }

    static void Build(string scene, string relativeOutput, BuildTarget target, BuildTargetGroup group)
    {
        // FrameTimingManager returns nothing without this flag, and the JSON would silently claim
        // "GPU timing unavailable" on a platform that supports it.
        PlayerSettings.enableFrameTimingStats = true;

        var root = Directory.GetParent(Application.dataPath).FullName;
        var output = Path.Combine(root, relativeOutput.Replace('/', Path.DirectorySeparatorChar));

        // A WebGL build output is a directory, a Windows one a file; only the parent is common.
        var parent = target == BuildTarget.WebGL ? output : Path.GetDirectoryName(output);
        Directory.CreateDirectory(parent);

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { scene },
            target = target,
            targetGroup = group,
            locationPathName = output,
            options = BuildOptions.Development
        });

        Debug.Log($"[RainBenchmark] build {report.summary.result} -> {output}");

        if (report.summary.result != BuildResult.Succeeded)
        {
            Debug.LogError("[RainBenchmark] benchmark build failed");
        }
    }

    // ---------------------------------------------------------------- scene generation

    /// <summary>
    /// Opaque still life plus twenty transparent quads and a rotating cube: the pass copies scene
    /// color after transparents, so transparent content is part of what is being measured.
    /// </summary>
    static Scene BuildScene(out Camera camera)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var lightObject = new GameObject("Directional Light");
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.1f;
        lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.13f, 0.15f, 0.18f, 1f);
        camera.transform.position = new Vector3(0f, 1f, -6f);
        cameraObject.AddComponent<AudioListener>();
        cameraObject.AddComponent<RainEffect>();

        var opaque = CreateMaterial("Host_BenchOpaque", new Color(0.62f, 0.58f, 0.52f, 1f), false);
        var transparent = CreateMaterial("Host_BenchTransparent", new Color(0.55f, 0.75f, 0.95f, 0.35f), true);

        var stillLife = new GameObject("StillLife").transform;
        var random = new System.Random(20260910);

        for (var i = 0; i < 18; i++)
        {
            // Sphere, Capsule, Cylinder, Cube — the four solid primitives.
            var prop = GameObject.CreatePrimitive((PrimitiveType)(i % 4));
            prop.name = $"Prop_{i}";
            prop.transform.SetParent(stillLife, false);
            prop.transform.localPosition = new Vector3(
                (float)(random.NextDouble() * 8.0 - 4.0),
                (float)(random.NextDouble() * 3.0 - 0.5),
                (float)(random.NextDouble() * 5.0));
            prop.transform.localRotation = Quaternion.Euler(
                (float)(random.NextDouble() * 360.0),
                (float)(random.NextDouble() * 360.0),
                (float)(random.NextDouble() * 360.0));
            prop.transform.localScale = Vector3.one * (0.4f + (float)random.NextDouble() * 0.8f);
            prop.GetComponent<Renderer>().sharedMaterial = opaque;
        }

        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor";
        floor.transform.SetParent(stillLife, false);
        floor.transform.localPosition = new Vector3(0f, -1.6f, 1f);
        floor.transform.localScale = new Vector3(20f, 0.2f, 20f);
        floor.GetComponent<Renderer>().sharedMaterial = opaque;

        var glass = new GameObject("TransparentQuads").transform;
        for (var i = 0; i < 20; i++)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = $"Glass_{i}";
            quad.transform.SetParent(glass, false);
            quad.transform.localPosition = new Vector3(-3.5f + i % 5 * 1.8f, 0.4f + i / 5 * 1.2f, -1.5f + i * 0.08f);
            quad.transform.localScale = Vector3.one * 1.6f;
            quad.GetComponent<Renderer>().sharedMaterial = transparent;
        }

        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "RotatingCube";
        cube.transform.position = new Vector3(0f, 0.6f, -2.5f);
        cube.GetComponent<Renderer>().sharedMaterial = opaque;
        cube.AddComponent<HostRotator>();

        return scene;
    }

    static Material CreateMaterial(string name, Color color, bool transparent)
    {
        var path = $"{HostDir}/{name}.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            return existing;
        }

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            throw new InvalidOperationException("URP/Lit not found; is URP installed?");
        }

        var material = new Material(shader) { name = name };
        material.SetColor("_BaseColor", color);

        if (transparent)
        {
            // The URP/Lit surface-type switch is a set of properties plus keywords; setting the
            // colour alpha alone leaves the material opaque.
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_AlphaClip", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        Directory.CreateDirectory(HostDir);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    // ---------------------------------------------------------------- profile generation

    static void CreateProfile(string name, Action<RainProfile> build)
    {
        var path = $"{ProfileDir}/{name}.asset";
        var profile = AssetDatabase.LoadAssetAtPath<RainProfile>(path);
        var created = profile == null;

        if (created)
        {
            profile = ScriptableObject.CreateInstance<RainProfile>();
        }

        profile.StaticLayers = Array.Empty<StaticLayerSettings>();
        profile.SimpleLayers = Array.Empty<SimpleLayerSettings>();
        profile.FlowLayers = Array.Empty<FlowLayerSettings>();
        profile.FrictionLayers = Array.Empty<FrictionLayerSettings>();

        build(profile);
        profile.Sanitize();

        if (created)
        {
            AssetDatabase.CreateAsset(profile, path);
        }
        else
        {
            EditorUtility.SetDirty(profile);
        }
    }

    static void BuildSparse(RainProfile profile)
    {
        profile.StaticLayers = new[] { Frame() };
        profile.SimpleLayers = new[] { Drops("Drops32", 32, new Vector2Int(30, 40)) };
    }

    static void BuildTrails(RainProfile profile)
    {
        profile.FlowLayers = new[] { Flow("Flow16", 16, 0.5f) };
        profile.FrictionLayers = new[] { Friction("Friction16", 16, 0f) };
    }

    static void BuildCombined(RainProfile profile)
    {
        profile.StaticLayers = new[] { Frame() };
        profile.SimpleLayers = new[] { Drops("Drops256", 256, new Vector2Int(240, 300)) };
        profile.FlowLayers = new[] { Flow("Flow32", 32, 0.5f) };
        profile.FrictionLayers = new[] { Friction("Friction32", 32, 0.5f) };
    }

    static StaticLayerSettings Frame()
    {
        var layer = new StaticLayerSettings
        {
            Name = "Frame",
            Depth = 0,
            Distortion = 40f,
            Relief = 0.2f,
            Darkness = 1.5f,
            FadeTime = 1f,
            AutoStart = true,
            OverlayColor = new Color(0.7f, 0.85f, 1f, 0.1f)
        };

        Assign(layer, "rain_frame");
        return layer;
    }

    static SimpleLayerSettings Drops(string name, int maxCount, Vector2Int rate)
    {
        var layer = new SimpleLayerSettings
        {
            Name = name,
            Depth = 1,
            MaxCount = maxCount,
            Duration = 1f,
            LifetimeRange = new Vector2(1.0f, 1.4f),
            EmissionRateRange = rate,
            SizeMin = new Vector2(0.08f, 0.08f),
            SizeMax = new Vector2(0.18f, 0.18f),
            Distortion = 25f,
            Relief = 0f,
            Darkness = 6f,
            AutoStart = true,
            OverlayColor = new Color(0.75f, 0.75f, 0.75f, 0f)
        };

        Assign(layer, "rain_drop");
        return layer;
    }

    static FlowLayerSettings Flow(string name, int maxCount, float blur)
    {
        var layer = new FlowLayerSettings
        {
            Name = name,
            Depth = 2,
            MaxCount = maxCount,
            Duration = 1f,
            LifetimeRange = new Vector2(3f, 5f),
            EmissionRateRange = new Vector2Int(5, 8),
            MaxPoints = 64,
            PointSpacing = 0.01f,
            WidthRange = new Vector2(0.06f, 0.12f),
            AccelerationRange = new Vector2(0.05f, 0.12f),
            Distortion = 30f,
            Relief = 0f,
            Blur = blur,
            Darkness = 4f,
            AutoStart = true,
            OverlayColor = new Color(0.75f, 0.75f, 0.75f, 0f)
        };

        Assign(layer, "flow");
        return layer;
    }

    static FrictionLayerSettings Friction(string name, int maxCount, float blur)
    {
        var layer = new FrictionLayerSettings
        {
            Name = name,
            Depth = 3,
            MaxCount = maxCount,
            Duration = 1f,
            LifetimeRange = new Vector2(3f, 5f),
            EmissionRateRange = new Vector2Int(5, 8),
            MaxPoints = 64,
            PointSpacing = 0.01f,
            WidthRange = new Vector2(0.06f, 0.12f),
            AccelerationRange = new Vector2(0.05f, 0.12f),
            Distortion = 30f,
            Relief = 0f,
            Blur = blur,
            Darkness = 4f,
            AutoStart = true,
            OverlayColor = new Color(0.75f, 0.75f, 0.75f, 0f),
            Field = AssetDatabase.LoadAssetAtPath<FrictionField>(
                PackageRoot + "/Runtime/Data/FrictionField_friction_map.asset")
        };

        Assign(layer, "flow");
        return layer;
    }

    /// <summary>Wires the overlay/normal/coverage triple that Task 8 baked for <paramref name="stem"/>.</summary>
    static void Assign(RainLayerSettings layer, string stem)
    {
        layer.OverlayTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureDir}/Rain/{stem}.tga");
        layer.NormalMap = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureDir}/Rain/{stem}_normal.tga");
        layer.CoverageMask = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureDir}/Coverage/{stem}_coverage.png");

        if (layer.OverlayTexture == null || layer.NormalMap == null || layer.CoverageMask == null)
        {
            Debug.LogError($"[RainBenchmark] missing texture set for '{stem}'");
        }
    }

}
