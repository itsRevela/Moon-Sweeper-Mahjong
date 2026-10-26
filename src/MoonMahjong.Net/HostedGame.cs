using System;
using System.Collections.Generic;
using System.Linq;
using MoonMahjong.Core;
using MoonMahjong.Table.Presentation;

namespace MoonMahjong.Net;

// The host's side of a crew game: the authoritative MatchHost, every seat's messages routed to its owner (and kept, so
// anyone who asks can be replayed up to date), the public stream to everyone else, and the pause: the game runs only
// while every person sits in their own chair. Someone gone from the session for PlayerTimeout seconds is played
// automatically from then on; if they come back, they watch.
internal sealed class HostedGame
{
    private readonly TableSession session;
    private readonly TableGame view;
    private readonly MatchHost host;
    private readonly MatchClock clock;
    private readonly RemoteSeat?[] remote = new RemoteSeat?[4];
    private readonly List<byte[]>[] seatLog = { new(), new(), new(), new() };
    private readonly List<byte[]> spectatorLog = new();
    private readonly float?[] missingSince = new float?[4];
    private readonly bool[] replaced = new bool[4];
    private readonly Dictionary<ulong, float> lastReplay = new();
    private float lastTime;
    private float lastPauseSent = float.NegativeInfinity;

    public HostedGame(TableSession session, TableGame view, TableOptions options, int seed, float time)
    {
        this.session = session;
        this.view = view;
        clock = new MatchClock(time);
        lastTime = time;
        // Before the engine exists: MatchHost's constructor already sends every seat its game start.
        session.Broadcast(StartMessage());

        var players = view.Names.Select((name, seat) => new GamePlayer(seat, name)).ToArray();
        var info = new GameStartInfo(players, StartingDealer: 0, options.StartingScore, GameLengths.RoundCount(view.Length),
            HanchanCount: 1, UmaHigher: 20, UmaLower: 10);
        var seats = new MatchSeat[4];
        for (var seat = 0; seat < 4; seat++)
        {
            var owner = view.Owners[seat];
            var s = seat;
            if (owner == session.Link.LocalId)
                seats[seat] = new LocalClientSeat(view.Client);
            else if (owner != 0)
                seats[seat] = remote[seat] = new RemoteSeat(message => SendToSeat(s, message));
            else
                seats[seat] = new BotSeat(new SimpleBot(new RandomClass(seed + 17 * (seat + 1))));
        }
        var feed = new SpectatorFeed(SendToSpectators);
        host = new MatchHost(info, options.Settings, new RandomClass(seed), seats, time, new IServerRoundPlayer[] { feed });
    }

    public uint Id => view.Id;

    // A seat owner's action; false (and ignored) unless it came from that seat's owner, who is not the host.
    public bool Receive(ulong from, int seat, ClientMessage message)
    {
        if (remote[seat] is not { } target || view.Owners[seat] != from || replaced[seat])
            return false;
        target.Deliver(message);
        return true;
    }

    // Everything someone needs to rebuild the game: the start, then their seat's messages or the public stream. At most
    // once per ReplayInterval each (asks are cheap to send; replays are not).
    public void Replay(ulong to, float time)
    {
        if (lastReplay.TryGetValue(to, out var last) && time - last < ReplayInterval)
            return;
        lastReplay[to] = time;

        var seat = PlayingSeat(to);
        session.Send(to, StartMessage(watcher: seat < 0 ? to : 0));
        if (seat >= 0)
            foreach (var payload in seatLog[seat])
                session.Send(to, new ToSeatMessage(view.Id, seat, payload));
        else
            foreach (var payload in spectatorLog)
                session.Send(to, new SpectateMessage(view.Id, payload));
        if (view.Paused)
            session.Send(to, PauseMessage());
    }

    private const float ReplayInterval = 2f;

    // A start lists the seats' owners; one sent to someone whose seat is played for them leaves them out, so they watch.
    public GameStartMessage StartMessage(ulong watcher = 0) =>
        new(view.Id, view.Owners.Select(owner => owner == watcher ? 0 : owner).ToArray(), view.Names.ToArray(), view.Length);

    // The seat this crewmate plays (not one played for them since they left), or -1.
    private int PlayingSeat(ulong id)
    {
        var seat = view.Owners.ToList().IndexOf(id);
        return seat >= 0 && !replaced[seat] ? seat : -1;
    }

    public void Update(float time, IChairs chairs)
    {
        var peers = new HashSet<ulong>(session.Link.ModdedPeers);
        var waiting = Waiting(time, chairs, peers);
        var paused = waiting.Count > 0;
        if (paused != view.Paused || !waiting.SequenceEqual(view.WaitingSeats) || paused && time - lastPauseSent >= TableSession.PauseRepeat)
        {
            view.Paused = paused;
            view.WaitingSeats = waiting;
            session.Broadcast(PauseMessage());
            lastPauseSent = time;
        }

        if (paused)
            clock.Pause();
        else
            clock.Resume();
        clock.Tick(time - lastTime);
        lastTime = time;
        if (!paused)
            host.Process(clock.Now);
        view.Client.Process();

        if (host.Finished && view.Ended == null)
        {
            view.Ended = EndReason.Finished;
            session.Broadcast(new EndMessage(view.Id, EndReason.Finished));
        }
    }

    // People not in their own chair; someone gone from the session too long stops counting (and is played for).
    private IReadOnlyList<int> Waiting(float time, IChairs chairs, HashSet<ulong> peers)
    {
        view.FollowLonePerson(chairs);
        var waiting = new List<int>();
        for (var seat = 0; seat < 4; seat++)
        {
            var owner = view.Owners[seat];
            if (owner == 0 || replaced[seat])
                continue;
            var present = owner == session.Link.LocalId || peers.Contains(owner);
            if (present)
                missingSince[seat] = null;
            else
            {
                missingSince[seat] ??= time;
                if (time - missingSince[seat] >= TableSession.PlayerTimeout)
                {
                    replaced[seat] = true;
                    remote[seat]!.IsDisconnected = true;
                    host.SeatDisconnected(seat);
                    continue;
                }
            }
            if (!present || chairs.Occupant(view.ChairOf(seat)) != owner)
                waiting.Add(seat);
        }
        return waiting;
    }

    private PauseMessage PauseMessage() =>
        new(view.Id, view.Paused, view.WaitingSeats.Aggregate(0, (mask, seat) => mask | 1 << seat));

    private void SendToSeat(int seat, ServerMessage message)
    {
        var payload = MessageCodec.Encode(message);
        seatLog[seat].Add(payload);
        if (!replaced[seat])
            session.Send(view.Owners[seat], new ToSeatMessage(view.Id, seat, payload));
    }

    private void SendToSpectators(ServerMessage message)
    {
        var payload = MessageCodec.Encode(message);
        spectatorLog.Add(payload);
        foreach (var peer in session.Link.ModdedPeers.Where(p => PlayingSeat(p) < 0))
            session.Send(peer, new SpectateMessage(view.Id, payload));
    }
}
