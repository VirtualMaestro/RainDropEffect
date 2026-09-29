using UnityEngine;

namespace RainDropEffect
{
    /// <summary>
    /// Trails that steer by a baked friction field instead of wobbling randomly, so drops follow the
    /// same channels down the glass every run.
    ///
    /// Each tick the drop walks a few short steps along gravity; at every step it looks sideways
    /// across a handful of candidates and moves to the most attractive one. That is the legacy
    /// argmax scan, with its dictionary/LINQ candidate selection replaced by a direct loop and its
    /// shuffle-on-ties replaced by a reservoir pick over all maxima (matrix tolerance (c)). Ties are
    /// taken, not refused: a flat region of the field is where legacy's random pick produced the
    /// meander, and the fan reaches as far sideways per frame as the drop falls, which is what makes
    /// the field legible to it at all.
    ///
    /// With no field assigned every score is zero, so the drop falls straight — still deterministic.
    /// </summary>
    internal sealed class FrictionLayerRuntime : LayerRuntime
    {
        struct Trail
        {
            public bool Active;
            public float Age;
            public float Lifetime;
            public float Accel;
            public float WidthMul;
            public Vector2 Pos;
        }

        readonly FrictionLayerSettings s;
        readonly Trail[] trails;
        readonly TrailBuffer buffer;

        EmitterState emitter;
        int activeCount;
        float simTime;

        public FrictionLayerRuntime(FrictionLayerSettings settings, RainShaderSet shaders, RainQuality quality)
            : base(settings, shaders, quality,
                LayerRuntimeFactory.TrailCapacity(settings.MaxCount, settings.MaxPoints, quality))
        {
            s = settings;
            trails = new Trail[Cap(settings, quality)];
            buffer = new TrailBuffer(trails.Length, Pts(settings, quality));

            if (settings.Field == null)
            {
                RainLog.WarnOnce("nofield:" + settings.Name,
                    $"friction layer '{settings.Name}' has no FrictionField; trails fall straight");
            }

            LogFanSpan(settings);
            LogMeshCapacity("friction", settings.Name, trails.Length, Mesh);
        }

        /// <summary>
        /// Reports the sideways reach of the scan against the field's cell size, so a fan too narrow
        /// to see the field is diagnosable from a log rather than a debugger — that is exactly the
        /// state the 2.0.0 port shipped in. Construction only; never per frame (decision D17).
        ///
        /// The real half-span is <c>speed * dt * LateralStepFactor</c> and therefore varies with the
        /// trail's age and the frame time, so this reports one reference point: the middle of the
        /// mean lifetime, the middle of the acceleration range, 60 fps and a 16:9 view.
        /// </summary>
        [System.Diagnostics.Conditional("RAINDROP_VERBOSE_LOG")]
        static void LogFanSpan(FrictionLayerSettings settings)
        {
            const float dt = 1f / 60f;
            const float aspect = 16f / 9f;

            var age = 0.5f * 0.5f * (settings.LifetimeRange.x + settings.LifetimeRange.y);
            var accel = 0.5f * (settings.AccelerationRange.x + settings.AccelerationRange.y);
            var speed = accel * age * age + settings.InitialVelocity * age;
            var half = speed * dt * settings.LateralStepFactor;

            var field = settings.Field;
            if (field == null || field.Width <= 0)
            {
                RainLog.Verbose(
                    $"friction layer '{settings.Name}': fan half-span {half:F5} at 60 fps, no field to compare against");
                return;
            }

            var texel = 2f * aspect / field.Width;
            RainLog.Verbose(
                $"friction layer '{settings.Name}': fan half-span {half:F5} at 60 fps, " +
                $"field texel {texel:F5} ({half / texel:F2} texels) — below ~1 texel the scan cannot see the field");
        }

        static int Cap(FrictionLayerSettings s, RainQuality q) =>
            LayerRuntimeFactory.TrailCount(s.MaxCount, q);

        static int Pts(FrictionLayerSettings s, RainQuality q) =>
            LayerRuntimeFactory.TrailPoints(s.MaxPoints, q);

        internal int ActiveCount => activeCount;

        internal int Capacity => trails.Length;

        internal bool TrailActive(int slot) => trails[slot].Active;

        internal Vector2 TrailPosition(int slot) => trails[slot].Pos;

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

