using System.Numerics;
using MoonMahjong.Table.Layout;
using static MoonMahjong.Table.Tests.TableTestHelpers;

namespace MoonMahjong.Table.Tests;

// The single OpenRiichi -> plugin conversion: Unity axes, observer at -Z, tile face on mesh +Z, image up on mesh +Y.
public sealed class CoordinateTests
{
    // RenderPlayer.VIEW_ANGLE: the observed hand leans back 0.44 pi from vertical, i.e. 0.06 pi (10.8 degrees) from flat.
    private static readonly float ObserverTilt = (0.5f - 0.44f) * MathF.PI;

    [Fact]
    public void ObserverHandTile_FacesTheObserverWithoutRoll()
    {
        var table = NewTable(observer: 0);
        int id = ExpectedDeal(0, 5)[0][0];

        var pose = table.GetTilePose(id, AfterDeal);

        // Leaning back towards OpenRiichi's high camera: the face points up and towards the observer (-Z).
        AssertVector(new Vector3(0, MathF.Cos(ObserverTilt), -MathF.Sin(ObserverTilt)), FaceNormal(pose));
    }

    [Fact]
    public void ObserverHandTile_IsUprightForTheObserver()
    {
        var table = NewTable(observer: 0);
        int id = ExpectedDeal(0, 5)[0][0];

        var pose = table.GetTilePose(id, AfterDeal);

        // The image top points away from the observer (and up), with no sideways component: readable, not rolled.
        AssertVector(new Vector3(0, MathF.Sin(ObserverTilt), MathF.Cos(ObserverTilt)), ImageUp(pose));
    }

    [Fact]
    public void SpectatorView_SeatZeroHandStandsFacingMinusZ()
    {
        // With observer -1 no hand is tilted: seat 0's tiles stand upright facing the camera side (-Z).
        var table = NewTable(observer: -1);
        int id = ExpectedDeal(0, 5)[0][0];

        var pose = table.GetTilePose(id, AfterDeal);

        AssertVector(-Vector3.UnitZ, FaceNormal(pose));
        AssertVector(Vector3.UnitY, ImageUp(pose));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void OpponentHandTiles_StandUprightFacingTheirOwner(int seatOffset)
    {
        var table = NewTable(observer: 0);
        int id = ExpectedDeal(0, 5)[seatOffset][0];

        var pose = table.GetTilePose(id, AfterDeal);

        AssertVector(SeatDirection(seatOffset), FaceNormal(pose));
        AssertVector(Vector3.UnitY, ImageUp(pose));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void ObserverHand_IsOnMinusZForEveryObserverSeat(int observer)
    {
        var table = NewTable(observer: observer, dealer: 2);
        var hands = ExpectedDeal(2, 5);

        foreach (int id in hands[observer])
        {
            var p = table.GetTilePose(id, AfterDeal).Position;
            Assert.InRange(p.Z, -HandDistance - 1, -HandDistance + 1.5f);
            Assert.InRange(p.X, -7f, 7f);
        }
    }

    [Fact]
    public void NextSeat_SitsOnTheObserversRight()
    {
        var table = NewTable(observer: 3);
        var shimocha = ExpectedDeal(0, 5)[0]; // seat 0 follows seat 3

        foreach (int id in shimocha)
            Assert.InRange(table.GetTilePose(id, AfterDeal).Position.X, HandDistance - 1, HandDistance + 1.5f);
    }

    [Fact]
    public void PondTile_FacesUpAndReadsFromTheObserversSeat()
    {
        var table = NewTable(observer: 0);
        int id = ExpectedDeal(0, 5)[0][0];
        table.TileDiscard(0, id, AfterDeal);

        var pose = table.GetTilePose(id, AfterDeal + 1);

        AssertVector(Vector3.UnitY, FaceNormal(pose));
        AssertVector(Vector3.UnitZ, ImageUp(pose));
    }

    [Fact]
    public void OpponentPondTile_ReadsFromItsOwnersSeat()
    {
        var table = NewTable(observer: 0);
        int id = ExpectedDeal(0, 5)[2][0];
        table.TileDiscard(2, id, AfterDeal);

        var pose = table.GetTilePose(id, AfterDeal + 1);

        AssertVector(Vector3.UnitY, FaceNormal(pose));
        AssertVector(-Vector3.UnitZ, ImageUp(pose));
    }

    [Fact]
    public void WallTile_FacesDown()
    {
        var table = NewTable();
        var dealt = ExpectedDeal(0, 5).SelectMany(h => h).ToHashSet();
        int id = Enumerable.Range(0, 136).First(i => !dealt.Contains(i) && i % 2 == 1);

        AssertVector(-Vector3.UnitY, FaceNormal(table.GetTilePose(id, AfterDeal)));
    }

    [Fact]
    public void LowerWallTile_RestsOnTheFelt()
    {
        var table = NewTable();

        // Before the split nothing has moved: tile 1 is the lower tile of the first stack of wall 0.
        var pose = table.GetTilePose(1, Start);

        Assert.Equal(TableLayout.TileSize.Z / 2, pose.Position.Y, 4);
    }

    [Fact]
    public void UpperWallTile_SitsOneThicknessHigher()
    {
        var table = NewTable();

        var lower = table.GetTilePose(1, Start).Position;
        var upper = table.GetTilePose(0, Start).Position;

        AssertVector(lower + new Vector3(0, TableLayout.TileSize.Z, 0), upper);
    }

    [Fact]
    public void TileSize_IsOpenRiichisTileLowProportions()
    {
        AssertVector(new Vector3(1f, 1.29031f, 0.72787f), TableLayout.TileSize, 1e-3f);
    }

    [Fact]
    public void CenterPiece_SitsOnTheFeltAtTheCentre()
    {
        var table = NewTable(observer: 0);

        AssertVector(Vector3.Zero, table.CenterPiecePose.Position);
        Assert.True(AngleBetween(Quaternion.Identity, table.CenterPiecePose.Rotation) < 1e-4f);
    }
}
