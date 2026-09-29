namespace RainDropEffect
{
    /// <summary>
    /// Quality tier of a profile. Caps live in <see cref="RainQualityCaps"/>, never in the settings
    /// data, so lowering the tier on a shipped profile costs nothing but a smaller cap.
    /// </summary>
    public enum RainQuality
    {
        Low = 0,
        Balanced = 1,
        High = 2
    }
}
