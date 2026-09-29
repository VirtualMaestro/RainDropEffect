using System;
using UnityEngine;

namespace RainDropEffect
{
    /// <summary>
    /// Trails that steer around a baked friction field instead of wobbling randomly, so drops
    /// follow the same paths every run.
    ///
    /// Fall speed is <c>Acceleration * t^2 + InitialVelocity * t</c> in normalized units per second,
    /// with <c>t</c> the trail's age. The default acceleration range is the legacy
    /// <c>0.06/0.2 * 3 * k</c> from the friction row of the migration matrix.
    /// </summary>
    [Serializable]
    public sealed class FrictionLayerSettings : EmitterLayerSettings
    {
        public Vector2 WidthRange = new Vector2(0.15f, 0.15f);

        /// <summary>
        /// Ribbon width along the trail: evaluated at 0 for the head and 1 for the tail. The default
        /// tapers so a freshly created layer reads as a drop dragging a trail rather than as a
        /// uniform rectangle; the shipped profiles author their own 0 -> 1 -> 0 lens shape.
        /// </summary>
        public AnimationCurve TrailWidth = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        [Range(0.004f, 0.2f)] public float PointSpacing = 0.01f;

        [Range(4, 96)] public int MaxPoints = 64;

        /// <summary>Baked in Task 21; until then a drop falls straight.</summary>
        public FrictionField Field;

        public float InitialVelocity = 0f;

        public Vector2 AccelerationRange = new Vector2(0.0376f, 0.125f);

        /// <summary>How often the field is sampled ahead of the drop, in samples per second.</summary>
        [Range(30, 600)] public float ScanRate = 150f;

        [Range(2, 8)] public int LateralSamples = 5;

        /// <summary>
        /// Sideways reach of the scan as a multiple of the per-frame downward step. Legacy's fan
        /// spanned exactly ±(one frame's fall), so 1 is parity; lower narrows the meander and 0
        /// makes the trail a plumb line.
        /// </summary>
        public float LateralStepFactor = 1f;

        public override int FamilyOrder => 3;
    }
}
