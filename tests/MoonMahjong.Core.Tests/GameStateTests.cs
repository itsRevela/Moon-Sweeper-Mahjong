using MoonMahjong.Core;

namespace MoonMahjong.Core.Tests;

public sealed class GameStateTests
{
    private const int StartingScore = 25000;

    private static GameState NewGame(int roundCount = 8, int startingScore = StartingScore, int startingDealer = 0)
    {
        var players = Enumerable.Range(0, 4).Select(i => new GamePlayer(i, "P" + i)).ToArray();
        var state = new GameState(new GameStartInfo(players, startingDealer, startingScore, roundCount, 1, 20, 10), ServerSettings.Default);
        state.StartRound(new RoundStartInfo(5));
        return state;
    }

    // A closed tanyao hand (234p 456p 678s 22s 46m waiting on 5m).
    private static Scoring Score(bool ron, bool dealer) => new TestHand
    {
        Concealed = "234p456p678s22s46m",
        Win = "5m",
        Ron = ron,
        Dealer = dealer,
        SeatWind = dealer ? Wind.East : Wind.South,
    }.Score();

    private static RoundFinishResult Ron(int winner, int loser, bool dealerWins = false, int riichiReturnIndex = -1) =>
        RoundFinishResult.Ron(new[] { Score(true, dealerWins) }, new[] { winner }, loser, 0, riichiReturnIndex);

    private static RoundFinishResult Tsumo(int winner, bool dealerWins) =>
        RoundFinishResult.Tsumo(Score(false, dealerWins), winner);

    private static RoundFinishResult EmptyWallDraw(int[] tenpai, int[]? nagashi = null) =>
        RoundFinishResult.Draw(tenpai, nagashi ?? Array.Empty<int>(), GameDrawType.EmptyWall);

    private static int[] Transfers(GameState state) => Enumerable.Range(0, 4).Select(i => state.GetPlayer(i).Transfer).ToArray();

    private static int[] Points(GameState state) => Enumerable.Range(0, 4).Select(i => state.GetPlayer(i).Points).ToArray();

    private static void NextRound(GameState state) => state.StartRound(new RoundStartInfo(5));

    // Dealer rotation and renchan

    [Fact]
    public void NonDealerWin_PassesTheDealerToTheNextSeat()
    {
        var state = NewGame();
        state.RoundFinished(Ron(winner: 1, loser: 2));
        NextRound(state);

        Assert.Equal(1, state.DealerIndex);
        Assert.Equal(1, state.CurrentRound);
        Assert.Equal(0, state.Renchan);
        Assert.Equal(Wind.East, state.GetPlayer(1).Wind);
        Assert.Equal(Wind.North, state.GetPlayer(0).Wind);
    }

    [Fact]
    public void DealerWin_IsRenchan_AndTheNextWinGetsThe300PointBonus()
    {
        var state = NewGame();
        state.RoundFinished(Tsumo(winner: 0, dealerWins: true));
        NextRound(state);

        Assert.Equal(0, state.DealerIndex);
        Assert.Equal(0, state.CurrentRound);
        Assert.Equal(1, state.Renchan);

        var result = Ron(winner: 2, loser: 3);
        state.RoundFinished(result);

        var points = result.Scores[0].TotalPoints;
        Assert.Equal(new[] { 0, 0, points + 300, -(points + 300) }, Transfers(state));
    }

    [Fact]
    public void DealerTsumo_EachOtherPlayerPaysTheHigherShare()
    {
        var state = NewGame();
        var result = Tsumo(winner: 0, dealerWins: true);
        state.RoundFinished(result);

        var score = result.Scores[0];
        Assert.Equal(new[] { score.TotalPoints, -score.TsumoPointsHigher, -score.TsumoPointsHigher, -score.TsumoPointsHigher }, Transfers(state));
        Assert.Equal(score.TotalPoints, 3 * score.TsumoPointsHigher);
    }

    [Fact]
    public void NonDealerTsumo_TheDealerPaysMore()
    {
        var state = NewGame();
        var result = Tsumo(winner: 2, dealerWins: false);
        state.RoundFinished(result);

        var score = result.Scores[0];
        Assert.Equal(new[] { -score.TsumoPointsHigher, -score.TsumoPointsLower, score.TotalPoints, -score.TsumoPointsLower }, Transfers(state));
        Assert.Equal(0, Transfers(state).Sum());
    }

    [Fact]
    public void ExhaustiveDraw_WithDealerTenpai_IsRenchan()
    {
        var state = NewGame();
        state.RoundFinished(EmptyWallDraw(new[] { 0, 2 }));
        NextRound(state);

        Assert.Equal(0, state.DealerIndex);
        Assert.Equal(1, state.Renchan);
    }

