// Ported from Practice-Riichi-Mahjong source/Game/Logic/AnimationTimings.vala (the render timings only) and the
// values of GameServer/Server/ServerMenu.vala create_start_info (OpenRiichi, GPLv3).
namespace MoonMahjong.Table.Layout;

/// <summary>
/// The animation timings the table uses (the render half of OpenRiichi's <c>AnimationTimings</c>; the score-screen
/// fades and decision times are not the table's business). The server must use the same values for its animation
/// delays, or bots act before the tiles arrive.
/// </summary>
public sealed record TableTimings
{
    public TableTimings(
        TableAnimationTime initialDraw,
        TableAnimationTime tileDraw,
        TableAnimationTime tileDiscard,
        TableAnimationTime call,
        TableAnimationTime handReveal,
        TableAnimationTime splitWall,
        TableAnimationTime doraFlip,
        TableAnimationTime win,
        TableAnimationTime riichi,
        TableAnimationTime handOrder)
    {
        InitialDraw = initialDraw;
        TileDraw = tileDraw;
        TileDiscard = tileDiscard;
        Call = call;
        HandReveal = handReveal;
        SplitWall = splitWall;
        DoraFlip = doraFlip;
        Win = win;
        Riichi = riichi;
        HandOrder = handOrder;
    }

    /// <summary>OpenRiichi's defaults (<c>ServerMenu.create_start_info</c>).</summary>
    public static TableTimings Default { get; } = new(
        initialDraw: new TableAnimationTime(0, 0.15f, 0),
        tileDraw: new TableAnimationTime(0, 0.15f, 0.2f),
        tileDiscard: new TableAnimationTime(0, 0.15f, 0.3f),
        call: new TableAnimationTime(0, 0.5f, 0),
        handReveal: new TableAnimationTime(0, 0.15f, 0.8f),
        splitWall: new TableAnimationTime(0, 0.5f, 0),
        doraFlip: new TableAnimationTime(0, 0.2f, 0),
        win: new TableAnimationTime(0, 0.5f, 0.5f),
        riichi: new TableAnimationTime(0, 0.3f, 0.5f),
        handOrder: new TableAnimationTime(0, 0.15f, 0));

    public TableAnimationTime InitialDraw { get; init; }
    public TableAnimationTime TileDraw { get; init; }
    public TableAnimationTime TileDiscard { get; init; }
    public TableAnimationTime Call { get; init; }
    public TableAnimationTime HandReveal { get; init; }
    public TableAnimationTime SplitWall { get; init; }
    public TableAnimationTime DoraFlip { get; init; }
    public TableAnimationTime Win { get; init; }
    public TableAnimationTime Riichi { get; init; }
    public TableAnimationTime HandOrder { get; init; }
}
