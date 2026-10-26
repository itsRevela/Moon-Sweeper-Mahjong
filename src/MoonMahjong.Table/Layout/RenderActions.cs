// Ported from Practice-Riichi-Mahjong source/Game/Rendering/RenderAction.vala (OpenRiichi, GPLv3).
using System.Collections.Generic;
using MoonMahjong.Core;

namespace MoonMahjong.Table.Layout;

// A queued table event. GameScene runs them one at a time; each holds the queue for its Time.Total.
internal abstract record RenderAction(TableAnimationTime Time);

internal sealed record RenderActionDelay(TableAnimationTime Time) : RenderAction(Time);

internal sealed record RenderActionSplitDeadWall(TableAnimationTime Time) : RenderAction(Time);

internal sealed record RenderActionInitialDraw(TableAnimationTime Time, RenderPlayer Player, int Tiles) : RenderAction(Time);

internal sealed record RenderActionDraw(TableAnimationTime Time, RenderPlayer Player) : RenderAction(Time);

internal sealed record RenderActionDrawDeadWall(TableAnimationTime Time, RenderPlayer Player) : RenderAction(Time);

internal sealed record RenderActionDiscard(TableAnimationTime Time, RenderPlayer Player, RenderTile Tile) : RenderAction(Time);

internal sealed record RenderActionRon(
    TableAnimationTime Time,
    RenderPlayer[] Winners,
    RenderPlayer? Discarder,
    RenderTile? Tile,
    RenderPlayer? ReturnRiichiPlayer,
    bool AllowDoraFlip) : RenderAction(Time);

internal sealed record RenderActionTsumo(TableAnimationTime Time, RenderPlayer Player) : RenderAction(Time);

internal sealed record RenderActionRiichi(TableAnimationTime Time, RenderPlayer Player, bool Open) : RenderAction(Time);

internal sealed record RenderActionReturnRiichi(TableAnimationTime Time, RenderPlayer Player) : RenderAction(Time);

internal sealed record RenderActionLateKan(TableAnimationTime Time, RenderPlayer Player, RenderTile Tile) : RenderAction(Time);

internal sealed record RenderActionClosedKan(TableAnimationTime Time, RenderPlayer Player, TileType TileType) : RenderAction(Time);

internal sealed record RenderActionOpenKan(
    TableAnimationTime Time,
    RenderPlayer Player,
    RenderPlayer Discarder,
    RenderTile Tile,
    RenderTile Tile1,
    RenderTile Tile2,
    RenderTile Tile3) : RenderAction(Time);

internal sealed record RenderActionPon(
    TableAnimationTime Time,
    RenderPlayer Player,
    RenderPlayer Discarder,
    RenderTile Tile,
    RenderTile Tile1,
    RenderTile Tile2) : RenderAction(Time);

internal sealed record RenderActionChii(
    TableAnimationTime Time,
    RenderPlayer Player,
    RenderPlayer Discarder,
    RenderTile Tile,
    RenderTile Tile1,
    RenderTile Tile2) : RenderAction(Time);

internal sealed record RenderActionGameDraw(TableAnimationTime Time, List<RenderPlayer> Players, GameDrawType DrawType) : RenderAction(Time);

internal sealed record RenderActionSetActive(bool Active) : RenderAction(TableAnimationTime.Zero);

internal sealed record RenderActionHandReveal(TableAnimationTime Time, RenderPlayer Player) : RenderAction(Time);

internal sealed record RenderActionFlipDora() : RenderAction(TableAnimationTime.Zero);

internal sealed record RenderActionFlipUraDora(TableAnimationTime Time) : RenderAction(Time);
