# Basic Setup

The smallest working setup: one camera, one rain profile.

## 1. Add the renderer feature

The effect draws from a `ScriptableRendererFeature`, so it has to be on the renderer your camera
uses:

1. Select your URP renderer asset (`Assets/Settings/<name>-Renderer.asset`).
2. **Add Renderer Feature → Rain Renderer Feature**.

Without the feature nothing renders and the component logs a warning once.

## 2. Add the component

Add **Rain Effect** to the camera that should show rain. One camera can carry several; each keeps
its own state, so a picture-in-picture camera is unaffected by the main one.

## 3. Assign a profile

Drop a `RainProfile` asset into the component's **Profile** field. Create an empty one with
**Assets → Create → Rain Drop Effect → Rain Profile**, or a working starting point with
**Assets → Create → Rain Drop Effect → Rain Profile (Basic Rain)** — a rain frame that fades in plus
falling droplets.

`Basic.unity` here already uses `BasicRainProfile.asset`, created from that preset.

## 4. Play it

```csharp
GetComponent<RainEffect>().Play();
```

The camera in this scene has **Preview In Edit Mode** on, so the rain animates in the Game view
without entering Play Mode. The inspector also has Play / Stop / Clear / Rebuild buttons.

## What is in the scene

- **Backdrop** — a still life (`StillLife.jpg` on `Backdrop.mat`) filling the wall behind the
  camera's view. Refraction is only legible against something with detail and colour in it; a flat
  or two-tone background hides the effect the sample exists to show.
- **Ground** — a checkered plane, so the lens distortion is easy to read as distortion.
- **Transparent Cube** — transparent geometry behind the drops, so the blur is visible too.

The profile draws its droplets at a spread of sizes and orientations with a short fade-in, which is
what makes rain read as rain rather than as a repeated stamp. If you regenerate the profile from
**Assets → Create → Rain Drop Effect → Rain Profile (Basic Rain)** you get the same values.
