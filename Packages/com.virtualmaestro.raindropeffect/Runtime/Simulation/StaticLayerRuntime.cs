using UnityEngine;

namespace RainDropEffect
{
    /// <summary>
    /// A layer that does not emit: one quad that fades in while playing and back out after
    /// <see cref="Stop"/>. Rain frame, frozen fill and frame, blood frame.
    ///
    /// The fade position is the whole state, so <see cref="Stop"/> reverses it and
    /// <see cref="Clear"/> drops it — a stopped layer keeps drawing until the fade reaches zero.
    /// </summary>
    internal sealed class StaticLayerRuntime : LayerRuntime
    {
        readonly StaticLayerSettings s;

        float fadeElapsed;
        bool playing;

        public StaticLayerRuntime(StaticLayerSettings settings, RainShaderSet shaders, RainQuality quality)
            : base(settings, shaders, quality, LayerRuntimeFactory.StaticCapacity())
        {
            s = settings;
        }

        public override bool IsPlaying => playing || fadeElapsed > 0f;

        public override bool IsEmitting => playing;

        public override void Play(ref RainRandom rng) => playing = true;

        public override void Stop() => playing = false;

        public override void Clear()
        {
            playing = false;
            fadeElapsed = 0f;
            Visible = false;
        }

        public override void Tick(in RainTickContext ctx, ref RainRandom rng)
        {
            var fadeTime = Mathf.Max(0f, s.FadeTime);
            fadeElapsed = playing
                ? Mathf.Min(fadeTime, fadeElapsed + ctx.Dt)
                : Mathf.Max(0f, fadeElapsed - ctx.Dt);
        }

        public override void Emit(in RainTickContext ctx)
        {
            Mesh.Begin();
            Visible = false;

            var fadeTime = Mathf.Max(0f, s.FadeTime);

            // FadeTime 0 is instant on/off: there is no progress to interpolate.
            var progress = fadeTime > 0f ? fadeElapsed / fadeTime : (playing ? 1f : 0f);
            var f = s.FadeCurve.Evaluate(progress) * ctx.Intensity;

            if (f <= 0f || (!playing && fadeElapsed <= 0f))
            {
                Mesh.End();
                return;
            }

            // Offset and Size are fractions of the view; x is widened by the aspect so a square
            // stays square.
            var center = s.FullScreen
                ? Vector2.zero
                : new Vector2(-2f * s.Offset.x * ctx.Aspect, -2f * s.Offset.y);
            var half = s.FullScreen ? new Vector2(ctx.Aspect, 1f) : s.Size;

            var tint = s.OverlayColor;
            tint.a *= f;

            // Legacy quirk: Darkness is not scaled by the fade curve, only by the tint alpha in
            // the shader.
            RainBatchMesh.AddQuad(Mesh, center, half, 0f, f, tint,
                new Vector4(s.Distortion * f, s.Relief * f, s.Blur * f, s.Darkness));

            Visible = true;
            Mesh.End();
        }
    }
}
