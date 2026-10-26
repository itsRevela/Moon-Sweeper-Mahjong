// Ported from Practice-Riichi-Mahjong source/Game/Rendering/GameRenderView.vala (OpenRiichi, GPLv3).
// Dropped: camera, lights, observe_next/prev, mouse handling, hover sound, options. See docs/table-layout.md.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using MoonMahjong.Core;

namespace MoonMahjong.Table.Layout;

/// <summary>
/// Where every tile and riichi stick on the table is, and how it moves: a port of OpenRiichi's 3D table
/// (<c>GameRenderView</c> and the <c>Render*</c> classes) without the rendering. Feed it the round's events with the
/// current time, then ask for poses each frame.
/// </summary>
/// <remarks>
/// <para><b>Time.</b> Every method takes the current time in seconds. Times must never decrease. Events are queued and
/// played one after another exactly as OpenRiichi plays them (an event waits until the previous one's animation time has
/// passed); queries advance the model to the given time. The same calls with the same times always give the same poses.</para>
/// <para><b>Coordinates.</b> Internally the model keeps OpenRiichi's space (right-handed, Y up, seat <c>i</c> turned
/// <c>i</c> quarter turns about +Y). Every pose it returns is converted once, to table-local plugin space:
/// Unity's left-handed axes with Y up; the table centre at the origin and the felt at Y = 0; the observer's seat on -Z,
/// looking towards +Z, so +X is the observer's right and the next seat in turn order (shimocha) sits at +X; one unit is
/// one tile width (multiply positions by the real tile width). Tile rotations are for a tile mesh with its face on local
/// +Z, its face image upright along local +Y and its width along X; tile proportions are <see cref="TileSize"/>.</para>
/// </remarks>
public sealed class TableLayout
{
    /// <summary>GameRenderView's tile_scale: OpenRiichi draws its tile model at 1.5 times its modelled size.</summary>
    private const float TileScale = 1.5f;

    private static readonly Vector3 OpenRiichiTileSize = OpenRiichiModels.TileObb * TileScale;

    private readonly SceneGraph graph;
    private readonly GameScene scene;
    private readonly TableCoordinates coordinates;
    private readonly TableTimings timings;
    private IReadOnlyList<IReadOnlyList<int>>? selectGroups;

