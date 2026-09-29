using System;
using System.Collections.Generic;

namespace RainDropEffect
{
    /// <summary>
    /// All live state for one <see cref="RainEffect"/>: the layer runtimes in draw order and the
    /// RNG they share.
    ///
    /// One state per component, never per profile — that is what lets two cameras run the same
    /// profile with different seeds and independent playback.
    /// </summary>
    internal sealed class RainRuntimeState : IDisposable
    {
        public readonly List<LayerRuntime> Layers = new List<LayerRuntime>(8);

        public RainRandom Rng;

        readonly List<RainLayerSettings> ordered = new List<RainLayerSettings>(8);

        public RainRuntimeState(RainProfile profile, RainShaderSet shaders, RainQuality quality, int seed)
        {
            // Seed 0 means "different every run"; anything else reproduces exactly.
            Rng = new RainRandom(seed == 0 ? Environment.TickCount : seed);

            profile.CollectOrdered(ordered);
            for (var i = 0; i < ordered.Count; i++)
            {
                var runtime = LayerRuntimeFactory.Create(ordered[i], shaders, quality);
                if (runtime != null)
                {
                    Layers.Add(runtime);
                }
            }
        }

        /// <summary>Number of enabled layers in the profile, whether or not a runtime exists for them.</summary>
        public int OrderedCount => ordered.Count;

        public bool IsPlaying
        {
            get
            {
                for (var i = 0; i < Layers.Count; i++)
                {
                    if (Layers[i].IsPlaying)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public bool IsEmitting
        {
            get
            {
                for (var i = 0; i < Layers.Count; i++)
                {
                    if (Layers[i].IsEmitting)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public void Play()
        {
            for (var i = 0; i < Layers.Count; i++)
            {
                Layers[i].Play(ref Rng);
            }
        }

        public void PlayAutoStart()
        {
            for (var i = 0; i < Layers.Count; i++)
            {
                if (Layers[i].Settings.AutoStart)
                {
                    Layers[i].Play(ref Rng);
                }
            }
        }

        public void Stop()
        {
            for (var i = 0; i < Layers.Count; i++)
            {
                Layers[i].Stop();
            }
        }

        public void Clear()
        {
            for (var i = 0; i < Layers.Count; i++)
            {
                Layers[i].Clear();
            }
        }

        public void Tick(in RainTickContext ctx)
        {
            for (var i = 0; i < Layers.Count; i++)
            {
                Layers[i].Tick(ctx, ref Rng);
            }
        }

        /// <summary>
        /// Builds this frame's draw list. Layers keep their order, so the render data is already
        /// sorted back to front; the pass draws it as-is.
        /// </summary>
        public void Emit(in RainTickContext ctx, RainRenderData into, RainQuality quality)
        {
            into.Clear();

            for (var i = 0; i < Layers.Count; i++)
            {
                var layer = Layers[i];
                layer.Emit(ctx);

                if (layer.Mesh != null && layer.Mesh.Overflowed)
                {
                    // Once per layer: the cap is a quality decision, not a per-frame error.
                    RainLog.WarnOnce("overflow:" + layer.Settings.Name,
                        $"layer '{layer.Settings.Name}' exceeded its mesh capacity; geometry truncated");
                }

                if (!layer.Visible || layer.Mesh == null || layer.Mesh.IsEmpty)
                {
                    continue;
                }

                into.Batches.Add(new RainBatch
                {
                    Mesh = layer.Mesh.Mesh,
                    Material = layer.Material,
                    ShaderPass = layer.IsLens ? RainShaderPass.Lens : RainShaderPass.Overlay,
                    IsLens = layer.IsLens
                });

                into.AnyVisible = true;
                into.AnyLens |= layer.IsLens;
                into.AnyBlur |= layer.UsesBlur;
            }

            into.BlurRadius = RainQualityCaps.BlurRadius(quality);
        }

        public void Dispose()
        {
            for (var i = 0; i < Layers.Count; i++)
            {
                Layers[i].Dispose();
            }

            Layers.Clear();
        }
    }
}
