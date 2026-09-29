using UnityEngine;

namespace RainDropEffect
{
    /// <summary>
    /// The emission clock shared by every emitting family (Simple here, Flow and Friction in
    /// Phase 5). It answers one question per tick: how many drops to spawn.
    ///
    /// The arithmetic reproduces the legacy <c>CheckSpawnTime</c>, catch-up spawning and the
    /// exclusive-max integer rate roll included, so a converted profile emits at the same rate.
    /// </summary>
    internal struct EmitterState
    {
        public enum Phase
        {
            Idle,
            Delay,
            Emitting,

            /// <summary>Emission stopped; what is on screen finishes. The layer clears it when empty.</summary>
            Draining
        }

        public Phase Current;

        float delayLeft;
        float oneShotLeft;
        float elapsed;
        float interval;

        /// <summary>This cycle rolled a rate of zero (or the duration is zero): emit nothing, no division.</summary>
        bool noSpawnCycle;

        public bool IsEmitting => Current == Phase.Emitting;

        /// <summary>
        /// Starts emission, or does nothing when already delayed or emitting — the legacy re-entry
        /// guard, so a second Play() does not queue a second delay.
        /// </summary>
        public void Play(EmitterLayerSettings s, ref RainRandom rng)
        {
            if (Current == Phase.Delay || Current == Phase.Emitting)
            {
                return;
            }

            if (s.Delay > 0f)
            {
                Current = Phase.Delay;
                delayLeft = s.Delay;
                return;
            }

            BeginEmitting(s, ref rng);
        }

        public void Stop()
        {
            if (Current != Phase.Idle)
            {
                Current = Phase.Draining;
            }
        }

        public void Clear()
        {
            Current = Phase.Idle;
            delayLeft = 0f;
            oneShotLeft = 0f;
            elapsed = 0f;
            interval = 0f;
            noSpawnCycle = false;
        }

        /// <summary>
        /// Advances the clock and returns how many drops to spawn this tick, 0 when not emitting.
        /// <paramref name="freeSlots"/> caps catch-up spawning after a long frame.
        /// </summary>
        public int Tick(float dt, int freeSlots, EmitterLayerSettings s, ref RainRandom rng)
        {
            switch (Current)
            {
                case Phase.Delay:
                    delayLeft -= dt;
                    if (delayLeft > 0f)
                    {
                        return 0;
                    }

                    BeginEmitting(s, ref rng);
                    return 0;

                case Phase.Emitting:
                    if (s.PlayOnce)
                    {
                        oneShotLeft -= dt;
                        if (oneShotLeft <= 0f)
                        {
                            Current = Phase.Draining;
                            return 0;
                        }
                    }

                    if (noSpawnCycle)
                    {
                        return 0;
                    }

                    elapsed += dt;
                    if (elapsed < interval)
                    {
                        return 0;
                    }

                    var n = Mathf.Min((int)(elapsed / interval), freeSlots);
                    NextInterval(s, ref rng);
                    elapsed = 0f;
                    return Mathf.Max(0, n);

                default:
                    return 0;
            }
        }

        void BeginEmitting(EmitterLayerSettings s, ref RainRandom rng)
        {
            Current = Phase.Emitting;
            elapsed = 0f;
            oneShotLeft = s.PlayOnce ? Mathf.Max(0f, s.Duration) : float.PositiveInfinity;
            NextInterval(s, ref rng);
        }

        void NextInterval(EmitterLayerSettings s, ref RainRandom rng)
        {
            var rate = rng.Range(s.EmissionRateRange.x, s.EmissionRateRange.y);
            noSpawnCycle = rate <= 0 || s.Duration <= 0f;
            interval = noSpawnCycle ? 0f : s.Duration / rate;
        }
    }
}
