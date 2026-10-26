using MoonMahjong.Core;
using MoonMahjong.Table.Presentation;

namespace MoonMahjong.Net.Tests;

// Rebuilding a game on a client that missed part of it: late watchers, restarts, comebacks, a lost End, a broken view
// (docs/crew-play.md, "Routing" and "Pausing and leaving").
public sealed class CatchUpTests
{
    private const ulong Ash = 101, Rin = 102, Kai = 103;

    // The plugin's rules: no decision timer answers for anyone.
    private static readonly ServerSettings NoTimer = ServerSettings.Default with { UnlimitedDecisionTime = true };

    // Ash hosts in chair 0, Rin plays in chair 2.
    private static Crew Dealt(GameLength length = GameLength.EastSouth, ServerSettings? settings = null)
    {
        var crew = new Crew(settings);
        crew.Add(Ash, "Ash", seed: 5);
        crew.Add(Rin, "Rin");
        crew.Sit(Ash, 0);
        crew.Run(1);
        crew.Sit(Rin, 2);
        crew.Run(1);
        crew[Ash].Session.ChooseLength(length);
        crew[Ash].Session.Deal();
        crew.Run(1);
        return crew;
    }

    private static Crew AfterRounds(int rounds, ServerSettings? settings = null)
    {
        var crew = Dealt(settings: settings);
        for (var i = 0; i < 200_000 && crew[Ash].Player!.Results.Count < rounds; i++)
            crew.Run(0.1f);
        Assert.Equal(rounds, crew[Ash].Player!.Results.Count);
        crew.Run(0.5f);
        return crew;
    }

    // Where the game stands as a client sees it.
    private static string State(Member member)
    {
        var game = member.Session.Game!.Client.Game!;
        var points = string.Join(",", Enumerable.Range(0, 4).Select(i => game.GetPlayer(i).Points));
        return $"{game.RoundWind} {game.CurrentRound} dealer {game.DealerIndex} honba {game.Renchan} points {points}";
    }

    private static int TilesLeft(Member member) => member.Session.Game?.Client.Round?.RoundState.TilesRemaining ?? -1;

    [Fact]
    public void LateSpectator_AfterSeveralRounds_SeesTheSameGame()
    {
        var crew = AfterRounds(3);
        crew.Add(Kai, "Kai");
        crew.Run(3);

        Assert.Equal(State(crew[Ash]), State(crew[Kai]));
    }

    [Fact]
    public void LateSpectator_AfterSeveralRounds_SeesEveryResult()
    {
        var crew = AfterRounds(3);
        crew.Add(Kai, "Kai");
        crew.Run(3);

        Assert.Equal(crew[Ash].Player!.Results.Count, crew[Kai].Player!.Results.Count);
    }

    [Fact]
    public void PlayerRestarting_AfterSeveralRounds_IsBackInTheSameGame()
    {
        var crew = AfterRounds(3);
        crew.Restart(Rin);
        crew.Run(3);

        Assert.Equal(State(crew[Ash]), State(crew[Rin]));
    }

    [Fact]
    public void PlayerRestarting_PlaysOnToTheEnd()
    {
        var crew = AfterRounds(2, NoTimer);
        crew.Restart(Rin);
        crew.RunToTheEnd();

        Assert.True(crew[Ash].Session.Game!.Over, "the game stalled after Rin restarted");
    }

    [Fact]
    public void TableSessionCreatedLate_AsksForTheRunningGame()
    {
        var crew = Dealt();
        crew.Add(Kai, "Kai");
        crew[Kai].Router = new CrewTables(); // in the crew, but this table is not loaded yet
        crew.Run(3);

        crew.Restart(Kai);
        crew.Run(3);

        Assert.Equal(State(crew[Ash]), State(crew[Kai]));
    }

    [Fact]
    public void RepeatedAsks_AreAnsweredOnce()
    {
        var crew = Dealt();
        crew.Add(Kai, "Kai");
        crew.Run(3);
        for (var i = 0; i < 5; i++)
            crew[Kai].Link.Send(Ash, TableProtocol.Encode(Crew.Key, new AskMessage(0)));
        crew.StepOnly(Ash);

        var starts = crew[Kai].Link.Inbox.Count(m => TableProtocol.Decode(m.Data).Message is GameStartMessage);
        Assert.Equal(1, starts);
    }