    /// <summary>
    /// Builds the table for a round and queues OpenRiichi's round start at <paramref name="time"/>: a 0.5 s pause, the
    /// wall split (dead wall from <see cref="RoundStartInfo.WallIndex"/>), the deal (4, 4, 4 then 1 tile per player,
    /// starting with the dealer) and the first dora flip.
    /// </summary>
    /// <param name="observerSeat">The seat whose hand faces the camera (bottom of the table), or -1 for a spectator
    /// (seen from seat 0, no hand tilted).</param>
    /// <param name="dealer">The dealer's seat.</param>
    /// <param name="gameStart">The game's start info; it must have four players. (Its animation timings were not ported to
    /// Core, so they are passed separately in <paramref name="timings"/>.)</param>
    /// <param name="roundStart">The round's start info; <see cref="RoundStartInfo.WallIndex"/> must be a dice sum, 2 to 12.</param>
    /// <param name="timings">Animation timings; use <see cref="TableTimings.Default"/> for OpenRiichi's.</param>
    /// <param name="time">The current time, in seconds.</param>
    /// <param name="handAreaRise">Mod addition: how far above the felt the players' hand areas (hands, drawn tiles and
    /// melds) stand, in tile widths, for a table whose frame is higher than its felt. 0 (OpenRiichi's flat table) or more.</param>
    public TableLayout(int observerSeat, int dealer, GameStartInfo gameStart, RoundStartInfo roundStart, TableTimings timings, float time,
        float handAreaRise = 0)
    {
        if (observerSeat < -1 || observerSeat > 3)
            throw new ArgumentOutOfRangeException(nameof(observerSeat), observerSeat, "The observer seat must be 0 to 3, or -1 for a spectator.");
        CheckSeat(dealer, nameof(dealer));
        if (gameStart == null)
            throw new ArgumentNullException(nameof(gameStart));
        if (gameStart.Players == null || gameStart.Players.Length != 4)
            throw new ArgumentException("The table has four seats; the game must have four players.", nameof(gameStart));
        if (roundStart == null)
            throw new ArgumentNullException(nameof(roundStart));
        if (roundStart.WallIndex < 2 || roundStart.WallIndex > 12)
            throw new ArgumentOutOfRangeException(nameof(roundStart), roundStart.WallIndex, "The wall index must be a dice sum, 2 to 12.");
        this.timings = timings ?? throw new ArgumentNullException(nameof(timings));
        CheckTime(time, nameof(time));
        if (!float.IsFinite(handAreaRise) || handAreaRise < 0)
            throw new ArgumentOutOfRangeException(nameof(handAreaRise), handAreaRise, "The hand area rise must be zero or a positive number of tile widths.");

        graph = new SceneGraph(time);
        var context = new GameRenderContext(timings, TileScale, OpenRiichiTileSize, observerSeat, dealer, roundStart.WallIndex,
            handAreaRise * OpenRiichiTileSize.X);
        int observerIndex = observerSeat != -1 ? observerSeat : 0;

        scene = new GameScene(context, graph);
        coordinates = new TableCoordinates(scene.Players[observerIndex].Node.Rotation, OpenRiichiTileSize.X);

        var center = scene.Table.CenterPiece.WorldTransform;
        CenterPiecePose = new ObjectPose(coordinates.ToPluginPosition(center.Position), coordinates.ToPluginObjectRotation(center.Rotation));

        BufferAction(new RenderActionDelay(TableAnimationTime.Preset(0.5f)));
        BufferAction(new RenderActionSplitDeadWall(timings.SplitWall));

        for (int i = 0; i < 16; i++)
            BufferAction(new RenderActionInitialDraw(timings.InitialDraw, scene.Players[(i + dealer) % 4], i < 12 ? 4 : 1));

        BufferAction(new RenderActionFlipDora());
        scene.AdvanceTo(time);
    }

    /// <summary>A tile's size in plugin units (X width = 1, Y face height, Z thickness): OpenRiichi's tile_low proportions,
    /// which its layout spacing is built on (about 26 : 33.5 : 18.9).</summary>
    public static Vector3 TileSize { get; } = new(1, OpenRiichiTileSize.Z / OpenRiichiTileSize.X, OpenRiichiTileSize.Y / OpenRiichiTileSize.X);

    /// <summary>The riichi stick's bounding box in plugin units (X length = 4, Y thickness, Z width). Its pivot is the
    /// stick model's origin, within 0.03 units of the box centre.</summary>
    public static Vector3 RiichiStickSize { get; } = OpenRiichiModels.StickObb * (4 / OpenRiichiModels.StickObb.X);

    /// <summary>The centre piece's bounding box in plugin units (OpenRiichi scales it to 2.9 tile widths per model unit).
    /// Its pivot is the centre of its base, on the felt.</summary>
    public static Vector3 CenterPieceSize { get; } = OpenRiichiModels.CenterPieceObb * 2.9f;

    /// <summary>The centre piece's pose (it never moves).</summary>
    public ObjectPose CenterPiecePose { get; }

    /// <summary>The latest time the model has been advanced to.</summary>
    public float CurrentTime => graph.CurrentTime;

    /// <summary>tile_assignment: reveals a tile's type (and red-five flag). Takes effect immediately.</summary>
    public void TileAssignment(Tile tile, float time)
    {
        if (tile == null)
            throw new ArgumentNullException(nameof(tile));
        CheckTileId(tile.ID, nameof(tile));
        Advance(time);

        scene.Tiles[tile.ID].Tile = new Tile(tile.ID, tile.TileType, tile.Dora);
    }

    /// <summary>tile_draw: the player draws the next tile of the live wall.</summary>
    public void TileDraw(int player, float time)
    {
        CheckSeat(player, nameof(player));
        Advance(time);
        BufferAction(new RenderActionDraw(timings.TileDraw, scene.Players[player]));
        scene.AdvanceTo(time);
    }

