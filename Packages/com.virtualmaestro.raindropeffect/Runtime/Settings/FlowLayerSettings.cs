using System;
using UnityEngine;

namespace RainDropEffect
{
    /// <summary>Drops that run down the glass leaving a ribbon trail, wobbling sideways as they go.</summary>
    [Serializable]
    public sealed class FlowLayerSettings : EmitterLayerSettings
    {
        public Vector2 WidthRange = new Vector2(0.15f, 0.15f);

        /// <summary>
        /// Ribbon width along the trail: evaluated at 0 for the head and 1 for the tail. The default
        /// tapers so a freshly created layer reads as a drop dragging a trail rather than as a
        /// uniform rectangle; the shipped profiles author their own 0 -> 1 -> 0 lens shape.
        /// </summary>
        public AnimationCurve TrailWidth = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        /// <summary>Distance between trail points in normalized screen units.</summary>
        [Range(0.004f, 0.2f)] public float PointSpacing = 0.01f;

        [Range(4, 96)] public int MaxPoints = 64;

        public float LateralAmplitude = 0.1f;

        [Range(0, 10)] public float LateralSmooth = 5f;

        public Vector2 FluctuationRateRange = new Vector2(5f, 5f);

        public float InitialVelocity = 0f;

        public Vector2 AccelerationRange = new Vector2(0.0125f, 0.0417f);

        public override int FamilyOrder => 2;
    }
}
