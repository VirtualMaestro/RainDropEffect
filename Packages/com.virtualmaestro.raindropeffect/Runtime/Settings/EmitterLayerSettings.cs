using System;
using UnityEngine;

namespace RainDropEffect
{
    /// <summary>
    /// Everything an emitting family needs regardless of what it emits: how many, for how long, and
    /// how the four shader inputs evolve over a drop's life.
    /// </summary>
    [Serializable]
    public abstract class EmitterLayerSettings : RainLayerSettings
    {
        public bool PlayOnce = false;
        public float Duration = 1f;
        public float Delay = 0f;

        [Range(1, 256)] public int MaxCount = 30;

        public Vector2 LifetimeRange = new Vector2(0.6f, 1.4f);
        public Vector2Int EmissionRateRange = new Vector2Int(2, 5);

        [Range(-2, 2)] public float SpawnOffsetY = 0f;

        public AnimationCurve AlphaOverLifetime = AnimationCurve.Constant(0, 1, 1);
        public AnimationCurve DistortionOverLifetime = AnimationCurve.Constant(0, 1, 1);
        public AnimationCurve ReliefOverLifetime = AnimationCurve.Constant(0, 1, 1);
        public AnimationCurve BlurOverLifetime = AnimationCurve.Constant(0, 1, 1);
    }
}