    /// <summary>dead_tile_draw: the player draws a replacement tile after a kan; this also flips the next dora and moves
    /// the live wall's last tile to the dead wall, as OpenRiichi does. <see cref="OpenKan"/> already calls it; after a
    /// closed or late kan, call it when the calls are finished.</summary>
    public void DeadTileDraw(int player, float time)
    {
        CheckSeat(player, nameof(player));
        Advance(time);
        BufferDeadTileDraw(player);
        scene.AdvanceTo(time);
    }

    /// <summary>tile_discard: the player discards a tile from their hand into their pond.</summary>
    public void TileDiscard(int player, int tileId, float time)
    {
        CheckSeat(player, nameof(player));
        CheckTileId(tileId, nameof(tileId));
        Advance(time);
        BufferAction(new RenderActionDiscard(timings.TileDiscard, scene.Players[player], scene.Tiles[tileId]));
        scene.AdvanceTo(time);
    }

    /// <summary>flip_dora: flips the next dora indicator immediately (not queued). OpenRiichi's client never calls this
    /// (the round start and <see cref="DeadTileDraw"/> flip doras); it fails before the wall has been split.</summary>
    public void FlipDora(float time)
    {
        Advance(time);
        scene.Wall.FlipDora();
    }

    /// <summary>Queues the ura-dora flip. <see cref="Ron"/> and <see cref="Tsumo"/> already queue it when a winner is in
    /// riichi, as OpenRiichi does; calling it again lowers the dora indicators a second time.</summary>
    public void FlipUraDora(float time)
    {
        Advance(time);
        BufferAction(new RenderActionFlipUraDora(TableAnimationTime.Zero));
        scene.AdvanceTo(time);
    }

    /// <summary>riichi: shows the player's stick and slides it to the centre; the next discard lies sideways. An open
    /// riichi also lays the hand face up.</summary>
    public void Riichi(int player, bool open, float time)
    {
        CheckSeat(player, nameof(player));
        Advance(time);
        BufferAction(new RenderActionRiichi(timings.Riichi, scene.Players[player], open));
        scene.AdvanceTo(time);
    }

    /// <summary>late_kan: adds the tile to the player's pon of the same type (the tile's type must be known).</summary>
    public void LateKan(int player, int tileId, float time)
    {
        CheckSeat(player, nameof(player));
        CheckTileId(tileId, nameof(tileId));
        Advance(time);
        BufferAction(new RenderActionLateKan(timings.Call, scene.Players[player], scene.Tiles[tileId]));
        scene.AdvanceTo(time);
    }

    /// <summary>closed_kan: moves the player's four tiles of <paramref name="type"/> (types must be known) to the call
    /// area, the middle two face down.</summary>
    public void ClosedKan(int player, TileType type, float time)
    {
        CheckSeat(player, nameof(player));
        Advance(time);
        BufferAction(new RenderActionClosedKan(timings.Call, scene.Players[player], type));
        scene.AdvanceTo(time);
    }

    /// <summary>open_kan: the called tile and three from the hand go to the caller's call area, then the caller draws
    /// from the dead wall (<see cref="DeadTileDraw"/> is queued here, as in OpenRiichi).</summary>
    public void OpenKan(int player, int discardPlayer, int tileId, int tile1Id, int tile2Id, int tile3Id, float time)
    {
        CheckSeat(player, nameof(player));
        CheckSeat(discardPlayer, nameof(discardPlayer));
        CheckTileId(tileId, nameof(tileId));
        CheckTileId(tile1Id, nameof(tile1Id));
        CheckTileId(tile2Id, nameof(tile2Id));
        CheckTileId(tile3Id, nameof(tile3Id));
        Advance(time);

        var tiles = scene.Tiles;
        BufferAction(new RenderActionOpenKan(timings.Call, scene.Players[player], scene.Players[discardPlayer],
            tiles[tileId], tiles[tile1Id], tiles[tile2Id], tiles[tile3Id]));
        BufferDeadTileDraw(player);
        scene.AdvanceTo(time);
    }

