using MoonMahjong.Core;
using MoonMahjong.Core.Tests.Harness;

namespace MoonMahjong.Core.Tests.Server;

public sealed class ServerRoundStateValidatorTests
{
    private static readonly bool[] AllCanRiichi = { true, true, true, true };

    private static ServerRoundStateValidator Start(Tile[] wall, ServerSettings? settings = null)
    {
        var validator = new ServerRoundStateValidator(settings ?? ServerSettings.Default, WallBuilder.Dealer, WallBuilder.WallIndex,
            new RandomClass(1), Wind.East, AllCanRiichi, wall);
        validator.Start();
        return validator;
    }

    private static Tile HandTile(ServerRoundStateValidator validator, int seat, TileType type) =>
        WallBuilder.Find(validator.GetPlayer(seat).Hand, type);

    // Starts the round, lets the dealer draw and discard its 5m, and asks for call decisions.
    private static (ServerRoundStateValidator Validator, List<int> Callers) DiscardFiveMan(Tile[] wall, ServerSettings? settings = null)
    {
        var validator = Start(wall, settings);
        validator.DrawWall();
        Assert.True(validator.DiscardTile(HandTile(validator, 0, TileType.Man5).ID));
        var callers = validator.DoPlayerCalls().Select(p => p.Index).ToList();
        return (validator, callers);
    }

    [Fact]
    public void IsPlayersTurn_IsOnlyTrueForTheCurrentPlayer()
    {
        var validator = Start(CallWalls.ChiiPonRon());
        validator.DrawWall();

        Assert.True(validator.IsPlayersTurn(0));
        Assert.False(validator.IsPlayersTurn(1));
    }

    [Fact]
    public void DiscardTile_RejectsATileFromAnotherPlayersHand()
    {
        var validator = Start(CallWalls.ChiiPonRon());
        validator.DrawWall();

        var tile = HandTile(validator, 1, TileType.Man4);

        Assert.False(validator.DiscardTile(tile.ID));
        Assert.Contains(tile, validator.GetPlayer(1).Hand);
        Assert.Equal(14, validator.GetPlayer(0).Hand.Count);
    }

    [Fact]
    public void DoPlayerCalls_ListsEveryPlayerWhoCanCallTheDiscard()
    {
        var (_, callers) = DiscardFiveMan(CallWalls.ChiiPonRon());

        Assert.Equal(new[] { 1, 2, 3 }, callers);
    }

    [Fact]
    public void DecideChii_RejectsAPlayerWhoIsNotNextAfterTheDiscarder()
    {
        var (validator, _) = DiscardFiveMan(CallWalls.ChiiPonRon());

        // Seat 3 holds 4m 6m too, but only seat 1 may chii seat 0's discard.
        var four = HandTile(validator, 3, TileType.Man4);
        var six = HandTile(validator, 3, TileType.Man6);

        Assert.False(validator.DecideChii(3, four.ID, six.ID));
    }

    [Fact]
    public void DecideChii_RejectsTilesThatDoNotFormASequenceWithTheDiscard()
    {
        var (validator, _) = DiscardFiveMan(CallWalls.ChiiPonRon());

        var one = HandTile(validator, 1, TileType.Man1);
        var nine = HandTile(validator, 1, TileType.Man9);

        Assert.False(validator.DecideChii(1, one.ID, nine.ID));
        Assert.True(validator.CanCall(1));
    }

    [Fact]
    public void DecidePon_RejectsAPlayerWithoutAPair()
    {
        var (validator, _) = DiscardFiveMan(CallWalls.ChiiPonRon());

        Assert.False(validator.DecidePon(1));
        Assert.True(validator.CanCall(1));
    }

    [Fact]
    public void DecideRon_RejectsAPlayerWhoIsNotWaitingOnTheDiscard()
    {
        var (validator, _) = DiscardFiveMan(CallWalls.ChiiPonRon());

        Assert.False(validator.DecideRon(2));
    }

    [Fact]
    public void Riichi_IsRejectedWhenNotTenpai()
    {
        var validator = Start(CallWalls.ChiiPonRon());
        validator.DrawWall();

        Assert.False(validator.Riichi(false));
    }

    [Fact]
    public void Riichi_IsAcceptedWhenTenpai()
    {
        var validator = Start(CallWalls.DealerTenpai());
        validator.DrawWall();

        Assert.True(validator.Riichi(false));
        Assert.True(validator.GetPlayer(0).InRiichi);
    }

    [Fact]
    public void OpenRiichi_IsRejectedWhenTheSettingIsOff()
    {
        var validator = Start(CallWalls.DealerTenpai(), ServerSettings.Default with { OpenRiichi = false });
        validator.DrawWall();

        Assert.False(validator.Riichi(true));
        Assert.False(validator.GetPlayer(0).InRiichi);
    }

    [Fact]
    public void OpenRiichi_IsAcceptedWhenTheSettingIsOn()
    {
        var validator = Start(CallWalls.DealerTenpai(), ServerSettings.Default with { OpenRiichi = true });
        validator.DrawWall();

        Assert.True(validator.Riichi(true));
        Assert.True(validator.GetPlayer(0).Open);
    }

