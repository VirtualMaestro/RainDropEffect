using System;
using UnityEngine;

namespace RainDropEffect
{
    /// <summary>Drops that appear, drift a little and fade. No trail.</summary>
    [Serializable]
    public sealed class SimpleLayerSettings : EmitterLayerSettings
    {
        public bool AutoRotate;

        public Vector2 SizeMin = new Vector2(0.15f, 0.15f);
        public Vector2 SizeMax = new Vector2(0.15f, 0.15f);

        public AnimationCurve SizeOverLifetime = AnimationCurve.Constant(0, 1, 1);
        public AnimationCurve DriftOverLifetime = AnimationCurve.Constant(0, 1, 0);

        public float DriftSpeed = 0.1252f;

        public override int FamilyOrder => 1;
    }
}