    [Fact]
    public void ExhaustiveDraw_WithDealerNoten_PassesTheDealer()
    {
        var state = NewGame();
        state.RoundFinished(EmptyWallDraw(new[] { 1 }));
        NextRound(state);

        Assert.Equal(1, state.DealerIndex);
        Assert.Equal(0, state.Renchan);
    }

    [Fact]
    public void AbortiveDraw_IsRenchan_WithoutPayments()
    {
        var state = NewGame();
        state.RoundFinished(RoundFinishResult.Draw(Array.Empty<int>(), Array.Empty<int>(), GameDrawType.FourWinds));
        NextRound(state);

        Assert.Equal(new[] { 0, 0, 0, 0 }, Transfers(state));
        Assert.Equal(0, state.DealerIndex);
        Assert.Equal(1, state.Renchan);
    }

    [Fact]
    public void RoundWind_TurnsSouthAfterEverySeatHasDealt()
    {
        var state = NewGame();
        for (var i = 0; i < 4; i++)
        {
            Assert.Equal(Wind.East, state.RoundWind);
            state.RoundFinished(EmptyWallDraw(Array.Empty<int>()));
            NextRound(state);
        }

        Assert.Equal(Wind.South, state.RoundWind);
        Assert.Equal(0, state.DealerIndex);
        Assert.Equal(4, state.CurrentRound);
        Assert.Equal(Wind.East, state.GetPlayer(0).Wind);
    }

    [Fact]
    public void Game_FinishesAfterRoundCountRounds_AndIgnoresFurtherRounds()
    {
        var state = NewGame(roundCount: 4);
        for (var i = 0; i < 4; i++)
        {
            Assert.False(state.GameIsFinished);
            Assert.NotNull(state.RoundFinished(EmptyWallDraw(Array.Empty<int>())));
            NextRound(state);
        }

        Assert.True(state.HanchanIsFinished);
        Assert.True(state.GameIsFinished);
        Assert.Equal(3, state.CurrentRound); // StartRound after the end changed nothing
        Assert.Null(state.RoundFinished(EmptyWallDraw(Array.Empty<int>())));
    }

    [Fact]
    public void Game_FinishesEarly_WhenAPlayerGoesBelowZero()
    {
        var state = NewGame(startingScore: 1000);
        var result = Ron(winner: 1, loser: 2);
        state.RoundFinished(result);

        Assert.True(state.GetPlayer(2).Points < 0);
        Assert.True(state.GameIsFinished);
    }

    // Riichi sticks

    [Fact]
    public void DeclareRiichi_PutsAStickOnTheTable()
    {
        var state = NewGame();
        state.DeclareRiichi(1);
        state.RoundFinished(EmptyWallDraw(new[] { 1 }));

        Assert.Equal(1, state.RiichiCount);
        Assert.False(state.ResetRiichi);
        Assert.Equal(StartingScore - 1000 + 3000, state.GetPlayer(1).Points);
    }

    // A riichi deposit leaves the player's points as it goes down, so a round's change is only what the round itself
    // paid (requested: a four-riichi draw showed -1,000 for everyone).
    [Fact]
    public void DeclareRiichi_TakesTheDepositFromTheShownPointsAtOnce()
    {
        var state = NewGame();
        state.DeclareRiichi(1);

        Assert.Equal(StartingScore - 1000, state.ShownPoints(1));
    }

    [Fact]
    public void AfterTheRound_ShownPointsAreThePoints_TheDepositTakenOnce()
    {
        var state = NewGame();
        state.DeclareRiichi(1);
        state.RoundFinished(EmptyWallDraw(new[] { 1 }));

        Assert.Equal(StartingScore - 1000 + 3000, state.ShownPoints(1));
    }

    [Fact]
    public void FourRiichiDraw_ChangesNobody()
    {
        var state = NewGame();
        for (var i = 0; i < 4; i++)
            state.DeclareRiichi(i);
        var score = state.RoundFinished(RoundFinishResult.Draw(Array.Empty<int>(), Array.Empty<int>(), GameDrawType.FourRiichi));

        Assert.Equal(new[] { 0, 0, 0, 0 }, score!.Players.Select(p => p.Change).ToArray());
    }

    [Fact]
    public void RiichiWinner_ChangeIsTheWinAndEveryStickOnTheTable()
    {
        var state = NewGame();
        state.DeclareRiichi(2);
        state.DeclareRiichi(3);
        var result = Ron(winner: 2, loser: 0);
        var score = state.RoundFinished(result);

        Assert.Equal(new[] { -result.Scores[0].TotalPoints, 0, result.Scores[0].TotalPoints + 2000, 0 },
            score!.Players.Select(p => p.Change).ToArray());
    }

