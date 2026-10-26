using MoonMahjong.Core;
using MoonMahjong.Core.Tests.Harness;

namespace MoonMahjong.Core.Tests.Server;

// The round driver: action handling and call resolution on fixed walls (ScriptedServerRoundState), and the decision
// timer's default actions on RegularServerRoundState.
public sealed class ServerRoundStateTests
{
    private static ScriptedServerRoundState StartedRound(Tile[] wall, ServerSettings? settings = null)
    {
        var round = new ScriptedServerRoundState(settings ?? ServerSettings.Default, wall);
        round.StartRound();
        return round;
    }

    private static Tile HandTile(ScriptedServerRoundState round, int seat, TileType type) => WallBuilder.Find(round.Hands[seat], type);

    // The dealer discards its 5m and the round asks the other players for call decisions.
    private static ScriptedServerRoundState AfterDealerDiscardsFiveMan(Tile[] wall, ServerSettings? settings = null)
    {
        var round = StartedRound(wall, settings);
        Assert.Equal(new[] { 0 }, round.TurnDecisions);

        round.Act(0, new TileDiscardClientAction(HandTile(round, 0, TileType.Man5).ID));
        return round;
    }

    [Fact]
    public void Discard_FromAPlayerWhoseTurnItIsNot_IsIgnored()
    {
        var round = StartedRound(CallWalls.ChiiPonRon());

        round.Act(1, new TileDiscardClientAction(HandTile(round, 1, TileType.Man4).ID));

        Assert.Empty(round.Discards);
        Assert.Empty(round.CallDecisions);
    }

    [Fact]
    public void Discard_FromThePlayerWhoseTurnItIs_IsAppliedAndOffersCalls()
    {
        var round = AfterDealerDiscardsFiveMan(CallWalls.ChiiPonRon());

        Assert.Equal(TileType.Man5, Assert.Single(round.Discards).TileType);
        Assert.Equal(new[] { 1, 2, 3 }, round.CallDecisions);
    }

    [Fact]
    public void InvalidPon_IsIgnoredAndThePlayerCanStillCall()
    {
        var round = AfterDealerDiscardsFiveMan(CallWalls.ChiiPonRon());

        round.Act(1, new PonClientAction()); // Seat 1 has no pair of 5m
        round.Act(1, new ChiiClientAction(HandTile(round, 1, TileType.Man4).ID, HandTile(round, 1, TileType.Man6).ID));
        round.Act(2, new NoCallClientAction());
        round.Act(3, new NoCallClientAction());

        Assert.Equal(new[] { 1 }, round.Chiis);
        Assert.Empty(round.Pons);
    }

    [Fact]
    public void RonBeatsPonAndChii()
    {
        var round = AfterDealerDiscardsFiveMan(CallWalls.ChiiPonRon());

        round.Act(1, new ChiiClientAction(HandTile(round, 1, TileType.Man4).ID, HandTile(round, 1, TileType.Man6).ID));
        round.Act(2, new PonClientAction());
        Assert.Null(round.Ron); // Still waiting for seat 3
        round.Act(3, new RonClientAction());

        Assert.Equal(new[] { 3 }, round.Ron!.Value.Winners);
        Assert.Equal(0, round.Ron.Value.Discarder);
        Assert.Empty(round.Pons);
        Assert.Empty(round.Chiis);
    }

    [Fact]
    public void PonBeatsChii_AndThePonCallerTakesTheTurn()
    {
        var round = AfterDealerDiscardsFiveMan(CallWalls.ChiiPonRon());

        round.Act(1, new ChiiClientAction(HandTile(round, 1, TileType.Man4).ID, HandTile(round, 1, TileType.Man6).ID));
        round.Act(2, new PonClientAction());
        round.Act(3, new NoCallClientAction());

        Assert.Equal(new[] { 2 }, round.Pons);
        Assert.Empty(round.Chiis);
        Assert.Equal(new[] { 0, 2 }, round.TurnDecisions);
    }

    [Fact]
    public void MultipleRon_EveryCallerWins()
    {
        var round = AfterDealerDiscardsFiveMan(CallWalls.DoubleRon(), ServerSettings.Default with { MultipleRon = true });

        round.Act(1, new RonClientAction());
        round.Act(3, new RonClientAction());

        Assert.Equal(new[] { 1, 3 }, round.Ron!.Value.Winners);
        Assert.Equal(2, round.Ron.Value.Scores.Length);
        Assert.All(round.Ron.Value.Scores, s => Assert.True(s.TotalPoints > 0));
    }

    [Fact]
    public void TripleRon_WithTripleRonDraw_EndsTheRoundInADraw()
    {
        var round = AfterDealerDiscardsFiveMan(CallWalls.TripleRon(), ServerSettings.Default with { TripleRonDraw = true });

        round.Act(1, new RonClientAction());
        round.Act(2, new RonClientAction());
        round.Act(3, new RonClientAction());

        Assert.Null(round.Ron);
        Assert.Equal(GameDrawType.TripleRon, round.Draw!.Value.Type);
        Assert.Equal(new[] { 1, 2, 3 }, round.Draw.Value.Indices);
    }

    [Fact]
    public void DefaultAction_DuringCalls_KeepsTheDecisionsAlreadyMade()
    {
        var round = AfterDealerDiscardsFiveMan(CallWalls.ChiiPonRon());

        round.Act(2, new PonClientAction());
        round.TimeOut(); // Seats 1 and 3 never answered

        Assert.Equal(new[] { 2 }, round.Pons);
    }

