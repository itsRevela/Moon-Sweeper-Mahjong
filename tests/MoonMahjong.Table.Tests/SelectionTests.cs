using MoonMahjong.Core;
using MoonMahjong.Table.Layout;
using static MoonMahjong.Table.Tests.TableTestHelpers;

namespace MoonMahjong.Table.Tests;

// GameRenderView's pick logic: a hovered tile is selectable when the table is active and a group lists it.
public sealed class SelectionTests
{
    private static IReadOnlyList<IReadOnlyList<int>> Groups(params int[][] groups) => groups;

    [Fact]
    public void Find_ReturnsTheFirstGroupListingTheTile()
    {
        Assert.Equal(0, TableLayout.FindTileSelectionGroup(Groups(new[] { 1, 2 }, new[] { 2, 3 }), 2));
    }

    [Fact]
    public void Find_ReturnsNullWhenNoGroupListsTheTile()
    {
        Assert.Null(TableLayout.FindTileSelectionGroup(Groups(new[] { 1, 2 }), 3));
    }

    [Fact]
    public void Find_ReturnsNullForNoTileOrNoGroups()
    {
        Assert.Null(TableLayout.FindTileSelectionGroup(Groups(new[] { 1 }), null));
        Assert.Null(TableLayout.FindTileSelectionGroup(null, 1));
    }

    [Fact]
    public void InactiveTable_SelectsNothing()
    {
        var table = NewTable(observer: 0);
        AssignAll(table, AfterDeal);
        int tile = ExpectedDeal(0, 5)[0][0];
        table.SetTileSelectGroups(Groups(new[] { tile }), AfterDeal);

        Assert.Null(table.GetTileSelectionGroup(tile, AfterDeal));
    }

    [Fact]
    public void ObserverDraw_ActivatesTheTable()
    {
        var table = NewTable(observer: 0);
        AssignAll(table, AfterDeal);
        int tile = ExpectedDeal(0, 5)[0][0];
        table.TileDraw(0, AfterDeal);
        table.SetTileSelectGroups(Groups(new[] { 99 }, new[] { tile }), AfterDeal);

        Assert.Equal(1, table.GetTileSelectionGroup(tile, AfterDeal));
    }

    [Fact]
    public void OpponentDraw_DoesNotActivateTheTable()
    {
        var table = NewTable(observer: 0);
        table.TileDraw(1, AfterDeal);

        Assert.False(table.IsActive(AfterDeal + 1));
    }

    [Fact]
    public void SetActiveTrue_WaitsForTheQueue()
    {
        var table = NewTable(observer: 0);
        table.SetActive(true, Start); // queued behind the round start sequence

        Assert.False(table.IsActive(Start + 1));
        Assert.True(table.IsActive(AfterDeal));
    }

    [Fact]
    public void SetActiveFalse_IsImmediateAndClearsTheGroups()
    {
        var table = NewTable(observer: 0);
        AssignAll(table, AfterDeal);
        int tile = ExpectedDeal(0, 5)[0][0];
        table.SetActive(true, AfterDeal);
        table.SetTileSelectGroups(Groups(new[] { tile }), AfterDeal);
        table.SetActive(false, AfterDeal);
        table.SetActive(true, AfterDeal);

        Assert.True(table.IsActive(AfterDeal));
        Assert.Null(table.GetTileSelectionGroup(tile, AfterDeal));
    }

    [Fact]
    public void UnrevealedTile_MatchesTileZerosGroup()
    {
        // Faithful to OpenRiichi: an unassigned RenderTile carries a Blank Tile with ID 0.
        var table = NewTable(observer: 0);
        table.TileAssignment(new Tile(0, TileType.Man1, false), AfterDeal);
        table.SetActive(true, AfterDeal);
        table.SetTileSelectGroups(Groups(new[] { 0 }), AfterDeal);

        Assert.Equal(0, table.GetTileSelectionGroup(100, AfterDeal));
    }

    [Fact]
    public void NothingHovered_SelectsNothing()
    {
        var table = NewTable(observer: 0);
        table.SetActive(true, AfterDeal);
        table.SetTileSelectGroups(Groups(new[] { 0 }), AfterDeal);

        Assert.Null(table.GetTileSelectionGroup(null, AfterDeal));
    }
}
