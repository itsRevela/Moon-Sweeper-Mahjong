using MoonMahjong.Core;

namespace MoonMahjong.Core.Tests.Server;

// The production path for a human seat: ClientGame behind a LocalClientSeat, driven only through the events and
// methods the plugin's UI uses (RoundCreated, the round's Set*State events, Client* calls, ScoresAcknowledged).
public sealed class MatchHostTests
{
    private const float Step = 0.1f;
    private const int MaxSteps = 400_000;

    private static GameStartInfo StartInfo(int roundCount) => new(
        Enumerable.Range(0, 4).Select(i => new GamePlayer(i, "P" + i)).ToArray(),
        StartingDealer: 0, StartingScore: 25000, RoundCount: roundCount, HanchanCount: 1, UmaHigher: 20, UmaLower: 10);

    private static (MatchHost Host, AutoPlayer Player) CreateMatch(int seed, int roundCount, bool acknowledge = true,
        ServerSettings? settings = null, bool answerTurns = true, bool answerCalls = true)
    {
        var client = new ClientGame();
        var player = new AutoPlayer(client, acknowledge, answerTurns, answerCalls);
        var seats = new List<MatchSeat> { new LocalClientSeat(client) };
        seats.AddRange(Enumerable.Range(1, 3).Select(i => new BotSeat(new SimpleBot(new RandomClass(seed * 7 + i)))));
        return (new MatchHost(StartInfo(roundCount), settings ?? ServerSettings.Default, new RandomClass(seed), seats, 0f), player);
    }

    private static float RunUntil(MatchHost host, AutoPlayer player, Func<bool> done)
    {
        var time = 0f;
        for (var steps = 0; !done() && steps < MaxSteps; steps++)
        {
            time += Step;
            host.Process(time);
            player.Poll();
        }
        Assert.True(done(), "the condition was never met within the step bound");
        return time;
    }

    private static void RunToTheEnd(MatchHost host, AutoPlayer player, float from)
    {
        var time = from;
        for (var steps = 0; !host.Finished && steps < MaxSteps; steps++)
        {
            time += Step;
            host.Process(time);
            player.Poll();
        }
    }

    // The plugin's default (DecisionTimeLimit 0): no decision timer ever answers for a player.
    private static readonly ServerSettings Unlimited = ServerSettings.Default with { UnlimitedDecisionTime = true };

    [Fact]
    public void DisconnectedSeat_HoldingATurnDecision_IsPlayedWithoutATimer()
    {
        var (host, player) = CreateMatch(seed: 1, roundCount: 4, settings: Unlimited, answerTurns: false);
        var time = RunUntil(host, player, () => player.IgnoredTurns > 0);
        time = RunFor(host, player, time, seconds: 30);

        host.SeatDisconnected(0);
        RunToTheEnd(host, player, time);

        Assert.True(host.Finished, "the game stalled on the disconnected seat's turn");
    }

    [Fact]
    public void DisconnectedSeat_HoldingACallDecision_IsPlayedWithoutATimer()
    {
        var (host, player) = CreateMatch(seed: 1, roundCount: 4, settings: Unlimited, answerCalls: false);
        var time = RunUntil(host, player, () => player.IgnoredCalls > 0);
        time = RunFor(host, player, time, seconds: 30);

        host.SeatDisconnected(0);
        RunToTheEnd(host, player, time);

        Assert.True(host.Finished, "the game stalled on the disconnected seat's call");
    }

    [Fact]
    public void UnansweredDecision_WithoutATimer_WaitsForThePlayer()
    {
        var (host, player) = CreateMatch(seed: 1, roundCount: 4, settings: Unlimited, answerTurns: false);
        var time = RunUntil(host, player, () => player.IgnoredTurns > 0);

        RunFor(host, player, time, seconds: 600);

        Assert.Empty(player.Results);
    }

