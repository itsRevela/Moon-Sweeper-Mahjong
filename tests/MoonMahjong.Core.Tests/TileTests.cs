using MoonMahjong.Core;

namespace MoonMahjong.Core.Tests;

public sealed class TileTests
{
    [Theory]
    [InlineData(TileType.Man1, TileType.Man2)]
    [InlineData(TileType.Man9, TileType.Man1)]
    [InlineData(TileType.Pin9, TileType.Pin1)]
    [InlineData(TileType.Sou9, TileType.Sou1)]
    [InlineData(TileType.Pei, TileType.Ton)]
    [InlineData(TileType.Chun, TileType.Haku)]
    [InlineData(TileType.Blank, TileType.Blank)]
    public void DoraIndicator_PointsAtNextTileWrappingWithinItsGroup(TileType indicator, TileType dora)
    {
        Assert.Equal(dora, new Tile(1, indicator, false).DoraIndicator());
    }

    [Fact]
    public void IsNeighbour_IsFalseAcrossSuits()
    {
        Assert.False(new Tile(1, TileType.Man9, false).IsNeighbour(new Tile(2, TileType.Pin1, false)));
    }

    [Fact]
    public void IsNeighbour_IsFalseForHonors()
    {
        Assert.False(new Tile(1, TileType.Ton, false).IsNeighbour(new Tile(2, TileType.Nan, false)));
    }

    [Fact]
    public void GetNumberIndex_IsZeroBasedWithinSuit()
    {
        Assert.Equal(4, new Tile(1, TileType.Sou5, false).GetNumberIndex());
    }

    [Fact]
    public void TilesEqualUnordered_IgnoresOrder()
    {
        var a = new Tile(1, TileType.Man1, false);
        var b = new Tile(2, TileType.Pin2, true);
        Assert.True(Tile.TilesEqualUnordered(new() { a, b }, new() { b, a }));
    }

    [Theory]
    [InlineData(0, Wind.East)]
    [InlineData(5, Wind.South)]
    [InlineData(-1, Wind.North)]
    [InlineData(-4, Wind.East)]
    public void WindsFromInt_WrapsAnyInteger(int value, Wind expected)
    {
        Assert.Equal(expected, Winds.FromInt(value));
    }

    [Fact]
    public void WindsNext_WrapsNorthToEast()
    {
        Assert.Equal(Wind.East, Winds.Next(Wind.North));
    }

    [Fact]
    public void WindsPrevious_WrapsEastToNorth()
    {
        Assert.Equal(Wind.North, Winds.Previous(Wind.East));
    }

    [Theory]
    [InlineData(0, ServerSettings.MinDecisionTime)]
    [InlineData(10_000, ServerSettings.MaxDecisionTime)]
    [InlineData(15, 15)]
    public void ServerSettings_ClampsDecisionTime(int requested, int expected)
    {
        Assert.Equal(expected, new ServerSettings { DecisionTime = requested }.DecisionTime);
    }
}
