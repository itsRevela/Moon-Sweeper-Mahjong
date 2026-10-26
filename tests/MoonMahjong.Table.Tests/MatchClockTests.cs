using MoonMahjong.Table.Presentation;

namespace MoonMahjong.Table.Tests;

// The game's own time: it runs while the player sits at the table and stands still while they are away (requested:
// leaving the table pauses the game).
public sealed class MatchClockTests
{
    [Fact]
    public void Clock_AdvancesWhileRunning()
    {
        var clock = new MatchClock(10f);

        clock.Tick(0.5f);

        Assert.Equal(10.5f, clock.Now, 4);
    }

    [Fact]
    public void Clock_StandsStillWhilePaused()
    {
        var clock = new MatchClock(10f);
        clock.Pause();

        clock.Tick(30f);

        Assert.Equal(10f, clock.Now, 4);
    }

    [Fact]
    public void Clock_ContinuesFromWhereItPausedOnResume()
    {
        var clock = new MatchClock(10f);
        clock.Tick(1f);
        clock.Pause();
        clock.Tick(300f);

        clock.Resume();
        clock.Tick(0.25f);

        Assert.Equal(11.25f, clock.Now, 4);
    }

    [Fact]
    public void Clock_ReportsWhetherItIsPaused()
    {
        var clock = new MatchClock(0f);
        clock.Pause();

        Assert.True(clock.Paused);
    }

    [Theory]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void Clock_IgnoresANegativeOrNonFiniteTick(float delta)
    {
        var clock = new MatchClock(5f);

        clock.Tick(delta);

        Assert.Equal(5f, clock.Now, 4);
    }

    [Fact]
    public void Clock_RejectsANonFiniteStart()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MatchClock(float.NaN));
    }
}
