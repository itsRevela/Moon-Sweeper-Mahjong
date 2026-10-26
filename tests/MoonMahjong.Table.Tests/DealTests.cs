using System.Numerics;
using MoonMahjong.Table.Layout;
using static MoonMahjong.Table.Tests.TableTestHelpers;

namespace MoonMahjong.Table.Tests;

public sealed class DealTests
{
    public static TheoryData<int, int> DealersAndWallIndices => new()
    {
        { 0, 2 }, { 0, 7 }, { 1, 5 }, { 2, 12 }, { 3, 9 }, { 3, 6 },
    };

    [Theory]
    [MemberData(nameof(DealersAndWallIndices))]
    public void AfterDeal_EachSeatHoldsItsThirteenTilesInItsHandArea(int dealer, int wallIndex)
    {
        var table = NewTable(observer: 1, dealer: dealer, wallIndex: wallIndex);
        var hands = ExpectedDeal(dealer, wallIndex);

        for (int seat = 0; seat < 4; seat++)
        {
            Assert.Equal(13, hands[seat].Count);
            int offset = (seat - 1 + 4) % 4;
            foreach (int id in hands[seat])
            {
                var (lateral, depth) = SeatCoords(table.GetTilePose(id, AfterDeal).Position, offset);
                Assert.InRange(depth, HandDistance - 1, HandDistance + 1.5f);
                Assert.InRange(lateral, -6.6f, 6.6f);
            }
        }
    }

    [Theory]
    [MemberData(nameof(DealersAndWallIndices))]
    public void AfterDeal_EveryOtherTileIsInTheWallRing(int dealer, int wallIndex)
    {
        var table = NewTable(dealer: dealer, wallIndex: wallIndex);
        var dealt = ExpectedDeal(dealer, wallIndex).SelectMany(h => h).ToHashSet();

        var wallTiles = Enumerable.Range(0, 136).Where(id => !dealt.Contains(id)).ToList();
        Assert.Equal(136 - 52, wallTiles.Count);

        foreach (int id in wallTiles)
        {
            var p = table.GetTilePose(id, AfterDeal).Position;
            float ring = Math.Max(Math.Abs(p.X), Math.Abs(p.Z));
            Assert.InRange(ring, 9.3f, 10.7f);
        }
    }

    [Theory]
    [MemberData(nameof(DealersAndWallIndices))]
    public void AfterDeal_NoTwoTilesOverlap(int dealer, int wallIndex)
    {
        var table = NewTable(observer: 2, dealer: dealer, wallIndex: wallIndex);

        var pairs = OverlappingPairs(AllPoses(table, AfterDeal));

        Assert.Empty(pairs);
    }

    [Fact]
    public void AfterDeal_NoTwoTileCentresAreCloserThanATileThickness()
    {
        var table = NewTable();
        var poses = AllPoses(table, AfterDeal);

        float closest = float.MaxValue;
        for (int i = 0; i < poses.Length; i++)
            for (int j = i + 1; j < poses.Length; j++)
                closest = Math.Min(closest, Vector3.Distance(poses[i].Position, poses[j].Position));

        Assert.True(closest >= TableLayout.TileSize.Z - Tolerance, $"closest centres {closest}");
    }

    [Fact]
    public void BeforeTheRoundStarts_AllTilesLieFaceDownInTheWalls()
    {
        var table = NewTable();

        foreach (var pose in AllPoses(table, Start))
        {
            AssertVector(-Vector3.UnitY, FaceNormal(pose));
            Assert.InRange(Math.Max(Math.Abs(pose.Position.X), Math.Abs(pose.Position.Z)), 9.3f, 10.7f);
        }
    }

    [Fact]
    public void DuringTheDeal_TilesArriveInDealOrder()
    {
        // The dealer's first four tiles are dealt 1.0 s after the start (pause + split); the next seat's 0.15 s later.
        var table = NewTable(dealer: 0);
        var hands = ExpectedDeal(0, 5);
        float firstDealEnd = Start + 1.0f + 0.15f;

        var dealerTile = table.GetTilePose(hands[0][0], firstDealEnd).Position;
        var nextSeatTile = table.GetTilePose(hands[1][0], firstDealEnd).Position;

        Assert.InRange(-dealerTile.Z, HandDistance - 1, HandDistance + 1.5f);
        Assert.InRange(Math.Max(Math.Abs(nextSeatTile.X), Math.Abs(nextSeatTile.Z)), 9.3f, 10.7f);
    }
}