    /// <summary>pon: the called tile (turned towards the discarder) and two from the hand go to the caller's call area.</summary>
    public void Pon(int player, int discardPlayer, int tileId, int tile1Id, int tile2Id, float time)
    {
        CheckSeat(player, nameof(player));
        CheckSeat(discardPlayer, nameof(discardPlayer));
        CheckTileId(tileId, nameof(tileId));
        CheckTileId(tile1Id, nameof(tile1Id));
        CheckTileId(tile2Id, nameof(tile2Id));
        Advance(time);

        var tiles = scene.Tiles;
        BufferAction(new RenderActionPon(timings.Call, scene.Players[player], scene.Players[discardPlayer],
            tiles[tileId], tiles[tile1Id], tiles[tile2Id]));
        scene.AdvanceTo(time);
    }

    /// <summary>chii: the called tile (sideways, on the left) and two from the hand go to the caller's call area.</summary>
    public void Chii(int player, int discardPlayer, int tileId, int tile1Id, int tile2Id, float time)
    {
        CheckSeat(player, nameof(player));
        CheckSeat(discardPlayer, nameof(discardPlayer));
        CheckTileId(tileId, nameof(tileId));
        CheckTileId(tile1Id, nameof(tile1Id));
        CheckTileId(tile2Id, nameof(tile2Id));
        Advance(time);

        var tiles = scene.Tiles;
        BufferAction(new RenderActionChii(timings.Call, scene.Players[player], scene.Players[discardPlayer],
            tiles[tileId], tiles[tile1Id], tiles[tile2Id]));
        scene.AdvanceTo(time);
    }

    /// <summary>ron: takes the tile out of the discarder's pond list at once (it stays where it lies), then queues the win:
    /// closed winning hands are laid face up, a returned riichi stick slides back, and the ura dora are flipped when a
    /// winner is in riichi and <paramref name="allowDoraFlip"/> is set. Pass -1 for "none" in the index arguments.</summary>
    public void Ron(int[] winnerIndices, int discardPlayerIndex, int tileId, int returnRiichiIndex, bool allowDoraFlip, float time)
    {
        CheckRon(winnerIndices, discardPlayerIndex, tileId, returnRiichiIndex);
        Advance(time);
        DoRon(winnerIndices, discardPlayerIndex, tileId, returnRiichiIndex, allowDoraFlip);
        scene.AdvanceTo(time);
    }

    /// <summary>tsumo: the drawn tile is set beside the hand, a closed hand is laid face up, and the ura dora are flipped if
    /// the winner is in riichi.</summary>
    public void Tsumo(int player, float time)
    {
        CheckSeat(player, nameof(player));
        Advance(time);
        BufferAction(new RenderActionTsumo(timings.Win, scene.Players[player]));
        scene.AdvanceTo(time);
    }

    /// <summary>draw: a triple ron shows as a ron by <paramref name="tenpaiIndices"/>; an empty wall, four riichi or void
    /// hand lays tenpai hands face up and (except for a void hand) turns the others face down. Other draws change
    /// nothing, as in OpenRiichi.</summary>
    public void Draw(int[] tenpaiIndices, GameDrawType drawType, float time)
    {
        CheckSeats(tenpaiIndices, nameof(tenpaiIndices));
        Advance(time);
        DoDraw(tenpaiIndices, drawType);
        scene.AdvanceTo(time);
    }

    /// <summary>game_finished: dispatches a round result to <see cref="Ron"/>, <see cref="Tsumo"/> or <see cref="Draw"/>.</summary>
    public void GameFinished(RoundFinishResult result, float time)
    {
        if (result == null)
            throw new ArgumentNullException(nameof(result));

        switch (result.Result)
        {
            case RoundFinishResult.RoundResultEnum.Draw:
                Draw(result.TenpaiIndices, result.DrawType, time);
                break;
            case RoundFinishResult.RoundResultEnum.Ron:
                Ron(result.WinnerIndices, result.LoserIndex, result.DiscardTile, result.RiichiReturnIndex, true, time);
                break;
            case RoundFinishResult.RoundResultEnum.Tsumo:
                if (result.WinnerIndices.Length == 0)
                    throw new ArgumentException("A tsumo result needs a winner.", nameof(result));
                Tsumo(result.WinnerIndices[0], time);
                break;
            default:
                Advance(time);
                break;
        }
    }

