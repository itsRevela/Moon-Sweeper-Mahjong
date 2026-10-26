using System;
using System.Collections.Generic;
using MoonMahjong.Core;

namespace MoonMahjong.Table.Presentation;

/// <summary>
/// The call shown as a hologram over the table when a round ends, before its result (requested): <see cref="Text"/>
/// above each of <see cref="Seats"/> (none: at the centre), a smaller <see cref="Detail"/> line under it, and the tile
/// the round was won on (<see cref="WinningTile"/>, an ID) highlighted.
/// </summary>
public sealed record RoundEndCall(string Text, string Detail, IReadOnlyList<int> Seats, int? WinningTile)
{
    /// <param name="tsumoTile">For a tsumo, the ID of the tile drawn (the result's score knows it).</param>
    public static RoundEndCall From(RoundFinishResult result, int? tsumoTile = null)
    {
        if (result == null)
            throw new ArgumentNullException(nameof(result));
        return result.Result switch
        {
            RoundFinishResult.RoundResultEnum.Ron => new RoundEndCall("RON", "", result.WinnerIndices, result.DiscardTile),
            RoundFinishResult.RoundResultEnum.Tsumo => new RoundEndCall("TSUMO", "", result.WinnerIndices, tsumoTile),
            RoundFinishResult.RoundResultEnum.Draw when result.NagashiIndices.Length > 0 =>
                new RoundEndCall("NAGASHI MANGAN", "", result.NagashiIndices, null),
            _ => new RoundEndCall("DRAW", TableText.DrawName(result.DrawType), Array.Empty<int>(), null),
        };
    }
}
