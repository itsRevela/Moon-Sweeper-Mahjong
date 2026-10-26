using MoonMahjong.Core;
using MoonMahjong.Table.Presentation;

namespace MoonMahjong.Table.Tests;

// The waits popup while choosing a riichi discard (requested, as in Mahjong Soul): the tiles that would win after the
// hovered discard, each with how many the player can't see yet.
public sealed class WaitsTests
{
    private int nextId;

    // "234p45m" -> tiles with fresh IDs (z: 1-4 winds, 5-7 dragons).
    private List<Tile> Tiles(string notation)
    {
        var tiles = new List<Tile>();
        var digits = new List<int>();
        foreach (var c in notation)
        {
            if (char.IsDigit(c))
            {
                digits.Add(c - '0');
                continue;
            }
            var first = c switch { 'm' => TileType.Man1, 'p' => TileType.Pin1, 's' => TileType.Sou1, _ => TileType.Ton };
            foreach (var d in digits)
                tiles.Add(new Tile(nextId++, first + (d - 1), false));
            digits.Clear();
        }
        return tiles;
    }

    private static Tile Of(List<Tile> tiles, TileType type) => tiles.First(t => t.TileType == type);

    // 234p 456p 678s 22s 45m + 7m: throwing 7m waits on 3m and 6m; throwing 4m waits on 6m alone.
    private List<Tile> Hand() => Tiles("234p456p678s22s457m");

    [Fact]
    public void After_ListsEveryTileThatWouldWin()
    {
        var hand = Hand();

        var waits = Waits.After(hand, new List<RoundStateCall>(), Of(hand, TileType.Man7), hand);

        Assert.Equal(new[] { TileType.Man3, TileType.Man6 }, waits.Select(w => w.Type));
    }

    [Fact]
    public void After_DependsOnTheDiscard()
    {
        var hand = Hand();

        var waits = Waits.After(hand, new List<RoundStateCall>(), Of(hand, TileType.Man4), hand);

        Assert.Equal(new[] { TileType.Man6 }, waits.Select(w => w.Type));
    }

    // Throwing one of three 2s keeps the other two as the pair.
    [Fact]
    public void After_ThrowsOnlyTheChosenCopy()
    {
        var hand = Tiles("234p456p678s222s45m");

        var waits = Waits.After(hand, new List<RoundStateCall>(), Of(hand, TileType.Sou2), hand);

        Assert.Equal(new[] { TileType.Man3, TileType.Man6 }, waits.Select(w => w.Type));
    }

    [Fact]
    public void Left_IsFourWhenNoneAreInView()
    {
        var hand = Hand();

        var waits = Waits.After(hand, new List<RoundStateCall>(), Of(hand, TileType.Man7), hand);

        Assert.Equal(new[] { 4, 4 }, waits.Select(w => w.Left));
    }

    [Fact]
    public void Left_DiscountsTilesInView()
    {
        var hand = Hand();
        var ponds = Tiles("66m3m");

        var waits = Waits.After(hand, new List<RoundStateCall>(), Of(hand, TileType.Man7), hand.Concat(ponds));

        Assert.Equal(new[] { 3, 2 }, waits.Select(w => w.Left));
    }

    // A called discard stays in its pond's list and is also in the caller's meld: it is still one tile.
    [Fact]
    public void Left_CountsATileSeenTwiceOnce()
    {
        var hand = Hand();
        var six = Tiles("6m")[0];

        var waits = Waits.After(hand, new List<RoundStateCall>(), Of(hand, TileType.Man7), hand.Append(six).Append(six));

        Assert.Equal(3, waits.Single(w => w.Type == TileType.Man6).Left);
    }

    [Fact]
    public void Left_IgnoresTilesNotYetKnown()
    {
        var hand = Hand();
        var hidden = new[] { new Tile(500, TileType.Blank, false), new Tile(501, TileType.Blank, false) };

        var waits = Waits.After(hand, new List<RoundStateCall>(), Of(hand, TileType.Man7), hand.Concat(hidden));

        Assert.Equal(new[] { 4, 4 }, waits.Select(w => w.Left));
    }

    [Fact]
    public void Left_IsZeroWhenEveryCopyIsInView()
    {
        var hand = Hand();
        var threes = Tiles("3333m");

        var waits = Waits.After(hand, new List<RoundStateCall>(), Of(hand, TileType.Man7), hand.Concat(threes));

        Assert.Equal(0, waits.Single(w => w.Type == TileType.Man3).Left);
    }

    [Fact]
    public void After_ADiscardThatLeavesNoWait_HasNone()
    {
        var hand = Hand();

        var waits = Waits.After(hand, new List<RoundStateCall>(), Of(hand, TileType.Sou2), hand);

        Assert.Empty(waits);
    }

    [Fact]
    public void Visible_IncludesTheDoraIndicator()
    {
        var wall = Enumerable.Range(0, 136).Select(i => new Tile(-1, TileType.Man1 + i / 4, false)).ToArray();
        var state = RoundState.Custom(ServerSettings.Default, Wind.East, 0, 0, new[] { true, true, true, true }, wall);
        state.Start();

        Assert.Contains(state.Dora[0], Waits.Visible(state));
    }
}
