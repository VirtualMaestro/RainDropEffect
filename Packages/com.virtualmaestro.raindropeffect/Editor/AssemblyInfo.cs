using System.Runtime.CompilerServices;

// The editor tests drive the preset creation and the renderer-feature helpers directly; neither is
// part of the package's public surface.
[assembly: InternalsVisibleTo("RainDropEffect.Tests.Editor")]
