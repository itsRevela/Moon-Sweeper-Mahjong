using System;

namespace MoonMahjong.Table.Presentation;

/// <summary>
/// The spoken calls as if heard over a suit radio (requested: in theme with the space suits, and fun): a squelch burst
/// as the channel opens, the voice squeezed into the radio band with a little crunch and a faint hiss, then the
/// squelch tail as it closes. Pure sample work, so it is the same on every machine.
/// </summary>
public static class RadioVoice
{
    public const double SquelchInSeconds = 0.05;
    public const double SquelchOutSeconds = 0.14;

    private const double LowCut = 450; // Hz: below this a suit radio carries nothing
    private const double HighCut = 2800; // Hz: and above this
    private const double Drive = 2.5; // into the soft clip: compresses the voice and adds the crunch
    private const double Hiss = 0.008; // static under the voice, before filtering
    private const double Squelch = 0.35; // the squelch bursts, before filtering
    private const double SquelchAttackSeconds = 0.005;
    private const int Seed = 7; // the static is the same every time

    /// <summary>Returns the radio version of interleaved <paramref name="samples"/>, mono, at the same rate.</summary>
    public static float[] Apply(float[] samples, int channels, int sampleRate)
    {
        if (samples == null)
            throw new ArgumentNullException(nameof(samples));
        if (channels < 1)
            throw new ArgumentOutOfRangeException(nameof(channels), channels, "At least one channel is needed.");
        if (sampleRate < 1)
            throw new ArgumentOutOfRangeException(nameof(sampleRate), sampleRate, "The sample rate must be positive.");
        if (samples.Length % channels != 0)
            throw new ArgumentException($"{samples.Length} samples aren't whole frames of {channels} channels.", nameof(samples));

        var frames = samples.Length / channels;
        var before = (int)Math.Round(SquelchInSeconds * sampleRate);
        var after = (int)Math.Round(SquelchOutSeconds * sampleRate);
        var attack = Math.Max(1, (int)Math.Round(SquelchAttackSeconds * sampleRate));
        var signal = new double[before + frames + after];
        var random = new Random(Seed);

        for (var i = 0; i < signal.Length; i++)
        {
            double voice = 0;
            if (i >= before && i < before + frames)
            {
                var frame = (i - before) * channels;
                for (var c = 0; c < channels; c++)
                    voice += samples[frame + c];
                voice /= channels;
            }

            // The squelch opens with a quick rise and falls away just as quickly before the voice; it closes with a
            // burst that fades out.
            double noise;
            if (i < before)
                noise = Squelch * Math.Min(1.0, Math.Min(i + 1.0, before - i) / attack);
            else if (i >= before + frames)
                noise = Squelch * Math.Pow(1.0 - (double)(i - before - frames) / after, 2);
            else
                noise = Hiss;
            signal[i] = voice + noise * (random.NextDouble() * 2 - 1);
        }

        // Two passes each way make the band's edges steep, as a radio's are.
        var high = Biquad.HighPass(LowCut, sampleRate);
        var low = Biquad.LowPass(HighCut, sampleRate);
        high.Run(signal);
        high.Run(signal);
        low.Run(signal);
        low.Run(signal);

        var radio = new float[signal.Length];
        for (var i = 0; i < signal.Length; i++)
            radio[i] = (float)Math.Tanh(Drive * signal[i]);
        return radio;
    }

    // An RBJ cookbook biquad (Q 1/sqrt 2), run in place; each run starts from rest.
    private readonly struct Biquad
    {
        private readonly double b0, b1, b2, a1, a2;

        private Biquad(double b0, double b1, double b2, double a0, double a1, double a2)
        {
            this.b0 = b0 / a0;
            this.b1 = b1 / a0;
            this.b2 = b2 / a0;
            this.a1 = a1 / a0;
            this.a2 = a2 / a0;
        }

        public static Biquad LowPass(double hertz, int rate)
        {
            var (cos, alpha) = Shape(hertz, rate);
            return new Biquad((1 - cos) / 2, 1 - cos, (1 - cos) / 2, 1 + alpha, -2 * cos, 1 - alpha);
        }

        public static Biquad HighPass(double hertz, int rate)
        {
            var (cos, alpha) = Shape(hertz, rate);
            return new Biquad((1 + cos) / 2, -(1 + cos), (1 + cos) / 2, 1 + alpha, -2 * cos, 1 - alpha);
        }

        private static (double Cos, double Alpha) Shape(double hertz, int rate)
        {
            var w = 2 * Math.PI * Math.Min(hertz, rate * 0.45) / rate;
            return (Math.Cos(w), Math.Sin(w) / (2 * Math.Sqrt(0.5)));
        }

        public void Run(double[] samples)
        {
            double x1 = 0, x2 = 0, y1 = 0, y2 = 0;
            for (var i = 0; i < samples.Length; i++)
            {
                var x = samples[i];
                var y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1;
                x1 = x;
                y2 = y1;
                y1 = y;
                samples[i] = y;
            }
        }
    }
}
