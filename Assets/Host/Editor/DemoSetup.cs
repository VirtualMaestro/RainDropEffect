using System.IO;
using System.Linq;
using RainDropEffect;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Host-only generator for <c>Assets/Host/Demo.unity</c>: the scene that can actually be opened from
/// this repository to look at the effects.
///
/// The Effects Gallery sample cannot serve that purpose here — it lives under <c>Samples~</c>, whose
/// tilde hides it from the AssetDatabase until Package Manager copies it into a consumer project.
/// This scene is generated from code for the same reason the benchmark scene is: a re-run has to
/// produce the same thing, so nothing depends on hand-placed props.
///
/// The backdrop is a single textured quad. Refraction is only legible against something with detail
/// in it, and one 180 KB texture carries that detail.
/// </summary>
public static class DemoSetup
{
    const string PackageRoot = "Packages/com.virtualmaestro.raindropeffect";
    const string ProfileDir = PackageRoot + "/Runtime/Profiles";
    const string HostDir = "Assets/Host";
    const string DemoScene = HostDir + "/Demo.unity";
    const string BackdropTexture = HostDir + "/Cottage.jpg";

    /// <summary>Rain presets offered as buttons, in order.</summary>
    static readonly string[] Presets = { "Rain1", "Rain2", "Rain3", "Rain4", "Rain5", "Rain6" };

    [MenuItem("Rain Drop Effect/Demo/Create Demo Scene")]
    public static void CreateDemoScene()
    {
        var presets = Presets
            .Select(n => AssetDatabase.LoadAssetAtPath<RainProfile>($"{ProfileDir}/{n}.asset"))
            .Where(p => p != null)
            .ToArray();

        if (presets.Length != Presets.Length)
        {
            Debug.LogError("[RainDemo] some rain presets are missing from " + ProfileDir);
            return;
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var lightObject = new GameObject("Directional Light");
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.1f;
        lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        BuildBackdrop();

        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";

        // RainEffect is [ExecuteAlways]: on an active object OnEnable runs the moment the component
        // is added, rebuilds against a profile that is not assigned yet, and warns about it. Build
        // the camera inactive and switch it on once every field is set.
        cameraObject.SetActive(false);

        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.13f, 0.15f, 0.18f, 1f);
        camera.transform.position = new Vector3(0f, 1f, -6f);
        cameraObject.AddComponent<AudioListener>();

        // Component order is draw order: weather first, then frost over it, then blood on top.
        var demo = cameraObject.AddComponent<DemoSample>();
        demo.Rain = AddEffect(cameraObject, presets[0]);
        demo.Frost = AddEffect(cameraObject, Load("Frozen"));
        demo.BloodFrame = AddEffect(cameraObject, Load("BloodFrame"));
        demo.BloodFlow = AddEffect(cameraObject, Load("BloodFlow"));
        demo.BloodSplatter = AddEffect(cameraObject, Load("BloodSplatter"));
        demo.Presets = presets;

        // Frost and blood start silent; their profiles carry AutoStart = 0, and Intensity 0 keeps
        // the frost invisible until the slider moves.
        demo.Frost.Intensity = 0f;
        demo.BloodFrame.Intensity = 0f;

        cameraObject.SetActive(true);

        Directory.CreateDirectory(HostDir);
        EditorSceneManager.SaveScene(scene, DemoScene);
        Debug.Log("[RainDemo] wrote " + DemoScene);
    }

    static RainEffect AddEffect(GameObject host, RainProfile profile)
    {
        var effect = host.AddComponent<RainEffect>();
        effect.Profile = profile;
        effect.Quality = RainQuality.High;
        return effect;
    }

    static RainProfile Load(string name)
    {
        var profile = AssetDatabase.LoadAssetAtPath<RainProfile>($"{ProfileDir}/{name}.asset");
        if (profile == null)
        {
            Debug.LogError($"[RainDemo] profile not found: {ProfileDir}/{name}.asset");
        }

        return profile;
    }

    /// <summary>
    /// One quad, 10 x 10, two units in front of the origin. The camera sits at (0, 1, -6) with a
    /// 60 degree field of view, so the quad over-fills the frame vertically and leaves the clear
    /// colour at the sides.
    /// </summary>
    static void BuildBackdrop()
    {
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(BackdropTexture);
        if (texture == null)
        {
            Debug.LogWarning("[RainDemo] backdrop texture missing: " + BackdropTexture);
        }

        var material = AssetDatabase.LoadAssetAtPath<Material>($"{HostDir}/Backdrop.mat");
        if (material == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("[RainDemo] URP/Lit not found; is URP installed?");
                return;
            }

            material = new Material(shader) { name = "Backdrop" };
            AssetDatabase.CreateAsset(material, $"{HostDir}/Backdrop.mat");
        }

        // URP/Lit reads _BaseMap; _MainTex is kept in step so a Built-in fallback shows the same.
        material.SetTexture("_BaseMap", texture);
        material.SetTexture("_MainTex", texture);
        EditorUtility.SetDirty(material);

        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "Backdrop";
        quad.transform.position = new Vector3(0f, 1f, 2f);
        quad.transform.localScale = new Vector3(10f, 10f, 1f);
        quad.GetComponent<Renderer>().sharedMaterial = material;
    }
}
