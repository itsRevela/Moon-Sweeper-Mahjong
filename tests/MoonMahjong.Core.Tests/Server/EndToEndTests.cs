using MoonMahjong.Core;
using MoonMahjong.Core.Tests.Harness;
using Xunit.Abstractions;

namespace MoonMahjong.Core.Tests.Server;

// Whole games on the in-memory harness: GameState + RegularServerGameRound (RegularServerRoundState inside) with
// SimpleBots behind BotConnections, driven by a simulated clock.
public sealed class EndToEndTests
{
    // OpenRiichi's game defaults (ServerMenu.create_start_info).
    private const int StartingScore = 25000;
    private const int UmaHigher = 20;
    private const int UmaLower = 10;

    // A hanchan is ~10 rounds of ~600 steps; this leaves a wide margin before declaring a stall.
    private const int MaxSteps = 200_000;

    private readonly ITestOutputHelper output;

    public EndToEndTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    private static GameStartInfo StartInfo(int roundCount) => new(
        new[] { new GamePlayer(0, "East"), new GamePlayer(1, "South"), new GamePlayer(2, "West"), new GamePlayer(3, "North") },
        StartingDealer: 0,
        StartingScore: StartingScore,
        RoundCount: roundCount,
        HanchanCount: 1,
        UmaHigher: UmaHigher,
        UmaLower: UmaLower);

    private static List<HarnessSeat> BotSeats(int seed) =>
        Enumerable.Range(0, 4).Select(i => (HarnessSeat)new MirroredBotSeat(new SimpleBot(new RandomClass(seed * 31 + i)))).ToList();

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void FullHanchan_WithFourSimpleBots_FinishesWithConsistentScores(int seed)
    {
        var harness = RunGame(seed, roundCount: 8, BotSeats(seed));

        AssertFinalScores(harness);
    }

    [Theory]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    public void EastOnlyGame_WithFourSimpleBots_FinishesWithConsistentScores(int seed)
    {
        var harness = RunGame(seed, roundCount: 4, BotSeats(seed));

        AssertFinalScores(harness);
    }

    // Seat 0 is a ClientRoundState pressing whatever it is offered (ron, tsumo, kan, riichi, discard, and pon/chii
    // when takeCalls), so the client controller's decision paths run against the real server. Each game checks that
    // the client computed the same result as the server for every round; together they must cover every decision.
    [Fact]
    public void FullHanchans_WithClientRoundStateSeat_ClientMirrorsEveryRoundAndTakesEveryKindOfDecision()
    {
        var decisions = new HashSet<string>();

        for (var seed = 21; seed <= 28; seed++)
        {
            var client = new ClientSeat(takeCalls: seed % 3 == 0);
            var harness = RunGameWithClient(seed, roundCount: 8, client);
            AssertFinalScores(harness);
            decisions.UnionWith(client.Decisions.Keys);
        }

        output.WriteLine("decisions taken: " + string.Join(", ", decisions.OrderBy(d => d)));
        foreach (var expected in new[] { "discard", "continue", "ron", "tsumo", "riichi", "pon", "chii" })
            Assert.Contains(expected, decisions);
    }

    private GameHarness RunGameWithClient(int seed, int roundCount, ClientSeat client)
    {
        var seats = new List<HarnessSeat> { client };
        seats.AddRange(BotSeats(seed).Skip(1));

        var harness = RunGame(seed, roundCount, seats);

        Assert.Equal(harness.Rounds.Count, client.Results.Count);
        for (var i = 0; i < harness.Rounds.Count; i++)
            AssertSameResult(harness.Rounds[i].Result, client.Results[i]);

        output.WriteLine("client decisions: " + string.Join(", ", client.Decisions.OrderBy(d => d.Key).Select(d => $"{d.Key}={d.Value}")));
        return harness;
    }

    private GameHarness RunGame(int seed, int roundCount, List<HarnessSeat> seats)
    {
        var settings = ServerSettings.Default;
        var harness = new GameHarness(StartInfo(roundCount), settings, seed, seats);
        harness.RoundChecked = AssertRoundInvariants;

        harness.RunToEnd(MaxSteps);

        Assert.True(harness.Finished, $"Game did not finish within {MaxSteps} steps (seed {seed})");
        Assert.True(harness.State.GameIsFinished);
        // Every seat answered well within the decision time, so the server never had to wait for a timeout.
        Assert.True(harness.LongestSilence < 10, $"The server went {harness.LongestSilence:F1}s without sending anything");

        foreach (var seat in seats)
            for (var p = 0; p < 4; p++)
            {
                Assert.Equal(harness.State.GetPlayer(p).Points, seat.MirroredState.GetPlayer(p).Points);
                Assert.Equal(harness.State.GetPlayer(p).Score, seat.MirroredState.GetPlayer(p).Score);
            }

        output.WriteLine($"seed {seed}: {harness.Rounds.Count} rounds in {harness.Steps} steps; " +
            string.Join(" ", harness.Rounds.Select(Describe)));
        output.WriteLine("points: " + string.Join(", ", Enumerable.Range(0, 4).Select(p => harness.State.GetPlayer(p).Points)) +
            "; score: " + string.Join(", ", Enumerable.Range(0, 4).Select(p => harness.State.GetPlayer(p).Score)));

        return harness;
    }

