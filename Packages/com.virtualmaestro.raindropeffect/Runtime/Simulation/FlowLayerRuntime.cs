using UnityEngine;

namespace RainDropEffect
{
    /// <summary>
    /// Rain streaks: drops that run down the glass leaving a ribbon behind them, wobbling sideways
    /// as they go.
    ///
    /// Vertical motion is closed form (<c>½·a·t² + v₀·t</c>) rather than integrated, so a trail is
    /// in the same place after one second whether the game ran at 30 or 120 fps. The sideways wobble
    /// is a target re-rolled on an exponential timer and integrated into an offset that PERSISTS
    /// across re-rolls, so successive intervals random-walk the drop sideways — the shape of the
    /// legacy per-frame compounding walk, without its frame-rate dependence.
    /// </summary>
    internal sealed class FlowLayerRuntime : LayerRuntime
    {
        struct Trail
        {
            public bool Active;
            public float Age;
            public float Lifetime;
            public float Accel;
            public float FluctRate;
            public float NextFluctAt;
            public float LateralTarget;

            /// <summary>Seconds since the last re-roll; drives the ramp, not the offset itself.</summary>
            public float LateralT;

            /// <summary>Accumulated sideways offset. Persists across re-rolls — that is the wander.</summary>
            public float Lateral;
            public float WidthMul;
            public Vector2 Start;
            public Vector2 Pos;
            public Vector2 WindOffset;
        }

        readonly FlowLayerSettings s;
        readonly Trail[] trails;
        readonly TrailBuffer buffer;

        EmitterState emitter;
        int activeCount;
        float simTime;

        public FlowLayerRuntime(FlowLayerSettings settings, RainShaderSet shaders, RainQuality quality)
            : base(settings, shaders, quality,
                LayerRuntimeFactory.TrailCapacity(settings.MaxCount, settings.MaxPoints, quality))
        {
            s = settings;
            trails = new Trail[Cap(settings, quality)];
            buffer = new TrailBuffer(trails.Length, Pts(settings, quality));

            LogMeshCapacity("flow", settings.Name, trails.Length, Mesh);
        }

        static int Cap(FlowLayerSettings s, RainQuality q) =>
            LayerRuntimeFactory.TrailCount(s.MaxCount, q);

        static int Pts(FlowLayerSettings s, RainQuality q) =>
            LayerRuntimeFactory.TrailPoints(s.MaxPoints, q);

        internal int ActiveCount => activeCount;

        internal int Capacity => trails.Length;

        internal bool TrailActive(int slot) => trails[slot].Active;

        internal Vector2 TrailPosition(int slot) => trails[slot].Pos;

        internal float TrailLateral(int slot) => trails[slot].Lateral;

        internal int TrailPointCount(int slot) => buffer.Count(slot);

        public override bool IsPlaying =>
            activeCount > 0 || emitter.Current == EmitterState.Phase.Delay || emitter.IsEmitting;

        public override bool IsEmitting => emitter.IsEmitting;

        public override void Play(ref RainRandom rng) => emitter.Play(s, ref rng);

        public override void Stop() => emitter.Stop();

        public override void Clear()
        {
            emitter.Clear();
            for (var i = 0; i < trails.Length; i++)
            {
                trails[i].Active = false;
            }

            buffer.ClearAll();
            activeCount = 0;
            simTime = 0f;
            Visible = false;
        }

        public override void Tick(in RainTickContext ctx, ref RainRandom rng)
        {
            simTime += ctx.Dt;

            var spawn = emitter.Tick(ctx.Dt, trails.Length - activeCount, s, ref rng);
            for (var k = 0; k < spawn; k++)
            {
                Spawn(ctx, ref rng);
            }

            var down = ctx.Down;
            var side = new Vector2(-down.y, down.x);
            var spacing = Mathf.Max(0.001f, s.PointSpacing);

            for (var i = 0; i < trails.Length; i++)
            {
                ref var t = ref trails[i];
                if (!t.Active)
                {
                    continue;
                }

                t.Age += ctx.Dt;
                var p = t.Lifetime > 0f ? t.Age / t.Lifetime : 1f;
                if (p >= 1f)
                {
                    t.Active = false;
                    activeCount--;
                    buffer.Clear(i);
                    continue;
                }

                if (t.Age >= t.NextFluctAt)
                {
                    t.LateralTarget = rng.Range(-s.LateralAmplitude, s.LateralAmplitude);
                    t.LateralT = 0f;
                    t.NextFluctAt = t.Age + NextFluctDelay(t.FluctRate, ref rng);
                }

                t.LateralT += ctx.Dt;
                t.Lateral += LateralVelocity(s.LateralSmooth, t.LateralT, t.LateralTarget) * ctx.Dt;

                var fall = 0.5f * t.Accel * t.Age * t.Age + s.InitialVelocity * t.Age;
                t.WindOffset += ctx.Wind * (p * ctx.Dt);
                t.Pos = t.Start + down * fall + side * t.Lateral + t.WindOffset;

                buffer.Push(i, t.Pos, simTime, spacing);
                buffer.Expire(i, simTime, t.Lifetime);
            }

            if (emitter.Current == EmitterState.Phase.Draining && activeCount == 0)
            {
                emitter.Clear();
            }
        }

