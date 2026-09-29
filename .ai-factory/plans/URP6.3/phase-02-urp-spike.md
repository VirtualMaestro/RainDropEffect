# Phase 2: URP Installation and Rendering Spike

Plan: [index.md](index.md)
Tasks: 3-8
Depends on: Phase 1 / Task 1 (baseline captured before URP replaces the legacy renderer)

`<pkg>` below means `Packages/com.virtualmaestro.raindropeffect` (decision D5 in `index.md`).

## Objective

Install and lock URP for Unity 6000.3.23f1, create the package skeleton, and prove with a compiled vertical slice that the Render Graph design in `index.md` D6–D9 renders portable refraction with transparent scene content, overlapping coverage, MSAA/HDR/render-scale variants, and a camera stack, on Windows D3D11, WebGL2, and one Android device. The phase ends with a written renderer decision. All later simulation and asset work depends on this gate.

## Current-Code Evidence

| Path | Symbols / lines | Why it matters |
|------|-----------------|----------------|
| `Packages/manifest.json` | dependencies: `com.github-glitchenzo.nugetforunity` (git), `com.unity.ide.rider 3.1.0`, `com.ivanmurzak.unity.mcp 0.90.0`, `com.unity.modules.*`; no render pipeline | URP must be added; host tooling stays host-only. |
| `ProjectSettings/GraphicsSettings.asset` | no `m_RenderPipelineAsset`; `m_AlwaysIncludedShaders` are built-ins only | Pipeline asset and renderer must be created and assigned. |
| `ProjectSettings/QualitySettings.asset` | no per-level `renderPipeline` overrides | Assign the same URP asset to all quality levels. |
| `ProjectSettings/ProjectSettings.asset` | `m_ActiveColorSpace: 0` (Gamma); Windows graphics API explicit D3D11 + D3D12; WebGL/iOS IL2CPP; Android Mono | Keep Gamma (D4). Android scripting backend switches to IL2CPP in Task 25 for device runs. |
| `Assets/RainDropEffect2/Shaders/Resources/RainDistortion (Forward).shader` | fragment math (see Phase 1 evidence) | Source of the lens formulas replicated in Task 6. |
| `Assets/RainDropEffect2/Shaders/Resources/RainNoDistortion.shader` | `Blend SrcAlpha OneMinusSrcAlpha`, `alpha *= tex.r*tex.g*tex.b`, `color.rgb = _Color.rgb*saturate(1-_Darkness)` | Source of the overlay pass formulas. |
| `Assets/RainDropEffect2/Textures/**` (21 textures, 10 normal/overlay pairs + `friction_map.tga`) | importer: `textureType 1` for `*_normal`, `isReadable 1` only for `friction_map` and `rain_white`, all `wrapMode Repeat`, no alpha | No texture ships a coverage channel; Task 8 bakes one per pair. |
| `C:\Program Files\Unity\Hub\Editor\` | `6000.3.23f1` and `6000.5.10f1` installed | Primary editor and the "newer version" for Phase 8 Task 32. |

## Files to Change

| Path | Action | Required change |
|------|--------|-----------------|
| `Packages/manifest.json` | modify | Add `com.unity.render-pipelines.universal` (editor-verified version), `com.unity.test-framework`, and `"testables": ["com.virtualmaestro.raindropeffect"]`. |
| `Packages/packages-lock.json` | modify (by Unity) | Locked URP/Core versions recorded in `docs/support-matrix.md`. |
| `Assets/Settings/Rain-URP.asset`, `Assets/Settings/Rain-URP-Renderer.asset` | create | Host pipeline asset + Universal Renderer (Forward) with `RainRendererFeature` added. |
| `ProjectSettings/GraphicsSettings.asset`, `ProjectSettings/QualitySettings.asset` | modify (via editor UI) | Assign `Rain-URP.asset` as default and for every quality level. |
| `<pkg>/package.json`, `README.md`, `CHANGELOG.md`, `LICENSE.md`, `Third Party Notices.md` | create | Package metadata (Task 4). |
| `<pkg>/Runtime/RainDropEffect.Runtime.asmdef`, `<pkg>/Editor/RainDropEffect.Editor.asmdef`, `<pkg>/Tests/Editor/RainDropEffect.Tests.Editor.asmdef`, `<pkg>/Tests/Runtime/RainDropEffect.Tests.Runtime.asmdef` | create | Assembly boundaries (Task 4). |
| `<pkg>/Runtime/Rendering/RainRendererFeature.cs`, `RainRenderPass.cs`, `RainShaderIds.cs` | create | URP integration (Task 5). |
| `<pkg>/Runtime/RainEffect.cs` (shell), `<pkg>/Runtime/RainLog.cs` | create | Camera registry, minimal state holder for the spike, logging helper (Task 5). Extended in Phase 4. |
| `<pkg>/Runtime/Geometry/RainVertex.cs`, `RainBatchMesh.cs` | create | Vertex layout and reusable dynamic mesh (Task 6). |
| `<pkg>/Runtime/Shaders/RainCommon.hlsl`, `RainLens.shader`, `RainBlur.shader`, `RainShaderSet.cs`, `RainShaderSet.asset` | create | Shader contract (Task 6). |
| `Assets/Spike/Spike.unity`, `Assets/Spike/RainSpikeDriver.cs` | create (host, temporary; deleted in Task 29) | Spike scene and hardcoded batch driver (Task 7). |
| `docs/renderer-decision.md` | create | Spike results and renderer decision record (Task 7). |
| `<pkg>/Editor/Baking/CoverageMaskBaker.cs`, `<pkg>/Runtime/Textures/Coverage/*_coverage.png` | create | Coverage baker and outputs (Task 8). |

## Task 3: Install and lock URP for the host project

### Intent

Provide the render pipeline every later task compiles against, with the exact URP patch recorded, while keeping the legacy scripts compiling (they are Built-in-only at the shader level, but their C# still compiles under URP; they simply render nothing).

### Implementation Steps

1. Add URP through the editor Package Manager ("Unity Registry" → Universal RP → Install) or through Unity MCP `package-add` with `packageName: "com.unity.render-pipelines.universal"` and no version. The editor resolves the 17.3.x patch verified for 6000.3.23f1. Do not hand-type a patch number in the manifest.
2. Add `com.unity.test-framework` (latest 1.x offered for this editor) the same way.
3. Edit `Packages/manifest.json`: add top-level `"testables": ["com.virtualmaestro.raindropeffect"]` (the package is created in Task 4; the entry is harmless until then).
4. Read `Packages/packages-lock.json` and record the resolved versions of `com.unity.render-pipelines.universal`, `com.unity.render-pipelines.core`, `com.unity.shadergraph`, `com.unity.test-framework` in a new `docs/support-matrix.md` table `Verified combinations` with row `6000.3.23f1 | URP <x.y.z> | Core <x.y.z> | Windows D3D11 editor | verified in Task 3`.
5. Create `Assets/Settings/Rain-URP.asset` (Assets → Create → Rendering → URP Asset (with Universal Renderer)); rename the generated renderer to `Rain-URP-Renderer.asset`. Set on the pipeline asset: `HDR = off`, `MSAA = Disabled`, `Render Scale = 1`, `Opaque Texture = off`, `Depth Texture = off`. Leave Universal Renderer in Forward mode, `Intermediate Texture = Auto`, `Post-processing = enabled` on the renderer data.
6. Assign `Rain-URP.asset` in Project Settings → Graphics → Default Render Pipeline and in Project Settings → Quality for every quality level.
7. Keep `m_ActiveColorSpace` at Gamma (decision D4). Do not run the Render Pipeline Converter on legacy materials; the legacy shaders are being replaced, not converted.
8. Open `Assets/RainDropEffect2/Demo/Demo1.unity`, enter Play Mode, confirm the console shows no compile errors and the still-life renders through URP (rain no longer renders; that is expected).

### Required Interfaces and Contracts

- `docs/support-matrix.md` is the only place package/editor versions are asserted; later tasks append rows, never rewrite verified ones.
- Host pipeline settings are development defaults; Task 7 varies them per test case and restores them.

### Error Handling and Logging

- If package resolution fails (offline registry), stop and report the Package Manager error text; do not pin a guessed version.
- Console must be free of errors after step 8; warnings about legacy shaders not supported by URP are expected and listed in `docs/support-matrix.md` §Known legacy warnings.

### Tests

Not planned for this task (project configuration). Compilation is verified in step 8.

### Acceptance Criteria

- `Packages/packages-lock.json` contains `com.unity.render-pipelines.universal` with a `17.3.` prefix version (if the editor resolves a different major/minor, record it and continue; the plan's API assumptions are re-checked in Task 5).
- Graphics and all Quality levels reference `Rain-URP.asset`.
- Play Mode in `Demo1.unity` runs without compile errors.

### Verification

- `grep -A2 '"com.unity.render-pipelines.universal"' Packages/packages-lock.json` → shows a `"version"` line.
- `grep -c "Rain-URP" ProjectSettings/GraphicsSettings.asset ProjectSettings/QualitySettings.asset` → both ≥ 1 (assets referenced by GUID; verify instead by opening Project Settings if grep shows 0 because the reference is a GUID).
- Unity MCP `console-get-logs` filtered to `Error` after Play → empty.

## Task 4: Create the package skeleton and assembly boundaries

### Intent

Give the spike and all later runtime code their final home so nothing is written twice. Enforces D5 (one URP-only package, Runtime/Editor/Tests separation).

### Implementation Steps

1. Create directory `<pkg>/` with `package.json`:
   ```json
   {
     "name": "com.virtualmaestro.raindropeffect",
     "displayName": "Rain Drop Effect URP",
     "version": "2.0.0-pre.1",
     "unity": "6000.3",
     "description": "Camera-space rain, frozen, blood and splash lens effects for Unity 6.3+ URP (Render Graph).",
     "keywords": ["rain", "urp", "camera effect", "post effect"],
     "license": "MIT",
     "author": { "name": "VirtualMaestro" },
     "dependencies": {
       "com.unity.render-pipelines.universal": "<locked version from Task 3>"
     },
     "samples": [
       { "displayName": "Basic Setup", "description": "One camera with a rain profile.", "path": "Samples~/Basic" },
       { "displayName": "Effects Gallery", "description": "All presets, blood HP demo, splash and frozen controls.", "path": "Samples~/EffectsGallery" }
     ]
   }
   ```
2. Create `<pkg>/LICENSE.md` = copy of repository `LICENSE` (MIT, Gaku Morio 2017) with an added line `Modifications and URP port copyright 2026 VirtualMaestro.` Create `<pkg>/Third Party Notices.md` listing the same original notice. Create `<pkg>/CHANGELOG.md` with `## [2.0.0-pre.1] - Unreleased` and `<pkg>/README.md` with a two-paragraph placeholder (final content in Task 32).
3. Create asmdefs:
   - `<pkg>/Runtime/RainDropEffect.Runtime.asmdef`: `name: RainDropEffect.Runtime`, `rootNamespace: RainDropEffect`, `references: ["Unity.RenderPipelines.Universal.Runtime", "Unity.RenderPipelines.Core.Runtime"]` (no `Unity.Collections`: geometry uses managed arrays, Task 6), `autoReferenced: true`, `noEngineReferences: false`, `allowUnsafeCode: false`.
   - `<pkg>/Editor/RainDropEffect.Editor.asmdef`: `name: RainDropEffect.Editor`, `rootNamespace: RainDropEffect.Editor`, `references: ["RainDropEffect.Runtime", "Unity.RenderPipelines.Universal.Runtime", "Unity.RenderPipelines.Universal.Editor", "Unity.RenderPipelines.Core.Runtime"]`, `includePlatforms: ["Editor"]`.
   - `<pkg>/Tests/Editor/RainDropEffect.Tests.Editor.asmdef`: references `RainDropEffect.Runtime`, `RainDropEffect.Editor`, `UnityEngine.TestRunner`, `UnityEditor.TestRunner`; `includePlatforms: ["Editor"]`; `defineConstraints: ["UNITY_INCLUDE_TESTS"]`; `precompiledReferences: ["nunit.framework.dll"]`; `overrideReferences: true`.
   - `<pkg>/Tests/Runtime/RainDropEffect.Tests.Runtime.asmdef`: references `RainDropEffect.Runtime`, `UnityEngine.TestRunner`, `Unity.RenderPipelines.Universal.Runtime`; `optionalUnityReferences` not used; `defineConstraints: ["UNITY_INCLUDE_TESTS"]`; `precompiledReferences: ["nunit.framework.dll"]`; `overrideReferences: true`.
4. Create empty folders with `.gitkeep`: `<pkg>/Runtime/Rendering`, `Runtime/Geometry`, `Runtime/Simulation`, `Runtime/Settings`, `Runtime/Shaders`, `Runtime/Textures`, `Runtime/Profiles`, `Runtime/Data`, `Editor/Inspectors`, `Editor/Baking`, `Documentation~`, `Samples~/Basic`, `Samples~/EffectsGallery`.
5. Add `Assets/RainDropEffect2/Editor/` scripts' assembly does not change (host `Assembly-CSharp-Editor`). No asmdef is added under `Assets/`.
6. Reimport (Unity MCP `assets-refresh`) and confirm four assemblies appear in the Rider/VS solution: `RainDropEffect.Runtime`, `RainDropEffect.Editor`, `RainDropEffect.Tests.Editor`, `RainDropEffect.Tests.Runtime`.

### Required Interfaces and Contracts

- Runtime assembly must never reference `UnityEditor`, samples, host scripts, or `Assets/Plugins/NuGet/*` (enforced by asmdef references and by Task 11's clean install).
- Namespace `RainDropEffect` (runtime), `RainDropEffect.Editor`, `RainDropEffect.Tests`.
- Package version string stays `2.0.0-pre.1` until Task 32.

### Error Handling and Logging

- asmdef reference errors surface as compile errors; fix references, do not enable `autoReferenced` on Editor/Test assemblies.

### Tests

- Create `<pkg>/Tests/Editor/PackageLayoutTests.cs` with one NUnit test `RuntimeAssemblyHasNoEditorReference`: load `RainDropEffect.Runtime.asmdef` JSON via `File.ReadAllText(Path.GetFullPath("Packages/com.virtualmaestro.raindropeffect/Runtime/RainDropEffect.Runtime.asmdef"))`, assert the `references` array contains no string containing `"Editor"` and `includePlatforms` is empty.
- Run: Unity MCP `tests-run` with `testMode: EditMode`, or CLI `"C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe" -batchmode -projectPath "C:\workspace\projects\unity\RainDropEffect" -runTests -testPlatform EditMode -testResults Logs\editmode.xml -logFile Logs\editmode.log`.

### Acceptance Criteria

- Package appears in Package Manager under "In Project" as an embedded package with version `2.0.0-pre.1`.
- The layout test passes.
- No compile errors.

### Verification

- `ls Packages/com.virtualmaestro.raindropeffect` → shows `package.json`, `Runtime`, `Editor`, `Tests`, `Samples~`, `Documentation~`.
- EditMode test run → `Logs\editmode.xml` contains `result="Passed"` for `PackageLayoutTests`.

## Task 5: Implement the renderer feature and Render Graph pass

### Intent

Build the single URP integration point (D6–D9): a stateless `RainRendererFeature` that, for cameras with a registered `RainEffect`, copies the current camera color once, draws rain batches into the copy while sampling the original, and forwards the copy as the new camera color. Later phases only change what batches are supplied, never the pass topology.

### Implementation Steps

1. Create `<pkg>/Runtime/RainLog.cs`:
   ```csharp
   namespace RainDropEffect {
     public static class RainLog {
       const string Prefix = "[RainDropEffect] ";
       [System.Diagnostics.Conditional("RAINDROP_VERBOSE_LOG")]
       public static void Verbose(string message, UnityEngine.Object context = null) => UnityEngine.Debug.Log(Prefix + message, context);
       public static void Warn(string message, UnityEngine.Object context = null) => UnityEngine.Debug.LogWarning(Prefix + message, context);
       public static void Error(string message, UnityEngine.Object context = null) => UnityEngine.Debug.LogError(Prefix + message, context);
       static readonly System.Collections.Generic.HashSet<string> once = new();
       public static void WarnOnce(string key, string message, UnityEngine.Object context = null) { if (once.Add(key)) Warn(message, context); }
       public static void ResetOnce() => once.Clear();
     }
   }
   ```
   Add `RAINDROP_VERBOSE_LOG` to the host's Player Settings → Scripting Define Symbols for Windows Standalone (development only; Task 32 documents it).
2. Create `<pkg>/Runtime/Rendering/RainShaderIds.cs`: static readonly ints via `Shader.PropertyToID` for `_RainSceneColor`, `_RainSceneBlur`, `_NormalMap`, `_OverlayTex`, `_CoverageTex`, `_RainBlurTexel`, `_RainBlurRadius`; and keyword `RainKeywords.Blur = new LocalKeyword(shader, "_RAIN_BLUR")` created lazily per shader.
3. Create `<pkg>/Runtime/RainEffect.cs` shell (full behaviour arrives in Task 13). For the spike it must contain the registry and the batch supply contract:
   ```csharp
   namespace RainDropEffect {
     [ExecuteAlways, RequireComponent(typeof(Camera))]
     public sealed partial class RainEffect : MonoBehaviour {
       static readonly Dictionary<Camera, List<RainEffect>> registry = new();
       public static bool TryGet(Camera cam, out List<RainEffect> effects) => registry.TryGetValue(cam, out effects);
       public static RainEffect SceneViewPreviewSource { get; private set; }   // first enabled effect with PreviewInSceneView
       public bool PreviewInSceneView;
       internal RainRenderData RenderData { get; private set; } = new RainRenderData();  // filled by simulation each frame
       Camera cachedCamera;
       void OnEnable() { cachedCamera = GetComponent<Camera>(); Register(); RainLog.Verbose($"enable on {cachedCamera.name}", this); }
       void OnDisable() { Unregister(); RainLog.Verbose($"disable on {cachedCamera?.name}", this); }
       void Register() { if (!registry.TryGetValue(cachedCamera, out var list)) registry[cachedCamera] = list = new List<RainEffect>(); if (!list.Contains(this)) list.Add(this); RefreshSceneViewSource(); }
       void Unregister() { if (registry.TryGetValue(cachedCamera, out var list)) { list.Remove(this); if (list.Count == 0) registry.Remove(cachedCamera); } RefreshSceneViewSource(); }
       static void RefreshSceneViewSource() { SceneViewPreviewSource = null; foreach (var kv in registry) foreach (var e in kv.Value) if (e.isActiveAndEnabled && e.PreviewInSceneView) { SceneViewPreviewSource = e; return; } }
     }
     internal sealed class RainRenderData {
       public readonly List<RainBatch> Batches = new(8);
       public bool AnyVisible, AnyLens, AnyBlur;
       public float BlurRadius;       // pixels at half resolution; set by quality (Task 23)
       public void Clear() { Batches.Clear(); AnyVisible = AnyLens = AnyBlur = false; }
     }
     internal struct RainBatch { public Mesh Mesh; public Material Material; public int ShaderPass; public bool IsLens; }
   }
   ```
   Registry entries are keyed by `Camera`; on domain reload the static dictionary is rebuilt by `OnEnable` (add `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { registry.Clear(); SceneViewPreviewSource = null; RainLog.ResetOnce(); }` for domain-reload-disabled Play Mode).
4. Create `<pkg>/Runtime/Rendering/RainRenderPass.cs`:
   ```csharp
   internal sealed class RainRenderPass : ScriptableRenderPass {
     class CopyData { }  // AddBlitPass needs none
     class DrawData { public List<RainBatch> Batches; public TextureHandle Source; public TextureHandle Blur; public MaterialPropertyBlock Mpb; }
     readonly MaterialPropertyBlock mpb = new();
     readonly List<RainBatch> frameBatches = new(16);
     Material blurMaterial; // owned by feature, assigned in constructor
     public RainRenderPass(Material blur) { blurMaterial = blur; renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing; requiresIntermediateTexture = true; profilingSampler = new ProfilingSampler("Rain Drop Effect"); }

     public override void RecordRenderGraph(RenderGraph rg, ContextContainer frameData) {
       var cameraData = frameData.Get<UniversalCameraData>();
       var resources  = frameData.Get<UniversalResourceData>();
       if (!RainRenderPass.Collect(cameraData, frameBatches, out bool anyLens, out bool anyBlur, out float blurRadius)) return;
       if (resources.isActiveTargetBackBuffer) { RainLog.WarnOnce("backbuffer:" + cameraData.camera.name, $"Camera '{cameraData.camera.name}' renders to the backbuffer; rain skipped. Enable an intermediate texture (post-processing, HDR, MSAA or Intermediate Texture=Always).", cameraData.camera); return; }
       TextureHandle source = resources.activeColorTexture;
       TextureHandle target = source;
       TextureHandle blur = TextureHandle.nullHandle;
       if (anyLens) {
         TextureDesc desc = rg.GetTextureDesc(source);
         desc.name = "_RainSceneColorCopy"; desc.msaaSamples = MSAASamples.None; desc.depthBufferBits = DepthBits.None; desc.clearBuffer = false; desc.bindTextureMS = false;
         target = rg.CreateTexture(desc);
         rg.AddBlitPass(source, target, Vector2.one, Vector2.zero, passName: "Rain Copy Scene Color");
         if (anyBlur && blurMaterial != null) {
           TextureDesc bdesc = desc; bdesc.name = "_RainSceneBlur";
           if (bdesc.sizeMode == TextureSizeMode.Scale) bdesc.scale *= 0.5f; else { bdesc.width = Mathf.Max(1, bdesc.width / 2); bdesc.height = Mathf.Max(1, bdesc.height / 2); }
           bdesc.filterMode = FilterMode.Bilinear;
           blur = rg.CreateTexture(bdesc);
           blurMaterial.SetFloat(RainShaderIds.BlurRadius, blurRadius);
           blurMaterial.SetVector(RainShaderIds.BlurTexel, new Vector4(1f / desc.width, 1f / desc.height, desc.width, desc.height)); // when sizeMode==Scale use cameraData.scaledWidth/Height
           rg.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(source, blur, blurMaterial, 0), "Rain Blur Scene Color");
         }
       }
       using (var builder = rg.AddRasterRenderPass<DrawData>("Rain Draw", out var data, profilingSampler)) {
         data.Batches = frameBatches; data.Source = source; data.Blur = blur; data.Mpb = mpb;
         builder.SetRenderAttachment(target, 0, AccessFlags.ReadWrite);
         if (anyLens) { builder.UseTexture(source, AccessFlags.Read); if (blur.IsValid()) builder.UseTexture(blur, AccessFlags.Read); }
         builder.AllowPassCulling(false);
         builder.SetRenderFunc(static (DrawData d, RasterGraphContext ctx) => {
           d.Mpb.Clear();
           // TextureHandle → RTHandle → Texture needs two explicit steps; C# applies only one implicit conversion.
           if (d.Source.IsValid()) { RTHandle src = d.Source; d.Mpb.SetTexture(RainShaderIds.SceneColor, src); }
           if (d.Blur.IsValid()) { RTHandle blr = d.Blur; d.Mpb.SetTexture(RainShaderIds.SceneBlur, blr); }
           for (int i = 0; i < d.Batches.Count; i++) { var b = d.Batches[i]; if (b.Mesh == null || b.Material == null) continue; ctx.cmd.DrawMesh(b.Mesh, Matrix4x4.identity, b.Material, 0, b.ShaderPass, d.Mpb); }
         });
       }
       if (anyLens) resources.cameraColor = target;
     }

     // Collect: which effects apply to this camera, in order; returns false when nothing visible.
     internal static bool Collect(UniversalCameraData cameraData, List<RainBatch> into, out bool anyLens, out bool anyBlur, out float blurRadius) {
       into.Clear(); anyLens = anyBlur = false; blurRadius = 0;
       var type = cameraData.cameraType;
       if (type == CameraType.Preview || type == CameraType.Reflection) return false;
       List<RainEffect> effects = null;
       if (type == CameraType.SceneView) { var s = RainEffect.SceneViewPreviewSource; if (s == null) return false; effects = new List<RainEffect>(1) { s }; }   // allocation only in Scene View
       else if (!RainEffect.TryGet(cameraData.camera, out effects)) return false;
       foreach (var e in effects) { if (!e.isActiveAndEnabled) continue; var rd = e.RenderData; if (!rd.AnyVisible) continue; into.AddRange(rd.Batches); anyLens |= rd.AnyLens; anyBlur |= rd.AnyBlur; blurRadius = Mathf.Max(blurRadius, rd.BlurRadius); }
       return into.Count > 0;
     }
   }
   ```
   Notes for the implementer: `TextureDesc.msaaSamples`/`depthBufferBits` are the field names in URP 17; `RenderGraphUtils` lives in `UnityEngine.Rendering.RenderGraphModule.Util`; `RasterCommandBuffer.DrawMesh` exists. If any name differs in the locked URP patch, fix the name and record the actual signature in `docs/renderer-decision.md` §API notes. Do not change the pass topology.
5. Create `<pkg>/Runtime/Rendering/RainRendererFeature.cs`:
   ```csharp
   public sealed class RainRendererFeature : ScriptableRendererFeature {
     [SerializeField] RainShaderSet shaders;   // auto-assigned in editor (Task 6)
     RainRenderPass pass; Material blurMaterial;
     public override void Create() { if (blurMaterial != null) CoreUtils.Destroy(blurMaterial); blurMaterial = shaders != null && shaders.Blur != null ? CoreUtils.CreateEngineMaterial(shaders.Blur) : null; pass = new RainRenderPass(blurMaterial); RainLog.Verbose("feature created; blur material " + (blurMaterial != null)); }
     public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) { var cam = renderingData.cameraData.camera; var t = renderingData.cameraData.cameraType; if (t == CameraType.Preview || t == CameraType.Reflection) return; if (t != CameraType.SceneView && !RainEffect.TryGet(cam, out _)) return; if (t == CameraType.SceneView && RainEffect.SceneViewPreviewSource == null) return; renderer.EnqueuePass(pass); }
     protected override void Dispose(bool disposing) { CoreUtils.Destroy(blurMaterial); blurMaterial = null; }
   #if UNITY_EDITOR
     void Reset() { shaders = UnityEditor.AssetDatabase.LoadAssetAtPath<RainShaderSet>("Packages/com.virtualmaestro.raindropeffect/Runtime/Shaders/RainShaderSet.asset"); }
     void OnValidate() { if (shaders == null) Reset(); }
   #endif
   }
   ```
6. Add `RainRendererFeature` to `Assets/Settings/Rain-URP-Renderer.asset` (Inspector → Add Renderer Feature). Confirm `shaders` is auto-filled after Task 6 creates the asset.

### Required Interfaces and Contracts

- Injection point fixed: `BeforeRenderingPostProcessing`; not user-configurable (D7).
- `RainRenderData` is written only by the owning `RainEffect` (Task 13/16) in `LateUpdate`; the pass reads it. No pass reads mutable settings objects.
- Multiple `RainEffect` components on one camera draw in component order (D11); one scene copy per camera per frame regardless of effect count.
- Overlay-only frames (`anyLens == false`) draw straight into `activeColorTexture` and perform no copy.
- The pass never samples its own render attachment (D8).

### Error Handling and Logging

- Backbuffer target: `WarnOnce` per camera name (text in step 4). Verbose log on feature `Create` and on effect enable/disable. No per-frame logs.
- Null mesh/material batches are skipped silently (they cannot occur after Task 16; the guard prevents a null-ref during Phase 4 development).
- Missing `RainShaderSet` on the feature: `RainLog.Error("RainRendererFeature has no RainShaderSet; blur disabled")` once in `Create`.

### Tests

- `<pkg>/Tests/Runtime/RainRegistryTests.cs` (PlayMode): create a camera GameObject, add `RainEffect`, assert `RainEffect.TryGet(cam, out var list) && list.Count == 1`; disable the component, assert `TryGet` returns false; add a second `RainEffect` to the same camera and assert order equals component order.
- Run: Unity MCP `tests-run` `testMode: PlayMode`, or CLI with `-testPlatform PlayMode`.

### Acceptance Criteria

- Project compiles with the feature added to the renderer.
- With no `RainEffect` in the scene, Frame Debugger shows no "Rain" passes.
- `RainRegistryTests` pass.

### Verification

- Unity MCP `console-get-logs` `logType: Error` → empty after entering Play Mode in `Demo1.unity`.
- Frame Debugger (Window → Analysis → Frame Debugger) on `Demo1.unity` → no `Rain Draw` entry when no effect exists.

## Task 6: Author the shader contract and reusable batch mesh

### Intent

Fix the vertex layout, shader formulas, blend/coverage rules, and dynamic mesh mechanism that both the spike and the production simulation use (D8–D10). No later task may change the vertex layout without updating this task's tests.

### Implementation Steps

1. Create `<pkg>/Runtime/Geometry/RainVertex.cs`:
   ```csharp
   [StructLayout(LayoutKind.Sequential)]
   public struct RainVertex {
     public Vector3 Position;   // x,y in normalized units (y ∈ [-1,1], x ∈ [-aspect,aspect]); z = opacity 0..1
     public Vector2 UV;
     public Color32 Tint;       // rgb overlay color, a overlay alpha (already multiplied by curves and intensity)
     public Vector4 Params;     // x distortion (px @1080p), y relief, z blur 0..1, w darkness
     public static readonly VertexAttributeDescriptor[] Layout = {
       new(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
       new(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
       new(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4),
       new(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 4) };
   }
   ```
2. Create `<pkg>/Runtime/Geometry/RainBatchMesh.cs`, a reusable owner of one `Mesh` plus persistent managed arrays `RainVertex[] vertices` and `ushort[] indices` (managed arrays so `ref` element access works without unsafe code; `Mesh.SetVertexBufferData`/`SetIndexBufferData` accept `T[]` directly):
   - `RainBatchMesh(int maxVertices, int maxIndices, string name)`: `maxVertices ≤ 65535` (throw `ArgumentOutOfRangeException` otherwise); allocates both arrays once; creates `Mesh { hideFlags = HideFlags.HideAndDontSave, name }`, calls `mesh.SetVertexBufferParams(maxVertices, RainVertex.Layout)`, `mesh.SetIndexBufferParams(maxIndices, IndexFormat.UInt16)`, `mesh.MarkDynamic()`, sets bounds to `new Bounds(Vector3.zero, new Vector3(100,100,100))` once.
   - `int VertexCount, IndexCount` and `void Begin()` (resets counts), `ref RainVertex AddVertex()` (returns `ref vertices[VertexCount++]`; when full sets `Overflowed = true` and returns `ref scratch`, a private field), `void AddQuadIndices(ushort baseVertex)` (adds `b, b+1, b+2, b+2, b+1, b+3`), `void AddIndex(ushort i)` (same overflow rule).
   - `void End()`: `mesh.SetVertexBufferData(vertices, 0, 0, VertexCount, 0, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers)`; `mesh.SetIndexBufferData(indices, 0, 0, IndexCount, same flags)`; `mesh.SetSubMesh(0, new SubMeshDescriptor(0, IndexCount, MeshTopology.Triangles) { bounds = mesh.bounds, vertexCount = VertexCount }, same flags)`. When `IndexCount == 0` set submesh index count 0.
   - `void Dispose()`: `CoreUtils.Destroy(mesh); mesh = null; vertices = null; indices = null;`.
   - `bool IsEmpty => IndexCount == 0`.
   - Static helper `AddQuad(RainBatchMesh m, Vector2 center, Vector2 halfSize, float rotationRad, float opacity, Color32 tint, Vector4 p)` writing four vertices with UVs `(0,0),(1,0),(0,1),(1,1)` matching `RainDropTools.CreateQuadMesh` corner order (`(-1,-1),(1,-1),(-1,1),(1,1)`) and calling `AddQuadIndices`.
3. Create `<pkg>/Runtime/Shaders/RainCommon.hlsl`:
   ```hlsl
   #ifndef RAIN_COMMON_INCLUDED
   #define RAIN_COMMON_INCLUDED
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
   struct RainAttributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; half4 tint : COLOR; float4 params : TEXCOORD1; };
   struct RainVaryings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 tint : COLOR; float4 params : TEXCOORD1; float opacity : TEXCOORD2; };
   RainVaryings RainVert(RainAttributes v) {
     RainVaryings o;
     float aspect = _ScreenParams.x / _ScreenParams.y;
     o.positionCS = float4(v.positionOS.x / aspect, v.positionOS.y, 0.0, 1.0);   // no flip: verified in Task 7 (D10)
     o.uv = v.uv; o.tint = v.tint; o.params = v.params; o.opacity = saturate(v.positionOS.z);
     return o;
   }
   // Distortion offset in UV: 'px' pixels at 1080p reference height, aspect corrected.
   float2 RainDistortionUV(float2 n, float px) { return px * n * float2(_ScreenParams.y / _ScreenParams.x, 1.0) / 1080.0; }
   #endif
   ```
4. Create `<pkg>/Runtime/Shaders/RainLens.shader` = `Shader "Hidden/RainDropEffect/Lens"` with two passes, `Tags { "RenderPipeline"="UniversalPipeline" }`, common state `Cull Off  ZWrite Off  ZTest Always  Blend SrcAlpha OneMinusSrcAlpha  ColorMask RGB`:
   - Pass 0 `Name "LENS"`: `#pragma multi_compile_local_fragment _ _RAIN_BLUR`, `#pragma vertex RainVert`, `#pragma fragment LensFrag`, `#pragma target 3.0`. Textures: `TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap); TEXTURE2D(_OverlayTex); SAMPLER(sampler_OverlayTex); TEXTURE2D(_CoverageTex); SAMPLER(sampler_CoverageTex); TEXTURE2D_X(_RainSceneColor); TEXTURE2D_X(_RainSceneBlur); SAMPLER(sampler_LinearClamp);` and `CBUFFER_START(UnityPerMaterial) float4 _NormalMap_ST; CBUFFER_END` (only so the material is SRP-Batcher-compatible; batching is explicit anyway).
     ```hlsl
     half4 LensFrag(RainVaryings i) : SV_Target {
       float2 n = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, i.uv)).xy;
       half4 overlay = SAMPLE_TEXTURE2D(_OverlayTex, sampler_OverlayTex, i.uv);
       half coverage = SAMPLE_TEXTURE2D(_CoverageTex, sampler_CoverageTex, i.uv).r;
       float2 screenUV = GetNormalizedScreenSpaceUV(i.positionCS.xy);
       float2 uvS = saturate(screenUV - RainDistortionUV(n, i.params.x));   // legacy subtracts; clamp offscreen (D10)
       half3 col = SAMPLE_TEXTURE2D_X(_RainSceneColor, sampler_LinearClamp, uvS).rgb;
       #if defined(_RAIN_BLUR)
         half3 blurred = SAMPLE_TEXTURE2D_X(_RainSceneBlur, sampler_LinearClamp, uvS).rgb;
         col = lerp(col, blurred, saturate(i.params.z * abs(n.x) * 8.0));
       #endif
       half overlayA = saturate(overlay.r * overlay.g * overlay.b);
       col += overlay.rgb * i.tint.rgb * i.tint.a;
       col *= saturate(1.0 - i.params.w * i.tint.a * overlayA);
       col *= (1.0 - n.x * i.params.y);
       return half4(col, coverage * i.opacity);
     }
     ```
   - Pass 1 `Name "OVERLAY"`: `#pragma vertex RainVert`, `#pragma fragment OverlayFrag`; no scene textures:
     ```hlsl
     half4 OverlayFrag(RainVaryings i) : SV_Target {
       float2 n = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, i.uv)).xy;
       half4 tex = SAMPLE_TEXTURE2D(_OverlayTex, sampler_OverlayTex, i.uv);
       half3 col = i.tint.rgb * saturate(1.0 - i.params.w) * tex.rgb * (1.0 - n.x * i.params.y);
       half a = i.tint.a * tex.r * tex.g * tex.b * i.opacity;
       return half4(col, a);
     }
     ```
   - Precision: positions/UV/offset in `float`; colors, masks, tint in `half` (D10). No `Fallback`.
5. Create `<pkg>/Runtime/Shaders/RainBlur.shader` = `Shader "Hidden/RainDropEffect/Blur"`: one pass, `Cull Off ZWrite Off ZTest Always`, `#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"`, `#pragma vertex Vert`, `#pragma fragment Frag`; uniforms `float4 _RainBlurTexel; float _RainBlurRadius;`; fragment = 9 horizontal taps at `uv.x + _RainBlurTexel.x * kern * _RainBlurRadius`, kern −4..4, weights `0.05, 0.09, 0.12, 0.15, 0.18, 0.15, 0.12, 0.09, 0.05`, sampling `SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv)` with `uv` clamped to `[0,1]`. Output alpha 1.
6. Create `<pkg>/Runtime/Shaders/RainShaderSet.cs` (`ScriptableObject` with `public Shader Lens; public Shader Blur;`) and the asset `<pkg>/Runtime/Shaders/RainShaderSet.asset` referencing both shaders (create via a temporary `[CreateAssetMenu]`, then remove the attribute or keep it hidden; the asset is committed with its `.meta`).
7. Material creation helper `<pkg>/Runtime/Rendering/RainMaterials.cs`: `static Material CreateLayerMaterial(RainShaderSet set, Texture normal, Texture overlay, Texture coverage, bool blur)` → `CoreUtils.CreateEngineMaterial(set.Lens)`, set textures by `RainShaderIds`, set keyword `_RAIN_BLUR` via `material.SetKeyword(new LocalKeyword(set.Lens, "_RAIN_BLUR"), blur)`, `material.name = $"Rain {normal?.name}"`. Missing textures fall back to `Texture2D.normalTexture` (normal), `Texture2D.blackTexture` (overlay), `Texture2D.whiteTexture` (coverage).

### Required Interfaces and Contracts

- Vertex semantics (position.z = opacity, TEXCOORD1 = params) are frozen; Task 16/19 emit exactly this layout.
- Shader pass indices: 0 = LENS (samples `_RainSceneColor`), 1 = OVERLAY (no scene read). `RainBatch.ShaderPass` uses these constants (`RainShaderPass.Lens = 0`, `RainShaderPass.Overlay = 1` in `RainShaderIds.cs`).
- Coverage rule (D9): lens alpha = `CoverageMask.r × opacity`; overlay alpha = `tint.a × tex.r×g×b × opacity`. Camera alpha is never written (`ColorMask RGB`).
- Distortion unit: pixels at 1080p reference height (Phase 1 matrix).
- Blur weight: `saturate(blur × |n.x| × 8)`; radius in half-resolution pixels supplied by quality (Task 23).

### Error Handling and Logging

- `RainBatchMesh` overflow: set `Overflowed`, stop writing, and let the caller `RainLog.WarnOnce("overflow:" + name, ...)` once per mesh (Task 16). Never throw in the frame loop.
- Shader compile errors are surfaced by the editor; the spike (Task 7) checks `Shader.isSupported` for both shaders and logs `RainLog.Error` if false.

### Tests

- `<pkg>/Tests/Editor/RainBatchMeshTests.cs` (EditMode):
  - `AddQuadWritesFourVerticesSixIndices`: `Begin`, `AddQuad`, `End`; assert `VertexCount == 4`, `IndexCount == 6`, `mesh.GetIndexCount(0) == 6`, `mesh.vertexCount == 4`... note `mesh.vertexCount` reports buffer capacity after `SetVertexBufferParams`; assert on `GetSubMesh(0).vertexCount == 4` instead.
  - `OverflowIsFlaggedNotThrown`: capacity 4 vertices, add two quads, assert `Overflowed == true` and no exception.
  - `EndWithNoGeometryYieldsEmptySubmesh`: assert `GetSubMesh(0).indexCount == 0`.
  - `DisposeDestroysMesh`: after `Dispose`, `Mesh == null` and `IsAllocated == false` (expose `internal bool IsAllocated => vertices != null`).
- Shader tests are covered by Task 7's visual spike; add `<pkg>/Tests/Editor/ShaderSetTests.cs`: load `RainShaderSet.asset`, assert `Lens != null && Blur != null && Lens.isSupported && Blur.isSupported`, `Lens.passCount == 2`, `Lens.FindPassTagValue(0, new ShaderTagId("Name"))` not required; instead assert `Lens.FindPropertyIndex("_NormalMap") >= 0` for `_NormalMap`, `_OverlayTex`, `_CoverageTex`.

### Acceptance Criteria

- Both shaders compile for D3D11, GLES3, Vulkan, Metal, and WebGL2 (check Inspector → Compile and show code → no errors for those APIs).
- EditMode tests above pass.
- `RainShaderSet.asset` is auto-assigned on the feature after reimport (Task 5 step 6).

### Verification

- EditMode test run → all `RainBatchMeshTests` and `ShaderSetTests` pass.
- Select `RainLens.shader` → Inspector shows no errors; `Compiled code` for `GLES3` and `WebGL 2` shows both passes.

## Task 7: Build and validate the vertical slice

### Intent

Prove the renderer under the real URP conditions listed in the research (transparents, overlap/coverage, MSAA, HDR, render scale, RenderTexture output, camera stack, Scene View, WebGL2, Android) before any family simulation is ported, and write the renderer decision that Phase 4+ builds on.

### Implementation Steps

1. Create `Assets/Spike/Spike.unity`: copy of `Assets/Baseline/Baseline.unity` (still-life plus the transparent quad from Task 1). Main camera gets a `RainEffect` component (shell) and a `RainSpikeDriver` component.
2. Create `Assets/Spike/RainSpikeDriver.cs` (host, global namespace, `[ExecuteAlways]`): owns one `RainBatchMesh(64, 96, "spike")` and two materials from `RainMaterials.CreateLayerMaterial` using textures `rain_drop_normal`/`rain_drop`/`rain_drop_coverage` (Task 8 output; before Task 8 exists use `Texture2D.whiteTexture` as coverage and note it) and `frozenframe_normal`/`frozenframe`/`frozenframe_coverage`. In `LateUpdate` it fills `RainEffect.RenderData` with:
   - batch A (lens): three quads with `Params = (50, 1.5, 0, 10)`, opacity 1, tint gray `(128,128,128,255)`: quad 1 at `(0.0, 0.0)` half-size `(0.3, 0.3)`; quad 2 at `(0.15, 0.1)` half-size `(0.3, 0.3)` (overlaps quad 1); quad 3 at `(-0.5, 0.5)` half-size `(0.2, 0.2)` rotated 0.7 rad (asymmetry check for orientation);
   - batch B (lens, full-screen frozen frame): one quad center `(0,0)` half-size `(aspect, 1)`, `Params = (20, 1.0, 0.5, 5)`, opacity 0.8, blur enabled on its material;
   - batch C (overlay): one quad at `(0.6,-0.6)` half-size `(0.2,0.2)` with `ShaderPass = Overlay`, tint `(200,60,60,255)`.
   Sets `AnyVisible = true`, `AnyLens = true`, `AnyBlur = <toggle>`, `BlurRadius = 3`.
   A public `bool[]` of toggles (`ShowA/ShowB/ShowC/EnableBlur`) lets each check isolate a batch.
3. Orientation check (D10): with only batch A, quad 3 must appear upper-left and rotated counter-clockwise, and refraction inside quad 1 must shift the scene consistently with the normal map (not mirrored vertically). If the image is vertically mirrored, change `RainVert` to `o.positionCS.y *= _ProjectionParams.x` **and** keep `GetNormalizedScreenSpaceUV`; if the drop is upright but refraction is mirrored, flip only `n.y` in `RainDistortionUV`. Record the outcome in `docs/renderer-decision.md` §Orientation with screenshots.
4. Coverage/overlap check (D9): with batch A only, the region of quad 2 that overlaps quad 1 must still show quad 1's refraction outside quad 2's coverage mask, and quad 2's interior must show undistorted-scene-based refraction (stable-source rule). Capture and store as `docs/images/spike-overlap.png`.
5. Transparent check: the blue transparent quad from the scene must be visibly refracted inside drops (proves `activeColorTexture` after transparents). Capture `spike-transparent.png`.
6. Matrix runs (each: change host pipeline/renderer settings, enter Play Mode, inspect Frame Debugger and Render Graph Viewer (Window → Analysis → Render Graph Viewer), capture): 
   | Case | Setting | Pass criteria |
   |---|---|---|
   | M1 | MSAA off, HDR off, scale 1 | Rain visible; passes `Rain Copy Scene Color`, `Rain Draw`; camera color after Rain Draw is `_RainSceneColorCopy`; no extra copy-back pass |
   | M2 | MSAA 4x | Rain visible; copy pass performs resolve; no "MSAA sample mismatch" errors |
   | M3 | HDR on | Rain visible; `_RainSceneColorCopy` format equals camera color format (check Render Graph Viewer) |
   | M4 | Render Scale 0.5 | Rain visible at correct proportions; `_ScreenParams`-based aspect correct |
   | M5 | Post-processing off on camera and renderer, Intermediate Texture = Auto | If the warning `renders to the backbuffer` appears, set Intermediate Texture = Always and re-check; record which combination needs `Always` |
   | M6 | Camera `targetTexture` = 1280×720 RenderTexture displayed on a UI RawImage | Rain visible in the RenderTexture |
   | M7 | Base + Overlay camera stack: Base renders still-life, Overlay renders a rotating cube; attach `RainEffect` to Base only, then to Overlay only | Record which attachment includes the overlay cube in refraction; expected: Overlay-camera attachment includes it, Base attachment does not (rain applied before overlay draws). Simulation ticks once per frame regardless (verify by a counter log in the driver) |
   | M8 | Scene View with `PreviewInSceneView = true` | Rain drawn in Scene View; with `false` not drawn; Preview camera (material preview window) never shows rain |
   | M9 | Orthographic main camera | Identical rain to perspective (screen-space effect) |
   | M10 | Screen Space Overlay UI canvas with text | Text stays sharp (drawn after rain); Screen Space Camera canvas text is refracted; document both |
7. Build and run WebGL2 (Build Profiles → Web, Development build off, WebGL2 API) and open in Chrome; capture `spike-webgl2.png`. Build and run Android (IL2CPP, ARM64, GLES3 first, then Vulkan) on the available device; capture `spike-android-gles3.png` and `spike-android-vulkan.png`. If a build module or device is unavailable, record `GAP:` in the decision doc; this gap is re-checked in Phase 8 and blocks release, not this phase.
8. Measure with Unity Profiler (GPU module where available) and record in `docs/renderer-decision.md`: number of native render passes with and without rain (Render Graph Viewer), GPU ms of `Rain Copy Scene Color`, `Rain Blur Scene Color`, `Rain Draw` on Windows D3D11 at 1920×1080, WebGL2 frame time with/without rain, Android frame time with/without rain.
9. Write `docs/renderer-decision.md` with sections: `Summary` (decision: direct batched refraction retained; distortion-field alternative not built — justified by measurements in step 8 unless `Rain Draw` GPU cost at High density estimate exceeds the §7 budget, in which case add a blocking open question to `index.md` before Phase 4), `API notes` (exact URP version and any renamed API from Task 5), `Orientation`, `Coverage`, `Matrix results` (table M1–M10 with pass/fail), `Platform results`, `Measurements`, `Gaps`.

### Required Interfaces and Contracts

- The spike must use only package runtime types (`RainEffect`, `RainBatchMesh`, `RainMaterials`, `RainShaderSet`) and no legacy scripts; if the spike needs a runtime change, make it in the package, not in `Assets/Spike`.
- Captured images live in `docs/images/` (PNG, ≤ 1280×720).

### Error Handling and Logging

- Driver logs `RainLog.Verbose` once per toggle change with the active batch set; nothing per frame.
- Simulation-tick counter for M7: `RainLog.Verbose($"ticks this second: {n}")` once per second only while `LogTicks` is toggled on.

### Tests

- Add `<pkg>/Tests/Runtime/RainRenderPassTests.cs` (PlayMode): create a camera with `targetTexture` 256×256 (URP renders to `targetTexture` through an intermediate), add `RainEffect`, supply one lens quad via a test helper that writes `RenderData` (internal access through `[assembly: InternalsVisibleTo("RainDropEffect.Tests.Runtime")]` in `<pkg>/Runtime/AssemblyInfo.cs`), render with `RenderPipeline.SubmitRenderRequest(cam, new UniversalRenderPipeline.SingleCameraRequest { destination = rt })` (not `Camera.Render()`, which is unreliable under SRP), then `yield return null`, set `RenderTexture.active = rt`, `ReadPixels` the center pixel and a corner pixel; assert the center pixel differs from a no-rain render of the same scene by more than 8/255 in at least one channel, and the corner pixel is unchanged. Precondition (assert in `[SetUp]`): `GraphicsSettings.currentRenderPipeline` is a `UniversalRenderPipelineAsset` whose default renderer contains `RainRendererFeature` (the host `Rain-URP.asset`); otherwise `Assert.Inconclusive`. This is the rendering contract regression test reused by later phases.

### Acceptance Criteria

- M1–M10 recorded with pass results, or explicit failures each mapped to a fix in `index.md` open questions.
- WebGL2 and Android (GLES3) captures exist, or `GAP:` entries exist.
- `docs/renderer-decision.md` exists with all sections and the direct-batched decision stated.
- `RainRenderPassTests` passes on Windows editor.

### Verification

- `ls docs/images | grep -c spike-` → ≥ 4.
- `grep -c "PASS" docs/renderer-decision.md` → ≥ 9 for M1–M10 (M5/M7 may be `PASS (conditional)`).
- PlayMode test run → `RainRenderPassTests` passed.

## Task 8: Bake coverage masks for all texture pairs

### Intent

Provide the explicit coverage channel the lens pass requires (D9) for every shipped overlay/normal pair, so Phase 7 conversion can assign masks without authoring work.

### Implementation Steps

1. Create `<pkg>/Editor/Baking/CoverageMaskBaker.cs` (`RainDropEffect.Editor`), menu `Rain Drop Effect/Bake Coverage Masks` and a static method `public static Texture2D Bake(Texture2D normal, Texture2D overlay, string outputAssetPath, int maxSize = 1024)`.
2. Reading pixels: for each input texture, save `TextureImporter` fields `isReadable`, `textureType`, `textureCompression`, `sRGBTexture`; set `isReadable = true`, `textureType = TextureImporterType.Default`, `textureCompression = TextureImporterCompression.Uncompressed`; `SaveAndReimport()`; read `GetPixels32()`; restore the saved fields; `SaveAndReimport()`. Wrap in `try/finally` so importer settings are always restored.
3. Mask computation at the target resolution `min(maxSize, max(width,height))` (downsample by box-averaging when larger):
   - `nx = (r/255 − 0.5)·2`, `ny = (g/255 − 0.5)·2` from the raw (uncompressed, Default-type) normal texture; `normalDev = sqrt(nx²+ny²) > 0.06`.
   - `overlayLum = max(r,g,b)/255 > 0.03` from the overlay texture (sampled at the same resolution).
   - `mask = normalDev || overlayLum`; dilate by 2 pixels (two 3×3 max passes); then one 3×3 box blur to soften edges; store as byte `0..255`.
4. Write `<pkg>/Runtime/Textures/Coverage/<overlayName>_coverage.png` (`EncodeToPNG` from an `R8`-formatted `Texture2D`; when `R8` is unsupported for `EncodeToPNG`, use `RGBA32` with the value replicated in R). Then set its importer: `textureType = SingleChannel`, `singleChannelComponent = Red`, `sRGBTexture = false`, `mipmapEnabled = true`, `wrapMode = Clamp`, `textureCompression = Compressed` (defaults to R8/BC4-class), `maxTextureSize = 1024`; `SaveAndReimport()`.
5. Menu command bakes the 10 pairs from `Assets/RainDropEffect2/Textures/`: `Rain/rain_drop`, `Rain/rain_frame`, `Rain/flow`, `Rain/bubble`, `Rain/wash`, `Rain/rain_white`, `Frozen/frozenfill`, `Frozen/frozenframe`, `Blood/bloodframe`, `Blood/bloodsplatter` (normal = `<name>_normal.tga`, overlay = `<name>.tga`). Textures are still under `Assets/` at this point; Task 9 moves them and the baker's path list is updated to `<pkg>/Runtime/Textures/...` in Task 9.
6. Log per texture: `RainLog.Verbose($"coverage {name}: {coveredPercent:0.0}% covered, {w}x{h}")`. Record the percentages in `docs/renderer-decision.md` §Coverage; any pair below 1% or above 99% coverage is flagged for manual review (a full-frame overlay such as `frozenfill` is expected to be near 100%; a drop such as `rain_drop` should be well below).
7. Re-run the Task 7 overlap check with the real `rain_drop_coverage` mask and update `spike-overlap.png`.

### Required Interfaces and Contracts

- Output naming `<overlayName>_coverage.png` is used by Task 26's converter to find masks automatically.
- `Bake` is deterministic for identical inputs (no random, no time).
- Baker must not leave importer settings changed (verified by test).

### Error Handling and Logging

- Missing normal or overlay: `RainLog.Error($"coverage bake skipped: missing {path}")`, continue with the next pair.
- Importer restore failure: `RainLog.Error` with the asset path; the `finally` block always attempts restore.

### Tests

- `<pkg>/Tests/Editor/CoverageMaskBakerTests.cs`: 
  - `BakeProducesMaskWhereNormalDeviates`: build two 16×16 `Texture2D` in memory (normal flat `(128,128,255)` except a 4×4 patch with `(200,128,255)`; overlay black), call an overload `BakeInMemory(Color32[] normal, Color32[] overlay, int w, int h)` (expose the pure function separately from the asset I/O), assert mask value > 0 inside the patch plus 2-px dilation and `== 0` at the far corner.
  - `BakeIsDeterministic`: two calls, byte-equal arrays.
  - `ImporterSettingsRestored`: for `rain_drop_normal.tga`, read importer fields before and after `Bake` and assert equality.

### Acceptance Criteria

- 10 coverage PNGs exist under `<pkg>/Runtime/Textures/Coverage/` with importer type SingleChannel, sRGB off.
- Coverage percentages recorded; `rain_drop` between 5% and 60%, `frozenfill` above 80%.
- Tests pass.

### Verification

- `ls Packages/com.virtualmaestro.raindropeffect/Runtime/Textures/Coverage/*_coverage.png | wc -l` → `10`.
- EditMode test run → `CoverageMaskBakerTests` passed.

## Phase Risks and Mitigations

- Risk: URP API names differ in the locked 17.3 patch (`TextureDesc` fields, `AddBlitPass` overloads, `RasterCommandBuffer.DrawMesh`).
  Mitigation: Task 5 step 4 note; fix names, keep topology, record in `docs/renderer-decision.md` §API notes.
- Risk: `activeColorTexture` is the backbuffer for cameras without post-processing/HDR/MSAA even with `requiresIntermediateTexture = true`.
  Mitigation: M5 records the exact setting that guarantees an intermediate texture; Task 17's inspector warns when the active renderer's Intermediate Texture is not `Always` and no other trigger applies.
- Risk: WebGL2/Android build modules or devices missing on the development machine.
  Mitigation: record `GAP:`; the plan's release gate (Task 31) blocks on it, the spike gate does not.
- Risk: legacy textures have no natural coverage and baked masks look wrong (halos).
  Mitigation: dilation/blur constants are parameters of `Bake`; visual check on `rain_drop` and `frozenframe` in Task 8 step 7; adjust and re-bake once, record the values.

## Phase Completion Checklist

- Every Task N in this phase satisfies its acceptance criteria.
- Required verification commands pass.
- `index.md` task checkboxes are updated immediately after verified completion.
