using System;
using UnityEngine;

namespace RainDropEffect
{
    /// <summary>
    /// A layer that does not emit: one quad that fades in and out. Frozen fill, blood frame and the
    /// full-screen wash all use it.
    /// </summary>
    [Serializable]
    public sealed class StaticLayerSettings : RainLayerSettings
    {
        public bool FullScreen = true;

        /// <summary>Fractions of the screen, not pixels (see the migration matrix unit column).</summary>
        public Vector2 Size = new Vector2(0.5f, 0.5f);

        public Vector2 Offset = Vector2.zero;

        [Range(0, 15)] public float FadeTime = 2f;

        public AnimationCurve FadeCurve = AnimationCurve.Linear(0, 0, 1, 1);

        public override int FamilyOrder => 0;
    }
}
