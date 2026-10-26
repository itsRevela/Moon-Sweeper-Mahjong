using MoonMahjong.Core;
using MoonMahjong.Table.Presentation;

namespace MoonMahjong.Table.Tests;

// The call shown as a hologram over the table when a round ends, before the result (requested): RON or TSUMO above
// each winner with the winning tile highlighted, or DRAW at the centre.
public sealed class RoundEndCallTests
{
    [Fact]
    public void Ron_IsCalledAboveTheWinner()
    {
        var call = RoundEndCall.From(RoundFinishResult.Ron(Array.Empty<Scoring>(), new[] { 2 }, 1, 50, -1));

        Assert.Equal(("RON", "2"), (call.Text, string.Join(",", call.Seats)));
    }

    [Fact]
    public void Ron_HighlightsTheDiscardItWonOn()
    {
        var call = RoundEndCall.From(RoundFinishResult.Ron(Array.Empty<Scoring>(), new[] { 2 }, 1, 50, -1));

        Assert.Equal(50, call.WinningTile);
    }

    [Fact]
    public void ADoubleRon_IsCalledAboveBothWinners()
    {
        var call = RoundEndCall.From(RoundFinishResult.Ron(Array.Empty<Scoring>(), new[] { 1, 3 }, 0, 50, -1));

        Assert.Equal(new[] { 1, 3 }, call.Seats);
    }

    [Fact]
    public void Tsumo_IsCalledAboveTheWinner()
    {
        var call = RoundEndCall.From(RoundFinishResult.Tsumo(null!, 3), tsumoTile: 77);

        Assert.Equal(("TSUMO", "3"), (call.Text, string.Join(",", call.Seats)));
    }

    [Fact]
    public void Tsumo_HighlightsTheTileDrawn()
    {
        var call = RoundEndCall.From(RoundFinishResult.Tsumo(null!, 3), tsumoTile: 77);

        Assert.Equal(77, call.WinningTile);
    }

    [Fact]
    public void ADraw_IsCalledAtTheCentre()
    {
        var call = RoundEndCall.From(RoundFinishResult.Draw(new[] { 0, 2 }, Array.Empty<int>(), GameDrawType.EmptyWall));

        Assert.Equal(("DRAW", 0, (int?)null), (call.Text, call.Seats.Count, call.WinningTile));
    }

    [Fact]
    public void ADraw_SaysWhichDraw()
    {
        var call = RoundEndCall.From(RoundFinishResult.Draw(Array.Empty<int>(), Array.Empty<int>(), GameDrawType.FourRiichi));

        Assert.Equal(TableText.DrawName(GameDrawType.FourRiichi), call.Detail);
    }

    [Fact]
    public void NagashiMangan_IsCalledAboveWhoMadeIt()
    {
        var call = RoundEndCall.From(RoundFinishResult.Draw(Array.Empty<int>(), new[] { 1 }, GameDrawType.EmptyWall));

        Assert.Equal(("NAGASHI MANGAN", "1"), (call.Text, string.Join(",", call.Seats)));
    }

    [Fact]
    public void AWinHasNoDetailLine()
    {
        var call = RoundEndCall.From(RoundFinishResult.Ron(Array.Empty<Scoring>(), new[] { 2 }, 1, 50, -1));

        Assert.Equal("", call.Detail);
    }
}
