// Mod addition (not in OpenRiichi, whose messages travel through its own engine's serialiser): the wire format for
// crew play. Every message is one format-version byte, one type byte and its fields; seats and tile IDs are bytes.
// Decoding treats the bytes as untrusted: anything unknown, truncated, trailing or out of range is an
// InvalidDataException, so a bad peer can never put the game into a state the rules would not.
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace MoonMahjong.Core;

public static class MessageCodec
{
    public const byte FormatVersion = 1;
    public const int MaxNameLength = 64;

    private const int Seats = 4;
    private const int TileCount = 136;
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    // Server messages use 1-99, client messages 100+, so one can never be read as the other.
    private enum Kind : byte
    {
        GameStart = 1, RoundStart, TileAssignment, TileDraw, TileDiscard, CallDecision, TurnDecision, Ron, Tsumo, Riichi,
        LateKan, ClosedKan, OpenKan, Pon, Chii, CallsFinished, Draw, PlayerLeft,
        GameAction = 101, DefaultTileAction, DefaultCallAction, MenuReady,
    }

    private enum Action : byte
    {
        TileDiscard = 1, NoCall, Ron, Tsumo, VoidHand, Riichi, LateKan, ClosedKan, OpenKan, Pon, Chii,
    }

    [Flags]
    private enum Rules : byte
    {
        OpenRiichi = 1, AkaDora = 2, MultipleRon = 4, TripleRonDraw = 8, UnlimitedDecisionTime = 16,
    }

    public static byte[] Encode(ServerMessage message) => Write(w =>
    {
        switch (message)
        {
            case ServerMessageGameStart m:
                w.Write((byte)Kind.GameStart);
                WriteStartInfo(w, m.Info);
                WriteSettings(w, m.Settings);
                w.Write((sbyte)m.PlayerIndex);
                break;
            case ServerMessageRoundStart m:
                w.Write((byte)Kind.RoundStart);
                w.Write((byte)m.Info.WallIndex);
                break;
            case ServerMessageTileAssignment m:
                w.Write((byte)Kind.TileAssignment);
                w.Write((byte)m.Tile.ID);
                w.Write((byte)m.Tile.TileType);
                w.Write(m.Tile.Dora);
                break;
            case ServerMessageTileDraw:
                w.Write((byte)Kind.TileDraw);
                break;
            case ServerMessageTileDiscard m:
                w.Write((byte)Kind.TileDiscard);
                w.Write((byte)m.TileID);
                break;
            case ServerMessageCallDecision:
                w.Write((byte)Kind.CallDecision);
                break;
            case ServerMessageTurnDecision:
                w.Write((byte)Kind.TurnDecision);
                break;
            case ServerMessageRon m:
                w.Write((byte)Kind.Ron);
                WriteSeats(w, m.GetPlayerIndices());
                break;
            case ServerMessageTsumo:
                w.Write((byte)Kind.Tsumo);
                break;
            case ServerMessageRiichi m:
                w.Write((byte)Kind.Riichi);
                w.Write(m.Open);
                break;
            case ServerMessageLateKan m:
                w.Write((byte)Kind.LateKan);
                w.Write((byte)m.TileID);
                break;
            case ServerMessageClosedKan m:
                w.Write((byte)Kind.ClosedKan);
                w.Write((byte)m.TileType);
                break;
            case ServerMessageOpenKan m:
                w.Write((byte)Kind.OpenKan);
                w.Write((byte)m.PlayerIndex);
                w.Write((byte)m.Tile1ID);
                w.Write((byte)m.Tile2ID);
                w.Write((byte)m.Tile3ID);
                break;
            case ServerMessagePon m:
                w.Write((byte)Kind.Pon);
                w.Write((byte)m.PlayerIndex);
                w.Write((byte)m.Tile1ID);
                w.Write((byte)m.Tile2ID);
                break;
            case ServerMessageChii m:
                w.Write((byte)Kind.Chii);
                w.Write((byte)m.PlayerIndex);
                w.Write((byte)m.Tile1ID);
                w.Write((byte)m.Tile2ID);
                break;
            case ServerMessageCallsFinished:
                w.Write((byte)Kind.CallsFinished);
                break;
            case ServerMessageDraw m:
                w.Write((byte)Kind.Draw);
                WriteSeats(w, m.GetTenpaiIndices());
                w.Write(m.VoidHand);
                w.Write(m.TripleRon);
                break;
            case ServerMessagePlayerLeft m:
                w.Write((byte)Kind.PlayerLeft);
                w.Write((byte)m.PlayerIndex);
                break;
            default:
                throw new ArgumentException($"{message?.GetType().Name ?? "null"} has no wire format", nameof(message));
        }
    });

