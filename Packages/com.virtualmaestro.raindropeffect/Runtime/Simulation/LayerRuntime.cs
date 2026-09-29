using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace RainDropEffect
{
    /// <summary>
    /// One layer's live state: its mesh, its material, and the simulation that fills them.
    ///
    /// A runtime owns GPU resources, so every one that is created must be disposed — the component
    /// does that in <c>OnDisable</c>. Settings are read-only here: several cameras may share one
    /// profile, and a layer that wrote into its settings would leak state between them.
    /// </summary>
    internal abstract class LayerRuntime : IDisposable
    {
        public RainLayerSettings Settings { get; }

        public RainBatchMesh Mesh { get; private set; }

        public Material Material { get; private set; }

        public bool IsLens => Settings.Mode == RainLayerMode.Lens;

        /// <summary>Blur is a lens-only effect and is off entirely at Low quality.</summary>
        public bool UsesBlur { get; private set; }

        /// <summary>Whether this layer produced geometry this frame.</summary>
        public bool Visible { get; protected set; }

        /// <summary>Has visible drops, is emitting, or is still fading out.</summary>
        public abstract bool IsPlaying { get; }

        public abstract bool IsEmitting { get; }

        /// <summary>
        /// Sizes the mesh from a budget <see cref="LayerRuntimeFactory"/> computed. Deriving it here
        /// rather than in each subclass keeps one copy of the arithmetic: a mesh sized too small
        /// does not throw, it silently drops geometry.
        /// </summary>
        protected LayerRuntime(RainLayerSettings settings, RainShaderSet shaders, RainQuality quality,
            in MeshCapacity capacity)
        {
            Settings = settings;
            Mesh = new RainBatchMesh(capacity.Vertices, capacity.Indices, settings.Name);
            UsesBlur = IsLens && settings.Blur > 0f && RainQualityCaps.BlurEnabled(quality);
            Material = RainMaterials.CreateLayerMaterial(
                shaders, settings.NormalMap, settings.OverlayTexture, settings.CoverageMask, UsesBlur);
        }

        /// <summary>
        /// Reports the mesh a trail family sized for itself, split into the ribbon allowance and the
        /// per-trail head quad. An arithmetic slip in that formula does not throw — RainBatchMesh
        /// sets <c>Overflowed</c> and drops the vertex — so the numbers have to be visible somewhere.
        /// Construction only; never per frame (decision D17).
        /// </summary>
        [System.Diagnostics.Conditional("RAINDROP_VERBOSE_LOG")]
        protected static void LogMeshCapacity(string family, string name, int trails, RainBatchMesh mesh)
        {
            RainLog.Verbose(
                $"{family} layer '{name}': {trails} trails, mesh capacity {mesh.VertexCapacity} vertices / " +
                $"{mesh.IndexCapacity} indices, of which {TrailBuffer.HeadVertices * trails} vertices and " +
                $"{TrailBuffer.HeadIndices * trails} indices are the head quads");
        }

        public abstract void Play(ref RainRandom rng);

        /// <summary>Stops emission; whatever is on screen finishes its life.</summary>
        public abstract void Stop();

        /// <summary>Removes everything immediately and cancels pending delays.</summary>
        public abstract void Clear();

        public abstract void Tick(in RainTickContext ctx, ref RainRandom rng);

        /// <summary>Writes this frame's geometry into <see cref="Mesh"/> and sets <see cref="Visible"/>.</summary>
        public abstract void Emit(in RainTickContext ctx);

        public void Dispose()
        {
            Mesh?.Dispose();
            Mesh = null;
            CoreUtils.Destroy(Material);
            Material = null;
        }
    }
}
