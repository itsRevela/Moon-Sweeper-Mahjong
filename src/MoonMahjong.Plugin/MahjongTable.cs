using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Il2CppInterop.Runtime.Attributes;
using MoonCleaners;
using MoonMahjong.Table.Layout;
using MoonMahjong.Table.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MoonMahjong.Plugin;

// One per lounge, on the chess table's GameObject. Owns the mahjong table's look, its four seats, and, while the
// local player sits here, the HUD and the game (Phase 1: the local player against three bots).
// Everything is placed in the chess table's local space (lounges sit at different points on the moon's sphere).
public sealed class MahjongTable : MonoBehaviour
{
    // MoonChessTable.Top: the board surface height in the table's local space (also used by its ViewFrom).
    private const float TableTop = 0.761f;
    private const uint LoungeRenderingLayer = 2; // MoonGameLounge.Awake sets this on every renderer it owns
    private const string TableMeshName = "Chess Table";

    private static MahjongAssets? assets;

    private readonly List<MoonLoungeSpot> seats = new();
    private Transform surface = null!;
    // How far the frame around the rail (where the hands and melds stand) is above the felt, in table-local units.
    private float frameRise;
    // The centre console's top, in the surface's local units (the console is built at the surface's origin).
    private float consoleTop;
    private CenterDisplay? display;
    private MahjongHud? hud;
    private MatchController? match;
    // The match's own time: it runs only while the local player sits here, so standing up pauses the game.
    private MatchClock? clock;
    // [Developer] DevAutoPlay deals by itself this long after sitting, leaving the start plate up for a dev screenshot.
    private const float DevDealDelay = 6f;
    private float? devDealAt;
    private int localSeat = -1;

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
        var local = MoonLoungePlayer.Local;
        localSeat = local == null ? -1 : seats.IndexOf(local.Spot);
        Plugin.Logger.LogInfo($"Local player sat at mahjong seat {localSeat} of {seats.Count} in '{transform.parent?.name}'");
        if (localSeat < 0)
            return;

        hud ??= CreateHud();
        if (match != null && clock != null)
        {
            // Back at the table: carry on where the game paused, facing whichever chair they took.
            var direction = SeatDirection(seats[localSeat]);
            match.Face(direction);
            display?.Face(direction);
            clock.Resume();
            Plugin.Logger.LogInfo($"Mahjong game resumed ({match.Describe().Console?.TilesLeft} tiles left, game clock {clock.Now:F2} s)");
        }
        hud.Show(match?.Describe());
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
        if (localSeat < 0)
            return;
        var local = MoonLoungePlayer.Local;
        if (local == null || local.Spot == null || seats.IndexOf(local.Spot) != localSeat)
        {
            LocalLeft();
            return;
        }

        try
        {
            if (devDealAt is { } dealAt && Time.time >= dealAt)
            {
                devDealAt = null;
                StartMatch(Plugin.Settings.RoundCount.Value);
            }
            if (match != null && clock != null)
            {
                clock.Tick(Time.deltaTime);
                var (pointer, click) = ReadPointer(local);
                match.Update(clock.Now, pointer, click);
                if (match.Finished)
                    EndMatch("game finished");
            }
            var state = match?.Describe();
            hud?.Show(state);
            display?.Show(state?.Console);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Mahjong table stopped the game after an error: {e}");
            EndMatch("error");
        }
    }

    [HideFromIl2Cpp]
    private MahjongHud CreateHud()
    {
        var created = new MahjongHud(transform, assets!, GameLengths.FromRoundCount(Plugin.Settings.RoundCount.Value));
        var callbacks = created.Callbacks;
        callbacks.Start = Deal;
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
    private void Deal(GameLength length) => StartMatch(GameLengths.RoundCount(length));

    private void StartMatch(int roundCount)
    {
        if (match != null || localSeat < 0)
            return;
        var names = Enumerable.Range(0, 4).Select(i => i == localSeat ? "You" : "Bot " + ((i - localSeat + 4) % 4)).ToArray();
        var observer = SeatDirection(seats[localSeat]);
        clock = new MatchClock(Time.time);
        match = new MatchController(surface, assets!, observer, localSeat, names, roundCount,
            Mathf.Max(0f, frameRise / assets!.TileSize.x),
            Plugin.Settings.GameSettings, clock.Now)
        {
            AutoPlayDelay = Plugin.Settings.DevAutoPlay.Value ? 1.2f : null,
            AutoAcknowledgeDelay = Plugin.Settings.DevAutoPlay.Value ? 8f : null, // long enough for a dev screenshot
        };
        hud ??= CreateHud();
        display = new CenterDisplay(surface, consoleTop, assets.TileSize.x, observer, hud.HeadFont, hud.TextFont);
        Plugin.Logger.LogInfo($"Mahjong game started at seat {localSeat} ({roundCount} rounds)");
    }

    private void EndMatch(string reason)
    {
        if (match == null)
            return;
        match.Dispose();
        match = null;
        clock = null;
        display?.Dispose();
        display = null;
        Plugin.Logger.LogInfo($"Mahjong game ended: {reason}");
    }

    // The local player stood up: the game pauses (the table keeps showing it) until they sit back down here.
    private void LocalLeft()
    {
        if (match != null && clock != null && !clock.Paused)
        {
            clock.Pause();
            Plugin.Logger.LogInfo($"Mahjong game paused: the player stood up ({match.Describe().Console?.TilesLeft} tiles left, game clock {clock.Now:F2} s)");
        }
        hud?.Dispose();
        hud = null;
        devDealAt = null;
        localSeat = -1;
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