    public static byte[] Encode(ClientMessage message) => Write(w =>
    {
        switch (message)
        {
            case ClientMessageGameAction m:
                w.Write((byte)Kind.GameAction);
                WriteAction(w, m.Action);
                break;
            case ClientMessageControlDefaultTileAction m:
                w.Write((byte)Kind.DefaultTileAction);
                w.Write((byte)m.TileID);
                break;
            case ClientMessageControlDefaultCallAction:
                w.Write((byte)Kind.DefaultCallAction);
                break;
            case ClientMessageMenuReady:
                w.Write((byte)Kind.MenuReady);
                break;
            default:
                throw new ArgumentException($"{message?.GetType().Name ?? "null"} has no wire format", nameof(message));
        }
    });

    public static ServerMessage DecodeServer(byte[] bytes) => Read<ServerMessage>(bytes, r =>
    {
        var kind = (Kind)r.ReadByte();
        return kind switch
        {
            Kind.GameStart => new ServerMessageGameStart(ReadStartInfo(r), ReadSettings(r), ReadSeat(r, allowNone: true)),
            Kind.RoundStart => new ServerMessageRoundStart(new RoundStartInfo(ReadInRange(r, 2, 12, "wall index"))),
            Kind.TileAssignment => new ServerMessageTileAssignment(new Tile(ReadTileId(r), ReadTileType(r), r.ReadBoolean())),
            Kind.TileDraw => new ServerMessageTileDraw(),
            Kind.TileDiscard => new ServerMessageTileDiscard(ReadTileId(r)),
            Kind.CallDecision => new ServerMessageCallDecision(),
            Kind.TurnDecision => new ServerMessageTurnDecision(),
            Kind.Ron => new ServerMessageRon(ReadSeats(r)),
            Kind.Tsumo => new ServerMessageTsumo(),
            Kind.Riichi => new ServerMessageRiichi(r.ReadBoolean()),
            Kind.LateKan => new ServerMessageLateKan(ReadTileId(r)),
            Kind.ClosedKan => new ServerMessageClosedKan(ReadTileType(r)),
            Kind.OpenKan => new ServerMessageOpenKan(ReadSeat(r), ReadTileId(r), ReadTileId(r), ReadTileId(r)),
            Kind.Pon => new ServerMessagePon(ReadSeat(r), ReadTileId(r), ReadTileId(r)),
            Kind.Chii => new ServerMessageChii(ReadSeat(r), ReadTileId(r), ReadTileId(r)),
            Kind.CallsFinished => new ServerMessageCallsFinished(),
            Kind.Draw => new ServerMessageDraw(ReadSeats(r), r.ReadBoolean(), r.ReadBoolean()),
            Kind.PlayerLeft => new ServerMessagePlayerLeft(ReadSeat(r)),
            _ => throw new InvalidDataException($"unknown server message type {(byte)kind}"),
        };
    });

    public static ClientMessage DecodeClient(byte[] bytes) => Read<ClientMessage>(bytes, r =>
    {
        var kind = (Kind)r.ReadByte();
        return kind switch
        {
            Kind.GameAction => new ClientMessageGameAction(ReadAction(r)),
            Kind.DefaultTileAction => new ClientMessageControlDefaultTileAction(ReadTileId(r)),
            Kind.DefaultCallAction => new ClientMessageControlDefaultCallAction(),
            Kind.MenuReady => new ClientMessageMenuReady(),
            _ => throw new InvalidDataException($"unknown client message type {(byte)kind}"),
        };
    });

