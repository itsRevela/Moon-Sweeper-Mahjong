using System;
using System.IO;
using System.Linq;
using System.Text;
using MoonMahjong.Table.Presentation;

namespace MoonMahjong.Net;

/// <summary>A crew-play message about one table (docs/crew-play.md).</summary>
public abstract record TableMessage;

/// <summary>The table's host and the game length they picked; repeated while the lobby is open.</summary>
public sealed record LobbyMessage(ulong Host, GameLength Length) : TableMessage;

/// <summary>A game was dealt: each seat's owner (0 for a bot) and name.</summary>
public sealed record GameStartMessage(uint GameId, ulong[] Owners, string[] Names, GameLength Length) : TableMessage;

/// <summary>Host to a seat's owner: one engine message (MessageCodec) for that seat.</summary>
public sealed record ToSeatMessage(uint GameId, int Seat, byte[] Payload) : TableMessage;

/// <summary>A seat's owner to the host: one of the seat's messages (MessageCodec).</summary>
public sealed record FromSeatMessage(uint GameId, int Seat, byte[] Payload) : TableMessage;

/// <summary>Host to everyone watching: one engine message of the public spectator stream.</summary>
public sealed record SpectateMessage(uint GameId, byte[] Payload) : TableMessage;

/// <summary>Host to everyone: whether the game is paused, and for which seats (bit n = seat n).</summary>
public sealed record PauseMessage(uint GameId, bool Paused, int WaitingSeats) : TableMessage;

public enum EndReason : byte
{
    Finished,
    HostLeft,
    Cancelled,
}

public sealed record EndMessage(uint GameId, EndReason Reason) : TableMessage;

/// <summary>To the host: replay this game (0: whatever is running) for my seat, or as a spectator.</summary>
public sealed record AskMessage(uint GameId) : TableMessage;

/// <summary>
/// The wire format of crew-play messages: 'M', 'J', a version, the table's key, a type and the fields. Decoding treats
/// the bytes as untrusted (they come from crewmates' machines): anything foreign, unknown, truncated, trailing or out of
/// range is an InvalidDataException.
/// </summary>
public static class TableProtocol
{
    public const byte Version = 1;
    public const int MaxKeyBytes = 64;
    public const int MaxNameLength = 64;
    public const int MaxPayload = 4096;

    private const int Seats = 4;
    private const char Replacement = '�';
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>A player's name as the protocol carries it: stray surrogate halves replaced, at most
    /// <see cref="MaxNameLength"/> characters, never cut inside a character.</summary>
    public static string ClipName(string? name) => Clip(Repair(name ?? ""), text => text.Length <= MaxNameLength);

    /// <summary>A table key as the protocol carries it: at most <see cref="MaxKeyBytes"/> bytes of UTF-8.</summary>
    public static string ClipKey(string? key) => Clip(Repair(key ?? ""), text => Utf8.GetByteCount(text) <= MaxKeyBytes);

