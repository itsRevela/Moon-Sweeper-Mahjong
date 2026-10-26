using MoonMahjong.Table.Presentation;

namespace MoonMahjong.Table.Tests;

// The spoken calls, heard as if over a suit radio (requested: in theme with the space suits, and fun).
public sealed class RadioVoiceTests
{
    private const int Rate = 44100;
    private static readonly int SquelchIn = (int)Math.Round(RadioVoice.SquelchInSeconds * Rate);
    private static readonly int SquelchOut = (int)Math.Round(RadioVoice.SquelchOutSeconds * Rate);

    // A stereo tone, the same on both channels.
    private static float[] Tone(double hertz, float amplitude = 0.5f, double seconds = 0.5)
    {
        var frames = (int)(seconds * Rate);
        var samples = new float[frames * 2];
        for (var i = 0; i < frames; i++)
            samples[2 * i] = samples[2 * i + 1] = (float)(amplitude * Math.Sin(2 * Math.PI * hertz * i / Rate));
        return samples;
    }

    // The call itself, between the squelch bursts.
    private static float[] Call(float[] radio, int frames) => radio.Skip(SquelchIn).Take(frames).ToArray();

    private static double Rms(IReadOnlyCollection<float> samples) => Math.Sqrt(samples.Sum(s => (double)s * s) / samples.Count);

    [Fact]
    public void Output_IsMono_WithSquelchBeforeAndAfterTheCall()
    {
        var radio = RadioVoice.Apply(Tone(1000), 2, Rate);

        Assert.Equal(SquelchIn + Rate / 2 + SquelchOut, radio.Length);
    }

    [Fact]
    public void LowRumble_IsCut()
    {
        var frames = Rate / 2;
        var low = Rms(Call(RadioVoice.Apply(Tone(100), 2, Rate), frames));
        var mid = Rms(Call(RadioVoice.Apply(Tone(1000), 2, Rate), frames));

        Assert.True(low < 0.1 * mid, $"100 Hz kept {low:F4} against 1 kHz's {mid:F4}");
    }

    [Fact]
    public void Highs_AreCut()
    {
        var frames = Rate / 2;
        var high = Rms(Call(RadioVoice.Apply(Tone(8000), 2, Rate), frames));
        var mid = Rms(Call(RadioVoice.Apply(Tone(1000), 2, Rate), frames));

        Assert.True(high < 0.1 * mid, $"8 kHz kept {high:F4} against 1 kHz's {mid:F4}");
    }

    [Fact]
    public void LoudInput_StaysWithinFullScale()
    {
        var radio = RadioVoice.Apply(Tone(1000, amplitude: 1f), 2, Rate);

        Assert.True(radio.Max(Math.Abs) <= 1f);
    }

    [Fact]
    public void Silence_GetsOnlyAFaintHiss()
    {
        var silent = new float[Rate];

        Assert.True(Call(RadioVoice.Apply(silent, 2, Rate), Rate / 2).Max(Math.Abs) < 0.05f);
    }

    [Fact]
    public void SquelchTail_IsHeardAfterTheCall()
    {
        var silent = new float[Rate];
        var radio = RadioVoice.Apply(silent, 2, Rate);

        Assert.True(Rms(radio.Skip(SquelchIn + Rate / 2).Take(SquelchOut / 3).ToArray()) > 0.02);
    }

    [Fact]
    public void SameCall_SoundsTheSameEveryTime()
    {
        Assert.Equal(RadioVoice.Apply(Tone(700), 2, Rate), RadioVoice.Apply(Tone(700), 2, Rate));
    }

    [Theory]
    [InlineData(0, Rate)]
    [InlineData(2, 0)]
    public void BadFormat_Throws(int channels, int rate)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RadioVoice.Apply(Tone(1000), channels, rate));
    }

    [Fact]
    public void PartialFrame_Throws()
    {
        Assert.Throws<ArgumentException>(() => RadioVoice.Apply(new float[3], 2, Rate));
    }
}
