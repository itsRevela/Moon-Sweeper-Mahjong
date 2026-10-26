using System.Numerics;
using MoonMahjong.Core;
using MoonMahjong.Table.Layout;
using static MoonMahjong.Table.Tests.TableTestHelpers;

namespace MoonMahjong.Table.Tests;

// Tsumo, ron, exhaustive draws and open riichi: hands laid face up or face down, ura dora flipped.
public sealed class RoundEndTests
{
    private const float Settle = 5f;

    private static void AssertHandFaces(TableLayout table, IEnumerable<int> tiles, Vector3 normal, float time)
    {
        foreach (int id in tiles)
            AssertVector(normal, FaceNormal(table.GetTilePose(id, time)));
    }

    [Fact]
    public void Tsumo_LaysTheClosedHandFaceUp()
    {
        var table = NewTable(observer: 0, dealer: 0);
        var hands = ExpectedDeal(0, 5, out var wall);
        int drawn = wall.DrawWall().ID;
        table.TileDraw(2, AfterDeal);
        table.Tsumo(2, AfterDeal + 1);

        AssertHandFaces(table, hands[2].Append(drawn), Vector3.UnitY, AfterDeal + Settle);
    }

    [Fact]
    public void Tsumo_SetsTheWinningTileApartOnTheRightOfTheHand()
    {
        var table = NewTable(observer: 0, dealer: 0);
        ExpectedDeal(0, 5, out var wall);
        int drawn = wall.DrawWall().ID;
        table.TileDraw(2, AfterDeal);
        table.Tsumo(2, AfterDeal + 1);

        var (lateral, _) = SeatCoords(table.GetTilePose(drawn, AfterDeal + Settle).Position, 2);

        // order_tile(tile, size + 0.5) with 14 tiles: 14.5 - 6.5 = 8 widths right of the hand's centre.
        Assert.Equal(8f, lateral, 3);
    }

    [Fact]
    public void Ron_LaysTheWinnersHandFaceUpAndLeavesTheTileInThePond()
    {
        var table = NewTable(observer: 0, dealer: 0);
        var hands = ExpectedDeal(0, 5);
        int tile = hands[2][0];
        table.TileDiscard(2, tile, AfterDeal);
        var inPond = table.GetTilePose(tile, AfterDeal + 1).Position;
        table.Ron(new[] { 3 }, 2, tile, -1, true, AfterDeal + 1);

        AssertHandFaces(table, hands[3], Vector3.UnitY, AfterDeal + Settle);
        AssertVector(inPond, table.GetTilePose(tile, AfterDeal + Settle).Position);
    }

    [Fact]
    public void Ron_ByARiichiWinner_FlipsTheUraDoraAndLowersTheIndicator()
    {
        var table = NewTable(observer: 0, dealer: 0);
        var hands = ExpectedDeal(0, 5, out var wall);
        int indicator = wall.Dora[0].ID, ura = wall.UraDora[0].ID;
        var indicatorBefore = table.GetTilePose(indicator, AfterDeal).Position;
        var uraBefore = table.GetTilePose(ura, AfterDeal).Position;

        table.Riichi(3, false, AfterDeal);
        table.TileDiscard(3, hands[3][0], AfterDeal);
        table.TileDiscard(2, hands[2][0], AfterDeal + 2);
        table.Ron(new[] { 3 }, 2, hands[2][0], -1, true, AfterDeal + 3);

        var uraAfter = table.GetTilePose(ura, AfterDeal + 3 + Settle);
        var indicatorAfter = table.GetTilePose(indicator, AfterDeal + 3 + Settle);

        AssertVector(Vector3.UnitY, FaceNormal(uraAfter));
        AssertVector(uraBefore, indicatorAfter.Position);
        // The ura dora slides out of the wall by a tile length, towards the wall's own seat (the dealer's: the observer).
        AssertVector(uraBefore - new Vector3(0, 0, TableLayout.TileSize.Y), uraAfter.Position);
        Assert.NotEqual(indicatorBefore, indicatorAfter.Position);
    }

    [Fact]
    public void Ron_WithoutRiichi_LeavesTheUraDoraHidden()
    {
        var table = NewTable(observer: 0, dealer: 0);
        var hands = ExpectedDeal(0, 5, out var wall);
        table.TileDiscard(2, hands[2][0], AfterDeal);
        table.Ron(new[] { 3 }, 2, hands[2][0], -1, true, AfterDeal + 1);

        AssertVector(-Vector3.UnitY, FaceNormal(table.GetTilePose(wall.UraDora[0].ID, AfterDeal + Settle)));
    }