    private static void WriteAction(BinaryWriter w, ClientAction action)
    {
        switch (action)
        {
            case TileDiscardClientAction a:
                w.Write((byte)Action.TileDiscard);
                w.Write((byte)a.Tile);
                break;
            case NoCallClientAction:
                w.Write((byte)Action.NoCall);
                break;
            case RonClientAction:
                w.Write((byte)Action.Ron);
                break;
            case TsumoClientAction:
                w.Write((byte)Action.Tsumo);
                break;
            case VoidHandClientAction:
                w.Write((byte)Action.VoidHand);
                break;
            case RiichiClientAction a:
                w.Write((byte)Action.Riichi);
                w.Write(a.Open);
                break;
            case LateKanClientAction a:
                w.Write((byte)Action.LateKan);
                w.Write((byte)a.Tile);
                break;
            case ClosedKanClientAction a:
                w.Write((byte)Action.ClosedKan);
                w.Write((byte)a.TileType);
                break;
            case OpenKanClientAction:
                w.Write((byte)Action.OpenKan);
                break;
            case PonClientAction:
                w.Write((byte)Action.Pon);
                break;
            case ChiiClientAction a:
                w.Write((byte)Action.Chii);
                w.Write((byte)a.Tile1);
                w.Write((byte)a.Tile2);
                break;
            default:
                throw new ArgumentException($"{action?.GetType().Name ?? "null"} has no wire format", nameof(action));
        }
    }

    private static ClientAction ReadAction(BinaryReader r)
    {
        var action = (Action)r.ReadByte();
        return action switch
        {
            Action.TileDiscard => new TileDiscardClientAction(ReadTileId(r)),
            Action.NoCall => new NoCallClientAction(),
            Action.Ron => new RonClientAction(),
            Action.Tsumo => new TsumoClientAction(),
            Action.VoidHand => new VoidHandClientAction(),
            Action.Riichi => new RiichiClientAction(r.ReadBoolean()),
            Action.LateKan => new LateKanClientAction(ReadTileId(r)),
            Action.ClosedKan => new ClosedKanClientAction(ReadTileType(r)),
            Action.OpenKan => new OpenKanClientAction(),
            Action.Pon => new PonClientAction(),
            Action.Chii => new ChiiClientAction(ReadTileId(r), ReadTileId(r)),
            _ => throw new InvalidDataException($"unknown client action {(byte)action}"),
        };
    }

    private static void WriteStartInfo(BinaryWriter w, GameStartInfo info)
    {
        if (info.Players.Length != Seats)
            throw new ArgumentException($"a game has {Seats} players, not {info.Players.Length}", nameof(info));
        foreach (var player in info.Players)
        {
            w.Write((byte)player.ID);
            WriteName(w, player.Name);
        }
        w.Write((byte)info.StartingDealer);
        w.Write(info.StartingScore);
        w.Write((byte)info.RoundCount);
        w.Write((byte)info.HanchanCount);
        w.Write(info.UmaHigher);
        w.Write(info.UmaLower);
    }

    private static GameStartInfo ReadStartInfo(BinaryReader r)
    {
        var players = new GamePlayer[Seats];
        for (var i = 0; i < Seats; i++)
            players[i] = new GamePlayer(ReadSeat(r), ReadName(r));
        if (players.Select(p => p.ID).Distinct().Count() != Seats)
            throw new InvalidDataException("the players' seats repeat");
        return new GameStartInfo(players, ReadSeat(r), ReadInRange(r.ReadInt32(), 0, 10_000_000, "starting score"),
            ReadInRange(r, 1, 16, "round count"), ReadInRange(r, 1, 4, "hanchan count"),
            ReadInRange(r.ReadInt32(), -1000, 1000, "uma"), ReadInRange(r.ReadInt32(), -1000, 1000, "uma"));
    }

    private static void WriteSettings(BinaryWriter w, ServerSettings settings)
    {
        var rules = (settings.OpenRiichi ? Rules.OpenRiichi : 0) | (settings.AkaDora ? Rules.AkaDora : 0)
                    | (settings.MultipleRon ? Rules.MultipleRon : 0) | (settings.TripleRonDraw ? Rules.TripleRonDraw : 0)
                    | (settings.UnlimitedDecisionTime ? Rules.UnlimitedDecisionTime : 0);
        w.Write((byte)rules);
        w.Write((ushort)settings.DecisionTime);
    }

