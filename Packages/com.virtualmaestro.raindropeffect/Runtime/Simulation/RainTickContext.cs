using UnityEngine;

namespace RainDropEffect
{
    /// <summary>
    /// Everything a layer needs to advance one frame. Passed by <c>in</c> so a tick cannot change
    /// what the next layer sees; the RNG travels separately by <c>ref</c> because drawing from it
    /// does change state.
    /// </summary>
    public struct RainTickContext
    {
        public float Dt;

        /// <summary>Gravity projected into screen space and normalized; (0,-1) when it degenerates.</summary>
        public Vector2 Down;

        public Vector2 Wind;
        public float Intensity;

        /// <summary>Width over height of the camera's target, so drops stay round.</summary>
        public float Aspect;

        public RainQuality Quality;
    }
}
