using UnityEngine;

namespace RainDropEffect
{
    /// <summary>
    /// Transient droplets: rain drops, splash bubbles, blood splatter. They appear, drift a little
    /// and fade; there is no trail.
    ///
    /// State is a fixed array of value-type slots allocated once from the quality cap, so the
    /// simulation never allocates after construction. Slot order is draw order, which matches the
    /// legacy render-queue-by-index behaviour closely enough under the stable-source rule.
    /// </summary>
    internal sealed class SimpleLayerRuntime : LayerRuntime
    {
        struct Drop
        {
            public bool Active;
            public float Age;
            public float Lifetime;
            public float Rotation;
            public Vector2 Pos;
            public Vector2 Size;
        }

        readonly Drop[] drops;
        readonly SimpleLayerSettings s;

        EmitterState emitter;
        int activeCount;

        public SimpleLayerRuntime(SimpleLayerSettings settings, RainShaderSet shaders, RainQuality quality)
            : base(settings, shaders, quality,
                LayerRuntimeFactory.DropCapacity(settings.MaxCount, quality))
        {
            s = settings;
            drops = new Drop[Cap(settings, quality)];
        }

        static int Cap(SimpleLayerSettings s, RainQuality q) =>
            LayerRuntimeFactory.DropCount(s.MaxCount, q);

        /// <summary>Drops currently alive. Exposed for the runtime tests, not part of the API.</summary>
        internal int ActiveCount => activeCount;

        internal int Capacity => drops.Length;

        internal bool DropActive(int slot) => drops[slot].Active;

        internal Vector2 DropPosition(int slot) => drops[slot].Pos;

        public override bool IsPlaying =>
            activeCount > 0 || emitter.Current == EmitterState.Phase.Delay || emitter.IsEmitting;

        public override bool IsEmitting => emitter.IsEmitting;

        public override void Play(ref RainRandom rng) => emitter.Play(s, ref rng);

        public override void Stop() => emitter.Stop();

        public override void Clear()
        {
            emitter.Clear();
            for (var i = 0; i < drops.Length; i++)
            {
                drops[i].Active = false;
            }

            activeCount = 0;
            Visible = false;
        }

        public override void Tick(in RainTickContext ctx, ref RainRandom rng)
        {
            var spawn = emitter.Tick(ctx.Dt, drops.Length - activeCount, s, ref rng);
            for (var k = 0; k < spawn; k++)
            {
                Spawn(ctx, ref rng);
            }

            for (var i = 0; i < drops.Length; i++)
            {
                ref var d = ref drops[i];
                if (!d.Active)
                {
                    continue;
                }

                d.Age += ctx.Dt;
                var p = d.Lifetime > 0f ? d.Age / d.Lifetime : 1f;
                if (p >= 1f)
                {
                    d.Active = false;
                    activeCount--;
                    continue;
                }

                // Legacy: pos += (-gforced) * 0.01 * PosYOverLifetime. The shipped curves are
                // negative, so drops move along gravity rather than against it.
                d.Pos += -ctx.Down * (s.DriftSpeed * s.DriftOverLifetime.Evaluate(p) * ctx.Dt)
                         + ctx.Wind * (p * ctx.Dt);
            }

            if (emitter.Current == EmitterState.Phase.Draining && activeCount == 0)
            {
                emitter.Clear();
            }
        }

        /// <summary>Fills the first free slot. A full layer silently skips the spawn, as the legacy did.</summary>
        void Spawn(in RainTickContext ctx, ref RainRandom rng)
        {
            for (var i = 0; i < drops.Length; i++)
            {
                ref var d = ref drops[i];
                if (d.Active)
                {
                    continue;
                }

                d.Active = true;
                activeCount++;
                d.Age = 0f;
                d.Lifetime = rng.Range(s.LifetimeRange.x, s.LifetimeRange.y);
                d.Pos = new Vector2(
                    rng.Range(-ctx.Aspect, ctx.Aspect),
                    rng.Range(-1f, 1f) + 2f * s.SpawnOffsetY);
                d.Size = new Vector2(
                    rng.Range(s.SizeMin.x, s.SizeMax.x),
                    rng.Range(s.SizeMin.y, s.SizeMax.y));
                d.Rotation = s.AutoRotate ? rng.Range(0f, 179.9f) * Mathf.Deg2Rad : 0f;
                return;
            }
        }

        public override void Emit(in RainTickContext ctx)
        {
            Mesh.Begin();
            Visible = false;

            if (ctx.Intensity <= 0f || activeCount == 0)
            {
                Mesh.End();
                return;
            }

            for (var i = 0; i < drops.Length; i++)
            {
                ref var d = ref drops[i];
                if (!d.Active)
                {
                    continue;
                }

                var p = d.Lifetime > 0f ? d.Age / d.Lifetime : 1f;
                var a = s.AlphaOverLifetime.Evaluate(p) * ctx.Intensity;
                if (a <= 0f)
                {
                    continue;
                }

                var tint = s.OverlayColor;
                tint.a *= a;

                // Legacy darkness is scaled by alpha, not by its own curve.
                var prm = new Vector4(
                    s.Distortion * s.DistortionOverLifetime.Evaluate(p) * ctx.Intensity,
                    s.Relief * s.ReliefOverLifetime.Evaluate(p) * ctx.Intensity,
                    s.Blur * s.BlurOverLifetime.Evaluate(p) * ctx.Intensity,
                    s.Darkness * ctx.Intensity);

                RainBatchMesh.AddQuad(Mesh, d.Pos, d.Size * s.SizeOverLifetime.Evaluate(p), d.Rotation, a, tint, prm);
                Visible = true;
            }

            Mesh.End();
        }
    }
}
