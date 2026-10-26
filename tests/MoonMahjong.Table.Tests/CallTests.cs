using System.Numerics;
using MoonMahjong.Core;
using MoonMahjong.Table.Layout;
using static MoonMahjong.Table.Tests.TableTestHelpers;

namespace MoonMahjong.Table.Tests;

// Calls (RenderCalls), seen from the observer (seat 0): the call area's corner is 8 world units right of and in front
// of the centre (the observer's near-right corner); melds run leftwards from it, the first one at the near edge.
public sealed class CallTests
{
    private static readonly float Corner = HandDistance;

    // The call area's depth: OpenRiichi's corner plus the mod's outward shift (ModLayout.HandOutwardShift); its
    // lateral extent is unchanged.
    private static readonly float CallDepth = HandDistance + ModLayout.HandOutwardShift;
    private static readonly float Length = TableLayout.TileSize.Y;
    private static readonly float FeltY = TableLayout.TileSize.Z / 2;

    private sealed record Scene(TableLayout Table, List<int>[] Hands, float Settled);

    // `discarder` discards its first tile and the observer calls it with its first two tiles.
    private static Scene Ponned(int discarder)
    {
        var table = NewTable(observer: 0, dealer: 0);
        var hands = ExpectedDeal(0, 5);
        AssignAll(table, AfterDeal);
        int called = hands[discarder][0];
        table.TileDiscard(discarder, called, AfterDeal);
        table.Pon(0, discarder, called, hands[0][0], hands[0][1], AfterDeal + 1);
        return new Scene(table, hands, AfterDeal + 3);
    }

    private static bool IsSideways(TilePose pose) => Math.Abs(WidthAxis(pose).Z) > 0.999f;

    [Fact]
    public void Pon_MovesTheThreeTilesIntoTheCallersCallArea()
    {
        var s = Ponned(discarder: 3);
        int[] meld = { s.Hands[3][0], s.Hands[0][0], s.Hands[0][1] };

        foreach (int id in meld)
        {
            var pose = s.Table.GetTilePose(id, s.Settled);
            AssertVector(Vector3.UnitY, FaceNormal(pose));
            Assert.Equal(FeltY, pose.Position.Y, 4);
            Assert.InRange(pose.Position.X, Corner - 2 - Length, Corner);
            Assert.InRange(pose.Position.Z, -CallDepth, -CallDepth + Length);
        }
    }

    [Fact]
    public void Pon_LeavesElevenTilesInTheHand()
    {
        var s = Ponned(discarder: 3);

        Assert.Equal(11, s.Table.Scene.Players[0].HandTiles.Count);
    }

    [Theory]
    [InlineData(1, 0)] // from the right (shimocha): called tile on the right
    [InlineData(2, 1)] // from across (toimen): in the middle
    [InlineData(3, 2)] // from the left (kamicha): on the left
    public void Pon_TurnsTheCalledTileSidewaysTowardsTheDiscarder(int discarder, int positionFromRight)
    {
        var s = Ponned(discarder);
        int called = s.Hands[discarder][0];

        var byX = new[] { called, s.Hands[0][0], s.Hands[0][1] }
            .Select(id => (Id: id, Pose: s.Table.GetTilePose(id, s.Settled)))
            .OrderByDescending(t => t.Pose.Position.X)
            .ToList();

        Assert.Equal(called, byX[positionFromRight].Id);
        Assert.True(IsSideways(byX[positionFromRight].Pose));
        Assert.Equal(1, byX.Count(t => IsSideways(t.Pose)));
    }

    [Fact]
    public void Pon_SidewaysTileHugsTheNearEdge()
    {
        var s = Ponned(discarder: 3);

        var called = s.Table.GetTilePose(s.Hands[3][0], s.Settled).Position;

        AssertVector(new Vector3(Corner - 2 - Length / 2, FeltY, -(CallDepth - 0.5f)), called);
    }

    [Fact]
    public void Pon_ProducesNoOverlaps()
    {
        var s = Ponned(discarder: 2);

        Assert.Empty(OverlappingPairs(AllPoses(s.Table, s.Settled)));
    }

    [Fact]
    public void Chii_PutsTheCalledTileSidewaysOnTheLeftAndTheRestInOrder()
    {
        var table = NewTable(observer: 0, dealer: 0);
        var hands = ExpectedDeal(0, 5);
        AssignAll(table, AfterDeal);
        int called = hands[3][0];
        int high = hands[0][4], low = hands[0][0]; // types (id / 4 + 1): hands[0][4] is the higher one
        table.TileDiscard(3, called, AfterDeal);
        table.Chii(0, 3, called, high, low, AfterDeal + 1);

        var calledPose = table.GetTilePose(called, AfterDeal + 3);
        float lowX = table.GetTilePose(low, AfterDeal + 3).Position.X;
        float highX = table.GetTilePose(high, AfterDeal + 3).Position.X;

        Assert.True(IsSideways(calledPose));
        Assert.True(calledPose.Position.X < lowX && lowX < highX, $"{calledPose.Position.X} < {lowX} < {highX}");
    }