    // Strict UTF-8 cannot encode half a surrogate pair (Steam names are not guaranteed to be well formed).
    private static string Repair(string text)
    {
        var repaired = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                repaired.Append(text[i]).Append(text[++i]);
            else
                repaired.Append(char.IsSurrogate(text[i]) ? Replacement : text[i]);
        }
        return repaired.ToString();
    }

    private static string Clip(string text, Func<string, bool> fits)
    {
        while (!fits(text))
        {
            var cut = text.Length - 1;
            if (cut > 0 && char.IsLowSurrogate(text[cut]) && char.IsHighSurrogate(text[cut - 1]))
                cut--;
            text = text.Substring(0, cut);
        }
        return text;
    }

    private enum Kind : byte
    {
        Lobby = 1, GameStart, ToSeat, FromSeat, Spectate, Pause, End, Ask,
    }

    public static byte[] Encode(string tableKey, TableMessage message)
    {
        using var stream = new MemoryStream();
        using (var w = new BinaryWriter(stream, Utf8, leaveOpen: true))
        {
            w.Write((byte)'M');
            w.Write((byte)'J');
            w.Write(Version);
            WriteKey(w, tableKey ?? throw new ArgumentNullException(nameof(tableKey)));
            WriteBody(w, message);
        }
        return stream.ToArray();
    }

    public static (string TableKey, TableMessage Message) Decode(byte[] bytes)
    {
        if (bytes == null || bytes.Length < 5 || bytes[0] != 'M' || bytes[1] != 'J')
            throw new InvalidDataException("not a mahjong table message");
        if (bytes[2] != Version)
            throw new InvalidDataException($"table protocol {bytes[2]}, this mod speaks {Version}");
        using var stream = new MemoryStream(bytes, 3, bytes.Length - 3, writable: false);
        using var r = new BinaryReader(stream, Utf8);
        try
        {
            var key = ReadKey(r);
            var message = ReadBody(r);
            if (stream.Position != stream.Length)
                throw new InvalidDataException($"{stream.Length - stream.Position} unexpected bytes after the message");
            return (key, message);
        }
        catch (EndOfStreamException e)
        {
            throw new InvalidDataException("the message is cut short", e);
        }
    }

    private static void WriteBody(BinaryWriter w, TableMessage message)
    {
        switch (message)
        {
            case LobbyMessage m:
                w.Write((byte)Kind.Lobby);
                w.Write(m.Host);
                w.Write((byte)m.Length);
                break;
            case GameStartMessage m:
                if (m.Owners.Length != Seats || m.Names.Length != Seats)
                    throw new ArgumentException("a game has four seats", nameof(message));
                w.Write((byte)Kind.GameStart);
                w.Write(m.GameId);
                foreach (var owner in m.Owners)
                    w.Write(owner);
                foreach (var name in m.Names)
                    WriteName(w, name);
                w.Write((byte)m.Length);
                break;
            case ToSeatMessage m:
                w.Write((byte)Kind.ToSeat);
                w.Write(m.GameId);
                w.Write((byte)CheckSeat(m.Seat));
                WritePayload(w, m.Payload);
                break;
            case FromSeatMessage m:
                w.Write((byte)Kind.FromSeat);
                w.Write(m.GameId);
                w.Write((byte)CheckSeat(m.Seat));
                WritePayload(w, m.Payload);
                break;
            case SpectateMessage m:
                w.Write((byte)Kind.Spectate);
                w.Write(m.GameId);
                WritePayload(w, m.Payload);
                break;
            case PauseMessage m:
                w.Write((byte)Kind.Pause);
                w.Write(m.GameId);
                w.Write(m.Paused);
                w.Write((byte)(m.WaitingSeats & 0xF));
                break;
            case EndMessage m:
                w.Write((byte)Kind.End);
                w.Write(m.GameId);
                w.Write((byte)m.Reason);
                break;
            case AskMessage m:
                w.Write((byte)Kind.Ask);
                w.Write(m.GameId);
                break;
            default:
                throw new ArgumentException($"{message?.GetType().Name ?? "null"} has no wire format", nameof(message));
        }
    }

    private static TableMessage ReadBody(BinaryReader r)
    {
        var kind = (Kind)r.ReadByte();
        switch (kind)
        {
            case Kind.Lobby:
                return new LobbyMessage(r.ReadUInt64(), ReadLength(r));
            case Kind.GameStart:
                {
                    var id = r.ReadUInt32();
                    var owners = Enumerable.Range(0, Seats).Select(_ => r.ReadUInt64()).ToArray();
                    var names = Enumerable.Range(0, Seats).Select(_ => ReadText(r, MaxNameLength * 4, "name")).ToArray();
                    if (names.Any(n => n.Length > MaxNameLength))
                        throw new InvalidDataException("a player name is too long");
                    return new GameStartMessage(id, owners, names, ReadLength(r));
                }
            case Kind.ToSeat:
                return new ToSeatMessage(r.ReadUInt32(), ReadSeat(r), ReadPayload(r));
            case Kind.FromSeat:
                return new FromSeatMessage(r.ReadUInt32(), ReadSeat(r), ReadPayload(r));
            case Kind.Spectate:
                return new SpectateMessage(r.ReadUInt32(), ReadPayload(r));
            case Kind.Pause:
                {
                    var id = r.ReadUInt32();
                    var paused = r.ReadBoolean();
                    var waiting = r.ReadByte();
                    if (waiting > 0xF)
                        throw new InvalidDataException($"waiting seats {waiting}");
                    return new PauseMessage(id, paused, waiting);
                }
            case Kind.End:
                {
                    var id = r.ReadUInt32();
                    var reason = (EndReason)r.ReadByte();
                    if (!Enum.IsDefined(typeof(EndReason), reason))
                        throw new InvalidDataException($"end reason {(byte)reason}");
                    return new EndMessage(id, reason);
                }
            case Kind.Ask:
                return new AskMessage(r.ReadUInt32());
            default:
                throw new InvalidDataException($"unknown table message type {(byte)kind}");
        }
    }

    private static int CheckSeat(int seat) =>
        seat is >= 0 and < Seats ? seat : throw new ArgumentOutOfRangeException(nameof(seat), seat, "seats are 0 to 3");

    private static int ReadSeat(BinaryReader r)
    {
        var seat = r.ReadByte();
        return seat < Seats ? seat : throw new InvalidDataException($"seat {seat}");
    }

    private static GameLength ReadLength(BinaryReader r)
    {
        var length = (GameLength)r.ReadByte();
        return Enum.IsDefined(typeof(GameLength), length) ? length : throw new InvalidDataException($"game length {(byte)length}");
    }

    private static void WritePayload(BinaryWriter w, byte[] payload)
    {
        if (payload == null || payload.Length > MaxPayload)
            throw new ArgumentException($"payloads are up to {MaxPayload} bytes", nameof(payload));
        w.Write((ushort)payload.Length);
        w.Write(payload);
    }

    private static byte[] ReadPayload(BinaryReader r)
    {
        var length = r.ReadUInt16();
        if (length > MaxPayload)
            throw new InvalidDataException($"a payload of {length} bytes");
        var payload = r.ReadBytes(length);
        return payload.Length == length ? payload : throw new InvalidDataException("the message ends inside its payload");
    }

    private static void WriteName(BinaryWriter w, string name)
    {
        if (name == null || name.Length > MaxNameLength)
            throw new ArgumentException($"names are up to {MaxNameLength} characters", nameof(name));
        WriteText(w, name, MaxNameLength * 4, nameof(name));
    }

    // The table key: one length byte, then UTF-8 (at most MaxKeyBytes).
    private static void WriteKey(BinaryWriter w, string key)
    {
        var bytes = Utf8.GetBytes(key);
        if (bytes.Length > MaxKeyBytes)
            throw new ArgumentException($"table keys are up to {MaxKeyBytes} bytes", nameof(key));
        w.Write((byte)bytes.Length);
        w.Write(bytes);
    }

    private static string ReadKey(BinaryReader r)
    {
        var length = r.ReadByte();
        if (length > MaxKeyBytes)
            throw new InvalidDataException($"a table key of {length} bytes");
        return Decode(r.ReadBytes(length), length, "table key");
    }

    private static void WriteText(BinaryWriter w, string text, int maxBytes, string what)
    {
        var bytes = Utf8.GetBytes(text);
        if (bytes.Length > maxBytes)
            throw new ArgumentException($"{what} is longer than {maxBytes} bytes", what);
        w.Write((ushort)bytes.Length);
        w.Write(bytes);
    }

    private static string ReadText(BinaryReader r, int maxBytes, string what)
    {
        var length = r.ReadUInt16();
        if (length > maxBytes)
            throw new InvalidDataException($"a {what} of {length} bytes");
        return Decode(r.ReadBytes(length), length, what);
    }

    private static string Decode(byte[] bytes, int length, string what)
    {
        if (bytes.Length != length)
            throw new InvalidDataException($"the message ends inside a {what}");
        try
        {
            return Utf8.GetString(bytes);
        }
        catch (DecoderFallbackException e)
        {
            throw new InvalidDataException($"a {what} is not valid UTF-8", e);
        }
    }
}
