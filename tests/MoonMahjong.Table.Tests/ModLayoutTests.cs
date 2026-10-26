using System.Numerics;
using MoonMahjong.Core;
using MoonMahjong.Table.Layout;
using static MoonMahjong.Table.Tests.TableTestHelpers;

namespace MoonMahjong.Table.Tests;

// The Moon Sweepers table's departures from OpenRiichi's layout (ModLayout), requested so the rail clips nothing.
public sealed class ModLayoutTests
{
    private const float Settle = 3f;

    [Fact]
    public void DrawnTile_StandsRightOfTheHandWithAGap()
    {
        var table = NewTable(observer: 0, dealer: 0);
        var hands = ExpectedDeal(0, 5, out var wall);
        int drawn = wall.DrawWall().ID;
        table.TileDraw(2, AfterDeal);

        var rightmost = hands[2].Max(id => SeatCoords(table.GetTilePose(id, AfterDeal + Settle).Position, 2).Lateral);
        var (lateral, _) = SeatCoords(table.GetTilePose(drawn, AfterDeal + Settle).Position, 2);

        Assert.Equal(rightmost + 1 + ModLayout.DrawnTileGap, lateral, 3);
    }

    [Fact]
    public void DrawnTile_LinesUpWithTheHand()
    {
        var table = NewTable(observer: 0, dealer: 0);
        var hands = ExpectedDeal(0, 5, out var wall);
        int drawn = wall.DrawWall().ID;
        table.TileDraw(2, AfterDeal);

        var handPose = table.GetTilePose(hands[2][0], AfterDeal + Settle);
        var drawnPose = table.GetTilePose(drawn, AfterDeal + Settle);

        Assert.Equal(SeatCoords(handPose.Position, 2).Depth, SeatCoords(drawnPose.Position, 2).Depth, 3);
        AssertVector(FaceNormal(handPose), FaceNormal(drawnPose));
    }

    [Fact]
    public void ObserversDrawnTile_StaysUprightInTheTiltedHand()
    {
        var table = NewTable(observer: 0, dealer: 0);
        var hands = ExpectedDeal(0, 5, out var wall);
        int drawn = wall.DrawWall().ID;
        table.TileDraw(0, AfterDeal);

        AssertVector(FaceNormal(table.GetTilePose(hands[0][0], AfterDeal + Settle)),
            FaceNormal(table.GetTilePose(drawn, AfterDeal + Settle)));
    }

    [Fact]
    public void StandingHand_SitsTheOutwardShiftBeyondOpenRiichisHandLine()
    {
        var table = NewTable(observer: 0, dealer: 0);
        var hands = ExpectedDeal(0, 5);

        var (_, depth) = SeatCoords(table.GetTilePose(hands[2][0], AfterDeal).Position, 2);

        // The hand line is the standing tiles' inner face, so a tile's centre is half a thickness further out.
        Assert.Equal(HandDistance + ModLayout.HandOutwardShift + TableLayout.TileSize.Z / 2, depth, 3);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void PlacedRiichiStick_LiesOnTopOfTheCentrePiece(int seat)
    {
        // OpenRiichi's centre piece slopes down at its edges and its stick height fits that slope; the plugin's console
        // is flat-topped, so the stick lay inside it and vanished (reported in-game).
        var table = NewTable(observer: 0);
        table.Riichi(seat, false, AfterDeal);

        var stick = table.GetRiichiStickPose(seat, AfterDeal + 2);

        Assert.Equal(TableLayout.CenterPieceSize.Y, stick.Position.Y - TableLayout.RiichiStickSize.Y / 2, 4);
    }

    // The hand area (hands, drawn tile, melds) stands on the table's frame, which can be higher than the felt.
    private const float Rise = 0.3f;

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void HandArea_RaisesTheStandingHand(int seat)
    {
        var hand = ExpectedDeal(0, 5)[seat];

        var flat = NewTable().GetTilePose(hand[0], AfterDeal).Position.Y;
        var raised = NewTable(handAreaRise: Rise).GetTilePose(hand[0], AfterDeal).Position.Y;

        Assert.Equal(flat + Rise, raised, 3);
    }

    [Fact]
    public void HandArea_RaisesTheMelds()
    {
        var table = NewTable(handAreaRise: Rise);
        var hands = ExpectedDeal(0, 5);
        AssignAll(table, AfterDeal);
        int called = hands[3][0];
        table.TileDiscard(3, called, AfterDeal);
        table.Pon(0, 3, called, hands[0][0], hands[0][1], AfterDeal + 1);

        foreach (int id in new[] { called, hands[0][0], hands[0][1] })
            Assert.Equal(TableLayout.TileSize.Z / 2 + Rise, table.GetTilePose(id, AfterDeal + 3).Position.Y, 3);
    }

    [Fact]
    public void HandArea_LeavesTheWallOnTheFelt()
    {
        ExpectedDeal(0, 5, out var wall);
        int next = wall.DrawWall().ID;

        var flat = NewTable().GetTilePose(next, AfterDeal).Position;
        var raised = NewTable(handAreaRise: Rise).GetTilePose(next, AfterDeal).Position;

        AssertVector(flat, raised);
    }

    [Theory]
    [InlineData(-0.1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void HandArea_RejectsANegativeOrNonFiniteRise(float rise)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TableLayout(0, 0, NewGame(0), new RoundStartInfo(5), TableTimings.Default, Start, rise));
    }
}
