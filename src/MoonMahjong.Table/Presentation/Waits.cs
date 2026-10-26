using System;
using System.Collections.Generic;
using System.Linq;
using MoonMahjong.Core;

namespace MoonMahjong.Table.Presentation;

/// <summary>A tile that would win, and how many of it are left: four less the copies the player can see.</summary>
public sealed record WaitTile(TileType Type, int Left);

/// <summary>
/// The waits popup while choosing a riichi discard (requested, as in Mahjong Soul): which tiles would win once the
/// hovered tile is thrown, and how many of each the player hasn't seen (their own hand, every pond, every call and the
/// dora indicators; the other hands are unknown).
/// </summary>
public static class Waits
{
    private const int Copies = 4;
    private static readonly TileType[] Types = Enumerable.Range((int)TileType.Man1, (int)TileType.Chun).Select(t => (TileType)t).ToArray();

    /// <summary>The tiles that complete <paramref name="hand"/> (14 tiles, with its calls) less <paramref name="discard"/>,
    /// in type order, each with how many remain unseen among <paramref name="visible"/>.</summary>
    public static IReadOnlyList<WaitTile> After(IReadOnlyList<Tile> hand, IReadOnlyList<RoundStateCall> calls, Tile discard,
        IEnumerable<Tile> visible)
    {
        if (hand == null)
            throw new ArgumentNullException(nameof(hand));
        if (calls == null)
            throw new ArgumentNullException(nameof(calls));
        if (discard == null)
            throw new ArgumentNullException(nameof(discard));
        if (visible == null)
            throw new ArgumentNullException(nameof(visible));

        var rest = hand.Where(t => t != discard).ToList();
        var callList = calls.ToList();
        var seen = visible.Where(t => t.TileType != TileType.Blank).GroupBy(t => t.ID).Select(g => g.First().TileType).ToList();
        return Types
            .Where(type => TileRules.WinningHand(rest.Append(new Tile(-1, type, false)).ToList(), callList))
            .Select(type => new WaitTile(type, Math.Max(0, Copies - seen.Count(t => t == type))))
            .ToList();
    }

    /// <summary>Every tile on the table whose face the local player knows.</summary>
    public static IReadOnlyList<Tile> Visible(RoundState state)
    {
        if (state == null)
            throw new ArgumentNullException(nameof(state));
        var tiles = new List<Tile>(state.Dora);
        for (var seat = 0; seat < 4; seat++)
        {
            var player = state.GetPlayer(seat);
            tiles.AddRange(player.Hand);
            tiles.AddRange(player.Pond);
            tiles.AddRange(player.Calls.SelectMany(call => call.Tiles));
        }
        return tiles.Where(t => t.TileType != TileType.Blank).ToList();
    }
}
