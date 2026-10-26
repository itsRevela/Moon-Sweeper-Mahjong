using MoonMahjong.Table.Presentation;

namespace MoonMahjong.Table.Tests;

// The Reset table sounds, made in code (requested: everyone at the table hears a reset coming, not too loud).
public sealed class ResetSoundsTests
{
    private static int Crossings(float[] samples, int from, int to)
    {
        var count = 0;
        for (var i = from + 1; i < to; i++)
            if (samples[i - 1] < 0 != samples[i] < 0)
                count++;
        return count;
    }

    public static TheoryData<string> Sounds() => new() { "charge", "cancel", "done" };

    private static float[] Make(string name) => name switch
    {
        "charge" => ResetSounds.Charge(),
        "cancel" => ResetSounds.Cancel(),
        _ => ResetSounds.Done(),
    };

    [Fact]
    public void TheCharge_LastsTheHold()
    {
        Assert.Equal((int)(ResetSounds.HoldSeconds * ResetSounds.SampleRate), ResetSounds.Charge().Length);
    }

    [Theory]
    [MemberData(nameof(Sounds))]
    public void EachSound_IsQuiet(string name)
    {
        Assert.InRange(Make(name).Max(Math.Abs), 0.05f, ResetSounds.Peak + 1e-4f);
    }

    [Theory]
    [MemberData(nameof(Sounds))]
    public void EachSound_StartsWithoutAClick(string name)
    {
        Assert.InRange(Math.Abs(Make(name)[0]), 0f, 0.01f);
    }

    [Theory]
    [MemberData(nameof(Sounds))]
    public void EachSound_EndsWithoutAClick(string name)
    {
        var samples = Make(name);
        Assert.InRange(Math.Abs(samples[^1]), 0f, 0.01f);
    }

    [Fact]
    public void TheCharge_Rises()
    {
        var charge = ResetSounds.Charge();
        var second = ResetSounds.SampleRate;

        Assert.True(Crossings(charge, charge.Length - second, charge.Length) > 2 * Crossings(charge, 0, second));
    }

    [Fact]
    public void LettingGo_Falls()
    {
        var cancel = ResetSounds.Cancel();
        var half = cancel.Length / 2;

        Assert.True(Crossings(cancel, 0, half) > Crossings(cancel, half, cancel.Length));
    }
}
