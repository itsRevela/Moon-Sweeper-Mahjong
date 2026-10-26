using System;
using System.Collections.Generic;
using System.IO;
using MoonMahjong.Core;
using MoonMahjong.Table.Presentation;

namespace MoonMahjong.Net;

/// <summary>The crew session as crew play needs it (the plugin's is Steam; the tests' is a loopback).</summary>
public interface ICrewLink
{
    ulong LocalId { get; }

    /// <summary>Crewmates in the session who have the mod (never the local player). Mod messages go only to them.</summary>
    IReadOnlyCollection<ulong> ModdedPeers { get; }

    string NameOf(ulong id);

    /// <summary>Reliable and in order, as long as both stay in the session.</summary>
    void Send(ulong to, byte[] data);
}

/// <summary>Who sits in each of a table's four chairs, in seat (turn) order; 0 for nobody.</summary>
public interface IChairs
{
    ulong Occupant(int chair);
}

/// <summary>The rules a host deals with, and the game length a new table offers first. <paramref name="Seed"/> fixes the
/// shuffle and game IDs (tests).</summary>
public sealed record TableOptions(ServerSettings Settings, int StartingScore = 25000, int? Seed = null,
    GameLength Length = GameLength.EastSouth);

/// <summary>Routes each incoming message to its table's session; counts what it had to throw away.</summary>
public sealed class CrewTables
{
    private readonly Dictionary<string, TableSession> sessions = new();

    /// <summary>Messages that were not mahjong table messages, were malformed, or were for no known table.</summary>
    public int Rejected { get; private set; }

    public void Add(TableSession session) => sessions[session.Key] = session;

    public void Deliver(ulong from, byte[] data)
    {
        try
        {
            var (key, message) = TableProtocol.Decode(data);
            if (sessions.TryGetValue(key, out var session))
                session.Handle(from, message);
            else
                Rejected++;
        }
        catch (InvalidDataException)
        {
            Rejected++;
        }
    }
}