    /// <summary>set_active: activating is queued (tiles become selectable when the queue reaches it); deactivating is
    /// immediate and also clears the selection groups. Drawing or calling as the observer activates the table too.</summary>
    public void SetActive(bool active, float time)
    {
        Advance(time);

        if (active)
            BufferAction(new RenderActionSetActive(active));
        else
            scene.Active = active;

        if (!active)
            selectGroups = null;

        scene.AdvanceTo(time);
    }

    /// <summary>
    /// set_tile_select_groups: what the observer may pick now, as one list of tile IDs per selection group (the IDs of
    /// each Core <c>TileSelectionGroup</c>'s <c>SelectionTiles</c>, in the same order), or null for none.
    /// </summary>
    public void SetTileSelectGroups(IReadOnlyList<IReadOnlyList<int>>? groups, float time)
    {
        if (groups != null && groups.Any(g => g == null))
            throw new ArgumentException("Selection groups must not contain null.", nameof(groups));
        Advance(time);
        selectGroups = groups;
    }

    /// <summary>Whether the table accepts tile picks (GameScene.active).</summary>
    public bool IsActive(float time)
    {
        Advance(time);
        return scene.Active;
    }

    /// <summary>
    /// The index (in the last <see cref="SetTileSelectGroups"/> list) of the selection group the hovered tile belongs
    /// to, or null when the table is inactive, nothing is hovered or the tile is not selectable
    /// (GameRenderView.mouse_move / mouse_event). Highlight that group's highlight tiles and, on click, select the
    /// hovered tile. As in OpenRiichi the hovered tile is matched by its assigned Tile's ID, which is 0 for a tile
    /// whose type is still unknown.
    /// </summary>
    public int? GetTileSelectionGroup(int? hoveredTileId, float time)
    {
        if (hoveredTileId is { } id)
            CheckTileId(id, nameof(hoveredTileId));
        Advance(time);

        if (!scene.Active || hoveredTileId == null)
            return null;

        return FindTileSelectionGroup(selectGroups, scene.Tiles[hoveredTileId.Value].Tile.ID);
    }

    /// <summary>GameRenderView.get_tile_selection_group as a pure function: the index of the first group listing
    /// <paramref name="tileId"/>, or null.</summary>
    public static int? FindTileSelectionGroup(IReadOnlyList<IReadOnlyList<int>>? groups, int? tileId)
    {
        if (tileId == null || groups == null)
            return null;

        for (int i = 0; i < groups.Count; i++)
            foreach (int t in groups[i])
                if (t == tileId.Value)
                    return i;

        return null;
    }

    /// <summary>The pose to draw tile <paramref name="tileId"/> (0 to 135) at, at <paramref name="time"/>.</summary>
    /// <summary>Mod addition: the sounds of every action that started by <paramref name="time"/> and was not taken
    /// before, oldest first (OpenRiichi's GameScene plays them as the actions start).</summary>
    public IReadOnlyList<SoundCue> TakeSounds(float time)
    {
        Advance(time);
        return scene.TakeSounds();
    }

    public TilePose GetTilePose(int tileId, float time)
    {
        CheckTileId(tileId, nameof(tileId));
        Advance(time);

        var tile = scene.Tiles[tileId];
        var world = tile.Node.WorldTransform;
        return new TilePose(
            coordinates.ToPluginPosition(world.Position),
            coordinates.ToPluginTileRotation(world.Rotation),
            tile.Tile.TileType,
            tile.Tile.Dora);
    }

