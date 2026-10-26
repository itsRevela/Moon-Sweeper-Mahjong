namespace MoonMahjong.Table.Layout;

/// <summary>
/// Deliberate departures from OpenRiichi's layout for the Moon Sweepers table (requested by the user 2026-10-25).
/// The 3D table's raised rosewood rail sits where OpenRiichi's hands and call areas meet the table edge, so:
/// </summary>
public static class ModLayout
{
    /// <summary>Each player's hand and call (meld) area move this many tile widths toward their owner (OpenRiichi: 0).
    /// With real-size tiles (TileScale 1) the frame between the rail and the table's edge is 2.35 tile widths deep and a
    /// meld (1.29) plus the standing hand (0.73) needs 2.02, so this centres them on the frame with about 4 mm to spare
    /// on each side; OpenRiichi's position cut 8 mm into the rail. A late kan's second sideways tile still reaches the
    /// rail: the frame is too narrow for two rows of melds.</summary>
    public const float HandOutwardShift = 0.45f;

    /// <summary>The newly drawn tile sits upright to the right of the hand, this many tile widths apart, instead of
    /// OpenRiichi's sideways tile beyond the hand toward the table centre (which the rail clipped).</summary>
    public const float DrawnTileGap = 0.25f;

    /// <summary>Called melds run in a row along the table edge, leftwards from the player's right corner, this many tile
    /// widths apart, as on a real table; OpenRiichi stacked each new meld one meld-depth nearer the centre.</summary>
    public const float MeldGap = 0.1f;
}
