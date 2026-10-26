using System.Numerics;
using MoonMahjong.Table.Layout;
using static MoonMahjong.Table.Tests.TableTestHelpers;

namespace MoonMahjong.Table.Tests;

// Ponds (RenderPond) and riichi sticks, seen from the observer (seat 0): the pond starts 3 tile widths in front of the
// centre and grows towards the observer, rows of 6 from the observer's left.
public sealed class DiscardTests
{
    private static readonly float Length = TableLayout.TileSize.Y;
    // On top of the centre piece (ModLayoutTests.PlacedRiichiStick_LiesOnTopOfTheCentrePiece; OpenRiichi: 0.0916817 world).
    private static readonly float StickHeight = TableLayout.CenterPieceSize.Y + TableLayout.RiichiStickSize.Y / 2;
    private static readonly float StickReach = 1.99919f / 2 * 2.9f * 0.8f;   // in tile widths

    private static (TableLayout Table, List<int> Hand) Discarded(int count, bool riichiFirst = false)
    {
        var table = NewTable(observer: 0, dealer: 0);
        var hand = ExpectedDeal(0, 5)[0];
        float t = AfterDeal;
        if (riichiFirst)
            table.Riichi(0, false, t);
        for (int i = 0; i < count; i++)
            table.TileDiscard(0, hand[i], t + i);
        return (table, hand);
    }

    private static float Settled(int count) => AfterDeal + count + 1;

    [Fact]
    public void FirstSixDiscards_FormTheFirstRowFromTheLeft()
    {
        var (table, hand) = Discarded(6);

        for (int i = 0; i < 6; i++)
            AssertVector(new Vector3(-2.5f + i, TableLayout.TileSize.Z / 2, -(3 + Length / 2)), table.GetTilePose(hand[i], Settled(6)).Position);
    }

    [Fact]
    public void SeventhDiscard_StartsTheSecondRowBehindTheFirst()
    {
        var (table, hand) = Discarded(8);

        AssertVector(new Vector3(-2.5f, TableLayout.TileSize.Z / 2, -(3 + Length * 1.5f)), table.GetTilePose(hand[6], Settled(8)).Position);
        AssertVector(new Vector3(-1.5f, TableLayout.TileSize.Z / 2, -(3 + Length * 1.5f)), table.GetTilePose(hand[7], Settled(8)).Position);
    }

    [Fact]
    public void ThirteenthDiscard_StartsTheThirdRow()
    {
        var (table, hand) = Discarded(13);

        AssertVector(new Vector3(-2.5f, TableLayout.TileSize.Z / 2, -(3 + Length * 2.5f)), table.GetTilePose(hand[12], Settled(13)).Position);
    }

    [Fact]
    public void RiichiDiscard_LiesSideways()
    {
        var (table, hand) = Discarded(2, riichiFirst: true);

        var pose = table.GetTilePose(hand[0], Settled(2));

        AssertVector(Vector3.UnitY, FaceNormal(pose));
        Assert.Equal(1f, Math.Abs(WidthAxis(pose).Z), 4);
    }

    [Fact]
    public void RiichiDiscard_IsAlignedToTheFarEdgeOfItsRow()
    {
        var (table, hand) = Discarded(2, riichiFirst: true);

        // x = left edge + half its length; z = row start + length - half a width.
        AssertVector(new Vector3(-3 + Length / 2, TableLayout.TileSize.Z / 2, -(3 + Length - 0.5f)), table.GetTilePose(hand[0], Settled(2)).Position);
    }

    [Fact]
    public void DiscardAfterTheRiichiTile_IsUprightAndMakesRoomForIt()
    {
        var (table, hand) = Discarded(2, riichiFirst: true);

        var pose = table.GetTilePose(hand[1], Settled(2));

        Assert.Equal(1f, Math.Abs(WidthAxis(pose).X), 4);
        Assert.Equal(-3 + Length + 0.5f, pose.Position.X, 4);
    }

    [Fact]
    public void CalledRiichiTile_TurnsTheNextDiscardSideways()
    {
        var table = NewTable(observer: 0, dealer: 0);
        var hands = ExpectedDeal(0, 5);
        float t = AfterDeal;
        table.Riichi(0, false, t);
        table.TileDiscard(0, hands[0][0], t);
        table.Pon(1, 0, hands[0][0], hands[1][0], hands[1][1], t + 2);
        table.TileDiscard(1, hands[1][2], t + 3);
        table.TileDiscard(0, hands[0][1], t + 4);

        var pose = table.GetTilePose(hands[0][1], t + 6);

        Assert.Equal(1f, Math.Abs(WidthAxis(pose).Z), 4);
    }

    [Fact]
    public void RiichiStick_IsHiddenUntilRiichi()
    {
        var table = NewTable();

        Assert.False(table.GetRiichiStickPose(0, AfterDeal).Visible);
    }

    [Fact]
    public void RiichiStick_SlidesToTheCentrePieceEdge()
    {
        var table = NewTable(observer: 0);
        table.Riichi(0, false, AfterDeal);

        var stick = table.GetRiichiStickPose(0, AfterDeal + 1);

        Assert.True(stick.Visible);
        AssertVector(new Vector3(0, StickHeight, -StickReach), stick.Position);
        Assert.True(AngleBetween(Quaternion.Identity, stick.Rotation) < 1e-4f);
    }

    [Fact]
    public void RiichiStick_OfTheNextSeat_LiesAlongThatSeatsEdge()
    {
        var table = NewTable(observer: 0);
        table.Riichi(1, false, AfterDeal);

        var stick = table.GetRiichiStickPose(1, AfterDeal + 1);

        AssertVector(new Vector3(StickReach, StickHeight, 0), stick.Position);
        Assert.Equal(1f, Math.Abs(Vector3.Transform(Vector3.UnitX, stick.Rotation).Z), 4);
    }

    [Fact]
    public void RiichiStick_IsReturnedTowardsItsOwnerWhenTheRiichiDiscardIsRonned()
    {
        var table = NewTable(observer: 0);
        int tile = ExpectedDeal(0, 5)[0][0];
        table.Riichi(0, false, AfterDeal);
        table.TileDiscard(0, tile, AfterDeal);
        table.Ron(new[] { 1 }, 0, tile, 0, true, AfterDeal + 2);

        var stick = table.GetRiichiStickPose(0, AfterDeal + 5);

        AssertVector(new Vector3(0, StickHeight, -HandDistance), stick.Position);
    }

    [Fact]
    public void RiichiStickSize_IsFourTileWidthsLong()
    {
        Assert.Equal(4f, TableLayout.RiichiStickSize.X, 5);
    }
}
