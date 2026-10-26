using System;
using System.Collections.Generic;
using System.Linq;
using MoonMahjong.Core;

namespace MoonMahjong.Table.Presentation;

/// <summary>One tile of a hand shown on the round result.</summary>
public sealed record HandTileView(TileType Type, bool RedFive, bool Sideways = false, bool FaceDown = false, bool Winning = false);

/// <summary>A tile and its left edge along the row, in tile widths.</summary>
public sealed record PlacedHandTile(HandTileView Tile, float X);

/// <summary>
/// The winner's hand laid out in a row for the round result (requested): the concealed tiles sorted by type, the
/// winning tile set apart, then each meld in the order it was called, its called tile turned sideways (a closed kan
/// shows its outer two tiles face down, as on the table).
/// </summary>
public static class WinningHand
{
    public const float GroupGap = 0.4f;

    /// <param name="sidewaysWidth">How many tile widths a sideways tile takes (the face's height over its width).</param>
    public static IReadOnlyList<PlacedHandTile> Arrange(IEnumerable<Tile> concealed, Tile winningTile,
        IEnumerable<RoundStateCall> calls, float sidewaysWidth)
    {
        if (concealed == null)
            throw new ArgumentNullException(nameof(concealed));
        if (winningTile == null)
            throw new ArgumentNullException(nameof(winningTile));
        if (calls == null)
            throw new ArgumentNullException(nameof(calls));

        var placed = new List<PlacedHandTile>();
        var x = 0f;
        foreach (var tile in concealed.OrderBy(t => (int)t.TileType).ThenBy(t => t.ID))
            x = Place(placed, new HandTileView(tile.TileType, tile.Dora), x, sidewaysWidth);

        x += GroupGap;
        x = Place(placed, new HandTileView(winningTile.TileType, winningTile.Dora, Winning: true), x, sidewaysWidth);

        foreach (var call in calls)
        {
            x += GroupGap;
            var closedKan = call.Type == RoundStateCall.CallType.ClosedKan;
            for (var i = 0; i < call.Tiles.Count; i++)
            {
                var tile = call.Tiles[i];
                var view = new HandTileView(tile.TileType, tile.Dora,
                    Sideways: !closedKan && call.CallTile != null && tile.ID == call.CallTile.ID,
                    FaceDown: closedKan && (i == 0 || i == call.Tiles.Count - 1));
                x = Place(placed, view, x, sidewaysWidth);
            }
        }
        return placed;
    }

    /// <summary>The row's length in tile widths.</summary>
    public static float Width(IReadOnlyList<PlacedHandTile> placed, float sidewaysWidth)
    {
        if (placed.Count == 0)
            return 0;
        var last = placed[placed.Count - 1];
        return last.X + (last.Tile.Sideways ? sidewaysWidth : 1);
    }

    private static float Place(List<PlacedHandTile> placed, HandTileView tile, float x, float sidewaysWidth)
    {
        placed.Add(new PlacedHandTile(tile, x));
        return x + (tile.Sideways ? sidewaysWidth : 1);
    }
}
