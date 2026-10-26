using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Il2CppInterop.Runtime.Attributes;
using MoonCleaners;
using MoonMahjong.Net;
using MoonMahjong.Table.Layout;
using MoonMahjong.Table.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MoonMahjong.Plugin;

// One per lounge, on the chess table's GameObject. Owns the mahjong table's look, its four seats, its crew-play session
// (docs/crew-play.md: the lobby, the deal and the game, hosted here or on a crewmate's PC; alone, it is the local player
// against three bots), the view of the game the table shows (to players and watchers alike), and, while the local
// player sits here, the HUD. The session runs every frame, seated or not: standing up only pauses the game.
// Everything is placed in the chess table's local space (lounges sit at different points on the moon's sphere).
public sealed class MahjongTable : MonoBehaviour
{
    // MoonChessTable.Top: the board surface height in the table's local space (also used by its ViewFrom).
    private const float TableTop = 0.761f;
    private const uint LoungeRenderingLayer = 2; // MoonGameLounge.Awake sets this on every renderer it owns
    private const string TableMeshName = "Chess Table";
    // [Developer] DevAutoPlay deals by itself this long after sitting, leaving the start plate up for a dev screenshot.
    private const float DevDealDelay = 6f;
    // Errors a table recovers from (TableSession.Recover) before it turns itself off for the rest of the session.
    private const int MaxFailures = 3;

    private static MahjongAssets? assets;

    private readonly List<MoonLoungeSpot> seats = new();
    private Transform surface = null!;
    // How far the frame around the rail (where the hands and melds stand) is above the felt, in table-local units.
    private float frameRise;
    // The centre console's top, in the surface's local units (the console is built at the surface's origin).
    private float consoleTop;
    private TableSession? session;
    private LoungeChairs? chairs;
    // The game the table shows, and the last one it stopped showing (so a finished game is not reopened).
    private MatchController? match;
    private TableGame? closedGame;
    private CenterDisplay? display;
    private Vector3 facing;
    private bool shownPaused; // for the log
    private MahjongHud? hud;
    private float? devDealAt;
    private int failures;
    private bool stopped;

    public MahjongTable(IntPtr pointer) : base(pointer)
    {
    }

    // The four mahjong chairs in turn order (counterclockwise seen from above).
    // Managed-only members are hidden from Il2CppInterop's class injection, which cannot map their types.
    [HideFromIl2Cpp]
    internal IReadOnlyList<MoonLoungeSpot> Seats => seats;

    internal void Install(MoonChessTable chessTable)
    {
        HideChess();
        var tableRenderer = FindTableRenderer();
        assets ??= LoadAssets(tableRenderer);

        var feltTop = BuildTableTop(tableRenderer);
        surface = new GameObject("Mahjong surface").transform;
        surface.SetParent(transform, false);
        surface.localPosition = new Vector3(0f, feltTop, 0f);
        BuildConsole();
    }

    internal void SeatsChanged()
    {
        RefreshSeats();
    }

    // MoonLoungePlayer.Enter has seated the local player at one of our chairs.
    internal void LocalSat()
    {
        RefreshSeats();
        var chair = LocalChair();
        Plugin.Logger.LogInfo($"Local player sat at mahjong chair {chair} of {seats.Count} in '{transform.parent?.name}'");
        if (chair < 0)
            return;
        hud ??= CreateHud();
        if (match == null && Plugin.Settings.DevAutoPlay.Value)
            devDealAt = Time.time + DevDealDelay;
    }

    // Where MoonLoungePlayer puts the camera while the local player sits here: behind and above their own seat,
    // looking at the table centre, like MoonChessTable.ViewFrom but for whichever of the four seats it is.
    internal void LocalView(out Vector3 position, out Quaternion rotation)
    {
        var local = MoonLoungePlayer.Local;
        var seatDirection = local != null && local.Spot != null ? SeatDirection(local.Spot) : Vector3.back;
        var config = Plugin.Settings;
        position = transform.TransformPoint(seatDirection * config.ViewDistance.Value + Vector3.up * (TableTop + config.ViewHeight.Value));
        var target = transform.TransformPoint(-seatDirection * 0.05f + Vector3.up * TableTop);
        rotation = Quaternion.LookRotation(target - position, transform.up);
    }

