using RainDropEffect;
using UnityEngine;

/// <summary>
/// Host-only demo driver: one camera, five <see cref="RainEffect"/> components, an IMGUI panel.
///
/// The gallery sample in <c>Samples~</c> is invisible to the AssetDatabase until Package Manager
/// copies it, so it cannot be opened from this repository. This scene is the version that can:
/// it lives under <c>Assets/</c>, opens straight from the Project window, and covers the three
/// things the gallery cannot show at a glance — rain presets, frost and blood.
///
/// Blood and frost profiles all ship with <c>AutoStart = 0</c>, so nothing appears until a button
/// calls <see cref="RainEffect.Play"/>. That is deliberate: they are event effects, not weather.
///
/// Never referenced by package code.
/// </summary>
public sealed class DemoSample : MonoBehaviour
{
    /// <summary>Weather layer: the profile swapped by the preset buttons.</summary>
    public RainEffect Rain;

    /// <summary>Frost overlay, driven by the slider through <see cref="RainEffect.Intensity"/>.</summary>
    public RainEffect Frost;

    /// <summary>Blood frame: the static vignette that stays while HP is low.</summary>
    public RainEffect BloodFrame;

    /// <summary>Blood flow: trails that run down after a hit. One burst per Play.</summary>
    public RainEffect BloodFlow;

    /// <summary>Blood splatter: the droplets of the hit itself. One burst per Play.</summary>
    public RainEffect BloodSplatter;

    /// <summary>Rain presets offered as buttons, in order.</summary>
    public RainProfile[] Presets = new RainProfile[0];

    int selected = -1;
    float frost;

    /// <summary>
    /// Accumulated damage, 0..1, driving the blood frame's <see cref="RainEffect.Intensity"/>.
    ///
    /// It does not decay on its own. An earlier version faded it out over six seconds, which made
    /// the Heal button meaningless: press it after the fade and nothing happens, press it during
    /// the fade and it is indistinguishable from the fade finishing. Blood is a state here, the way
    /// 1.x drove it from HP — Hit raises it, Heal is the only thing that lowers it.
    /// </summary>
    float blood;

    void Awake()
    {
        if (Rain == null || Frost == null || BloodFrame == null || BloodFlow == null || BloodSplatter == null)
        {
            Debug.LogWarning($"{nameof(DemoSample)} on '{name}' is missing an effect; disabling. " +
                             "Regenerate the scene with Rain Drop Effect > Demo > Create Demo Scene.", this);
            enabled = false;
        }
    }

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(12f, 12f, 220f, Screen.height - 24f), GUI.skin.box);

        GUILayout.Label("Rain presets");
        for (var i = 0; i < Presets.Length; i++)
        {
            if (Presets[i] == null)
            {
                continue;
            }

            var label = i == selected ? "> " + Presets[i].name : Presets[i].name;
            if (GUILayout.Button(label, GUILayout.Height(24f)))
            {
                SelectPreset(i);
            }
        }

        if (GUILayout.Button("No rain", GUILayout.Height(24f)))
        {
            selected = -1;
            Rain.Stop();
            Rain.Clear();
        }

        GUILayout.Space(8f);
        GUILayout.Label($"Frost  {frost:0.00}");

        var next = GUILayout.HorizontalSlider(frost, 0f, 1f);
        if (!Mathf.Approximately(next, frost))
        {
            SetFrost(next);
        }

        GUILayout.Space(8f);
        GUILayout.Label($"Blood  {blood:0.00}");

        if (GUILayout.Button("Hit  (+0.45)", GUILayout.Height(28f)))
        {
            Hit();
        }

        // Greyed out at zero, so the button visibly has nothing to undo rather than looking broken.
        GUI.enabled = blood > 0f;
        if (GUILayout.Button("Heal  (clear blood)", GUILayout.Height(24f)))
        {
            Heal();
        }

        GUI.enabled = true;

        GUILayout.Space(8f);
        GUILayout.Label("Quality");

        if (GUILayout.Button("Low", GUILayout.Height(22f))) SetQuality(RainQuality.Low);
        if (GUILayout.Button("Balanced", GUILayout.Height(22f))) SetQuality(RainQuality.Balanced);
        if (GUILayout.Button("High", GUILayout.Height(22f))) SetQuality(RainQuality.High);

        GUILayout.EndArea();
    }

    void SelectPreset(int index)
    {
        selected = index;
        Rain.Profile = Presets[index];
        Rain.Rebuild();
        Rain.Play();
    }

    void SetFrost(float value)
    {
        frost = value;
        Frost.Intensity = value;

        // Intensity only scales what is drawn; the simulation still has to be running for the
        // static frost layers to exist at all.
        if (value > 0f && !Frost.IsPlaying)
        {
            Frost.Play();
        }
        else if (value <= 0f)
        {
            Frost.Stop();
        }
    }

    /// <summary>
    /// One hit: the splatter and the flow are one-shot profiles, so each needs a Clear before the
    /// Play or the second hit finds the emitter already drained.
    /// </summary>
    void Hit()
    {
        blood = Mathf.Min(1f, blood + 0.45f);

        BloodFrame.Intensity = blood;
        if (!BloodFrame.IsPlaying)
        {
            BloodFrame.Play();
        }

        BloodSplatter.Clear();
        BloodSplatter.Play();

        BloodFlow.Clear();
        BloodFlow.Play();
    }

    /// <summary>
    /// The inverse of <see cref="Hit"/>: drops the accumulated damage and takes the red frame off
    /// screen at once. <c>Stop</c> alone would reverse the static layer's 0.82 s fade instead, which
    /// reads as the effect ending by itself; <c>Clear</c> is what makes the button legible.
    /// </summary>
    void Heal()
    {
        blood = 0f;
        BloodFrame.Intensity = 0f;
        BloodFrame.Clear();
        BloodFlow.Clear();
        BloodSplatter.Clear();
    }

    void SetQuality(RainQuality quality)
    {
        foreach (var effect in new[] { Rain, Frost, BloodFrame, BloodFlow, BloodSplatter })
        {
            if (effect == null)
            {
                continue;
            }

            var wasPlaying = effect.IsPlaying;
            effect.Quality = quality;
            effect.Rebuild();

            if (wasPlaying)
            {
                effect.Play();
            }
        }
    }
}
