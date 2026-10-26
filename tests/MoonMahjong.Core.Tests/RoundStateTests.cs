using MoonMahjong.Core;
using MoonMahjong.Core.Tests.Harness;

namespace MoonMahjong.Core.Tests;

public sealed class RoundStateTests
{
    private static readonly bool[] AllCanRiichi = { true, true, true, true };

    private static RoundState Custom(Tile[] wall, bool[]? canRiichi = null)
    {
        var state = RoundState.Custom(ServerSettings.Default, Wind.East, WallBuilder.Dealer, WallBuilder.WallIndex, canRiichi ?? AllCanRiichi, wall);
        state.Start();
        return state;
    }

    // Seat 1 draws nothing but terminals and honours, seat 2 is tenpai from the start, and every player discards
    // the tile they draw until the wall runs out.
    private static Tile[] NagashiWall() => WallBuilder.Build(
        new[]
        {
            "258m258p258s36m36p", // noten
            "47m47p47s19m19p19s1z", // noten
            "234p456p678s22s46m", // tenpai on 5m
            "69m69p69s3s234567z", // noten
        },
        new[] { null, "111m99m111p99p111s99s111z" });

    private static RoundState PlayOutByTsumogiri(RoundState state)
    {
        while (!state.GameOver)
        {
            var tile = state.TileDraw();
            Assert.True(state.TileDiscard(tile.ID));
            state.CallsFinished();
        }

        return state;
    }

    [Fact]
    public void Start_DealsThirteenTilesToEverySeat()
    {
        var state = Custom(NagashiWall());

        for (var i = 0; i < 4; i++)
            Assert.Equal(13, state.GetPlayer(i).Hand.Count);
        Assert.Equal(70, state.TilesRemaining);
        Assert.Equal(0, state.CurrentPlayer.Index);
    }

    [Fact]
    public void SeatWinds_StartFromTheDealer()
    {
        var state = new RoundState(ServerSettings.Default, 0, Wind.East, 2, 5, AllCanRiichi);

        Assert.Equal(Wind.East, state.GetPlayer(2).Wind);
        Assert.Equal(Wind.South, state.GetPlayer(3).Wind);
        Assert.Equal(Wind.West, state.GetPlayer(0).Wind);
        Assert.Equal(Wind.North, state.GetPlayer(1).Wind);
    }

    [Fact]
    public void EmptyWall_EndsTheRoundInAnExhaustiveDraw()
    {
        var state = PlayOutByTsumogiri(Custom(NagashiWall()));

        Assert.True(state.TilesEmpty);
        Assert.Equal(GameDrawType.EmptyWall, state.GameDrawType);
    }

    [Fact]
    public void ExhaustiveDraw_ReportsTheTenpaiPlayers()
    {
        var state = PlayOutByTsumogiri(Custom(NagashiWall()));

        Assert.Equal(new[] { 2 }, state.GetTenpaiPlayers().Select(p => p.Index));
    }

    [Fact]
    public void ExhaustiveDraw_ReportsNagashiForAPondOfOnlyTerminalsAndHonours()
    {
        var state = PlayOutByTsumogiri(Custom(NagashiWall()));

        Assert.Equal(new[] { 1 }, state.GetNagashiIndices());
    }

    [Fact]
    public void Nagashi_IsOnlyReportedOnceTheWallIsEmpty()
    {
        var state = Custom(NagashiWall());
        for (var i = 0; i < 8; i++)
        {
            var tile = state.TileDraw();
            state.TileDiscard(tile.ID);
            state.CallsFinished();
        }

        Assert.Empty(state.GetNagashiIndices());
    }

    [Fact]
    public void Nagashi_IsLostWhenADiscardIsCalled()
    {
        var player = new RoundStatePlayer(1, false, Wind.South, true, true);
        foreach (var tile in TestHelpers.TestTiles("19m1z"))
        {
            player.Draw(tile);
            Assert.True(player.Discard(tile));
        }

        Assert.True(player.HasNagashiMangan());
        player.RobTile(player.Pond[0]);
        Assert.False(player.HasNagashiMangan());
    }

    [Fact]
    public void ExhaustiveDraw_FeedsGameStatePayments()
    {
        var state = PlayOutByTsumogiri(Custom(NagashiWall()));
        var tenpai = state.GetTenpaiPlayers().Select(p => p.Index).ToArray();
        var game = new GameState(new GameStartInfo(Enumerable.Range(0, 4).Select(i => new GamePlayer(i, "P" + i)).ToArray(), 0, 25000, 8, 1, 20, 10), ServerSettings.Default);
        game.StartRound(new RoundStartInfo(0));

        game.RoundFinished(RoundFinishResult.Draw(tenpai, state.GetNagashiIndices(), state.GameDrawType));

        Assert.Equal(new[] { 20000, 32000, 26000, 22000 }, Enumerable.Range(0, 4).Select(i => game.GetPlayer(i).Points));
        Assert.False(game.DoRenchan); // The dealer (seat 0) was noten
    }

    [Fact]
    public void Riichi_SetsTheRiichiReturnIndexUntilTheDiscardIsNotCalled()
    {
        var state = Custom(Server.CallWalls.DealerTenpai());
        var drawn = state.TileDraw();

        Assert.True(state.Riichi(false));
        Assert.Equal(0, state.RiichiReturnIndex);

        Assert.True(state.TileDiscard(drawn.ID));
        Assert.Equal(0, state.RiichiReturnIndex);

        state.CallsFinished();
        Assert.Equal(-1, state.RiichiReturnIndex);
        Assert.True(state.GetPlayer(0).InRiichi);
    }

    [Fact]
    public void Riichi_IsRejectedWithoutTenpai()
    {
        var state = Custom(NagashiWall());
        state.TileDraw();

        Assert.False(state.CanRiichi());
        Assert.False(state.Riichi(false));
        Assert.Equal(-1, state.RiichiReturnIndex);
    }

    [Fact]
    public void Riichi_IsRejectedForAPlayerWhoCannotAffordIt()
    {
        var state = Custom(Server.CallWalls.DealerTenpai(), new[] { false, true, true, true });
        state.TileDraw();

        Assert.False(state.Riichi(false));
    }

    [Fact]
    public void Riichi_OnlyTheDrawnTileCanBeDiscardedAfterTheRiichiDiscard()
    {
        var state = Custom(Server.CallWalls.DealerTenpai());
        var drawn = state.TileDraw();
        state.Riichi(false);
        state.TileDiscard(drawn.ID);
        state.CallsFinished();

        // Go round the table back to the dealer.
        for (var i = 0; i < 3; i++)
        {
            var t = state.TileDraw();
            state.TileDiscard(t.ID);
            state.CallsFinished();
        }

        var newest = state.TileDraw();
        var other = state.CurrentPlayer.Hand.First(t => t != newest);

        Assert.Equal(new[] { newest }, state.CurrentPlayer.GetDiscardTiles());
        Assert.False(state.TileDiscard(other.ID));
    }
}
