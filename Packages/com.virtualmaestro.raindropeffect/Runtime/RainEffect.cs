using System.Collections.Generic;
using UnityEngine;

namespace RainDropEffect
{
    /// <summary>
    /// Camera-side entry point and the public API of the package: one owner per camera for the
    /// profile, the seeded RNG, the layer runtimes, their meshes and materials, playback, the tick
    /// cadence, and the render data the pass reads.
    ///
    /// Several components may sit on one camera (the blood sample stacks six). Each keeps its own
    /// state and its own render data, so they play and stop independently; the pass concatenates
    /// their batches in component order.
    ///
    /// The component never writes into the profile, so one profile asset can back any number of
    /// cameras.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    [AddComponentMenu("Rendering/Rain Effect")]
    public sealed partial class RainEffect : MonoBehaviour
    {
        /// <summary>Bounded catch-up (D13): a long frame advances the simulation by at most this.</summary>
        const float MaxDeltaTime = 0.1f;

        /// <summary>Seconds between two "dt clamped" messages, so a slow scene cannot spam the log.</summary>
        const float ClampLogInterval = 5f;

#if UNITY_EDITOR
        const string ShaderSetPath =
            "Packages/com.virtualmaestro.raindropeffect/Runtime/Shaders/RainShaderSet.asset";
#endif

        static readonly Dictionary<Camera, List<RainEffect>> registry = new Dictionary<Camera, List<RainEffect>>();

        public RainProfile Profile;

        [Range(0, 1)] public float Intensity = 1f;

        /// <summary>Screen-space push, in normalized units per second.</summary>
        public Vector2 Wind;

        /// <summary>World-space gravity; projected into screen space every frame.</summary>
        public Vector3 Gravity = Vector3.down;

        public RainQuality Quality = RainQuality.High;

        /// <summary>0 reseeds every run; anything else reproduces a run exactly.</summary>
        public int Seed = 0;

        /// <summary>Draw this effect in the Scene View as well. Only the first enabled source is used.</summary>
        public bool PreviewInSceneView;

        /// <summary>Simulate outside Play Mode, driven by real time.</summary>
        public bool PreviewInEditMode;

        [SerializeField, HideInInspector] RainShaderSet shaderSet;

        Camera cachedCamera;
        RainRuntimeState state;
        RainTickContext lastContext;

        bool rebuildRequested;
        float lastRealtime;
        float lastClampLogTime = float.NegativeInfinity;

        // What the current state was built from; a change to any of these needs a rebuild.
        RainProfile builtProfile;
        RainQuality builtQuality;
        int builtSeed;

        /// <summary>The effect the Scene View camera renders, or null.</summary>
        public static RainEffect SceneViewPreviewSource { get; private set; }

        /// <summary>Geometry for this frame. Written by the simulation in LateUpdate, read by the pass.</summary>
        internal RainRenderData RenderData { get; private set; } = new RainRenderData();

        internal RainRuntimeState State => state;

        public bool IsPlaying => state != null && state.IsPlaying;

        public bool IsEmitting => state != null && state.IsEmitting;

        public static bool TryGet(Camera cam, out List<RainEffect> effects)
        {
            return registry.TryGetValue(cam, out effects);
        }