    [Fact]
    public void GameFinished_RonResult_ActsLikeRon()
    {
        var table = NewTable(observer: 0, dealer: 0);
        var hands = ExpectedDeal(0, 5);
        table.TileDiscard(2, hands[2][0], AfterDeal);
        table.GameFinished(RoundFinishResult.Ron(Array.Empty<Scoring>(), new[] { 1 }, 2, hands[2][0], -1), AfterDeal + 1);

        AssertHandFaces(table, hands[1], Vector3.UnitY, AfterDeal + Settle);
    }

    [Fact]
    public void ExhaustiveDraw_LaysTenpaiHandsFaceUp()
    {
        var table = NewTable(observer: 0, dealer: 0);
        var hands = ExpectedDeal(0, 5);
        table.Draw(new[] { 1 }, GameDrawType.EmptyWall, AfterDeal);

        AssertHandFaces(table, hands[1], Vector3.UnitY, AfterDeal + Settle);
    }

    [Fact]
    public void ExhaustiveDraw_TurnsOtherOpponentsHandsFaceDown()
    {
        var table = NewTable(observer: 0, dealer: 0);
        var hands = ExpectedDeal(0, 5);
        table.Draw(new[] { 1 }, GameDrawType.EmptyWall, AfterDeal);

        AssertHandFaces(table, hands[2].Concat(hands[3]), -Vector3.UnitY, AfterDeal + Settle);
    }

    [Fact]
    public void ExhaustiveDraw_AlsoTurnsTheObserversNotenHandFaceDown()
    {
        // Faithful to a Vala quirk: GameScene.observer is never assigned, so the observer is not exempted.
        var table = NewTable(observer: 0, dealer: 0);
        var hands = ExpectedDeal(0, 5);
        table.Draw(new[] { 1 }, GameDrawType.EmptyWall, AfterDeal);

        AssertHandFaces(table, hands[0], -Vector3.UnitY, AfterDeal + Settle);
    }

    [Fact]
    public void ClosedHand_LiesOnTheFelt()
    {
        var table = NewTable(observer: 0, dealer: 0);
        var hands = ExpectedDeal(0, 5);
        table.Draw(Array.Empty<int>(), GameDrawType.EmptyWall, AfterDeal);

        foreach (int id in hands[2])
            Assert.Equal(TableLayout.TileSize.Z / 2, table.GetTilePose(id, AfterDeal + Settle).Position.Y, 4);
    }

    [Fact]
    public void VoidHandDraw_ClosesNoHand()
    {
        var table = NewTable(observer: 0, dealer: 0);
        var hands = ExpectedDeal(0, 5);
        table.Draw(Array.Empty<int>(), GameDrawType.VoidHand, AfterDeal);

        AssertHandFaces(table, hands[2], Vector3.UnitZ, AfterDeal + Settle);
    }

    [Fact]
    public void FourWindsDraw_ChangesNothing()
    {
        var reference = NewTable(observer: 0, dealer: 0);
        var table = NewTable(observer: 0, dealer: 0);
        table.Draw(new[] { 0, 1, 2, 3 }, GameDrawType.FourWinds, AfterDeal);

        Assert.Equal(AllPoses(reference, AfterDeal + Settle), AllPoses(table, AfterDeal + Settle));
    }

    [Fact]
    public void TripleRonDraw_RevealsTheThreeWinners()
    {
        var table = NewTable(observer: 0, dealer: 0);
        var hands = ExpectedDeal(0, 5);
        table.Draw(new[] { 1, 2, 3 }, GameDrawType.TripleRon, AfterDeal);

        AssertHandFaces(table, hands[1].Concat(hands[2]).Concat(hands[3]), Vector3.UnitY, AfterDeal + Settle);
    }

    [Fact]
    public void OpenRiichi_LaysTheHandFaceUp()
    {
        var table = NewTable(observer: 0, dealer: 0);
        var hands = ExpectedDeal(0, 5);
        table.Riichi(2, true, AfterDeal);

        AssertHandFaces(table, hands[2], Vector3.UnitY, AfterDeal + Settle);
    }
}
