using System.Numerics;
using MoonMahjong.Core;
using MoonMahjong.Table.Layout;
using MoonMahjong.Table.Presentation;
using static MoonMahjong.Table.Tests.TableTestHelpers;

namespace MoonMahjong.Table.Tests;

// The game state shown on the table's centre console (requested in place of the screen's info plate).
public sealed class ConsoleLayoutTests
{
    private static ConsoleReadout Readout(int local = 0, int current = 1) => new(Wind.East, 0, 2, 1, 58,
        new[]
        {
            new ConsoleSeat(Wind.East, 25000), new ConsoleSeat(Wind.South, 31200),
            new ConsoleSeat(Wind.West, 18800), new ConsoleSeat(Wind.North, 25000),
        },
        current, local);

    private static ConsoleLabel Only(IReadOnlyList<ConsoleLabel> labels, ConsoleRole role) => labels.Single(l => l.Role == role);

    private static ConsoleLabel Seat(IReadOnlyList<ConsoleLabel> labels, int seat) =>
        labels.Single(l => l.Role == ConsoleRole.Seat && l.Seat == seat);

    // The label's rectangle on the console top (its yaw is a multiple of 90 degrees).
    private static (float MinX, float MaxX, float MinZ, float MaxZ) Footprint(ConsoleLabel label)
    {
        var turned = MathF.Abs(MathF.Abs(label.Yaw) - 90) < 0.01f;
        var (w, d) = turned ? (label.Size.Y, label.Size.X) : (label.Size.X, label.Size.Y);
        return (label.Center.X - w / 2, label.Center.X + w / 2, label.Center.Z - d / 2, label.Center.Z + d / 2);
    }

    private static bool Overlap((float MinX, float MaxX, float MinZ, float MaxZ) a, (float MinX, float MaxX, float MinZ, float MaxZ) b) =>
        a.MinX < b.MaxX && b.MinX < a.MaxX && a.MinZ < b.MaxZ && b.MinZ < a.MaxZ;

    [Fact]
    public void Centre_ShowsTheRound()
    {
        Assert.Equal("East 1", Only(ConsoleLayout.Arrange(Readout()), ConsoleRole.Round).Text);
    }

    [Fact]
    public void Centre_ShowsTheTilesLeft()
    {
        Assert.Equal("58 tiles", Only(ConsoleLayout.Arrange(Readout()), ConsoleRole.TilesLeft).Text);
    }

    [Fact]
    public void Centre_ShowsTheHonbaCount()
    {
        Assert.Equal("2 honba", Only(ConsoleLayout.Arrange(Readout()), ConsoleRole.Honba).Text);
    }

    [Fact]
    public void Centre_ShowsTheRiichiSticksOnTheTable()
    {
        Assert.Equal("1 riichi", Only(ConsoleLayout.Arrange(Readout()), ConsoleRole.RiichiSticks).Text);
    }