    /// <summary>The pose of the seat's riichi stick, at <paramref name="time"/>; hidden until the seat declares riichi.</summary>
    public StickPose GetRiichiStickPose(int seat, float time)
    {
        CheckSeat(seat, nameof(seat));
        Advance(time);

        var stick = scene.Players[seat].RiichiStick;
        var world = stick.WorldTransform;
        return new StickPose(
            coordinates.ToPluginPosition(world.Position),
            coordinates.ToPluginObjectRotation(world.Rotation),
            stick.Visible);
    }

    // For tests: the OpenRiichi scene behind the poses.
    internal GameScene Scene => scene;

    private void DoRon(int[] winnerIndices, int discardPlayerIndex, int tileId, int returnRiichiIndex, bool allowDoraFlip)
    {
        var players = scene.Players;
        RenderPlayer? discardPlayer = discardPlayerIndex != -1 ? players[discardPlayerIndex] : null;
        RenderPlayer? returnRiichiPlayer = returnRiichiIndex != -1 ? players[returnRiichiIndex] : null;

        RenderTile? tile = null;
        if (tileId != -1)
        {
            tile = scene.Tiles[tileId];
            discardPlayer!.RobTile(tile);
        }

        var winners = new RenderPlayer[winnerIndices.Length];
        for (int i = 0; i < winners.Length; i++)
            winners[i] = players[winnerIndices[i]];

        BufferAction(new RenderActionRon(timings.Win, winners, discardPlayer, tile, returnRiichiPlayer, allowDoraFlip));
    }

    private void DoDraw(int[] tenpaiIndices, GameDrawType drawType)
    {
        if (drawType == GameDrawType.TripleRon)
        {
            DoRon(tenpaiIndices, -1, -1, -1, false);
            return;
        }

        if (drawType == GameDrawType.EmptyWall ||
            drawType == GameDrawType.FourRiichi ||
            drawType == GameDrawType.VoidHand)
        {
            var tenpaiPlayers = new List<RenderPlayer>();
            foreach (int i in tenpaiIndices)
                tenpaiPlayers.Add(scene.Players[i]);

            BufferAction(new RenderActionGameDraw(TableAnimationTime.Zero, tenpaiPlayers, drawType));
        }
    }

    private void BufferDeadTileDraw(int player) =>
        BufferAction(new RenderActionDrawDeadWall(timings.TileDraw, scene.Players[player]));

    private void BufferAction(RenderAction action) => scene.AddAction(action);

    private void Advance(float time)
    {
        CheckTime(time, nameof(time));
        if (time < graph.CurrentTime)
            throw new ArgumentOutOfRangeException(nameof(time), time, $"Time must not go backwards (the model is at {graph.CurrentTime}).");
        scene.AdvanceTo(time);
    }

    private static void CheckRon(int[] winnerIndices, int discardPlayerIndex, int tileId, int returnRiichiIndex)
    {
        CheckSeats(winnerIndices, nameof(winnerIndices));
        if (discardPlayerIndex != -1)
            CheckSeat(discardPlayerIndex, nameof(discardPlayerIndex));
        if (tileId != -1)
        {
            CheckTileId(tileId, nameof(tileId));
            if (discardPlayerIndex == -1)
                throw new ArgumentException("A ron on a discarded tile needs the discarding player.", nameof(discardPlayerIndex));
        }
        if (returnRiichiIndex != -1)
            CheckSeat(returnRiichiIndex, nameof(returnRiichiIndex));
    }

    private static void CheckTime(float time, string name)
    {
        if (float.IsNaN(time) || float.IsInfinity(time))
            throw new ArgumentOutOfRangeException(name, time, "Time must be a finite number of seconds.");
    }

    private static void CheckSeat(int seat, string name)
    {
        if (seat < 0 || seat > 3)
            throw new ArgumentOutOfRangeException(name, seat, "Seats are 0 to 3.");
    }

    private static void CheckSeats(int[] seats, string name)
    {
        if (seats == null)
            throw new ArgumentNullException(name);
        foreach (int seat in seats)
            CheckSeat(seat, name);
    }

    private static void CheckTileId(int tileId, string name)
    {
        if (tileId < 0 || tileId > 135)
            throw new ArgumentOutOfRangeException(name, tileId, "Tile IDs are 0 to 135.");
    }
}