    [Fact]
    public void OpenKan_PlacesFourTilesAndDrawsAReplacementTile()
    {
        var table = NewTable(observer: 0, dealer: 0);
        var hands = ExpectedDeal(0, 5, out var wall);
        int called = hands[2][0];
        table.TileDiscard(2, called, AfterDeal);
        table.OpenKan(0, 2, called, hands[0][0], hands[0][1], hands[0][2], AfterDeal + 1);
        wall.FlipDora();
        int replacement = wall.DrawDeadWall().ID;

        var meld = new[] { called, hands[0][0], hands[0][1], hands[0][2] }.Select(id => table.GetTilePose(id, AfterDeal + 4)).ToList();

        Assert.All(meld, p => Assert.InRange(p.Position.X, Corner - 3 - Length, Corner));
        Assert.Equal(1, meld.Count(IsSideways));
        Assert.Contains(table.Scene.Players[0].HandTiles, t => t.Index == replacement);
        Assert.Equal(11, table.Scene.Players[0].HandTiles.Count);
    }

    [Fact]
    public void ClosedKan_ShowsTheMiddleTwoTilesFaceDown()
    {
        var table = NewTable(observer: 0, dealer: 0);
        var hand = ExpectedDeal(0, 5)[0];
        AssignAll(table, AfterDeal);
        var quad = hand.Take(4).ToArray();
        foreach (int id in quad)
            table.TileAssignment(new Tile(id, TileType.Chun, false), AfterDeal);
        table.ClosedKan(0, TileType.Chun, AfterDeal);

        var byX = quad.Select(id => table.GetTilePose(id, AfterDeal + 2)).OrderBy(p => p.Position.X).ToList();

        Assert.Equal(new[] { 1f, -1f, -1f, 1f }, byX.Select(p => MathF.Round(FaceNormal(p).Y)).ToArray());
        Assert.All(byX, p => Assert.InRange(p.Position.X, Corner - 4, Corner));
        Assert.All(byX, p => Assert.Equal(-(CallDepth - Length / 2), p.Position.Z, 4));
    }

    [Fact]
    public void LateKan_StacksTheAddedTileBesideTheCalledOne()
    {
        var table = NewTable(observer: 0, dealer: 0);
        var hands = ExpectedDeal(0, 5);
        int called = hands[2][0];
        int[] own = { hands[0][0], hands[0][1], hands[0][2] };
        foreach (int id in own.Append(called))
            table.TileAssignment(new Tile(id, TileType.Haku, false), AfterDeal);
        table.TileDiscard(2, called, AfterDeal);
        table.Pon(0, 2, called, own[0], own[1], AfterDeal + 1);
        table.LateKan(0, own[2], AfterDeal + 3);

        var calledPose = table.GetTilePose(called, AfterDeal + 5);
        var addedPose = table.GetTilePose(own[2], AfterDeal + 5);

        Assert.True(IsSideways(calledPose) && IsSideways(addedPose));
        Assert.Equal(calledPose.Position.X, addedPose.Position.X, 4);
        Assert.Equal(1f, addedPose.Position.Z - calledPose.Position.Z, 4); // one tile width nearer the centre
    }

    // Mod change (requested): calls run in a row along the table edge, leftwards from the corner, a small gap apart.
    // OpenRiichi stacked each new call one call-depth nearer the centre.
    private static (List<TilePose> First, List<TilePose> Second) TwoPons()
    {
        var s = Ponned(discarder: 3);
        int called = s.Hands[1][0];
        s.Table.TileDiscard(1, called, s.Settled);
        s.Table.Pon(0, 1, called, s.Hands[0][2], s.Hands[0][3], s.Settled + 1);

        List<TilePose> Poses(params int[] ids) => ids.Select(id => s.Table.GetTilePose(id, s.Settled + 3)).ToList();
        return (Poses(s.Hands[3][0], s.Hands[0][0], s.Hands[0][1]), Poses(called, s.Hands[0][2], s.Hands[0][3]));
    }

    private static float HalfWidthX(TilePose pose) => IsSideways(pose) ? Length / 2 : 0.5f;

    [Fact]
    public void SecondCall_StaysOnTheFirstCallsRow()
    {
        var (_, second) = TwoPons();

        Assert.All(second, p => Assert.InRange(p.Position.Z, -CallDepth, -CallDepth + Length));
    }

    [Fact]
    public void SecondCall_LiesLeftOfTheFirstWithAGap()
    {
        var (first, second) = TwoPons();

        var firstLeftEdge = first.Min(p => p.Position.X - HalfWidthX(p));
        var secondRightEdge = second.Max(p => p.Position.X + HalfWidthX(p));

        Assert.Equal(firstLeftEdge - ModLayout.MeldGap, secondRightEdge, 3);
    }
}