    [Fact]
    public void RonOnTheRiichiDiscard_ChangeIsJustTheRonPayment()
    {
        var state = NewGame();
        state.DeclareRiichi(1);
        var result = Ron(winner: 2, loser: 1, riichiReturnIndex: 1);
        var score = state.RoundFinished(result);

        Assert.Equal(-result.Scores[0].TotalPoints, score!.Players[1].Change);
    }

    [Fact]
    public void RiichiSticks_CarryOverDraws_AndGoToTheNextWinner()
    {
        var state = NewGame();
        state.DeclareRiichi(1);
        state.RoundFinished(EmptyWallDraw(Array.Empty<int>()));
        NextRound(state);
        Assert.Equal(1, state.RiichiCount);

        state.DeclareRiichi(3);
        var result = Ron(winner: 2, loser: 0);
        state.RoundFinished(result);

        var points = result.Scores[0].TotalPoints;
        Assert.Equal(new[] { -points, 0, points + 2000, -1000 }, Transfers(state));
        Assert.True(state.ResetRiichi);

        NextRound(state);
        Assert.Equal(0, state.RiichiCount);
    }

    [Fact]
    public void RonOnTheRiichiDiscard_ReturnsTheStick()
    {
        var state = NewGame();
        state.DeclareRiichi(0);
        var result = Ron(winner: 1, loser: 0, riichiReturnIndex: 0);
        state.RoundFinished(result);

        var points = result.Scores[0].TotalPoints;
        Assert.Equal(new[] { -points, points, 0, 0 }, Transfers(state));
        Assert.Equal(0, state.RiichiCount);
    }

    [Fact]
    public void DeclareRiichi_IsIgnoredBelow1000Points()
    {
        var state = NewGame(startingScore: 900);
        state.DeclareRiichi(0);

        Assert.Equal(0, state.RiichiCount);
        Assert.Equal(0, state.GetPlayer(0).Transfer);
        Assert.All(state.CanRiichi(), Assert.False);
    }

    [Fact]
    public void GameEnd_GivesTheRemainingSticksToTheTopPlayer_AndAppliesUma()
    {
        var state = NewGame(roundCount: 1);
        state.DeclareRiichi(3);
        state.RoundFinished(EmptyWallDraw(Array.Empty<int>()));

        Assert.True(state.GameIsFinished);
        // Seats 0-2 tie on 25000; ties keep seat order from the starting dealer, so seat 0 is top.
        Assert.Equal(new[] { 26000, 25000, 25000, 24000 }, Points(state));
        Assert.Equal(new[] { 36, 5, -15, -26 }, Enumerable.Range(0, 4).Select(i => state.GetPlayer(i).Score));
    }

    // Exhaustive-draw payments

    [Theory]
    [InlineData(new int[0], new[] { 0, 0, 0, 0 })]
    [InlineData(new[] { 2 }, new[] { -1000, -1000, 3000, -1000 })]
    [InlineData(new[] { 0, 3 }, new[] { 1500, -1500, -1500, 1500 })]
    [InlineData(new[] { 0, 1, 2 }, new[] { 1000, 1000, 1000, -3000 })]
    [InlineData(new[] { 0, 1, 2, 3 }, new[] { 0, 0, 0, 0 })]
    public void ExhaustiveDraw_NotenPlayersPayTheTenpaiPlayers(int[] tenpai, int[] expected)
    {
        var state = NewGame();
        state.RoundFinished(EmptyWallDraw(tenpai));

        Assert.Equal(expected, Transfers(state));
    }

    // Nagashi mangan

    [Fact]
    public void Nagashi_ByTheDealer_IsPaid4000ByEveryone()
    {
        var state = NewGame();
        state.RoundFinished(EmptyWallDraw(Array.Empty<int>(), new[] { 0 }));

        Assert.Equal(new[] { 12000, -4000, -4000, -4000 }, Transfers(state));
    }

    [Fact]
    public void Nagashi_ByANonDealer_IsPaidLikeAManganTsumo_OnTopOfTenpaiPayments()
    {
        var state = NewGame();
        state.RoundFinished(EmptyWallDraw(new[] { 2 }, new[] { 1 }));

        // Nagashi: +8000 for seat 1, -4000 dealer, -2000 the others; tenpai: +3000 seat 2, -1000 the others.
        Assert.Equal(new[] { -5000, 7000, 1000, -3000 }, Transfers(state));
    }
}