    private static ServerSettings ReadSettings(BinaryReader r)
    {
        var rules = (Rules)r.ReadByte();
        if ((byte)rules > 31)
            throw new InvalidDataException($"unknown rule flags {(byte)rules}");
        return new ServerSettings
        {
            OpenRiichi = rules.HasFlag(Rules.OpenRiichi),
            AkaDora = rules.HasFlag(Rules.AkaDora),
            MultipleRon = rules.HasFlag(Rules.MultipleRon),
            TripleRonDraw = rules.HasFlag(Rules.TripleRonDraw),
            UnlimitedDecisionTime = rules.HasFlag(Rules.UnlimitedDecisionTime),
            DecisionTime = ReadInRange(r.ReadUInt16(), ServerSettings.MinDecisionTime, ServerSettings.MaxDecisionTime, "decision time"),
        };
    }

    private static void WriteName(BinaryWriter w, string name)
    {
        if (name == null || name.Length > MaxNameLength)
            throw new ArgumentException($"player names are 0 to {MaxNameLength} characters", nameof(name));
        var bytes = Utf8.GetBytes(name);
        w.Write((ushort)bytes.Length);
        w.Write(bytes);
    }

    private static string ReadName(BinaryReader r)
    {
        var length = r.ReadUInt16();
        if (length > MaxNameLength * 4)
            throw new InvalidDataException($"a player name of {length} bytes");
        var bytes = r.ReadBytes(length);
        if (bytes.Length != length)
            throw new InvalidDataException("the message ends inside a player name");
        string name;
        try
        {
            name = Utf8.GetString(bytes);
        }
        catch (DecoderFallbackException e)
        {
            throw new InvalidDataException("a player name is not valid UTF-8", e);
        }
        if (name.Length > MaxNameLength)
            throw new InvalidDataException($"a player name of {name.Length} characters");
        return name;
    }

    private static void WriteSeats(BinaryWriter w, int[] seats)
    {
        w.Write((byte)seats.Length);
        foreach (var seat in seats)
            w.Write((byte)seat);
    }

    private static int[] ReadSeats(BinaryReader r)
    {
        var count = ReadInRange(r, 0, Seats, "seat count");
        var seats = new int[count];
        for (var i = 0; i < count; i++)
            seats[i] = ReadSeat(r);
        return seats;
    }

    private static int ReadSeat(BinaryReader r, bool allowNone = false)
    {
        int seat = allowNone ? r.ReadSByte() : r.ReadByte();
        return ReadInRange(seat, allowNone ? -1 : 0, Seats - 1, "seat");
    }

    private static int ReadTileId(BinaryReader r) => ReadInRange(r, 0, TileCount - 1, "tile");

    private static TileType ReadTileType(BinaryReader r) => (TileType)ReadInRange(r, (int)TileType.Blank, (int)TileType.Chun, "tile type");

    private static int ReadInRange(BinaryReader r, int min, int max, string what) => ReadInRange(r.ReadByte(), min, max, what);

    private static int ReadInRange(int value, int min, int max, string what) =>
        value >= min && value <= max ? value : throw new InvalidDataException($"{what} {value} is outside {min} to {max}");

    private static byte[] Write(Action<BinaryWriter> body)
    {
        using var stream = new MemoryStream();
        using (var w = new BinaryWriter(stream, Utf8, leaveOpen: true))
        {
            w.Write(FormatVersion);
            body(w);
        }
        return stream.ToArray();
    }

    private static T Read<T>(byte[] bytes, Func<BinaryReader, T> body)
    {
        if (bytes == null || bytes.Length < 2)
            throw new InvalidDataException("a message needs at least a format version and a type");
        if (bytes[0] != FormatVersion)
            throw new InvalidDataException($"message format {bytes[0]}, this game reads format {FormatVersion}");
        using var stream = new MemoryStream(bytes, 1, bytes.Length - 1, writable: false);
        using var r = new BinaryReader(stream, Utf8);
        T value;
        try
        {
            value = body(r);
        }
        catch (EndOfStreamException e)
        {
            throw new InvalidDataException("the message is cut short", e);
        }
        if (stream.Position != stream.Length)
            throw new InvalidDataException($"{stream.Length - stream.Position} unexpected bytes after the message");
        return value;
    }
}