                // Legacy advanced by the whole kinematic displacement every frame, which at 60 Hz is
                // a speed of Accel*t^2 + InitialVelocity*t; the settings already carry the converted
                // constants (see the friction row of the migration matrix).
                var speed = t.Accel * t.Age * t.Age + s.InitialVelocity * t.Age;
                var step = speed * ctx.Dt;
                if (step > 0f)
                {
                    t.Pos = FrictionStep(t.Pos, step, ctx, ref rng);
                }

                t.Pos += ctx.Wind * (p * ctx.Dt);

                buffer.Push(i, t.Pos, simTime, spacing);
                buffer.Expire(i, simTime, t.Lifetime);
            }

            if (emitter.Current == EmitterState.Phase.Draining && activeCount == 0)
            {
                emitter.Clear();
            }
        }

        /// <summary>
        /// Walks <paramref name="distance"/> down the field in a few short steps, choosing the most
        /// attractive sideways offset at each one.
        /// </summary>
        Vector2 FrictionStep(Vector2 pos, float distance, in RainTickContext ctx, ref RainRandom rng)
        {
            var iterations = IterFor(s.ScanRate, ctx.Dt);
            var lateral = Mathf.Clamp(s.LateralSamples, 2, 8);

            var down = ctx.Down;
            var side = new Vector2(-down.y, down.x);

            // Legacy tied the sideways reach to the whole downward advance, not to one scan step:
            // GetNextPositionWithFriction offset its candidates by left*dv .. right*dv where dv was
            // the frame's fall distance, i.e. half-span == downward advance. Deriving the fan from
            // stepLength instead shrank it by the iteration count on top of the frame time, which
            // put the entire candidate fan inside a third of one field texel — every candidate then
            // scored the same and the drop fell on a plumb line.
            var stepLength = distance / iterations;
            var half = distance * s.LateralStepFactor;
            var lateralStep = 2f * half / lateral;

            var current = pos;
            for (var k = 0; k < iterations; k++)
            {
                current += down * stepLength;

                var best = float.NegativeInfinity;
                var bestCount = 0;
                var bestPos = current;

                for (var j = 0; j <= lateral; j++)
                {
                    var offset = j * lateralStep - half;
                    var candidate = current + side * offset;

                    // Normalized units to viewport: x spans [-aspect, aspect], y spans [-1, 1].
                    var u = candidate.x / ctx.Aspect * 0.5f + 0.5f;
                    var v = candidate.y * 0.5f + 0.5f;
                    var score = s.Field != null ? s.Field.Sample(u, v) : 0f;

                    if (score > best + 1e-6f)
                    {
                        best = score;
                        bestPos = candidate;
                        bestCount = 1;
                    }
                    else if (Mathf.Abs(score - best) <= 1e-6f)
                    {
                        // Reservoir pick: uniform among all maxima, and reproducible under a seed.
                        bestCount++;
                        if (rng.Range(0, bestCount) == 0)
                        {
                            bestPos = candidate;
                        }
                    }
                }

                // Every candidate scored the same. With a field assigned that is a flat region of it,
                // and legacy PickRandomWeightedElement answered a flat region with a random lateral
                // pick (Shuffle(kvList); return kvList[0]) — that random walk over the field IS the
                // meander. bestPos already holds a uniform reservoir pick over the tied set, so the
                // fair pick costs nothing extra and stays reproducible under a seed.
                //
                // With no field there is nothing to be flat about, so carry straight on rather than
                // drifting sideways for no visible reason.
                if (bestCount < lateral + 1 || s.Field != null)
                {
                    current = bestPos;
                }
            }

            return current;
        }

        /// <summary>
        /// How many scan steps one tick takes. Legacy clamped this to 2..5 iterations per frame;
        /// ScanRate expresses the same thing per second so it survives a frame-rate change.
        /// </summary>
        internal static int IterFor(float scanRate, float dt)
        {
            return Mathf.Clamp(Mathf.RoundToInt(scanRate * dt), 2, 5);
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
                t.WidthMul = rng.Range(s.WidthRange.x, s.WidthRange.y);
                t.Pos = new Vector2(
                    rng.Range(-ctx.Aspect, ctx.Aspect),
                    rng.Range(-1f, 1f) + 2f * s.SpawnOffsetY);

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