    private static int Run(MatchHost host, AutoPlayer player)
    {
        var steps = 0;
        var time = 0f;
        while (!host.Finished && steps < MaxSteps)
        {
            time += Step;
            host.Process(time);
            player.Poll();
            steps++;
        }
        return steps;
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void GameWithLocalHumanSeat_FinishesWithEveryRoundAgreeingWithTheHost(int seed)
    {
        var (host, player) = CreateMatch(seed, roundCount: 4);
        var hostResults = new List<RoundFinishResult>();
        host.RoundFinished += (result, _) => hostResults.Add(result);

        Run(host, player);

        Assert.True(host.Finished, "the game did not finish within the step bound");
        Assert.Equal(hostResults.Count, player.Results.Count);
        for (var i = 0; i < hostResults.Count; i++)
        {
            Assert.Equal(hostResults[i].Result, player.Results[i].Result);
            Assert.Equal(hostResults[i].WinnerIndices, player.Results[i].WinnerIndices);
        }
    }

    // Requested: a round's result stays up until every person at the table has pressed Next round; OpenRiichi moved
    // on by itself after the round-end delay.
    private static float RunUntilFirstResult(MatchHost host, AutoPlayer player)
    {
        var time = 0f;
        for (var steps = 0; player.Results.Count == 0 && steps < MaxSteps; steps++)
        {
            time += Step;
            host.Process(time);
            player.Poll();
        }
        Assert.Single(player.Results);
        return time;
    }

    private static float RunFor(MatchHost host, AutoPlayer player, float from, float seconds)
    {
        var time = from;
        while (time < from + seconds)
        {
            time += Step;
            host.Process(time);
            player.Poll();
        }
        return time;
    }

    [Fact]
    public void RoundEnd_WaitsForTheHumanSeatHoweverLongItTakes()
    {
        var (host, player) = CreateMatch(seed: 1, roundCount: 4, acknowledge: false);
        var time = RunUntilFirstResult(host, player);

        RunFor(host, player, time, seconds: 600);

        Assert.Equal(1, player.RoundsCreated);
    }

    [Fact]
    public void RoundEnd_StartsTheNextRoundOnceTheHumanSeatAcknowledges()
    {
        var (host, player) = CreateMatch(seed: 1, roundCount: 4, acknowledge: false);
        var time = RunUntilFirstResult(host, player);
        time = RunFor(host, player, time, seconds: 60);

        player.Client.ScoresAcknowledged();
        RunFor(host, player, time, seconds: 1);

        Assert.Equal(2, player.RoundsCreated);
    }

    [Fact]
    public void RoundEnd_DoesNotWaitForADisconnectedSeat()
    {
        var (host, player) = CreateMatch(seed: 1, roundCount: 4, acknowledge: false);
        var time = RunUntilFirstResult(host, player);

        host.SeatDisconnected(0);
        RunFor(host, player, time, seconds: 1);

        Assert.Equal(2, player.RoundsCreated);
    }

    // Crew play: anyone with the mod who isn't seated watches through a spectator feed (seat -1, public information).
    private sealed class SpectatorFeed : IServerRoundPlayer
    {
        public SpectatorFeed(ClientGame client, bool seesAllHands = false)
        {
            Client = client;
            SeesAllHands = seesAllHands;
        }

        public ClientGame Client { get; }
        public List<ServerMessage> Received { get; } = new();
        public bool IsSpectator => true;
        public bool SeesAllHands { get; }
        public bool IsDisconnected => false;

        public void Send(ServerMessage message)
        {
            Received.Add(message);
            Client.Receive(message);
        }
    }

    private static int AssignmentsBeforeTheFirstDiscard(bool seesAllHands)
    {
        var feed = new SpectatorFeed(new ClientGame(), seesAllHands);
        var client = new ClientGame();
        var player = new AutoPlayer(client);
        var seats = new List<MatchSeat> { new LocalClientSeat(client) };
        seats.AddRange(Enumerable.Range(1, 3).Select(i => new BotSeat(new SimpleBot(new RandomClass(i)))));
        var host = new MatchHost(StartInfo(4), ServerSettings.Default, new RandomClass(3), seats, 0f, new[] { feed });
        var time = 0f;
        for (var steps = 0; !feed.Received.OfType<ServerMessageTileDiscard>().Any() && steps < MaxSteps; steps++)
        {
            time += Step;
            host.Process(time);
            player.Poll();
        }
        return feed.Received.TakeWhile(m => m is not ServerMessageTileDiscard).OfType<ServerMessageTileAssignment>().Count();
    }

    [Fact]
    public void PublicSpectator_IsToldOnlyPublicTilesBeforeTheFirstDiscard()
    {
        // The dora indicator, and the discarded tile itself (revealed just before its discard message).
        Assert.Equal(2, AssignmentsBeforeTheFirstDiscard(seesAllHands: false));
    }

    [Fact]
    public void OpenRiichiSpectator_StillSeesEveryHand()
    {
        Assert.True(AssignmentsBeforeTheFirstDiscard(seesAllHands: true) >= 4 * 13, "an all-seeing spectator is told every dealt tile");
    }

    [Fact]
    public void Spectator_IsToldTheGameStartedWithNoSeat()
    {
        var spectator = new ClientGame();
        var client = new ClientGame();
        var seats = new List<MatchSeat> { new LocalClientSeat(client) };
        seats.AddRange(Enumerable.Range(1, 3).Select(i => new BotSeat(new SimpleBot(new RandomClass(i)))));
        _ = new MatchHost(StartInfo(4), ServerSettings.Default, new RandomClass(1), seats, 0f, new[] { new SpectatorFeed(spectator) });

        spectator.Process();

        Assert.Equal(-1, spectator.PlayerIndex);
    }

    [Fact]
    public void Spectator_SeesEveryRoundEndAsThePlayersDo()
    {
        var spectator = new ClientGame();
        var spectatorResults = new List<RoundFinishResult>();
        spectator.RoundEnded += (result, _) => spectatorResults.Add(result);
        var client = new ClientGame();
        var player = new AutoPlayer(client);
        var seats = new List<MatchSeat> { new LocalClientSeat(client) };
        seats.AddRange(Enumerable.Range(1, 3).Select(i => new BotSeat(new SimpleBot(new RandomClass(7 * 2 + i)))));
        var host = new MatchHost(StartInfo(4), ServerSettings.Default, new RandomClass(2), seats, 0f, new[] { new SpectatorFeed(spectator) });

        var time = 0f;
        for (var steps = 0; !host.Finished && steps < MaxSteps; steps++)
        {
            time += Step;
            host.Process(time);
            spectator.Process();
            player.Poll();
        }

        Assert.True(host.Finished);
        Assert.Equal(player.Results.Select(r => (r.Result, string.Join(",", r.WinnerIndices))),
            spectatorResults.Select(r => (r.Result, string.Join(",", r.WinnerIndices))));
    }

    // A watcher or player who joins late is replayed the whole game at once (crew play): every round must still be read
    // in order, even though one round's end and the next round's start arrive in the same batch.
    [Fact]
    public void Spectator_CatchingUpOnAWholeGameInOneBatch_SeesEveryRoundEnd()
    {
        var spectator = new ClientGame();
        var spectatorResults = new List<RoundFinishResult>();
        spectator.RoundEnded += (result, _) => spectatorResults.Add(result);
        var client = new ClientGame();
        var player = new AutoPlayer(client);
        var seats = new List<MatchSeat> { new LocalClientSeat(client) };
        seats.AddRange(Enumerable.Range(1, 3).Select(i => new BotSeat(new SimpleBot(new RandomClass(7 * 2 + i)))));
        var host = new MatchHost(StartInfo(4), ServerSettings.Default, new RandomClass(2), seats, 0f, new[] { new SpectatorFeed(spectator) });
        RunToTheEnd(host, player, 0f);

        spectator.Process();

        Assert.Equal(player.Results.Select(r => (r.Result, string.Join(",", r.WinnerIndices))),
            spectatorResults.Select(r => (r.Result, string.Join(",", r.WinnerIndices))));
    }

    [Fact]
    public void Spectator_CatchingUpOnAWholeGameInOneBatch_EndsWithTheHostsPoints()
    {
        var spectator = new ClientGame();
        var client = new ClientGame();
        var player = new AutoPlayer(client);
        var seats = new List<MatchSeat> { new LocalClientSeat(client) };
        seats.AddRange(Enumerable.Range(1, 3).Select(i => new BotSeat(new SimpleBot(new RandomClass(7 * 3 + i)))));
        var host = new MatchHost(StartInfo(4), ServerSettings.Default, new RandomClass(3), seats, 0f, new[] { new SpectatorFeed(spectator) });
        RunToTheEnd(host, player, 0f);

        spectator.Process();

        Assert.Equal(Enumerable.Range(0, 4).Select(i => client.Game!.GetPlayer(i).Points),
            Enumerable.Range(0, 4).Select(i => spectator.Game!.GetPlayer(i).Points));
    }

    [Fact]
    public void ClientGame_KnowsTheRulesTheHostDealtWith()
    {
        var client = new ClientGame();
        var seats = new List<MatchSeat> { new LocalClientSeat(client) };
        seats.AddRange(Enumerable.Range(1, 3).Select(i => new BotSeat(new SimpleBot(new RandomClass(i)))));
        var rules = ServerSettings.Default with { UnlimitedDecisionTime = true, OpenRiichi = true };
        _ = new MatchHost(StartInfo(4), rules, new RandomClass(1), seats, 0f);

        client.Process();

        Assert.Equal(rules, client.Settings);
    }

    [Fact]
    public void ClientGame_MirrorsTheHostsFinalPoints()
    {
        var (host, player) = CreateMatch(seed: 5, roundCount: 4);

        Run(host, player);

        for (var i = 0; i < 4; i++)
            Assert.Equal(host.State.GetPlayer(i).Points, player.Client.Game!.GetPlayer(i).Points);
    }

    [Fact]
    public void ScoresAcknowledged_SendsMenuReadyBetweenRounds()
    {
        var (host, player) = CreateMatch(seed: 9, roundCount: 4);

        Run(host, player);

        Assert.True(player.ReadyMessages >= player.Results.Count - 1, $"{player.ReadyMessages} ready messages for {player.Results.Count} rounds");
    }

    [Fact]
    public void ClientGame_FinishesAfterTheLastRoundIsAcknowledged()
    {
        var (host, player) = CreateMatch(seed: 4, roundCount: 4);

        Run(host, player);

        Assert.True(player.Client.Finished);
    }

    [Fact]
    public void ScoresAcknowledged_WithoutAFinishedRound_SendsNothing()
    {
        var client = new ClientGame();
        var sent = 0;
        client.MessageSent += _ => sent++;

        client.ScoresAcknowledged();

        Assert.Equal(0, sent);
    }

    [Fact]
    public void ClientGame_RejectsRoundMessagesBeforeTheGameStarts()
    {
        var client = new ClientGame();
        client.Receive(new ServerMessageRoundStart(new RoundStartInfo(5)));

        Assert.Throws<InvalidOperationException>(client.Process);
    }

    [Fact]
    public void MatchHost_RequiresFourSeats()
    {
        var seats = new List<MatchSeat> { new BotSeat(new SimpleBot(new RandomClass(1))) };

        Assert.Throws<ArgumentException>(() => new MatchHost(StartInfo(4), ServerSettings.Default, new RandomClass(1), seats, 0f));
    }

    // Plays seat 0 the way a hurried human would: win when offered, otherwise pass on calls and discard the newest
    // tile. It reacts to the same UI-state events the plugin's buttons do.
    private sealed class AutoPlayer
    {
        private ClientRoundState? round;
        private bool timer, continueEnabled, ron, tsumo;
        private List<TileSelectionGroup>? groups;

        private readonly bool answerTurns, answerCalls;

        // acknowledge: press Next round as soon as a result is shown (false: leave that to the test).
        // answerTurns / answerCalls false: sit on that kind of decision forever (a player who walked away).
        public AutoPlayer(ClientGame client, bool acknowledge = true, bool answerTurns = true, bool answerCalls = true)
        {
            Client = client;
            this.answerTurns = answerTurns;
            this.answerCalls = answerCalls;
            client.RoundCreated += (r, _, _) =>
            {
                RoundsCreated++;
                Attach(r);
            };
            client.RoundEnded += (result, _) =>
            {
                Results.Add(result);
                if (acknowledge)
                    client.ScoresAcknowledged();
            };
            client.MessageSent += message =>
            {
                if (message is ClientMessageMenuReady)
                    ReadyMessages++;
            };
        }

        public ClientGame Client { get; }
        public List<RoundFinishResult> Results { get; } = new();
        public int ReadyMessages { get; private set; }
        public int RoundsCreated { get; private set; }
        public int IgnoredTurns { get; private set; }
        public int IgnoredCalls { get; private set; }

        private void Attach(ClientRoundState r)
        {
            round = r;
            r.SetTimerState += v => timer = v;
            r.SetContinueState += v => continueEnabled = v;
            r.SetRonState += v => ron = v;
            r.SetTsumoState += v => tsumo = v;
            r.SetTileSelectGroups += v => groups = v;
        }

        // Like a person, answer after the frame's messages are handled: ClientRoundState raises SetTimerState(true)
        // before it publishes the selectable tile groups.
        public void Poll()
        {
            if (timer)
                Respond();
        }

        private void Respond()
        {
            var r = round!;
            if (continueEnabled && !answerCalls)
                IgnoredCalls++;
            else if (!continueEnabled && !answerTurns)
                IgnoredTurns++;
            else if (continueEnabled)
            {
                if (ron)
                    r.ClientRon();
                else
                    r.ClientContinue();
            }
            else if (tsumo)
                r.ClientTsumo();
            else
                r.ClientTileSelected(groups!.Last(g => g.SelectionTiles.Count > 0).SelectionTiles[0]);
        }
    }
}