    [Fact]
    public void GetCall_RonBeatsPonAndChii()
    {
        var (validator, _) = DiscardFiveMan(CallWalls.ChiiPonRon());
        Assert.True(validator.DecideChii(1, HandTile(validator, 1, TileType.Man4).ID, HandTile(validator, 1, TileType.Man6).ID));
        Assert.True(validator.DecidePon(2));
        Assert.True(validator.DecideRon(3));

        var call = validator.GetCall()!;

        Assert.Equal(CallDecisionType.Ron, call.CallType);
        Assert.Equal(new[] { 3 }, call.Callers.Select(p => p.Index));
        Assert.True(validator.GameOver);
    }

    [Fact]
    public void GetCall_PonBeatsChii()
    {
        var (validator, _) = DiscardFiveMan(CallWalls.ChiiPonRon());
        Assert.True(validator.DecideChii(1, HandTile(validator, 1, TileType.Man4).ID, HandTile(validator, 1, TileType.Man6).ID));
        Assert.True(validator.DecidePon(2));
        validator.NoCall(3);

        var call = validator.GetCall()!;

        Assert.Equal(CallDecisionType.Pon, call.CallType);
        Assert.Equal(2, call.Callers.Single().Index);
        Assert.Equal(2, validator.GetCurrentPlayer().Index);
        Assert.True(validator.IsPlayersTurn(2));
    }

    [Fact]
    public void GetCall_ChiiWinsWhenNobodyElseCalls()
    {
        var (validator, _) = DiscardFiveMan(CallWalls.ChiiPonRon());
        Assert.True(validator.DecideChii(1, HandTile(validator, 1, TileType.Man4).ID, HandTile(validator, 1, TileType.Man6).ID));
        validator.NoCall(2);
        validator.NoCall(3);

        var call = validator.GetCall()!;

        Assert.Equal(CallDecisionType.Chii, call.CallType);
        Assert.Equal(1, call.Callers.Single().Index);
        Assert.Equal(2, validator.GetPlayer(1).Player.Calls.Single().Tiles.Count(t => t.TileType != TileType.Man5));
    }

    [Fact]
    public void GetCall_WithNoCalls_ReturnsNullAndPassesTheTurn()
    {
        var (validator, _) = DiscardFiveMan(CallWalls.ChiiPonRon());
        validator.DefaultCallDecisions();

        Assert.True(validator.CallsFinished);
        Assert.Null(validator.GetCall());
        Assert.Equal(1, validator.GetCurrentPlayer().Index);
    }

    [Fact]
    public void GetCall_WithMultipleRonOn_EveryRonCallerWins()
    {
        var (validator, callers) = DiscardFiveMan(CallWalls.DoubleRon(), ServerSettings.Default with { MultipleRon = true });
        Assert.Equal(new[] { 1, 3 }, callers);
        Assert.True(validator.DecideRon(1));
        Assert.True(validator.DecideRon(3));

        var call = validator.GetCall()!;

        Assert.Equal(CallDecisionType.Ron, call.CallType);
        Assert.Equal(new[] { 1, 3 }, call.Callers.Select(p => p.Index));
        Assert.False(call.Draw);
        Assert.Equal(2, validator.GetRonScore()!.Length);
    }

    [Fact]
    public void GetCall_WithMultipleRonOff_OnlyTheFirstPlayerAfterTheDiscarderWins()
    {
        var (validator, _) = DiscardFiveMan(CallWalls.DoubleRon(), ServerSettings.Default with { MultipleRon = false });
        Assert.True(validator.DecideRon(3));
        Assert.True(validator.DecideRon(1));

        var call = validator.GetCall()!;

        Assert.Equal(new[] { 1 }, call.Callers.Select(p => p.Index));
    }

    [Fact]
    public void GetCall_ThreeRonsWithTripleRonDrawOn_IsADraw()
    {
        var (validator, callers) = DiscardFiveMan(CallWalls.TripleRon(), ServerSettings.Default with { TripleRonDraw = true });
        Assert.Equal(new[] { 1, 2, 3 }, callers);
        foreach (var seat in callers)
            Assert.True(validator.DecideRon(seat));

        var call = validator.GetCall()!;

        Assert.True(call.Draw);
        Assert.Equal(3, call.Callers.Length);
        Assert.Equal(GameDrawType.TripleRon, validator.GameDrawType);
    }

    [Fact]
    public void GetCall_ThreeRonsWithTripleRonDrawOff_AllThreeWin()
    {
        var settings = ServerSettings.Default with { TripleRonDraw = false, MultipleRon = true };
        var (validator, callers) = DiscardFiveMan(CallWalls.TripleRon(), settings);
        foreach (var seat in callers)
            Assert.True(validator.DecideRon(seat));

        var call = validator.GetCall()!;

        Assert.False(call.Draw);
        Assert.Equal(new[] { 1, 2, 3 }, call.Callers.Select(p => p.Index));
        Assert.Equal(GameDrawType.None, validator.GameDrawType);
    }

    [Fact]
    public void GetPlayer_ThrowsForASeatThatDoesNotExist()
    {
        var validator = Start(CallWalls.ChiiPonRon());

        Assert.Throws<ArgumentOutOfRangeException>(() => validator.GetPlayer(-1));
    }
}
