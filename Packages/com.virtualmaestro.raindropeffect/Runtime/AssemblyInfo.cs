using System.Runtime.CompilerServices;

// The inspector reports layer counts and drives playback, both of which are internal state.
[assembly: InternalsVisibleTo("RainDropEffect.Editor")]

[assembly: InternalsVisibleTo("RainDropEffect.Tests.Editor")]
[assembly: InternalsVisibleTo("RainDropEffect.Tests.Runtime")]