    private void Update()
    {
        if (stopped)
            return;
        try
        {
            if (EnsureSession())
                Step();
        }
        catch (Exception e)
        {
            // Drop the view; a host ends the game for everyone, anyone else is replayed it afresh (TableSession.Recover).
            failures++;
            Plugin.Logger.LogError($"Mahjong table '{session?.Key}' error {failures} of {MaxFailures}: {e}");
            CloseGame();
            session?.Recover();
            if (failures < MaxFailures)
                return;
            hud?.Dispose();
            hud = null;
            stopped = true;
            Plugin.Logger.LogError($"Mahjong table '{session?.Key}' is off until the game restarts");
        }
    }

    private void Step()
    {
        var now = Time.time;
        var table = session!;
        var chair = LocalChair();
        if (chair < 0 && hud != null)
            LocalLeft();

        // A new game gets its view before the session first processes it, so the view sees every round event.
        ShowGame(table.Game, now);
        match?.Advance(now);
        table.Update(now);

        if (devDealAt is { } dealAt && now >= dealAt)
        {
            devDealAt = null;
            Deal();
        }

        HudState? state = null;
        if (match != null && IsDone(match, chair))
            CloseGame();
        if (match != null)
        {
            var game = match.Game;
            if (game.Paused != shownPaused)
            {
                shownPaused = game.Paused;
                Plugin.Logger.LogInfo(game.Paused
                    ? $"Mahjong game {game.Id} paused: waiting for {string.Join(", ", game.WaitingSeats.Select(seat => game.Names[seat]))}"
                    : $"Mahjong game {game.Id} resumed");
            }
            Face(ObserverDirection(game, match.ObserverSeat));
            var playing = chair >= 0 && game.LocalSeat >= 0 && game.ChairOf(game.LocalSeat) == chair;
            var (pointer, click) = playing ? ReadPointer(MoonLoungePlayer.Local!) : (null, false);
            match.Update(now, pointer, click);
            state = match.Describe();
            var notice = TableNotices.During(game, chair);
            state.Status = string.Join("   ", new[] { notice, state.Status }.Where(text => text.Length > 0));
        }
        display?.Show(state?.Console);
        hud?.Show(state, match == null ? Lobby() : null);
    }

    // The table is set up once the lounge has numbered its spots (MoonGameLounge.Awake, which may run after ours).
    private bool EnsureSession()
    {
        if (session != null)
            return true;
        var lounge = GetComponentInParent<MoonGameLounge>();
        if (lounge == null)
            return false;
        RefreshSeats();
        if (seats.Count != 4)
            return false;
        chairs = new LoungeChairs(lounge, seats);
        var key = TableKey(lounge.Key);
        var config = Plugin.Settings;
        session = new TableSession(key, CrewHub.Link, chairs,
            new TableOptions(config.GameSettings, Length: GameLengths.FromRoundCount(config.RoundCount.Value)));
        CrewHub.Register(session);
        Plugin.Logger.LogInfo($"Mahjong table '{key}' ready in '{lounge.name}'");
        return true;
    }

    // The lounge's key names this table to the crew (the game's own lounge messages use it too), within the protocol's limit.
    private static string TableKey(string? key)
    {
        var clipped = TableProtocol.ClipKey(key);
        return clipped.Length > 0 ? clipped : throw new InvalidOperationException("the lounge has no key to name its mahjong table by");
    }

    [HideFromIl2Cpp]
    private void ShowGame(TableGame? game, float now)
    {
        if (game == null || game == match?.Game || game == closedGame)
            return;
        CloseGame();
        var observerSeat = game.LocalSeat >= 0 ? game.LocalSeat : 0;
        facing = ObserverDirection(game, observerSeat);
        match = new MatchController(surface, assets!, game, facing, Mathf.Max(0f, frameRise / assets!.TileSize.x), now)
        {
            AutoPlayDelay = Plugin.Settings.DevAutoPlay.Value ? 1.2f : null,
            AutoAcknowledgeDelay = Plugin.Settings.DevAutoPlay.Value ? 8f : null, // long enough for a dev screenshot
        };
        display = new CenterDisplay(surface, consoleTop, assets.TileSize.x, facing, MoonFonts.WorkshopHead, MoonFonts.WorkshopText);
        Plugin.Logger.LogInfo($"Mahjong game {game.Id} shown: {string.Join(", ", game.Names)} ({game.Length}); local seat {game.LocalSeat}, host {game.Host}");
    }