    [Fact]
    public void DefaultAction_OnTurn_DiscardsTheDrawnTile()
    {
        var round = StartedRound(CallWalls.ChiiPonRon());

        round.TimeOut();

        // The dealer's draw (5p) is the newest tile, which is the default discard.
        Assert.Equal(TileType.Pin5, Assert.Single(round.Discards).TileType);
    }

    // RegularServerRoundState with a real decision timer: nobody acts, so the round must move on by itself.
    [Fact]
    public void TurnDecision_TimesOut_IntoTheDefaultDiscard()
    {
        var settings = ServerSettings.Default;
        var timings = AnimationTimings.Default(settings);
        var round = new RegularServerRoundState(settings, Wind.East, 0, 5, new RandomClass(7), new[] { true, true, true, true }, timings);

        Tile? drawn = null;
        float? decisionAt = null, discardAt = null;
        Tile? discarded = null;
        var time = 0f;

        round.GameDrawTile += (p, tile, _) => drawn ??= p == 0 ? tile : null;
        round.GameGetTurnDecision += _ => decisionAt ??= time;
        round.GameDiscardTile += tile =>
        {
            discarded ??= tile;
            discardAt ??= time;
        };

        round.Start(time);
        while (discardAt == null && time < 60)
            round.Process(time += 0.1f);

        Assert.NotNull(decisionAt);
        Assert.NotNull(discardAt);
        Assert.InRange(discardAt!.Value - decisionAt!.Value, timings.DecisionTime - 0.01f, timings.DecisionTime + 0.5f);
        Assert.Same(drawn, discarded);
    }

    // The mod's default for real players: no per-turn time limit, so an idle turn decision waits indefinitely.
    [Fact]
    public void TurnDecision_WithUnlimitedDecisionTime_NeverTimesOut()
    {
        var settings = ServerSettings.Default with { UnlimitedDecisionTime = true };
        var round = new RegularServerRoundState(settings, Wind.East, 0, 5, new RandomClass(7), new[] { true, true, true, true }, AnimationTimings.Default(settings));
        var decisions = 0;
        var discards = 0;
        round.GameGetTurnDecision += _ => decisions++;
        round.GameDiscardTile += _ => discards++;

        var time = 0f;
        round.Start(time);
        while (time < 600)
            round.Process(time += 0.1f);

        Assert.Equal(1, decisions);
        Assert.Equal(0, discards);
    }

    [Fact]
    public void CallDecision_WithUnlimitedDecisionTime_WaitsForTheCaller()
    {
        var settings = ServerSettings.Default with { UnlimitedDecisionTime = true };
        var timings = AnimationTimings.Default(settings);

        // A seed whose first discard can be called; the dealer discards at once, then nobody answers the call.
        for (var seed = 1; seed < 200; seed++)
        {
            var round = new RegularServerRoundState(settings, Wind.East, 0, 5, new RandomClass(seed), new[] { true, true, true, true }, timings);
            var callDecisions = 0;
            var callsFinished = 0;
            var time = 0f;
            Tile? dealerDraw = null;
            round.GameDrawTile += (player, tile, _) => dealerDraw ??= player == 0 ? tile : null;
            round.GameGetTurnDecision += player =>
            {
                // The dealer discards the tile it just drew (always legal); then the call decision waits.
                if (player == 0 && dealerDraw != null)
                    round.BufferAction(new ClientServerAction(0, new TileDiscardClientAction(dealerDraw.ID)));
            };
            round.GameGetCallDecision += _ => callDecisions++;
            round.GameCallsFinished += () => callsFinished++;

            round.Start(time);
            while (time < 600 && callsFinished == 0 && (callDecisions == 0 || time < 300))
                round.Process(time += 0.1f);

            if (callDecisions == 0)
                continue;

            Assert.Equal(0, callsFinished);
            return;
        }

        Assert.Fail("No seed produced a callable first discard");
    }

    [Fact]
    public void ServerSettings_KeepOpenRiichisTimedDecisionsByDefault()
    {
        Assert.False(ServerSettings.Default.UnlimitedDecisionTime);
    }

    [Fact]
    public void CallDecision_TimesOut_IntoNoCall()
    {
        var settings = ServerSettings.Default;
        var timings = AnimationTimings.Default(settings);

        // Find a seed whose first discard (the dealer's timed-out tsumogiri) can be called by someone.
        for (var seed = 1; seed < 200; seed++)
        {
            var round = new RegularServerRoundState(settings, Wind.East, 0, 5, new RandomClass(seed), new[] { true, true, true, true }, timings);

            float? callDecisionAt = null, finishedAt = null;
            var nextDrawer = -1;
            var time = 0f;

            round.GameGetCallDecision += _ => callDecisionAt ??= time;
            round.GameCallsFinished += () => finishedAt ??= time;
            round.GameDrawTile += (p, _, _) =>
            {
                if (finishedAt != null && nextDrawer == -1)
                    nextDrawer = p;
            };

            round.Start(time);
            while (finishedAt == null && time < 60)
                round.Process(time += 0.1f);

            if (callDecisionAt == null)
                continue;

            Assert.NotNull(finishedAt);
            Assert.InRange(finishedAt!.Value - callDecisionAt.Value, timings.DecisionTime - 0.01f, timings.DecisionTime + 0.5f);
            Assert.Equal(1, nextDrawer); // Nobody called, so the turn passed to the dealer's right
            return;
        }

        Assert.Fail("No seed produced a callable first discard");
    }
}
