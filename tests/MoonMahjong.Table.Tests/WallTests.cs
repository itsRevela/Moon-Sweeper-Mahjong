using System.Numerics;
using MoonMahjong.Table.Layout;
using static MoonMahjong.Table.Tests.TableTestHelpers;

namespace MoonMahjong.Table.Tests;

// The wall split, dead wall and dora flips, cross-checked against the rules engine's wall (Core.RoundStateWall),
// which uses the same tile IDs.
public sealed class WallTests
{
    public static TheoryData<int> WallIndices => new() { 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };

    // The engine's dead wall: the 14 tiles after the 122 live ones.
    private static HashSet<int> ExpectedDeadWall(int dealer, int wallIndex)
    {
        int index = (4 - dealer) % 4 * 34 + wallIndex * 2;
        return Enumerable.Range(0, 14).Select(i => (index + 122 + i) % 136).ToHashSet();
    }

    [Theory]
    [MemberData(nameof(WallIndices))]
    public void Split_DeadWallHasTheEnginesFourteenTiles(int wallIndex)
    {
        var table = NewTable(dealer: 1, wallIndex: wallIndex);
        table.GetTilePose(0, AfterDeal);

        var dead = table.Scene.Wall.DeadParts.SelectMany(p => p.Tiles).Select(t => t.Index).ToHashSet();

        Assert.Equal(14, dead.Count);
        Assert.Equal(ExpectedDeadWall(1, wallIndex), dead);
    }

    [Theory]
    [MemberData(nameof(WallIndices))]
    public void AfterDeal_OnlyTheEnginesDoraIndicatorIsFaceUpInTheWall(int wallIndex)
    {
        var table = NewTable(dealer: 3, wallIndex: wallIndex);
        var dealt = ExpectedDeal(3, wallIndex, out var wall).SelectMany(h => h).ToHashSet();
        int indicator = wall.Dora[0].ID;

        foreach (int id in Enumerable.Range(0, 136).Where(i => !dealt.Contains(i)))
        {
            var expected = id == indicator ? Vector3.UnitY : -Vector3.UnitY;
            AssertVector(expected, FaceNormal(table.GetTilePose(id, AfterDeal)));
        }
    }

    [Fact]
    public void DoraIndicator_StaysOnTopOfItsStack()
    {
        var table = NewTable(wallIndex: 9);
        ExpectedDeal(0, 9, out var wall);

        var indicator = table.GetTilePose(wall.Dora[0].ID, AfterDeal).Position;
        var ura = table.GetTilePose(wall.UraDora[0].ID, AfterDeal).Position;

        AssertVector(ura + new Vector3(0, TableLayout.TileSize.Z, 0), indicator);
    }

    [Fact]
    public void Split_OpensAGapOfHalfATileOnEachSideOfTheDeadWall()
    {
        // wall index 9 > 7: the dead wall is stacks 2..8 of the dealer's wall; the live part slides one and a half tile
        // widths one way, the remnant (stacks 0..1) the other way, leaving 0.5 + 1.5 = 2 widths between stack centres.
        var table = NewTable(dealer: 0, wallIndex: 9);
        // The split ends 1.0 s after the start; the first dealt tiles have not started moving yet.
        var poses = AllPoses(table, Start + 1.0f);

        float StackX(int stack) => poses[stack * 2 + 1].Position.X;

        // Wall 0 runs from +8 (stack 0) towards -8 (stack 16) in front of seat 0.
        Assert.Equal(-1.5f, StackX(9) - (8 - 9), 3);
        Assert.Equal(1.5f, StackX(1) - (8 - 1), 3);
        Assert.Equal(8 - 2, StackX(2), 3);
    }

    [Fact]
    public void TileDraw_TakesTheEnginesNextLiveTile()
    {
        var table = NewTable(dealer: 2, wallIndex: 4);
        ExpectedDeal(2, 4, out var wall);
        float t = AfterDeal;

        for (int i = 0; i < 6; i++)
        {
            int seat = (2 + i) % 4;
            int expected = wall.DrawWall().ID;
            table.TileDraw(seat, t);
            Assert.Contains(table.Scene.Players[seat].HandTiles, tile => tile.Index == expected);
            table.TileDiscard(seat, expected, t + 0.4f);
            t += 1;
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(7)]
    [InlineData(12)]
    public void DeadTileDraw_TakesTheEnginesReplacementTile(int wallIndex)
    {
        var table = NewTable(dealer: 0, wallIndex: wallIndex);
        ExpectedDeal(0, wallIndex, out var wall);
        wall.FlipDora();
        int replacement = wall.DrawDeadWall().ID;

        table.DeadTileDraw(0, AfterDeal);

        Assert.Contains(table.Scene.Players[0].HandTiles, tile => tile.Index == replacement);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(7)]
    [InlineData(12)]
    public void DeadTileDraw_FlipsTheEnginesNextDoraIndicator(int wallIndex)
    {
        var table = NewTable(dealer: 0, wallIndex: wallIndex);
        ExpectedDeal(0, wallIndex, out var wall);
        int kanDora = wall.FlipDora().ID;

        table.DeadTileDraw(0, AfterDeal);

        AssertVector(Vector3.UnitY, FaceNormal(table.GetTilePose(kanDora, AfterDeal + 1)));
    }

    [Fact]
    public void DeadTileDraw_MovesTheLastLiveTileToTheDeadWall()
    {
        var table = NewTable(dealer: 0, wallIndex: 5);
        // The engine's live wall starts at ID 10 (wall 0, stack 5) and holds 122 tiles.
        int lastLive = (5 * 2 + 121) % 136;

        table.DeadTileDraw(0, AfterDeal);

        var dead = table.Scene.Wall.DeadParts.SelectMany(p => p.Tiles).Select(t => t.Index).ToList();
        Assert.Contains(lastLive, dead);
        Assert.Equal(14, dead.Count); // 14 - 1 drawn + 1 moved
    }

    [Fact]
    public void FlipDora_FlipsTheEnginesNextIndicatorImmediately()
    {
        var table = NewTable(dealer: 1, wallIndex: 10);
        ExpectedDeal(1, 10, out var wall);
        int next = wall.FlipDora().ID;

        table.FlipDora(AfterDeal);

        AssertVector(Vector3.UnitY, FaceNormal(table.GetTilePose(next, AfterDeal + 0.2f)));
    }

    [Fact]
    public void FlipUraDora_TurnsTheUraIndicatorFaceUp()
    {
        var table = NewTable(dealer: 1, wallIndex: 10);
        ExpectedDeal(1, 10, out var wall);

        table.FlipUraDora(AfterDeal);

        AssertVector(Vector3.UnitY, FaceNormal(table.GetTilePose(wall.UraDora[0].ID, AfterDeal + 0.2f)));
    }

    [Fact]
    public void FlipDora_BeforeTheSplit_Throws()
    {
        var table = NewTable();

        Assert.Throws<InvalidOperationException>(() => table.FlipDora(Start));
    }
}
