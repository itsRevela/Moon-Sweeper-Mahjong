using MoonMahjong.Table.Presentation;

namespace MoonMahjong.Table.Tests;

// The start screen's choice (requested): an East-only game or a full East + South game.
public sealed class GameLengthTests
{
    [Theory]
    [InlineData(GameLength.EastOnly, 4)]
    [InlineData(GameLength.EastSouth, 8)]
    public void RoundCount_IsFourRoundsPerWind(GameLength length, int rounds)
    {
        Assert.Equal(rounds, GameLengths.RoundCount(length));
    }

    [Theory]
    [InlineData(GameLength.EastOnly, "East only")]
    [InlineData(GameLength.EastSouth, "East + South")]
    public void Name_IsWhatTheButtonSays(GameLength length, string name)
    {
        Assert.Equal(name, GameLengths.Name(length));
    }

    [Theory]
    [InlineData(1, GameLength.EastOnly)]
    [InlineData(4, GameLength.EastOnly)]
    [InlineData(5, GameLength.EastSouth)]
    [InlineData(8, GameLength.EastSouth)]
    [InlineData(16, GameLength.EastSouth)]
    public void FromRoundCount_PicksTheNearestChoice(int rounds, GameLength length)
    {
        Assert.Equal(length, GameLengths.FromRoundCount(rounds));
    }
}
