// The authoritative game across rounds, following Vala GameServer.Server.process: run the round, hand its result to
// GameState, wait until every seat is ready, start the next round, until the game ends. Mod change (requested): a
// result stays up until every person has acknowledged it (bots and disconnected seats count as ready); OpenRiichi also
// moved on by itself once the round-end delay ran out.
// Single-threaded and time-driven: call Process(time) once per frame with a monotonic clock in seconds.
using System;
using System.Collections.Generic;
using System.Linq;

namespace MoonMahjong.Core;

public sealed class MatchHost
{
    private readonly RandomClass rnd;
    private readonly ServerSettings settings;
    private readonly AnimationTimings timings;
    private readonly List<MatchSeat> seats;
    private readonly List<IServerRoundPlayer> players = new();
    private readonly IReadOnlyList<IServerRoundPlayer> spectators;
    private readonly bool[] ready;
    private readonly bool[] disconnected;
    private ServerGameRound? round;
    private Stage stage;

    // spectators (crew play): watchers who get the game start with no seat (-1) and every round's public messages.
    public MatchHost(GameStartInfo startInfo, ServerSettings settings, RandomClass rnd, IReadOnlyList<MatchSeat> seats, float time,
        IReadOnlyList<IServerRoundPlayer>? spectators = null)
    {
        if (seats.Count != 4)
            throw new ArgumentException($"A Riichi game needs 4 seats, got {seats.Count}", nameof(seats));
        if (startInfo.Players.Length != seats.Count)
            throw new ArgumentException($"GameStartInfo has {startInfo.Players.Length} players for {seats.Count} seats", nameof(startInfo));

        this.settings = settings;
        this.rnd = rnd;
        this.seats = seats.ToList();
        timings = AnimationTimings.Default(settings);
        ready = new bool[seats.Count];
        disconnected = new bool[seats.Count];

        State = new GameState(startInfo, settings);

        for (var i = 0; i < seats.Count; i++)
        {
            var seatIndex = i;
            var player = new ObservedPlayer(seats[i].Player, this, seatIndex);
            players.Add(player);
            seats[i].MessageSent += message => MessageReceived(seatIndex, message);
            player.Send(new ServerMessageGameStart(startInfo, settings, i));
        }
        this.spectators = spectators ?? Array.Empty<IServerRoundPlayer>();
        foreach (var spectator in this.spectators)
            spectator.Send(new ServerMessageGameStart(startInfo, settings, -1));

        StartRound(time);
    }

    public GameState State { get; }
    public bool Finished { get; private set; }

    // Every message the host sends, by seat (for transports and diagnostics).
    public event Action<int, ServerMessage>? MessageSent;

    // After GameState has taken a round's result.
    public event Action<RoundFinishResult, RoundScoreState>? RoundFinished;

    public event Action<GameState>? GameFinished;

    // One frame: advance the server, then let every seat react (bots decide, the local client handles messages).
    public void Process(float time)
    {
        ProcessServer(time);
        foreach (var seat in seats)
            seat.Tick();
    }

    // A seat dropped out (Phase 2): the round plays default actions for it from now on, and round ends stop waiting for it.
    public void SeatDisconnected(int seat)
    {
        disconnected[seat] = true;
        round?.PlayerDisconnected(seat);
    }

    private void ProcessServer(float time)
    {
        if (Finished)
            return;

        if (stage == Stage.Active)
        {
            round!.Process(time);

            if (!round.Finished)
                return;

            var result = round.Result ?? throw new InvalidOperationException("A round finished without a result");
            var score = State.RoundFinished(result) ?? throw new InvalidOperationException("GameState rejected the round result");
            RoundFinished?.Invoke(result, score);

            if (State.GameIsFinished)
                stage = Stage.GameFinished;
            else if (State.HanchanIsFinished)
                stage = Stage.HanchanFinished;
            else if (State.RoundIsFinished)
                stage = Stage.RoundFinished;
            return;
        }

        // After the last round nothing waits on the players: each client keeps its final result up until its player
        // closes it (ClientGame sends no ready then, as the Vala GameController returns to the menu).
        if (stage == Stage.GameFinished)
        {
            Finished = true;
            GameFinished?.Invoke(State);
            return;
        }

        for (var i = 0; i < seats.Count; i++)
            if (!seats[i].AlwaysReady && !ready[i] && !disconnected[i])
                return;

        StartRound(time);
    }

    // Vala Server.message_received
    private void MessageReceived(int seat, ClientMessage message)
    {
        // A client shows the result (and its player may press Next round) a few frames before the server round
        // reports itself finished, so readiness counts whenever it arrives. Clients only send it after a result, and
        // StartRound clears it.
        if (message is ClientMessageMenuReady)
            ready[seat] = true;
        else if (stage == Stage.Active)
            round!.MessageReceived(players[seat], message);
    }

    private void StartRound(float time)
    {
        Array.Clear(ready, 0, ready.Length);
        stage = Stage.Active;

        var info = new RoundStartInfo(rnd.IntRange(1, 7) + rnd.IntRange(1, 7)); // two dice, as Vala RegularServer rolls them
        State.StartRound(info);
        round = new RegularServerGameRound(info, settings, players, spectators, State.RoundWind,
            State.DealerIndex, rnd, State.CanRiichi(), timings);
        round.DeclareRiichi += State.DeclareRiichi;
        round.Start(time);
    }

    private enum Stage
    {
        Active,
        GameFinished,
        HanchanFinished,
        RoundFinished,
    }

    // Lets the host report every outgoing message without each seat having to.
    private sealed class ObservedPlayer : IServerRoundPlayer
    {
        private readonly IServerRoundPlayer inner;
        private readonly MatchHost host;
        private readonly int seat;

        public ObservedPlayer(IServerRoundPlayer inner, MatchHost host, int seat)
        {
            this.inner = inner;
            this.host = host;
            this.seat = seat;
        }

        public bool IsSpectator => inner.IsSpectator;
        public bool SeesAllHands => inner.SeesAllHands;
        // SeatDisconnected also covers the rounds after it (a seat itself may not know it was given up on).
        public bool IsDisconnected => inner.IsDisconnected || host.disconnected[seat];

        public void Send(ServerMessage message)
        {
            host.MessageSent?.Invoke(seat, message);
            inner.Send(message);
        }
    }
}
