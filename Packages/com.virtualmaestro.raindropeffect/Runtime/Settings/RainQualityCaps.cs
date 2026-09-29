namespace RainDropEffect
{
    /// <summary>
    /// The only place a quality tier turns into a number.
    ///
    /// Caps are applied when a simulation allocates, never per frame, so a profile authored at High
    /// still runs at Low without touching its serialized data.
    /// </summary>
    public static class RainQualityCaps
    {
        public static int MaxDrops(RainQuality q) => q == RainQuality.Low ? 48 : q == RainQuality.Balanced ? 128 : 256;

        public static int MaxTrails(RainQuality q) => q == RainQuality.Low ? 8 : q == RainQuality.Balanced ? 16 : 32;

        public static int MaxPoints(RainQuality q) => q == RainQuality.Low ? 32 : q == RainQuality.Balanced ? 64 : 96;

        public static bool BlurEnabled(RainQuality q) => q != RainQuality.Low;

        /// <summary>
        /// Blur radius in source pixels per tap. Widened from 2/3 to 4/6 by finding F1: the original
        /// kernel reached about a tenth as far as the 1.x blur it replaces. The tap count is fixed at
        /// nine, so a wider radius costs no extra samples — only reach.
        /// </summary>
        public static float BlurRadius(RainQuality q) => q == RainQuality.High ? 6f : 4f;
    }
}