    [Fact]
    public void Centre_ReadsTopToBottom_RoundTilesHonbaRiichi()
    {
        var centre = ConsoleLayout.Arrange(Readout()).Where(l => l.Role != ConsoleRole.Seat).OrderByDescending(l => l.Center.Z);

        Assert.Equal(new[] { ConsoleRole.Round, ConsoleRole.TilesLeft, ConsoleRole.Honba, ConsoleRole.RiichiSticks }, centre.Select(l => l.Role));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void Centre_FacesTheLocalPlayer(int local)
    {
        var centre = ConsoleLayout.Arrange(Readout(local)).Where(l => l.Role != ConsoleRole.Seat);

        Assert.All(centre, l => Assert.Equal(0f, l.Yaw));
    }

    [Fact]
    public void EachSeat_ShowsItsWindLetterAndScore()
    {
        var labels = ConsoleLayout.Arrange(Readout());

        Assert.Equal(new[] { "E  25,000", "S  31,200", "W  18,800", "N  25,000" }, Enumerable.Range(0, 4).Select(s => Seat(labels, s).Text));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void LocalPlayersScore_SitsAtTheNearEdgeReadingUpright(int local)
    {
        var mine = Seat(ConsoleLayout.Arrange(Readout(local)), local);

        Assert.Equal(0f, mine.Center.X, 4);
        Assert.True(mine.Center.Z < 0);
        Assert.Equal(0f, mine.Yaw);
    }

    [Fact]
    public void OtherScores_FaceTheirOwnSeats()
    {
        var labels = ConsoleLayout.Arrange(Readout(local: 0));

        // Seat 1 sits on the right (+X), seat 2 opposite (+Z), seat 3 on the left (-X); yaw follows Unity's
        // Quaternion.Euler(0, yaw, 0).
        Assert.Equal((1f, -90f), (MathF.Sign(Seat(labels, 1).Center.X), Seat(labels, 1).Yaw));
        Assert.Equal((1f, 180f), (MathF.Sign(Seat(labels, 2).Center.Z), Seat(labels, 2).Yaw));
        Assert.Equal((-1f, 90f), (MathF.Sign(Seat(labels, 3).Center.X), Seat(labels, 3).Yaw));
    }

    [Fact]
    public void OnlyTheCurrentSeat_IsHighlighted()
    {
        var labels = ConsoleLayout.Arrange(Readout(current: 2));

        Assert.Equal(new[] { 2 }, labels.Where(l => l.Highlighted).Select(l => l.Seat));
    }

    [Fact]
    public void NoSeat_IsHighlightedBetweenTurns()
    {
        Assert.DoesNotContain(ConsoleLayout.Arrange(Readout(current: -1)), l => l.Highlighted);
    }

    [Fact]
    public void Labels_DoNotOverlap()
    {
        var labels = ConsoleLayout.Arrange(Readout());

        for (var i = 0; i < labels.Count; i++)
            for (var j = i + 1; j < labels.Count; j++)
                Assert.False(Overlap(Footprint(labels[i]), Footprint(labels[j])), $"{labels[i].Text} overlaps {labels[j].Text}");
    }

    [Fact]
    public void Labels_LeaveTheRiichiSticksUncovered()
    {
        var table = NewTable(observer: 0);
        for (var seat = 0; seat < 4; seat++)
            table.Riichi(seat, false, AfterDeal);

        foreach (var label in ConsoleLayout.Arrange(Readout(local: 0)))
            for (var seat = 0; seat < 4; seat++)
            {
                var stick = table.GetRiichiStickPose(seat, AfterDeal + 2);
                var along = MathF.Abs(Vector3.Transform(Vector3.UnitX, stick.Rotation).X) > 0.5f;
                var (halfX, halfZ) = along
                    ? (TableLayout.RiichiStickSize.X / 2, TableLayout.RiichiStickSize.Z / 2)
                    : (TableLayout.RiichiStickSize.Z / 2, TableLayout.RiichiStickSize.X / 2);
                var footprint = (stick.Position.X - halfX, stick.Position.X + halfX, stick.Position.Z - halfZ, stick.Position.Z + halfZ);
                Assert.False(Overlap(Footprint(label), footprint), $"{label.Text} covers seat {seat}'s riichi stick");
            }
    }

    [Fact]
    public void Labels_StayOnTheConsole()
    {
        var half = TableLayout.CenterPieceSize.X / 2;

        Assert.All(ConsoleLayout.Arrange(Readout()), l =>
        {
            var (minX, maxX, minZ, maxZ) = Footprint(l);
            Assert.True(minX >= -half && maxX <= half && minZ >= -half && maxZ <= half, l.Text);
        });
    }

    [Fact]
    public void Arrange_RejectsAReadoutWithoutFourSeats()
    {
        var readout = Readout() with { Seats = new[] { new ConsoleSeat(Wind.East, 25000) } };

        Assert.Throws<ArgumentException>(() => ConsoleLayout.Arrange(readout));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public void Arrange_RejectsAnUnknownLocalSeat(int local)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ConsoleLayout.Arrange(Readout(local)));
    }
}