    private static string Describe(RoundRecord r) => r.Result.Result switch
    {
        RoundFinishResult.RoundResultEnum.Ron => "ron(" + string.Join("+", r.Result.WinnerIndices) + "<-" + r.Result.LoserIndex + ")",
        RoundFinishResult.RoundResultEnum.Tsumo => "tsumo(" + r.Result.WinnerIndices[0] + ")",
        _ => r.Result.DrawType.ToString(),
    };

    private static void AssertRoundInvariants(RoundRecord record)
    {
        AssertValidResult(record.Result);

        // Riichi sticks still on the table count towards the total; once a winner (or, at the end of the game,
        // the top player) has taken them, ResetRiichi is set and they are part of someone's points.
        var points = record.Score.Players.Sum(p => p.Points);
        var sticks = record.ResetRiichi ? 0 : 1000 * record.RiichiCount;
        Assert.Equal(4 * StartingScore, points + sticks);
    }

    private static void AssertValidResult(RoundFinishResult result)
    {
        switch (result.Result)
        {
            case RoundFinishResult.RoundResultEnum.Ron:
                Assert.NotEmpty(result.WinnerIndices);
                Assert.Equal(result.WinnerIndices.Length, result.Scores.Length);
                Assert.Equal(result.WinnerIndices.Length, result.WinnerIndices.Distinct().Count());
                Assert.InRange(result.LoserIndex, 0, 3);
                Assert.DoesNotContain(result.LoserIndex, result.WinnerIndices);
                foreach (var score in result.Scores)
                {
                    Assert.True(score.Valid);
                    Assert.True(score.HasValidYaku() || score.Yakuman > 0);
                    Assert.True(score.RonPoints > 0 && score.TotalPoints == score.RonPoints);
                }
                break;

            case RoundFinishResult.RoundResultEnum.Tsumo:
                Assert.Single(result.WinnerIndices);
                var tsumo = Assert.Single(result.Scores);
                Assert.True(tsumo.Valid);
                Assert.True(tsumo.HasValidYaku() || tsumo.Yakuman > 0);
                Assert.True(tsumo.TotalPoints > 0 && tsumo.RonPoints == 0);
                break;

            case RoundFinishResult.RoundResultEnum.Draw:
                Assert.NotEqual(GameDrawType.None, result.DrawType);
                Assert.Equal(result.TenpaiIndices.Length, result.TenpaiIndices.Distinct().Count());
                Assert.All(result.TenpaiIndices, i => Assert.InRange(i, 0, 3));
                if (result.DrawType != GameDrawType.EmptyWall)
                    Assert.Empty(result.NagashiIndices);
                break;

            default:
                Assert.Fail("Round finished without a result: " + result.Result);
                break;
        }
    }

    private static void AssertSameResult(RoundFinishResult server, RoundFinishResult client)
    {
        Assert.Equal(server.Result, client.Result);
        Assert.Equal(server.WinnerIndices, client.WinnerIndices);
        Assert.Equal(server.Scores.Select(s => s.TotalPoints), client.Scores.Select(s => s.TotalPoints));
        Assert.Equal(server.TenpaiIndices, client.TenpaiIndices);
        Assert.Equal(server.NagashiIndices, client.NagashiIndices);
        Assert.Equal(server.DrawType, client.DrawType);
        if (server.Result == RoundFinishResult.RoundResultEnum.Ron)
        {
            Assert.Equal(server.LoserIndex, client.LoserIndex);
            Assert.Equal(server.RiichiReturnIndex, client.RiichiReturnIndex);
        }
    }

    // Final ranking: GameState.CalculateScore orders players by points (ties keep seat order from the starting
    // dealer), rounds everyone but the top player to the nearest 1000 against a 30000 return, gives the top player
    // the balance, then adds uma +20/+10/-10/-20.
    private static void AssertFinalScores(GameHarness harness)
    {
        var state = harness.State;
        var ranked = Enumerable.Range(0, 4)
            .Select(i => state.GetPlayer((state.StartingDealerIndex + i) % 4))
            .Select((player, seatOrder) => (player, seatOrder))
            .OrderByDescending(x => x.player.Points)
            .ThenBy(x => x.seatOrder)
            .Select(x => x.player)
            .ToList();

        var uma = new[] { UmaHigher, UmaLower, -UmaLower, -UmaHigher };
        var raw = new int[4];
        for (var i = 1; i < 4; i++)
        {
            var p = ranked[i].Points;
            p += p > 0 ? 500 : -500;
            raw[i] = p / 1000 - StartingScore / 1000 - 5;
        }
        raw[0] = -(raw[1] + raw[2] + raw[3]);

        for (var i = 0; i < 4; i++)
            Assert.Equal(raw[i] + uma[i], ranked[i].Score);

        Assert.Equal(0, ranked.Sum(p => p.Score));
        for (var i = 1; i < 4; i++)
            Assert.True(ranked[i - 1].Score >= ranked[i].Score, "Final scores must follow the ranking");

        // The remaining riichi sticks went to the top player, so the table holds exactly the starting points.
        Assert.Equal(4 * StartingScore, ranked.Sum(p => p.Points));
    }
}