    // A game leaves the table when its host has left, when the local player closes its final result, or (for a watcher
    // who is not sitting, and so has no result to close) as soon as it is over.
    [HideFromIl2Cpp]
    private static bool IsDone(MatchController shown, int chair)
    {
        var game = shown.Game;
        return game.Ended == EndReason.HostLeft || shown.Finished || game.Over && game.LocalSeat < 0 && chair < 0;
    }

    private void CloseGame()
    {
        if (match == null)
            return;
        closedGame = match.Game;
        Plugin.Logger.LogInfo($"Mahjong game {closedGame.Id} closed ({closedGame.Ended?.ToString() ?? "still running"})");
        match.Dispose();
        match = null;
        display?.Dispose();
        display = null;
    }

    // The chair of the seat the table is drawn for (it moves when a lone player sits down elsewhere).
    [HideFromIl2Cpp]
    private Vector3 ObserverDirection(TableGame game, int observerSeat) => SeatDirection(seats[game.ChairOf(observerSeat)]);

    private void Face(Vector3 direction)
    {
        if (direction == facing)
            return;
        facing = direction;
        match?.Face(direction);
        display?.Face(direction);
    }

    [HideFromIl2Cpp]
    private HudLobby Lobby()
    {
        var table = session!;
        return new HudLobby(table.IsLobbyHost, table.LobbyLength, TableNotices.Players(table.Link, chairs!), TableNotices.Lobby(table));
    }

    [HideFromIl2Cpp]
    private MahjongHud CreateHud()
    {
        var created = new MahjongHud(transform, assets!);
        var callbacks = created.Callbacks;
        callbacks.ChooseLength = ChooseLength;
        callbacks.Deal = Deal;
        callbacks.Acknowledge = () => match?.Acknowledge();
        callbacks.Chii = () => match?.Chii();
        callbacks.Pon = () => match?.Pon();
        callbacks.Kan = () => match?.Kan();
        callbacks.Riichi = () => match?.Riichi();
        callbacks.Tsumo = () => match?.Tsumo();
        callbacks.Ron = () => match?.Ron();
        callbacks.Continue = () => match?.Continue();
        callbacks.VoidHand = () => match?.VoidHand();
        return created;
    }

    [HideFromIl2Cpp]
    private void ChooseLength(GameLength length)
    {
        if (session is { IsLobbyHost: true })
            session.ChooseLength(length);
    }

    // The start plate's Deal (only the table's host has it), or the dev auto-player's.
    private void Deal()
    {
        if (session is not { IsLobbyHost: true, GameRunning: false } || LocalChair() < 0)
            return;
        session.Deal();
        ShowGame(session.Game, Time.time);
    }

    // The local player stood up: the session keeps running (the host pauses the game until they sit back down).
    private void LocalLeft()
    {
        hud?.Dispose();
        hud = null;
        devDealAt = null;
    }

    // The local player's chair at this table, or -1.
    private int LocalChair()
    {
        var local = MoonLoungePlayer.Local;
        if (local == null || local.Spot == null)
            return -1;
        for (var i = 0; i < seats.Count; i++)
            if (seats[i] != null && seats[i].Pointer == local.Spot.Pointer)
                return i;
        return -1;
    }

    // The mouse ray for tile picking; none while the pointer is over the HUD.
    private static (Ray? Pointer, bool Click) ReadPointer(MoonLoungePlayer local)
    {
        var camera = local.walker != null ? local.walker.viewCamera : null;
        var mouse = MoonInput.PlayMouse;
        if (camera == null || mouse == null)
            return (null, false);
        var overHud = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        if (overHud)
            return (null, false);
        var position = mouse.position.ReadValue();
        return (camera.ScreenPointToRay(new Vector3(position.x, position.y, 0f)), mouse.leftButton.wasPressedThisFrame);
    }

