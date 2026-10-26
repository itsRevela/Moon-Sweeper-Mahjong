using System;

namespace MoonMahjong.Table.Presentation;

/// <summary>
/// The Reset table sounds (requested: everyone at the table hears a reset coming, not too loud), made in code as mono
/// samples: a rising charge over the hold, a falling blip when someone lets go, and a chime when the reset goes through.
/// Each fades in and out, so none clicks.
/// </summary>
public static class ResetSounds
{
    public const int SampleRate = 44100;

    /// <summary>How long Reset table is held before the stuck game ends.</summary>
    public const float HoldSeconds = 5f;

    /// <summary>The loudest any sample gets (full scale is 1).</summary>
    public const float Peak = 0.3f;

    /// <summary>A hum whose pitch climbs two octaves over the hold, with a shimmer that quickens and swells.</summary>
    public static float[] Charge()
    {
        const float from = 150f, to = 600f;
        var samples = new float[(int)(HoldSeconds * SampleRate)];
        var ratio = to / from;
        var log = Math.Log(ratio);
        for (var i = 0; i < samples.Length; i++)
        {
            var t = (double)i / SampleRate;
            var progress = t / HoldSeconds;
            // Exponential sweep: the phase is the integral of from * ratio^(t / HoldSeconds).
            var phase = 2 * Math.PI * from * HoldSeconds / log * (Math.Pow(ratio, progress) - 1);
            var tone = 0.62 * Math.Sin(phase) + 0.25 * Math.Sin(2 * phase) + 0.13 * Math.Sin(3 * phase);
            // The shimmer speeds from 3 to 14 beats a second.
            var shimmerPhase = 2 * Math.PI * (3 * t + 11 / (2 * HoldSeconds) * t * t);
            var shimmer = 0.7 + 0.3 * Math.Sin(shimmerPhase);
            var swell = 0.35 + 0.65 * progress;
            samples[i] = (float)(tone * shimmer * swell * Envelope(t, HoldSeconds, 0.12, 0.06));
        }
        return Normalised(samples);
    }

    /// <summary>A short blip falling from 440 to 160 Hz.</summary>
    public static float[] Cancel()
    {
        const double seconds = 0.28, from = 440, to = 160;
        var samples = new float[(int)(seconds * SampleRate)];
        var phase = 0.0;
        for (var i = 0; i < samples.Length; i++)
        {
            var t = (double)i / SampleRate;
            phase += 2 * Math.PI * (from + (to - from) * t / seconds) / SampleRate;
            samples[i] = (float)((0.8 * Math.Sin(phase) + 0.2 * Math.Sin(2 * phase)) * Envelope(t, seconds, 0.01, 0.12));
        }
        return Normalised(samples, 0.75f);
    }

    /// <summary>Two bright notes, C6 then G6, each ringing down.</summary>
    public static float[] Done()
    {
        const double seconds = 0.9, second = 0.14;
        var samples = new float[(int)(seconds * SampleRate)];
        for (var i = 0; i < samples.Length; i++)
        {
            var t = (double)i / SampleRate;
            var value = Note(t, 1046.5) + (t >= second ? Note(t - second, 1568.0) : 0);
            samples[i] = (float)(value * Envelope(t, seconds, 0.005, 0.05));
        }
        return Normalised(samples);
    }

    private static double Note(double t, double frequency) =>
        Math.Exp(-5 * t) * (0.8 * Math.Sin(2 * Math.PI * frequency * t) + 0.2 * Math.Sin(4 * Math.PI * frequency * t));

    // Linear fade in over `attack` seconds and out over the last `release` seconds.
    private static double Envelope(double t, double length, double attack, double release) =>
        Math.Min(1, Math.Min(t / attack, (length - t) / release));

    private static float[] Normalised(float[] samples, float share = 1f)
    {
        var loudest = 0f;
        foreach (var s in samples)
            loudest = Math.Max(loudest, Math.Abs(s));
        if (loudest > 0)
            for (var i = 0; i < samples.Length; i++)
                samples[i] = samples[i] / loudest * Peak * share;
        return samples;
    }
}
