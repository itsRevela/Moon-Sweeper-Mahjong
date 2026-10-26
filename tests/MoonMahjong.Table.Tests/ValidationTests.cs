using MoonMahjong.Core;
using MoonMahjong.Table.Layout;
using static MoonMahjong.Table.Tests.TableTestHelpers;

namespace MoonMahjong.Table.Tests;

public sealed class ValidationTests
{
    [Fact]
    public void Time_MustNotGoBackwards()
    {
        var table = NewTable();
        table.GetTilePose(0, AfterDeal);

        Assert.Throws<ArgumentOutOfRangeException>(() => table.TileDraw(0, AfterDeal - 1));
    }

    [Fact]
    public void Time_MustBeFinite()
    {
        var table = NewTable();

        Assert.Throws<ArgumentOutOfRangeException>(() => table.GetTilePose(0, float.NaN));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public void Seat_MustBeZeroToThree(int seat)
    {
        var table = NewTable();

        Assert.Throws<ArgumentOutOfRangeException>(() => table.TileDraw(seat, AfterDeal));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(136)]
    public void TileId_MustBeZeroTo135(int tileId)
    {
        var table = NewTable();

        Assert.Throws<ArgumentOutOfRangeException>(() => table.GetTilePose(tileId, AfterDeal));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(13)]
    public void WallIndex_MustBeADiceSum(int wallIndex)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NewTable(wallIndex: wallIndex));
    }

    [Fact]
    public void ObserverSeat_MustBeASeatOrSpectator()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NewTable(observer: -2));
    }

    [Fact]
    public void Game_MustHaveFourPlayers()
    {
        var game = new GameStartInfo(new[] { new GamePlayer(0, "A") }, 0, 25000, 8, 1, 20, 10);

        Assert.Throws<ArgumentException>(() => new TableLayout(0, 0, game, new RoundStartInfo(5), TableTimings.Default, 0));
    }

    [Fact]
    public void Ron_OnATileNeedsTheDiscarder()
    {
        var table = NewTable();

        Assert.Throws<ArgumentException>(() => table.Ron(new[] { 1 }, -1, 5, -1, true, AfterDeal));
    }

    [Fact]
    public void Ron_NeedsWinners()
    {
        var table = NewTable();

        Assert.Throws<ArgumentNullException>(() => table.Ron(null!, 0, 5, -1, true, AfterDeal));
    }

    [Fact]
    public void LateKan_WithoutAMatchingPon_FailsWhenItIsPlayed()
    {
        var table = NewTable();
        AssignAll(table, AfterDeal);

        // The queue is idle, so the late kan plays (and fails) at once.
        Assert.Throws<InvalidOperationException>(() => table.LateKan(0, ExpectedDeal(0, 5)[0][0], AfterDeal));
    }

    [Fact]
    public void LateKan_WithoutAMatchingPon_QueuedBehindOtherEvents_FailsWhenTheQueueReachesIt()
    {
        var table = NewTable();
        AssignAll(table, Start);
        table.LateKan(0, ExpectedDeal(0, 5)[0][0], Start); // queued behind the round start sequence

        Assert.Throws<InvalidOperationException>(() => table.GetTilePose(0, AfterDeal));
    }

    [Fact]
    public void AnimationTime_MustNotBeNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TableAnimationTime(0, -0.1f, 0));
    }

    [Fact]
    public void TileAssignment_RevealsTheType()
    {
        var table = NewTable();
        table.TileAssignment(new Tile(7, TileType.Pin5, true), AfterDeal);

        var pose = table.GetTilePose(7, AfterDeal);

        Assert.True(pose.TypeKnown);
        Assert.Equal(TileType.Pin5, pose.Type);
        Assert.True(pose.RedFive);
    }

    [Fact]
    public void UnassignedTile_HasUnknownType()
    {
        var table = NewTable();

        Assert.False(table.GetTilePose(7, AfterDeal).TypeKnown);
    }
}