        /// <summary>
        /// Whether this camera has anything to draw at all. The renderer feature consults this
        /// before enqueuing, so an effect at zero intensity, drained or cleared costs no copy, no
        /// blur and no draw (NFR-02).
        /// </summary>
        internal static bool AnyVisibleFor(Camera cam)
        {
            if (!registry.TryGetValue(cam, out var effects))
            {
                return false;
            }

            for (var i = 0; i < effects.Count; i++)
            {
                var effect = effects[i];
                if (effect != null && effect.isActiveAndEnabled
                    && effect.RenderData != null && effect.RenderData.AnyVisible)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Starts every layer. Layers already emitting are not restarted (D12).</summary>
        public void Play()
        {
            if (state == null)
            {
                Rebuild();
            }

            state?.Play();
            RainLog.Verbose("Play", this);
        }

        /// <summary>Stops emission and lets what is on screen finish its life (D12).</summary>
        public void Stop()
        {
            state?.Stop();
            RainLog.Verbose("Stop", this);
        }

        /// <summary>Removes everything on screen at once and cancels pending delays (D12).</summary>
        public void Clear()
        {
            state?.Clear();
            RenderData.Clear();
            RainLog.Verbose("Clear", this);
        }

        /// <summary>
        /// Rebuilds the runtime from the current profile, quality and seed. Everything on screen is
        /// lost; playback is not resumed (Update restores it when the rebuild came from OnValidate).
        /// </summary>
        public void Rebuild()
        {
            state?.Dispose();
            state = null;
            RenderData.Clear();

            builtProfile = Profile;
            builtQuality = Quality;
            builtSeed = Seed;

            if (Profile == null)
            {
                RainLog.WarnOnce("noprofile:" + WarnKey(this), $"RainEffect on '{name}' has no profile", this);
                return;
            }

            if (shaderSet == null)
            {
                RainLog.Error(
                    "RainEffect has no RainShaderSet (select the component in the editor to auto-assign)", this);
                return;
            }

            state = new RainRuntimeState(Profile, shaderSet, Quality, Seed);

            if (state.OrderedCount == 0)
            {
                RainLog.WarnOnce("nolayers:" + WarnKey(Profile),
                    $"profile '{Profile.name}' has no enabled layers", this);
            }

            RainLog.Verbose($"Rebuild (layers={state.Layers.Count}, quality={Quality}, seed={Seed})", this);
        }

        void OnEnable()
        {
            cachedCamera = GetComponent<Camera>();
            lastRealtime = Time.realtimeSinceStartup;
            Register();
            Rebuild();

            if (Application.isPlaying)
            {
                state?.PlayAutoStart();
            }

            RainLog.Verbose($"enable on {cachedCamera.name}", this);
        }

        void OnDisable()
        {
            Unregister();
            ReleaseState();
            RainLog.Verbose($"disable on {(cachedCamera != null ? cachedCamera.name : "<destroyed camera>")}", this);
        }

        void OnDestroy()
        {
            Unregister();
            ReleaseState();
        }

        void ReleaseState()
        {
            state?.Dispose();
            state = null;
            RenderData.Clear();
        }

        void Update()
        {
            if (!Application.isPlaying && !PreviewInEditMode)
            {
                return;
            }

            if (rebuildRequested)
            {
                rebuildRequested = false;
                var wasPlaying = IsPlaying;
                Rebuild();
                if (wasPlaying)
                {
                    state?.Play();
                }
            }

            if (state == null)
            {
                return;
            }

            var dt = Application.isPlaying
                ? Time.deltaTime
                : Mathf.Clamp(Time.realtimeSinceStartup - lastRealtime, 0f, MaxDeltaTime);
            lastRealtime = Time.realtimeSinceStartup;

            Advance(dt);
        }

        void LateUpdate()
        {
            if (state == null || (!Application.isPlaying && !PreviewInEditMode))
            {
                RenderData.Clear();
                return;
            }

            Publish();
        }

        /// <summary>Advances the simulation by one step. The only place the tick context is built.</summary>
        void Advance(float dt)
        {
            if (dt > MaxDeltaTime)
            {
                LogClamp(dt);
                dt = MaxDeltaTime;
            }

            lastContext = MakeContext(dt);
            state.Tick(lastContext);
        }

        void Publish()
        {
            // The target can be resized between Update and LateUpdate; drops must not go oval.
            lastContext.Aspect = CurrentAspect();
            state.Emit(lastContext, RenderData, Quality);
        }

        /// <summary>
        /// A per-object key for the once-only warnings.
        ///
        /// The requirement is only that two distinct objects get distinct keys, and
        /// <c>GetInstanceID()</c>, <c>GetEntityId()</c> and the managed identity hash all satisfy it.
        /// The one place they differ — a Unity instance id survives a domain reload, a managed
        /// identity hash does not — cannot matter here, because <c>RainLog.once</c> is a static
        /// <c>HashSet</c> that a domain reload recreates empty anyway.
        ///
        /// So the choice is free, and a plain .NET API is the one that needs no version branch:
        /// <c>GetInstanceID()</c> is obsolete-as-error in 6000.5 in favour of <c>GetEntityId()</c>,
        /// which 6000.3 does not have, and an <c>#if</c> between them is exactly what D3 forbids.
        /// This is not a rule worked around — there is nothing here for a rule to apply to.
        /// </summary>
        static int WarnKey(object target)
        {
            return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(target);
        }

        /// <summary>Test hook: the same step Update takes, with a caller-chosen delta time.</summary>
        internal void TickForTest(float dt)
        {
            if (state == null)
            {
                return;
            }

            Advance(dt);
        }

        /// <summary>Test hook: the same publication LateUpdate performs.</summary>
        internal void EmitForTest()
        {
            if (state == null)
            {
                RenderData.Clear();
                return;
            }

            Publish();
        }

        RainTickContext MakeContext(float dt)
        {
            return new RainTickContext
            {
                Dt = dt,
                Down = ProjectGravity(),
                Wind = Wind,
                Intensity = Intensity,
                Aspect = CurrentAspect(),
                Quality = Quality
            };
        }

        /// <summary>
        /// Gravity in screen space. Equivalent to the legacy
        /// <c>GetGForcedScreenMovement(...).normalized.xy</c> for the camera transform; a gravity
        /// pointing straight at the camera degenerates and falls back to down.
        /// </summary>
        Vector2 ProjectGravity()
        {
            if (cachedCamera == null)
            {
                return Vector2.down;
            }

            var local = cachedCamera.transform.InverseTransformDirection(Gravity);
            var d = new Vector2(local.x, local.y);
            return d.sqrMagnitude < 1e-8f ? Vector2.down : d.normalized;
        }

        /// <summary>
        /// Width over height of what the camera renders into. URP render scale changes the pixel
        /// count but not the aspect, so the unscaled size is the right one.
        /// </summary>
        float CurrentAspect()
        {
            if (cachedCamera == null)
            {
                return 1f;
            }

            var target = cachedCamera.targetTexture;
            var width = target != null ? target.width : cachedCamera.pixelWidth;
            var height = target != null ? target.height : cachedCamera.pixelHeight;
            return width / (float)Mathf.Max(1, height);
        }

        void LogClamp(float dt)
        {
            var now = Time.realtimeSinceStartup;
            if (now - lastClampLogTime < ClampLogInterval)
            {
                return;
            }

            lastClampLogTime = now;
            RainLog.Verbose($"dt clamped from {dt:0.###} to {MaxDeltaTime}", this);
        }

        void Register()
        {
            if (cachedCamera == null)
            {
                return;
            }

            if (!registry.TryGetValue(cachedCamera, out var list))
            {
                registry[cachedCamera] = list = new List<RainEffect>();
            }

            if (!list.Contains(this))
            {
                list.Add(this);
            }

            RefreshSceneViewSource();
        }

        void Unregister()
        {
            if (cachedCamera != null && registry.TryGetValue(cachedCamera, out var list))
            {
                list.Remove(this);
                if (list.Count == 0)
                {
                    registry.Remove(cachedCamera);
                }
            }

            RefreshSceneViewSource();
        }

        static void RefreshSceneViewSource()
        {
            SceneViewPreviewSource = null;
            foreach (var pair in registry)
            {
                var list = pair.Value;
                for (var i = 0; i < list.Count; i++)
                {
                    var effect = list[i];
                    if (effect != null && effect.isActiveAndEnabled && effect.PreviewInSceneView)
                    {
                        SceneViewPreviewSource = effect;
                        return;
                    }
                }
            }
        }

        void OnValidate()
        {
            Intensity = Mathf.Clamp01(Intensity);

#if UNITY_EDITOR
            if (shaderSet == null)
            {
                shaderSet = UnityEditor.AssetDatabase.LoadAssetAtPath<RainShaderSet>(ShaderSetPath);
            }
#endif

            // Rebuilding here would run inside the inspector's serialization callback; defer it.
            if (Profile != builtProfile || Quality != builtQuality || Seed != builtSeed)
            {
                rebuildRequested = true;
            }
        }

#if UNITY_EDITOR
        void Reset()
        {
            EditorAutoAssignShaderSet();
        }

        /// <summary>Points the component at the shipped shader set. Also the hook the editor test uses.</summary>
        internal void EditorAutoAssignShaderSet()
        {
            shaderSet = UnityEditor.AssetDatabase.LoadAssetAtPath<RainShaderSet>(ShaderSetPath);
            RainLog.Verbose($"shader set assigned: {shaderSet != null}", this);
        }

        internal RainShaderSet EditorShaderSet => shaderSet;

        /// <summary>
        /// True when some enabled component wants an edit-mode preview. Read from the editor loop,
        /// which is why it walks the existing registry instead of scanning the scene every 16 ms.
        /// </summary>
        internal static bool AnyEditModePreviewActive()
        {
            foreach (var pair in registry)
            {
                var list = pair.Value;
                for (var i = 0; i < list.Count; i++)
                {
                    var effect = list[i];
                    if (effect != null && effect.isActiveAndEnabled && effect.PreviewInEditMode)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
#endif

        /// <summary>Play Mode with domain reload disabled keeps statics alive; clear them explicitly.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            registry.Clear();
            SceneViewPreviewSource = null;
            RainLog.ResetOnce();
        }
    }

    /// <summary>What the pass draws for one effect this frame.</summary>
    internal sealed class RainRenderData
    {
        public readonly List<RainBatch> Batches = new List<RainBatch>(8);

        public bool AnyVisible;
        public bool AnyLens;
        public bool AnyBlur;

        /// <summary>Blur radius in half-resolution pixels; set by quality (Task 23).</summary>
        public float BlurRadius;

        public void Clear()
        {
            Batches.Clear();
            AnyVisible = false;
            AnyLens = false;
            AnyBlur = false;
        }
    }

    /// <summary>One draw: a mesh, its layer material, and which pass of the lens shader to use.</summary>
    internal struct RainBatch
    {
        public Mesh Mesh;
        public Material Material;
        public int ShaderPass;
        public bool IsLens;
    }
}
