using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace RainDropEffect.Tests
{
    /// <summary>
    /// The shared blur is demand-driven: it costs a half-resolution pass only when a visible lens
    /// layer asks for it and the quality tier allows it. These tests assert that through the
    /// rendered image, because that is the only place the difference is observable.
    /// </summary>
    public class RainBlurPassTests
    {
        const int Size = 256;

        GameObject cameraObject;
        GameObject backdrop;
        RenderTexture target;
        RainBatchMesh mesh;
        Material material;
        Material backdropMaterial;
        Texture2D checker;
        Texture2D tiltedNormal;

        [SetUp]
        public void SetUp()
        {
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null)
            {
                Assert.Inconclusive("The active render pipeline is not URP; the rain pass cannot run.");
            }

            var lens = Shader.Find("Hidden/RainDropEffect/Lens");
            Assert.IsNotNull(lens, "Lens shader not found");

            target = new RenderTexture(Size, Size, 24) { name = "RainBlurPassTests" };
            cameraObject = new GameObject("RainBlurTestCamera", typeof(Camera));

            var camera = cameraObject.GetComponent<Camera>();
            camera.targetTexture = target;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.25f, 0.5f, 0.75f, 1f);

            // The blur weight is proportional to |n.x|, so a flat normal map would switch the blur
            // off no matter what the settings say. This one leans fully sideways.
            tiltedNormal = new Texture2D(4, 4, TextureFormat.RGBA32, false, true);
            var tilted = new Color32(255, 128, 255, 255);
            var pixels = tiltedNormal.GetPixels32();
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = tilted;
            }

            tiltedNormal.SetPixels32(pixels);
            tiltedNormal.Apply();

            material = new Material(lens);
            material.SetTexture(RainShaderIds.NormalMap, tiltedNormal);
            // Black overlay and no relief: the drop then shows the scene sample and nothing else,
            // which is the only way a blur difference is observable at all.
            material.SetTexture(RainShaderIds.OverlayTex, Texture2D.blackTexture);
            material.SetTexture(RainShaderIds.CoverageTex, Texture2D.whiteTexture);
            // Local keyword, set the same way RainMaterials does: EnableKeyword(string) would go to
            // the global set and leave the local variant switched off.
            material.SetKeyword(RainKeywords.Blur(lens), true);

            mesh = new RainBatchMesh(16, 24, "blur-batch");

            // A blur is invisible against a flat colour, so the camera needs something with edges
            // in it before the test can see anything at all.
            CreateCheckeredBackdrop(camera);
        }

        void CreateCheckeredBackdrop(Camera camera)
        {
            const int size = 16;
            checker = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat
            };

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    checker.SetPixel(x, y, (x + y) % 2 == 0 ? Color.black : Color.white);
                }
            }

            checker.Apply();

            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            Assert.IsNotNull(unlit, "URP/Unlit not found");
            backdropMaterial = new Material(unlit);
            backdropMaterial.SetTexture("_BaseMap", checker);

            // Tile it hard: a 9-tap blur over a few source pixels is invisible against wide blocks,
            // so the backdrop has to carry detail at the scale the blur actually works at.
            backdropMaterial.SetTextureScale("_BaseMap", new Vector2(48f, 48f));

            backdrop = GameObject.CreatePrimitive(PrimitiveType.Quad);
            backdrop.name = "RainBlurBackdrop";
            backdrop.GetComponent<Renderer>().sharedMaterial = backdropMaterial;
            backdrop.transform.position = camera.transform.position + camera.transform.forward * 2f;
            backdrop.transform.rotation = camera.transform.rotation;
            backdrop.transform.localScale = new Vector3(8f, 8f, 1f);
        }

        [TearDown]
        public void TearDown()
        {
            mesh?.Dispose();

            if (material != null)
            {
                Object.DestroyImmediate(material);
            }

            if (backdrop != null)
            {
                Object.DestroyImmediate(backdrop);
            }

            if (backdropMaterial != null)
            {
                Object.DestroyImmediate(backdropMaterial);
            }

            if (checker != null)
            {
                Object.DestroyImmediate(checker);
            }

            if (tiltedNormal != null)
            {
                Object.DestroyImmediate(tiltedNormal);
            }

            if (cameraObject != null)
            {
                Object.DestroyImmediate(cameraObject);
            }

            if (target != null)
            {
                target.Release();
                Object.DestroyImmediate(target);
            }
        }

        [UnityTest]
        public IEnumerator BlurChangesTheImageOnlyWhenRequestedAndAllowed()
        {
            var camera = cameraObject.GetComponent<Camera>();
            var effect = cameraObject.AddComponent<RainEffect>();
            yield return null;

            // Same pass, same blur texture: only the per-vertex blur amount differs, which is what
            // the quality tier and the layer setting between them decide.
            Fill(effect, blur: 0.8f, blurRadius: 3f, wantsBlur: true);
            var withBlur = Render(camera);

            Fill(effect, blur: 0f, blurRadius: 3f, wantsBlur: true);
            var withoutBlur = Render(camera);

            Assert.Greater(MaxChannelDelta(withBlur, withoutBlur), 4,
                "a blurred drop must not look identical to an unblurred one");
        }

        [UnityTest]
        public IEnumerator LowQualityDisablesBlurEntirely()
        {
            // The tier decision is data, not a render detail; it is what removes the pass.
            Assert.IsFalse(RainQualityCaps.BlurEnabled(RainQuality.Low));
            Assert.IsTrue(RainQualityCaps.BlurEnabled(RainQuality.Balanced));
            Assert.IsTrue(RainQualityCaps.BlurEnabled(RainQuality.High));

            var camera = cameraObject.GetComponent<Camera>();
            var effect = cameraObject.AddComponent<RainEffect>();
            yield return null;

            // A layer built at Low never sets UsesBlur, so the render data never asks for the pass.
            Fill(effect, blur: 0.8f, blurRadius: RainQualityCaps.BlurRadius(RainQuality.High), wantsBlur: false);
            var low = Render(camera);

            Fill(effect, blur: 0f, blurRadius: 0f, wantsBlur: false);
            var reference = Render(camera);

            Assert.LessOrEqual(MaxChannelDelta(low, reference), 1,
                "without the blur pass the blur amount must have no effect at all");
        }

        [UnityTest]
        public IEnumerator ZeroBlurMatchesNoBlurRequest()
        {
            var camera = cameraObject.GetComponent<Camera>();
            var effect = cameraObject.AddComponent<RainEffect>();
            yield return null;

            // Blur 0 with the pass available must land on the same pixels as never asking for it:
            // the blur term multiplies out, so the copy is what gets sampled either way.
            Fill(effect, blur: 0f, blurRadius: 3f, wantsBlur: true);
            var requested = Render(camera);

            Fill(effect, blur: 0f, blurRadius: 0f, wantsBlur: false);
            var notRequested = Render(camera);

            Assert.LessOrEqual(MaxChannelDelta(requested, notRequested), 1);
        }

        void Fill(RainEffect effect, float blur, float blurRadius, bool wantsBlur)
        {
            mesh.Begin();
            RainBatchMesh.AddQuad(mesh, Vector2.zero, new Vector2(0.5f, 0.5f), 0f, 1f,
                new Color32(128, 128, 128, 255), new Vector4(5f, 0f, blur, 0f));
            mesh.End();

            var data = effect.RenderData;
            data.Clear();
            data.Batches.Add(new RainBatch
            {
                Mesh = mesh.Mesh, Material = material, ShaderPass = RainShaderPass.Lens, IsLens = true
            });
            data.AnyVisible = true;
            data.AnyLens = true;
            data.AnyBlur = wantsBlur;
            data.BlurRadius = blurRadius;
        }

        Color32[] Render(Camera camera)
        {
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
            if (RenderPipeline.SupportsRenderRequest(camera, request))
            {
                RenderPipeline.SubmitRenderRequest(camera, request);
            }

            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var readback = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            readback.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            readback.Apply(false, false);
            RenderTexture.active = previous;

            // A strip inside the drop: one sample can land in the middle of a block where a blur
            // changes nothing, so the comparison walks across several.
            var strip = new Color32[48];
            for (var i = 0; i < strip.Length; i++)
            {
                strip[i] = readback.GetPixel(Size / 2 - 24 + i, Size / 2);
            }

            Object.DestroyImmediate(readback);
            return strip;
        }

        static int MaxChannelDelta(Color32[] a, Color32[] b)
        {
            var worst = 0;
            for (var i = 0; i < a.Length; i++)
            {
                var delta = Mathf.Max(Mathf.Abs(a[i].r - b[i].r),
                    Mathf.Max(Mathf.Abs(a[i].g - b[i].g), Mathf.Abs(a[i].b - b[i].b)));
                worst = Mathf.Max(worst, delta);
            }

            return worst;
        }
    }
}
