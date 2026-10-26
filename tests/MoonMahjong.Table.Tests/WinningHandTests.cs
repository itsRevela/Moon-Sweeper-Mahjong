using MoonMahjong.Core;
using MoonMahjong.Table.Presentation;

namespace MoonMahjong.Table.Tests;

// The winner's hand as the round result shows it (requested): concealed tiles sorted, the winning tile set apart,
// then the melds with their called tile turned sideways.
public sealed class WinningHandTests
{
    private const float Sideways = 1.4f;

    private static Tile T(int id, TileType type, bool red = false) => new(id, type, red);

    private static readonly Tile[] Concealed = { T(40, TileType.Pin3), T(1, TileType.Man1), T(80, TileType.Sou2), T(41, TileType.Pin3) };
    private static readonly Tile Win = T(2, TileType.Man1);

    private static RoundStateCall Pon() =>
        new(RoundStateCall.CallType.Pon, new List<Tile> { T(120, TileType.Chun), T(121, TileType.Chun), T(122, TileType.Chun) }, T(120, TileType.Chun), 1);

    [Fact]
    public void ConcealedTiles_AreSortedByType()
    {
        var placed = WinningHand.Arrange(Concealed, Win, Array.Empty<RoundStateCall>(), Sideways);

        Assert.Equal(new[] { TileType.Man1, TileType.Pin3, TileType.Pin3, TileType.Sou2 }, placed.Take(4).Select(p => p.Tile.Type));
    }

    [Fact]
    public void WinningTile_FollowsTheHandAfterAGap()
    {
        var placed = WinningHand.Arrange(Concealed, Win, Array.Empty<RoundStateCall>(), Sideways);

        var win = placed.Single(p => p.Tile.Winning);
        Assert.Equal(TileType.Man1, win.Tile.Type);
        Assert.Equal(4 + WinningHand.GroupGap, win.X, 4);
    }

    [Fact]
    public void Melds_FollowTheWinningTile_WithTheCalledTileSideways()
    {
        var placed = WinningHand.Arrange(Concealed, Win, new[] { Pon() }, Sideways);

        var meld = placed.Skip(5).ToList();
        Assert.Equal(3, meld.Count);
        Assert.Equal(5 + 2 * WinningHand.GroupGap, meld[0].X, 4);
        Assert.Equal(new[] { true, false, false }, meld.Select(p => p.Tile.Sideways));
        Assert.Equal(meld[0].X + Sideways, meld[1].X, 4);
    }

    [Fact]
    public void ClosedKan_ShowsItsOuterTilesFaceDown()
    {
        var kan = new RoundStateCall(RoundStateCall.CallType.ClosedKan,
            Enumerable.Range(100, 4).Select(id => T(id, TileType.Haku)).ToList(), null, 0);

        var meld = WinningHand.Arrange(Concealed, Win, new[] { kan }, Sideways).Skip(5).ToList();

        Assert.Equal(new[] { true, false, false, true }, meld.Select(p => p.Tile.FaceDown));
        Assert.DoesNotContain(meld, p => p.Tile.Sideways);
    }

    [Fact]
    public void RedFives_KeepTheirRedTexture()
    {
        var placed = WinningHand.Arrange(new[] { T(16, TileType.Man5, red: true) }, Win, Array.Empty<RoundStateCall>(), Sideways);

        Assert.True(placed.Single(p => p.Tile.Type == TileType.Man5).Tile.RedFive);
    }

    [Fact]
    public void Width_CountsSidewaysTilesAsLonger()
    {
        var placed = WinningHand.Arrange(Concealed, Win, new[] { Pon() }, Sideways);

        Assert.Equal(5 + 2 + Sideways + 2 * WinningHand.GroupGap, WinningHand.Width(placed, Sideways), 4);
    }

    [Fact]
    public void Arrange_RejectsAMissingWinningTile()
    {
        Assert.Throws<ArgumentNullException>(() => WinningHand.Arrange(Concealed, null!, Array.Empty<RoundStateCall>(), Sideways));
    }
}
