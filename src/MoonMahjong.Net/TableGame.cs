using System;
using System.Collections.Generic;
using System.Linq;
using MoonMahjong.Core;
using MoonMahjong.Table.Presentation;

namespace MoonMahjong.Net;

/// <summary>
/// One crew game as this client sees it: who plays where, the local view of the game (a player's or a spectator's
/// ClientGame, which the plugin draws and plays through), and whether the table is paused or has ended.
/// </summary>
public sealed class TableGame
{
    internal TableGame(uint id, ulong host, ulong[] owners, string[] names, GameLength length, int localSeat)
    {
        Id = id;
        Host = host;
        Owners = (ulong[])owners.Clone();
        Names = (string[])names.Clone();
        Length = length;
        LocalSeat = localSeat;
    }

    public uint Id { get; }
    public ulong Host { get; }

    /// <summary>Each seat's owner (0 for a bot).</summary>
    public IReadOnlyList<ulong> Owners { get; }

    public IReadOnlyList<string> Names { get; }
    public GameLength Length { get; }

    /// <summary>The local player's seat, or -1 when watching.</summary>
    public int LocalSeat { get; }

    public ClientGame Client { get; } = new();

    /// <summary>Which chair each seat is played from: seat + ChairOffset (mod 4). With one person at the table the game
    /// follows them to whichever chair they sit in (the host turns the seats); with more, seats keep their chairs.</summary>
    public int ChairOffset { get; internal set; }

    public int ChairOf(int seat) => (seat + ChairOffset) % 4;

    // Dealt to one person (the rest bots): their seat goes to whichever chair they sit in. Host and watchers alike work
    // it out from the chairs (which the game itself keeps in sync), so nobody has to send it.
    internal void FollowLonePerson(IChairs chairs)
    {
        var people = Enumerable.Range(0, 4).Where(seat => Owners[seat] != 0).ToList();
        if (people.Count != 1)
            return;
        var seat = people[0];
        var chair = Enumerable.Range(0, 4).FirstOrDefault(c => chairs.Occupant(c) == Owners[seat], -1);
        if (chair >= 0)
            ChairOffset = (chair - seat + 4) % 4;
    }

    public bool Paused { get; internal set; }

    /// <summary>The seats the table is waiting for: their owners are not in their own chairs.</summary>
    public IReadOnlyList<int> WaitingSeats { get; internal set; } = Array.Empty<int>();

    /// <summary>Seconds the host has been out of the session (0 while they are in it).</summary>
    public float HostMissingFor { get; internal set; }

    public EndReason? Ended { get; internal set; }

    // This client's copy failed (TableSession.Recover): it is no longer read, and a replay from the host replaces it.
    internal bool Broken { get; set; }

    /// <summary>Finished, or abandoned because the host left.</summary>
    public bool Over => Ended != null;
}