        /// <summary>
        /// Sideways speed, in normalized units per second, <paramref name="elapsed"/> seconds after
        /// the current target was rolled.
        ///
        /// Legacy accumulated <c>posXDt += 0.01 * Smooth * dt</c> and then did
        /// <c>Slerp(pos, pos + right * rnd1, posXDt)</c> — a lerp from the CURRENT position, so each
        /// frame added <c>posXDt * rnd1</c> to wherever the drop already was and the offset
        /// COMPOUNDED. Integrating that over one re-roll interval tau gives a drift of
        /// <c>0.01 * Smooth * rnd1 * tau² / (2 * dt)</c>: quadratic in elapsed time, and carrying a
        /// <c>1 / dt</c> that makes the legacy form frame-rate dependent.
        ///
        /// Differentiating that drift gives the per-second speed
        /// <c>0.01 / dt * Smooth * rnd1 * elapsed</c>. Pinning <c>dt</c> to the 60 fps reference every
        /// other constant carried over from the Built-in original was converted against turns
        /// <c>0.01 / dt</c> into the constant 0.6, and the result no longer depends on the frame rate.
        ///
        /// <c>Mathf.Min(1f, ...)</c> reproduces Slerp's own clamp on <c>posXDt</c>. At the shipped
        /// Smooth of 5 it bites only after 20 s in a single interval, so it is a bound, not a shape.
        ///
        /// The 2.0.0 port instead treated the eased value as an ABSOLUTE offset from the spawn point
        /// and reset it on every re-roll, which bounded the wobble at about 0.001 and pulled it back
        /// toward the spawn column — a straight vertical line rather than a wander.
        /// </summary>
        internal static float LateralVelocity(float smooth, float elapsed, float target)
        {
            const float referenceRate = 60f;
            var ramp = Mathf.Min(1f, 0.01f * smooth * elapsed);
            return ramp * referenceRate * target;
        }

        /// <summary>
        /// Time until the next sideways re-roll, exponentially distributed with mean 1/rate. The
        /// legacy version rolled a chance every 0.01 s, which is the same distribution sampled by a
        /// frame-rate-dependent method.
        /// </summary>
        internal static float NextFluctDelay(float rate, ref RainRandom rng)
        {
            if (rate <= 0f)
            {
                return float.PositiveInfinity;
            }

            var u = Mathf.Clamp(rng.NextFloat(), 1e-6f, 1f - 1e-6f);
            return Mathf.Max(0.01f, -Mathf.Log(1f - u) / rate);
        }

        void Spawn(in RainTickContext ctx, ref RainRandom rng)
        {
            for (var i = 0; i < trails.Length; i++)
            {
                ref var t = ref trails[i];
                if (t.Active)
                {
                    continue;
                }

                t.Active = true;
                activeCount++;
                t.Age = 0f;
                t.Lifetime = rng.Range(s.LifetimeRange.x, s.LifetimeRange.y);
                t.Accel = rng.Range(s.AccelerationRange.x, s.AccelerationRange.y);
                t.FluctRate = rng.Range(s.FluctuationRateRange.x, s.FluctuationRateRange.y);
                t.NextFluctAt = 0.01f;
                t.LateralTarget = 0f;
                t.LateralT = 0f;
                t.Lateral = 0f;
                t.WindOffset = Vector2.zero;
                t.WidthMul = rng.Range(s.WidthRange.x, s.WidthRange.y);
                t.Start = new Vector2(
                    rng.Range(-ctx.Aspect, ctx.Aspect),
                    rng.Range(-1f, 1f) + 2f * s.SpawnOffsetY);
                t.Pos = t.Start;

                buffer.Clear(i);
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

            for (var i = 0; i < trails.Length; i++)
            {
                ref var t = ref trails[i];
                if (!t.Active)
                {
                    continue;
                }

                var p = t.Lifetime > 0f ? t.Age / t.Lifetime : 1f;
                var a = s.AlphaOverLifetime.Evaluate(p) * ctx.Intensity;
                if (a <= 0f)
                {
                    continue;
                }

                var tint = s.OverlayColor;
                tint.a *= a;

                var prm = new Vector4(
                    s.Distortion * s.DistortionOverLifetime.Evaluate(p) * ctx.Intensity,
                    s.Relief * s.ReliefOverLifetime.Evaluate(p) * ctx.Intensity,
                    s.Blur * s.BlurOverLifetime.Evaluate(p) * ctx.Intensity,
                    s.Darkness);

                if (buffer.EmitRibbon(i, Mesh, t.WidthMul, s.TrailWidth, a, tint, prm))
                {
                    Visible = true;
                }
            }

            Mesh.End();
        }
    }
}
