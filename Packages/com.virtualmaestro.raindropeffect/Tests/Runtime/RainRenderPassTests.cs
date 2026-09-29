using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace RainDropEffect.Tests
{
    /// <summary>
    /// The rendering contract: with one visible lens batch the camera image changes where the drop
    /// is and stays untouched everywhere else. Reused by later phases as the regression test for
    /// the pass topology.
    /// </summary>
    public class RainRenderPassTests
    {
        const int Size = 256;

        GameObject cameraObject;
        GameObject overlayObject;
        RenderTexture target;
        RainBatchMesh mesh;
        Material material;

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

            target = new RenderTexture(Size, Size, 24) { name = "RainRenderPassTests" };
            cameraObject = new GameObject("RainTestCamera", typeof(Camera));

            var camera = cameraObject.GetComponent<Camera>();
            camera.targetTexture = target;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.25f, 0.5f, 0.75f, 1f);

            material = new Material(lens);
            material.SetTexture(RainShaderIds.NormalMap, Texture2D.normalTexture);
            material.SetTexture(RainShaderIds.OverlayTex, Texture2D.whiteTexture);
            material.SetTexture(RainShaderIds.CoverageTex, Texture2D.whiteTexture);

            mesh = new RainBatchMesh(16, 24, "test-batch");
        }

        [TearDown]
        public void TearDown()
        {
            mesh?.Dispose();

            if (material != null)
            {
                Object.DestroyImmediate(material);
            }

            if (overlayObject != null)
            {
                Object.DestroyImmediate(overlayObject);
                overlayObject = null;
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
        public IEnumerator LensBatchChangesTheCentreAndLeavesTheCornerAlone()
        {
            var camera = cameraObject.GetComponent<Camera>();

            yield return null;
            var without = Render(camera);

            var effect = cameraObject.AddComponent<RainEffect>();
            yield return null;

            // Fill after the frame boundary: RainEffect.LateUpdate clears its render data every
            // frame, so data injected before the yield would be gone by the time the pass runs.
            FillOneCentredQuad(effect);

            var with = Render(camera);

            var centreDelta = MaxChannelDelta(without.centre, with.centre);
            var cornerDelta = MaxChannelDelta(without.corner, with.corner);

            Assert.Greater(centreDelta, 8, "the drop must change the centre pixel");
            Assert.LessOrEqual(cornerDelta, 1, "the corner pixel must be untouched");
        }

        [UnityTest]
        public IEnumerator LensBatchRendersThroughACameraStack()
        {
            // Q8: the original topology drew into a copy and reassigned resourceData.cameraColor,
            // which a Base camera with a stack ignores - the Overlay cameras keep the original
            // attachment and the rain is dropped. This is the regression test for that.
            var camera = cameraObject.GetComponent<Camera>();
            // The extension adds the component when the camera does not have it yet.
            var baseData = camera.GetUniversalAdditionalCameraData();

            overlayObject = new GameObject("RainTestOverlay", typeof(Camera));
            var overlayCamera = overlayObject.GetComponent<Camera>();
            overlayCamera.clearFlags = CameraClearFlags.Nothing;
            overlayCamera.GetUniversalAdditionalCameraData().renderType = CameraRenderType.Overlay;
            baseData.cameraStack.Add(overlayCamera);

            yield return null;
            var without = Render(camera);

            var effect = cameraObject.AddComponent<RainEffect>();
            yield return null;
            FillOneCentredQuad(effect);

            var with = Render(camera);

            Assert.Greater(MaxChannelDelta(without.centre, with.centre),
                8, "the drop must be visible when the base camera has a stack");
            Assert.LessOrEqual(MaxChannelDelta(without.corner, with.corner),
                1, "the corner pixel must be untouched");
        }

        [UnityTest]
        public IEnumerator InactiveEffectEnqueuesNoPass()
        {
            // NFR-02: nothing visible means no copy, no blur and no draw, so the image must be
            // pixel-identical to a frame with no effect on the camera at all.
            var camera = cameraObject.GetComponent<Camera>();

            yield return null;
            var without = Render(camera);

            var effect = cameraObject.AddComponent<RainEffect>();
            effect.Intensity = 0f;
            yield return null;

            var with = Render(camera);

            Assert.AreEqual(0, MaxChannelDelta(without.centre, with.centre),
                "an effect with nothing visible must not change a single pixel");
            Assert.AreEqual(0, MaxChannelDelta(without.corner, with.corner));
        }

        void FillOneCentredQuad(RainEffect effect)
        {
            mesh.Begin();
            RainBatchMesh.AddQuad(mesh, Vector2.zero, new Vector2(0.5f, 0.5f), 0f, 1f,
                new Color32(128, 128, 128, 255), new Vector4(50f, 1.5f, 0f, 10f));
            mesh.End();

            var data = effect.RenderData;
            data.Clear();
            data.Batches.Add(new RainBatch
            {
                Mesh = mesh.Mesh, Material = material, ShaderPass = RainShaderPass.Lens, IsLens = true
            });
            data.AnyVisible = true;
            data.AnyLens = true;
            data.BlurRadius = 3f;
        }

        (Color32 centre, Color32 corner) Render(Camera camera)
        {
            // Camera.Render() is unreliable under SRP; the render request is the supported path.
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

            var centre = readback.GetPixel(Size / 2, Size / 2);
            var corner = readback.GetPixel(2, 2);
            Object.DestroyImmediate(readback);
            return ((Color32)centre, (Color32)corner);
        }

        static int MaxChannelDelta(Color32 a, Color32 b)
        {
            return Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Max(Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b)));
        }
    }
}
