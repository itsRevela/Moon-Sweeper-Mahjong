using System;
using System.Collections.Generic;
using System.Linq;
using MoonMahjong.Core;

namespace MoonMahjong.Table.Presentation;

/// <summary>
/// Hovering a tile in the hand greys its copies on the board (requested, as in Mahjong Soul): the face-up tiles of the
/// same type in every pond, every call and the dora indicators. Hands, the local one included, are left alone.
/// </summary>
public static class BoardTiles
{
    /// <summary>The IDs of the face-up tiles of <paramref name="type"/> on the board.</summary>
    public static ISet<int> Matching(RoundState state, TileType type)
    {
        if (state == null)
            throw new ArgumentNullException(nameof(state));
        var matching = new HashSet<int>();
        if (type == TileType.Blank)
            return matching;
        var board = new List<Tile>(state.Dora);
        for (var seat = 0; seat < 4; seat++)
        {
            var player = state.GetPlayer(seat);
            board.AddRange(player.Pond);
            board.AddRange(player.Calls.SelectMany(call => call.Tiles));
        }
        matching.UnionWith(board.Where(tile => tile.TileType == type).Select(tile => tile.ID));
        return matching;
    }
}