    private void RefreshSeats()
    {
        seats.Clear();
        var lounge = GetComponentInParent<MoonGameLounge>();
        var spots = lounge?._Spots_k__BackingField;
        if (spots == null)
            return;
        var found = new List<MoonLoungeSpot>();
        for (var i = 0; i < spots.Length; i++)
        {
            var spot = spots[i];
            if (spot != null && (spot.kind == MoonLoungeSpot.Kind.ChessWhite || spot.kind == MoonLoungeSpot.Kind.ChessBlack))
                found.Add(spot);
        }
        // Counterclockwise seen from above (turn order): angle measured from -Z toward +X.
        seats.AddRange(found.OrderBy(s => SeatAngle(SeatDirection(s))));
    }

    // Table-local unit vector from the centre toward a seat.
    private Vector3 SeatDirection(MoonLoungeSpot spot)
    {
        var local = transform.InverseTransformPoint(spot.transform.position);
        local.y = 0f;
        return local.sqrMagnitude < 1e-6f ? Vector3.back : local.normalized;
    }

    private static float SeatAngle(Vector3 direction)
    {
        var angle = Mathf.Atan2(direction.x, -direction.z) * Mathf.Rad2Deg;
        return angle < 0f ? angle + 360f : angle;
    }

    // Pieces, piece templates, move marks: everything under the table except its own mesh.
    private void HideChess()
    {
        for (var i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i);
            if (child.name != TableMeshName)
                child.gameObject.SetActive(false);
        }
    }

    private MeshRenderer FindTableRenderer()
    {
        var mesh = transform.Find(TableMeshName);
        var renderer = mesh != null ? mesh.GetComponent<MeshRenderer>() : null;
        if (renderer == null)
            throw new InvalidOperationException($"'{name}' has no '{TableMeshName}' MeshRenderer to take the game's material from");
        return renderer;
    }

    private static MahjongAssets LoadAssets(MeshRenderer tableRenderer)
    {
        var directory = Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location)!, "assets");
        var template = tableRenderer.sharedMaterial;
        Plugin.Logger.LogInfo($"Loading mahjong assets from {directory} with shader '{template.shader.name}'");
        return MahjongAssets.Load(directory, template, Plugin.Settings.TileScale.Value);
    }

    // The 3D automatic-table top (frame, rail, recessed felt) over the chess table's top, sized to the table mesh
    // (its bounds are in table-local space). Returns the felt bed's height, where tiles rest.
    private float BuildTableTop(MeshRenderer tableRenderer)
    {
        var meshFilter = tableRenderer.GetComponent<MeshFilter>();
        var bounds = meshFilter.sharedMesh.bounds;
        var side = Mathf.Min(bounds.size.x, bounds.size.z);
        Plugin.Logger.LogInfo($"Chess table mesh bounds {bounds.center} size {bounds.size}; table top side {side:F3}");

        var shell = new GameObject("Mahjong table top").transform;
        shell.SetParent(transform, false);
        shell.localPosition = new Vector3(0f, bounds.max.y, 0f);
        shell.localScale = Vector3.one * side;
        AddMesh(shell, "Frame", assets!.TableFrameMesh, assets.FrameMaterial);
        AddMesh(shell, "Rail", assets.TableRailMesh, assets.RailMaterial);
        AddMesh(shell, "Felt", assets.TableFeltMesh, assets.FeltMaterial);
        // Tiles rest on the felt bed (its mesh is flat at its top); the hand areas stand on the frame (also flat on top).
        frameRise = (assets.TableFrameMesh.bounds.max.y - assets.TableFeltMesh.bounds.max.y) * side;
        return bounds.max.y + assets.TableFeltMesh.bounds.max.y * side;
    }

    // The centre console, sized to OpenRiichi's centre piece (its riichi sticks rest on it; ponds stop at its edge).
    private void BuildConsole()
    {
        var console = new GameObject("Mahjong console").transform;
        console.SetParent(surface, false);
        console.localScale = Vector3.one * (assets!.TileSize.x * TableLayout.CenterPieceSize.X);
        AddMesh(console, "Console", assets.ConsoleMesh, assets.ConsoleMaterial);
        consoleTop = assets.ConsoleMesh.bounds.max.y * console.localScale.y;
    }

    private static void AddMesh(Transform parent, string objectName, Mesh mesh, Material material)
    {
        var part = new GameObject(objectName);
        part.transform.SetParent(parent, false);
        part.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = part.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderingLayerMask = LoungeRenderingLayer;
    }
}
