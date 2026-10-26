using System;
using System.Collections.Generic;
using System.Numerics;
using MoonMahjong.Core;
using MoonMahjong.Table.Layout;

namespace MoonMahjong.Table.Presentation;

/// <summary>A seat's wind and score, for the centre console.</summary>
public sealed record ConsoleSeat(Wind Wind, int Points);

/// <summary>The game state the centre console shows. Seats are game seats 0-3; <paramref name="CurrentSeat"/> is -1
/// between turns; <paramref name="LocalSeat"/> is the seat the table is drawn for (the observer).</summary>
public sealed record ConsoleReadout(Wind RoundWind, int CurrentRound, int Honba, int RiichiSticks, int TilesLeft,
    IReadOnlyList<ConsoleSeat> Seats, int CurrentSeat, int LocalSeat);

public enum ConsoleRole
{
    Round,
    TilesLeft,
    Honba,
    RiichiSticks,
    Seat,
}

/// <summary>One line of text lying on the console top. <see cref="Center"/> is in TableLayout's space (Unity axes,
/// observer at -Z, one unit = one tile width) from the console top's centre; <see cref="Yaw"/> turns the text, read
/// upright from -Z at 0, about +Y as Unity's <c>Quaternion.Euler(0, yaw, 0)</c>. <see cref="Size"/> is the text box
/// (width along the text, height across it) and <see cref="EmHeight"/> the largest font size that box should use, both
/// in tile widths. <see cref="Seat"/> is -1 for the centre lines.</summary>
public sealed record ConsoleLabel(ConsoleRole Role, int Seat, string Text, Vector3 Center, float Yaw, Vector2 Size,
    float EmHeight, bool Highlighted);

/// <summary>
/// Lays the game state out on the centre console, as on an automatic table (requested in place of the screen's info
/// plate): the round, tiles left and honba/riichi counts in the middle, facing the local player, and each seat's wind
/// and score along its own edge, facing that seat. Everything stays inside the line where riichi sticks rest.
/// </summary>
public static class ConsoleLayout
{
    // OpenRiichi's RenderTable.RiichiOffset: a declared riichi stick lies across each seat's edge of the centre piece.
    private static readonly float StickReach = TableLayout.CenterPieceSize.X / 2 * 0.8f;
    private static readonly float InnerHalf = StickReach - TableLayout.RiichiStickSize.Z / 2 - 0.08f;

    private const float SeatHeight = 0.6f;
    private const float SeatEm = 0.5f;
    private const float BlockGap = 0.1f;
    private const float LineGap = 0.07f;

    // The middle block's lines, top (far side) to bottom: role, box height, font size. Honba and riichi get a short
    // line each: as one line ("0 honba   0 riichi") auto-sizing shrank it to about 5 px tall at 1080p.
    private static readonly (ConsoleRole Role, float Height, float Em)[] CentreLines =
    {
        (ConsoleRole.Round, 0.74f, 0.62f),
        (ConsoleRole.TilesLeft, 0.6f, 0.5f),
        (ConsoleRole.Honba, 0.58f, 0.48f),
        (ConsoleRole.RiichiSticks, 0.58f, 0.48f),
    };

    public static IReadOnlyList<ConsoleLabel> Arrange(ConsoleReadout readout)
    {
        Validate(readout);
        var labels = new List<ConsoleLabel>();
        AddCentre(readout, labels);
        for (var seat = 0; seat < 4; seat++)
            labels.Add(SeatLabel(readout, seat));
        return labels;
    }

    private static void Validate(ConsoleReadout readout)
    {
        if (readout == null)
            throw new ArgumentNullException(nameof(readout));
        if (readout.Seats == null || readout.Seats.Count != 4)
            throw new ArgumentException("The console shows four seats.", nameof(readout));
        if (readout.LocalSeat < 0 || readout.LocalSeat > 3)
            throw new ArgumentOutOfRangeException(nameof(readout), readout.LocalSeat, "The local seat must be 0 to 3.");
        if (readout.CurrentSeat < -1 || readout.CurrentSeat > 3)
            throw new ArgumentOutOfRangeException(nameof(readout), readout.CurrentSeat, "The current seat must be 0 to 3, or -1.");
    }

    private static void AddCentre(ConsoleReadout readout, List<ConsoleLabel> labels)
    {
        var width = 2 * (InnerHalf - SeatHeight - BlockGap);
        var stack = LineGap * (CentreLines.Length - 1);
        foreach (var line in CentreLines)
            stack += line.Height;

        var top = stack / 2;
        foreach (var (role, height, em) in CentreLines)
        {
            var text = role switch
            {
                ConsoleRole.Round => TableText.RoundName(readout.RoundWind, readout.CurrentRound),
                ConsoleRole.TilesLeft => $"{readout.TilesLeft} tiles",
                ConsoleRole.Honba => $"{readout.Honba} honba",
                _ => $"{readout.RiichiSticks} riichi",
            };
            labels.Add(new ConsoleLabel(role, -1, text, new Vector3(0, 0, top - height / 2), 0, new Vector2(width, height), em, false));
            top -= height + LineGap;
        }
    }

    // Seat `seat` sits `relative` places after the local player: 0 near (-Z), 1 right (+X), 2 far (+Z), 3 left (-X).
    private static ConsoleLabel SeatLabel(ConsoleReadout readout, int seat)
    {
        var relative = (seat - readout.LocalSeat + 4) % 4;
        var distance = InnerHalf - SeatHeight / 2;
        var center = relative switch
        {
            0 => new Vector3(0, 0, -distance),
            1 => new Vector3(distance, 0, 0),
            2 => new Vector3(0, 0, distance),
            _ => new Vector3(-distance, 0, 0),
        };
        float yaw = relative switch { 0 => 0, 1 => -90, 2 => 180, _ => 90 };
        var info = readout.Seats[seat];
        var text = $"{TableText.WindName(info.Wind)[0]}  {TableText.Points(info.Points)}";
        // Boxes end where the neighbouring seats' boxes begin, so adjacent edges never collide at the corners.
        var size = new Vector2(2 * (InnerHalf - SeatHeight), SeatHeight);
        return new ConsoleLabel(ConsoleRole.Seat, seat, text, center, yaw, size, SeatEm, seat == readout.CurrentSeat);
    }
}
