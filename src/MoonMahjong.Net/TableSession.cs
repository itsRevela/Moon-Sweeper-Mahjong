using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MoonMahjong.Core;
using MoonMahjong.Table.Presentation;

namespace MoonMahjong.Net;

/// <summary>
/// One mahjong table as this client takes part in it (docs/crew-play.md): the lobby before a deal (the first to sit
/// hosts; two claims at once go to the lower Steam ID), the deal, and then either hosting the game (HostedGame) or
/// following it as a player or spectator. Call <see cref="Update"/> every frame, seated or not, and hand it every
/// message for this table through <see cref="Handle"/> (CrewTables does that). A new <see cref="Game"/>'s ClientGame
/// is only processed inside Update, so subscribe to its events as soon as Game changes, before the next Update.
/// Whoever appears in the crew is asked for this table's running game, so a client that joins, restarts or loads the
/// table late is replayed it by its host.
/// </summary>
public sealed class TableSession
{
    public const float LobbyRepeat = 3f;
    public const float PauseRepeat = 3f;
    public const float HostTimeout = 180f;
    public const float PlayerTimeout = 180f;
    // A lobby host not heard from for this long is forgotten (their table may have failed or been unloaded).
    public const float LobbyHostSilence = 3 * LobbyRepeat + 1f;

    private readonly IChairs chairs;
    private readonly TableOptions options;
    private readonly Random random;
    private readonly HashSet<ulong> knownPeers = new();
    private HostedGame? hosted;
    private float lastLobbySent = float.NegativeInfinity;
    private float lastLobbyHeard;
    private float? hostGoneSince;
    private float now;