    // The host's End can share a client frame with the final round's last messages.
    [Fact]
    public void LastResult_ArrivingWithTheEnd_IsStillShown()
    {
        var crew = Dealt(GameLength.EastOnly);
        var held = false;
        for (var i = 0; i < 300_000 && !(crew[Ash].Session.Game!.Over && crew[Rin].Session.Game!.Over); i++)
        {
            crew.StepOnly(Ash);
            held = !held && HoldsARoundEnd(crew[Rin]); // a round's end waits for the host's next frame, once
            if (!held)
                crew.StepOnly(Rin);
        }
        crew.Run(2);

        Assert.True(crew[Rin].Session.Game!.Client.Finished, "Rin never saw the final result");
    }

    private static bool HoldsARoundEnd(Member member) => member.Link.Inbox.Any(m =>
        TableProtocol.Decode(m.Data).Message is ToSeatMessage seat &&
        MessageCodec.DecodeServer(seat.Payload) is ServerMessageRon or ServerMessageTsumo or ServerMessageDraw);

    [Fact]
    public void PlayerLeavingMidDecision_WithoutATimer_IsPlayedAfterTheTimeout()
    {
        var crew = Dealt(GameLength.EastOnly, NoTimer);
        for (var i = 0; i < 3000; i++)
            crew.StepOnly(Ash); // Rin's frames stop: the game soon waits on one of Rin's decisions
        crew.Leave(Rin);
        crew.RunToTheEnd();

        Assert.True(crew[Ash].Session.Game!.Over, "the game stalled on the player who left");
    }

    [Fact]
    public void ReplacedPlayer_ComingBack_WatchesTheGame()
    {
        var crew = Dealt();
        crew.Run(5);
        crew.Leave(Rin);
        crew.Run(TableSession.PlayerTimeout + 5);
        crew.Add(Rin, "Rin");
        crew.Sit(Rin, 2);
        crew.Run(3);
        var before = TilesLeft(crew[Rin]);
        crew.Run(30);

        Assert.NotEqual(before, TilesLeft(crew[Rin]));
    }

    [Fact]
    public void ReplacedPlayer_ComingBack_IsToldTheyAreWatching()
    {
        var crew = Dealt();
        crew.Run(5);
        crew.Leave(Rin);
        crew.Run(TableSession.PlayerTimeout + 5);
        crew.Add(Rin, "Rin");
        crew.Run(3);

        Assert.Equal(-1, crew[Rin].Session.Game!.LocalSeat);
    }

    // A client that never got the host's End still learns the game is over: the host's table is open again.
    [Fact]
    public void MissedEnd_IsCaughtFromTheHostsNextLobby()
    {
        var crew = Dealt(GameLength.EastOnly);
        for (var i = 0; i < 300_000 && !crew[Ash].Session.Game!.Over; i++)
        {
            crew.StepOnly(Ash);
            DropEnds(crew[Rin]);
            crew.StepOnly(Rin);
        }
        for (var i = 0; i < 100; i++)
        {
            crew.StepOnly(Ash);
            DropEnds(crew[Rin]);
            crew.StepOnly(Rin);
        }

        Assert.True(crew[Rin].Session.Game!.Over);
    }

    private static void DropEnds(Member member)
    {
        var kept = member.Link.Inbox.Where(m => TableProtocol.Decode(m.Data).Message is not EndMessage).ToList();
        member.Link.Inbox.Clear();
        foreach (var message in kept)
            member.Link.Inbox.Enqueue(message);
    }

    // The plugin calls Recover when its view of the game failed: a player or watcher is replayed the game afresh.
    [Fact]
    public void BrokenView_IsReplayedAfresh()
    {
        var crew = Dealt();
        crew.Run(10);
        var broken = crew[Rin].Session.Game;

        crew[Rin].Session.Recover();
        crew.Run(2);

        Assert.NotSame(broken, crew[Rin].Session.Game);
    }

    [Fact]
    public void BrokenView_ReplayedAfresh_MatchesTheHost()
    {
        var crew = Dealt();
        crew.Run(10);

        crew[Rin].Session.Recover();
        crew.Run(2);

        Assert.Equal(State(crew[Ash]), State(crew[Rin]));
    }

    // ... and a host whose game failed ends it for everyone, so nobody waits on it.
    [Fact]
    public void BrokenHost_EndsTheGameForEveryone()
    {
        var crew = Dealt();
        crew.Run(10);

        crew[Ash].Session.Recover();
        crew.Run(1);

        Assert.Equal(EndReason.Cancelled, crew[Rin].Session.Game!.Ended);
    }
}
