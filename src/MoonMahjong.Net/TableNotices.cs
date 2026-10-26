using System;
using System.Collections.Generic;
using System.Linq;

namespace MoonMahjong.Net;

/// <summary>What the seated player's HUD says about the table: who deals, who plays, and why the game is waiting.</summary>
public static class TableNotices
{
    private static readonly string[] Numbers = { "no", "one", "two", "three", "four" };

    /// <summary>Under the start plate: who will deal (nothing for the host, whose plate has the Deal button).</summary>
    public static string Lobby(TableSession session) =>
        session.LobbyHost != 0 && !session.IsLobbyHost ? $"Waiting for {session.Link.NameOf(session.LobbyHost)} to deal" : "";

    /// <summary>Who the next deal seats: you, then crewmates with the mod in chair order, then the bots for the rest.</summary>
    public static string Players(ICrewLink link, IChairs chairs)
    {
        var peers = new HashSet<ulong>(link.ModdedPeers);
        var people = new List<string> { "You" };
        for (var chair = 0; chair < 4; chair++)
        {
            var occupant = chairs.Occupant(chair);
            if (occupant != 0 && occupant != link.LocalId && peers.Contains(occupant))
                people.Add(link.NameOf(occupant));
        }
        var bots = 4 - people.Count;
        if (bots > 0)
            people.Add($"{Numbers[bots]} computer player{(bots == 1 ? "" : "s")}");
        return people.Count == 1 ? people[0] : string.Join(", ", people.Take(people.Count - 1)) + " and " + people[^1];
    }

    /// <summary>During a game, for the player sitting in <paramref name="localChair"/> (-1: standing); "" when all is well.</summary>
    public static string During(TableGame game, int localChair)
    {
        if (game.HostMissingFor > 0 && !game.Over)
        {
            var left = (int)Math.Max(0, Math.Floor(TableSession.HostTimeout - game.HostMissingFor));
            return $"{NameOf(game, game.Host)} left the crew. The game ends in {left / 60}:{left % 60:D2} unless they come back.";
        }
        if (localChair >= 0 && game.LocalSeat < 0)
            return "Watching: this game was dealt before you sat down";
        if (localChair >= 0 && game.ChairOf(game.LocalSeat) != localChair)
        {
            var here = Enumerable.Range(0, 4).First(seat => game.ChairOf(seat) == localChair);
            var where = ((game.ChairOf(game.LocalSeat) - localChair + 4) % 4) switch
            {
                1 => "to your right",
                2 => "across the table",
                _ => "to your left",
            };
            return $"This is {game.Names[here]}'s seat. Yours is the one {where}.";
        }
        var waiting = game.WaitingSeats.Where(seat => seat != game.LocalSeat).Select(seat => game.Names[seat]).ToList();
        if (game.Paused && waiting.Count > 0)
            return "Paused: waiting for " + (waiting.Count == 1 ? waiting[0] : string.Join(", ", waiting.Take(waiting.Count - 1)) + " and " + waiting[^1]);
        return "";
    }

    private static string NameOf(TableGame game, ulong id)
    {
        var seat = game.Owners.ToList().IndexOf(id);
        return seat >= 0 ? game.Names[seat] : "The host";
    }
}