    public TableSession(string key, ICrewLink link, IChairs chairs, TableOptions options)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        Link = link ?? throw new ArgumentNullException(nameof(link));
        this.chairs = chairs ?? throw new ArgumentNullException(nameof(chairs));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        random = options.Seed is { } seed ? new Random(seed) : new Random();
        LobbyLength = options.Length;
    }

    public string Key { get; }
    public ICrewLink Link { get; }

    /// <summary>The local player's chair at this table, or -1.</summary>
    public int LocalChair => Enumerable.Range(0, 4).FirstOrDefault(c => chairs.Occupant(c) == Link.LocalId, -1);

    /// <summary>The lobby's host (0: no lobby, e.g. nobody seated or a game running).</summary>
    public ulong LobbyHost { get; private set; }

    public bool IsLobbyHost => LobbyHost != 0 && LobbyHost == Link.LocalId;
    public GameLength LobbyLength { get; private set; }

    /// <summary>The latest game at this table this client knows of (running, finished or abandoned).</summary>
    public TableGame? Game { get; private set; }

    public bool GameRunning => Game is { Over: false };

    /// <summary>Game messages this session threw away: forged, for another game, or malformed.</summary>
    public int Rejected { get; private set; }

    public void ChooseLength(GameLength length)
    {
        if (!IsLobbyHost)
            throw new InvalidOperationException("Only the table's host chooses the game's length.");
        LobbyLength = length;
        Broadcast(new LobbyMessage(Link.LocalId, length));
    }

    public void Deal()
    {
        if (!IsLobbyHost || GameRunning)
            throw new InvalidOperationException("Only the table's host deals, and only when no game is running.");
        var localChair = LocalChair;
        if (localChair < 0)
            throw new InvalidOperationException("The host deals from their chair.");

        var peers = new HashSet<ulong>(Link.ModdedPeers);
        var owners = new ulong[4];
        var names = new string[4];
        var bots = 0;
        for (var chair = 0; chair < 4; chair++)
        {
            var occupant = chairs.Occupant(chair);
            owners[chair] = occupant != 0 && (occupant == Link.LocalId || peers.Contains(occupant)) ? occupant : 0;
            names[chair] = owners[chair] != 0 ? TableProtocol.ClipName(Link.NameOf(owners[chair])) : $"Bot {++bots}";
        }

        // Nothing changes unless the game is set up and its start sent.
        var game = new TableGame((uint)random.Next(1, int.MaxValue), Link.LocalId, owners, names, LobbyLength, localChair);
        var host = new HostedGame(this, game, options, random.Next(), now);
        Game = game;
        hosted = host;
        hostGoneSince = null;
        LobbyHost = 0;
    }

    /// <summary>
    /// The local view of the running game failed (the plugin caught an exception from it). A host ends the game for
    /// everyone (<see cref="EndReason.Cancelled"/>) rather than leave them waiting; a player or watcher stops reading
    /// their copy and asks the host to replay the game afresh.
    /// </summary>
    public void Recover()
    {
        if (Game is not { Over: false } game)
            return;
        if (hosted != null)
        {
            game.Ended = EndReason.Cancelled;
            Broadcast(new EndMessage(game.Id, EndReason.Cancelled));
            hosted = null;
        }
        else
        {
            game.Broken = true;
            Send(game.Host, new AskMessage(game.Id));
        }
    }

    /// <summary>After a restart: ask whoever hosts this table to replay the running game.</summary>
    public void Ask() => Broadcast(new AskMessage(0));

    public void Handle(ulong from, TableMessage message)
    {
        try
        {
            switch (message)
            {
                case LobbyMessage m:
                    HandleLobby(from, m);
                    break;
                case GameStartMessage m:
                    HandleStart(from, m);
                    break;
                case ToSeatMessage m when IsFromHost(from, m.GameId) && m.Seat == Game!.LocalSeat:
                    Game.Client.Receive(MessageCodec.DecodeServer(m.Payload));
                    break;
                case SpectateMessage m when IsFromHost(from, m.GameId) && Game!.LocalSeat < 0:
                    Game.Client.Receive(MessageCodec.DecodeServer(m.Payload));
                    break;
                case FromSeatMessage m when hosted?.Id == m.GameId:
                    if (!hosted.Receive(from, m.Seat, MessageCodec.DecodeClient(m.Payload)))
                        Rejected++;
                    break;
                case PauseMessage m when IsFromHost(from, m.GameId):
                    Game!.Paused = m.Paused;
                    Game.WaitingSeats = Enumerable.Range(0, 4).Where(seat => (m.WaitingSeats & 1 << seat) != 0).ToList();
                    break;
                case EndMessage m when IsFromHost(from, m.GameId):
                    Game!.Ended ??= m.Reason;
                    break;
                case AskMessage m when hosted != null && Game is { Over: false } && (m.GameId == 0 || m.GameId == hosted.Id):
                    hosted.Replay(from, now);
                    break;
                case AskMessage:
                    break; // nothing running here to replay
                default:
                    Rejected++;
                    break;
            }
        }
        catch (InvalidDataException)
        {
            Rejected++;
        }
    }

    public void Update(float time)
    {
        now = time;
        var peers = new HashSet<ulong>(Link.ModdedPeers);
        var joined = peers.Where(peer => !knownPeers.Contains(peer)).ToList();
        foreach (var peer in joined)
            Send(peer, new AskMessage(0)); // whoever hosts a running game here replays it to us

        if (hosted != null && Game is { Over: false })
            hosted.Update(time, chairs);
        else if (Game is { Over: false } running)
            FollowHost(running, time);
        else if (Game is { Broken: false } over)
            over.Client.Process(); // what arrived with the game's End (its last result, say)
        UpdateLobby(time, peers, joined);

        knownPeers.Clear();
        knownPeers.UnionWith(peers);
    }

    internal void Send(ulong to, TableMessage message) => Link.Send(to, TableProtocol.Encode(Key, message));

    internal void Broadcast(TableMessage message)
    {
        var bytes = TableProtocol.Encode(Key, message);
        foreach (var peer in Link.ModdedPeers)
            Link.Send(peer, bytes);
    }

    private bool IsFromHost(ulong from, uint gameId) => Game != null && Game.Id == gameId && Game.Host == from && hosted == null;

    private bool IsSeated(ulong id) => id != 0 && Enumerable.Range(0, 4).Any(chair => chairs.Occupant(chair) == id);

    // A player's or spectator's side: feed the local view (unless it broke and a replay is on its way), and give up on
    // a host who has left the session.
    private void FollowHost(TableGame game, float time)
    {
        if (!game.Broken)
        {
            game.FollowLonePerson(chairs);
            game.Client.Process();
        }
        if (Link.ModdedPeers.Contains(game.Host))
        {
            hostGoneSince = null;
            game.HostMissingFor = 0;
            return;
        }
        hostGoneSince ??= time;
        game.HostMissingFor = time - hostGoneSince.Value;
        if (game.HostMissingFor >= HostTimeout)
            game.Ended = EndReason.HostLeft;
    }

    private void HandleStart(ulong from, GameStartMessage m)
    {
        // Ignore a second host's game while one is running (unless it is a replay of this one, which restarts it).
        if (GameRunning && (Game!.Host != from || hosted != null) || !MayStart(from, m))
        {
            Rejected++;
            return;
        }
        var seat = Array.IndexOf(m.Owners, Link.LocalId);
        var game = new TableGame(m.GameId, from, m.Owners, m.Names, m.Length, seat);
        if (seat >= 0)
            game.Client.MessageSent += message => Send(from, new FromSeatMessage(m.GameId, seat, MessageCodec.Encode(message)));
        Game = game;
        hosted = null;
        hostGoneSince = null;
        LobbyHost = 0;
    }

    // A game comes from a crewmate with the mod who plays in it, seats nobody twice, and either hosts this table's lobby,
    // hosts the game we follow (a replay), or sits at this table; or, if we know of nothing here, anyone (a late join).
    private bool MayStart(ulong from, GameStartMessage m)
    {
        if (!Link.ModdedPeers.Contains(from) || Array.IndexOf(m.Owners, from) < 0)
            return false;
        var people = m.Owners.Where(owner => owner != 0).ToList();
        if (people.Distinct().Count() != people.Count)
            return false;
        return from == LobbyHost || from == Game?.Host || IsSeated(from) || LobbyHost == 0 && !GameRunning;
    }

    private void HandleLobby(ulong from, LobbyMessage m)
    {
        if (GameRunning)
        {
            // The host of the game we follow has opened their table again: that game is over, though its End never came.
            if (hosted != null || from != Game!.Host || m.Host != from)
                return;
            Game.Ended ??= EndReason.Finished;
        }
        if (m.Host == 0)
        {
            if (LobbyHost == from)
                LobbyHost = 0;
            return;
        }
        if (m.Host != from || !IsSeated(from))
        {
            Rejected++; // one can only claim a table for oneself, from one of its chairs
            return;
        }
        // The first claim stands; two at once go to the lower ID (the other side yields when it hears this one).
        if (LobbyHost == 0 || LobbyHost == from || from < LobbyHost)
        {
            LobbyHost = from;
            LobbyLength = m.Length;
            lastLobbyHeard = now;
        }
    }

    private void UpdateLobby(float time, HashSet<ulong> peers, List<ulong> joined)
    {
        if (GameRunning)
        {
            LobbyHost = 0;
            return;
        }
        var seated = LocalChair >= 0;
        if (LobbyHost != 0 && LobbyHost != Link.LocalId && (!peers.Contains(LobbyHost) || time - lastLobbyHeard > LobbyHostSilence))
            LobbyHost = 0; // the host left the session, or their table stopped answering
        if (IsLobbyHost && !seated)
        {
            Broadcast(new LobbyMessage(0, LobbyLength));
            LobbyHost = 0;
        }
        if (LobbyHost == 0 && seated)
        {
            LobbyHost = Link.LocalId;
            lastLobbySent = float.NegativeInfinity;
        }

        if (IsLobbyHost)
        {
            if (time - lastLobbySent >= LobbyRepeat)
            {
                Broadcast(new LobbyMessage(Link.LocalId, LobbyLength));
                lastLobbySent = time;
            }
            foreach (var peer in joined)
                Send(peer, new LobbyMessage(Link.LocalId, LobbyLength)); // just joined the session: tell them at once
        }
    }
}
