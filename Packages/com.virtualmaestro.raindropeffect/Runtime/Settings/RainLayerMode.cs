namespace RainDropEffect
{
    /// <summary>
    /// How a layer draws. <see cref="Lens"/> refracts the camera colour behind the drop;
    /// <see cref="Overlay"/> blends the overlay texture on top without sampling the scene.
    /// </summary>
    public enum RainLayerMode
    {
        Lens = 0,
        Overlay = 1
    }
}
