using MoonMahjong.Core;
using MoonMahjong.Table.Presentation;

namespace MoonMahjong.Table.Tests;

// Hovering a tile in the hand greys its copies on the board (requested, as in Mahjong Soul): the face-up tiles of that
// type in the ponds, the calls and the dora indicators, never anyone's hand.
public sealed class BoardTilesTests
{
    // A started round on a wall in type order: the dealer's first draw is in hand; nobody has discarded yet.
    private static RoundState Round()
    {
        var wall = Enumerable.Range(0, 136).Select(i => new Tile(-1, TileType.Man1 + i / 4, false)).ToArray();
        var state = RoundState.Custom(ServerSettings.Default, Wind.East, 0, 0, new[] { true, true, true, true }, wall);
        state.Start();
        return state;
    }

    [Fact]
    public void Matching_IncludesTheDoraIndicator()
    {
        var state = Round();
        var indicator = state.Dora[0];

        Assert.Contains(indicator.ID, BoardTiles.Matching(state, indicator.TileType));
    }

    [Fact]
    public void Matching_IncludesDiscards()
    {
        var state = Round();
        var discard = state.CurrentPlayer.Hand[0];
        state.TileDiscard(discard.ID);

        Assert.Contains(discard.ID, BoardTiles.Matching(state, discard.TileType));
    }

    [Fact]
    public void Matching_LeavesHandsAlone()
    {
        var state = Round();
        var inHand = state.GetPlayer(1).Hand[0];

        Assert.DoesNotContain(inHand.ID, BoardTiles.Matching(state, inHand.TileType));
    }

    [Fact]
    public void Matching_OnlyTilesOfThatType()
    {
        var state = Round();
        var indicator = state.Dora[0];
        var other = indicator.TileType == TileType.Chun ? TileType.Man1 : indicator.TileType + 1;

        Assert.DoesNotContain(indicator.ID, BoardTiles.Matching(state, other));
    }

    [Fact]
    public void Matching_NothingForAnUnknownTile()
    {
        Assert.Empty(BoardTiles.Matching(Round(), TileType.Blank));
    }
}
