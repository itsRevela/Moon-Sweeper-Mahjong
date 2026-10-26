using System;
using System.Collections.Generic;
using MoonMahjong.Table.Layout;
using UnityEngine;
using NVector3 = System.Numerics.Vector3;
using NQuaternion = System.Numerics.Quaternion;

namespace MoonMahjong.Plugin;

// Draws one round's TableLayout: a GameObject per tile (and per riichi stick) placed at the layout's pose each frame.
// The layout's space has the observer on -Z, one unit per tile width, the felt at Y = 0; the root turns that toward
// the local player's chair and the unit becomes the real tile width.
internal sealed class TableView : IDisposable
{
    public const int TileCount = 136;
    private const uint LoungeRenderingLayer = 2;

    // Selectable tiles under the mouse rise by this many tile widths.
    private const float HoverLift = 0.25f;

    private readonly MahjongAssets assets;
    private readonly TableLayout layout;
    private readonly Transform root;
    private readonly float unit;
    private readonly Transform[] tiles = new Transform[TileCount];
    private readonly MeshRenderer[] renderers = new MeshRenderer[TileCount];
    private readonly Material?[] shownMaterial = new Material?[TileCount];
    private readonly Dictionary<int, int> tileByCollider = new();
    private readonly Transform[] sticks = new Transform[4];

    public TableView(Transform surface, MahjongAssets assets, TableLayout layout, Vector3 observerDirection)
    {
        this.assets = assets;
        this.layout = layout;
        unit = assets.TileSize.x;
        root = new GameObject("Mahjong round").transform;
        root.SetParent(surface, false);
        Face(observerDirection);

        for (var id = 0; id < TileCount; id++)
        {
            var tile = new GameObject("Tile " + id);
            tile.transform.SetParent(root, false);
            tile.AddComponent<MeshFilter>().sharedMesh = assets.TileMesh;
            renderers[id] = tile.AddComponent<MeshRenderer>();
            renderers[id].renderingLayerMask = LoungeRenderingLayer;
            var box = tile.AddComponent<BoxCollider>();
            box.size = assets.TileSize;
            box.isTrigger = true;
            tileByCollider[box.GetInstanceID()] = id;
            tiles[id] = tile.transform;
        }

        for (var seat = 0; seat < sticks.Length; seat++)
        {
            var stick = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stick.name = "Riichi stick " + seat;
            UnityEngine.Object.Destroy(stick.GetComponent<Collider>());
            stick.transform.SetParent(root, false);
            stick.transform.localScale = ToUnity(TableLayout.RiichiStickSize) * unit;
            var stickRenderer = stick.GetComponent<MeshRenderer>();
            stickRenderer.sharedMaterial = assets.RiichiStickMaterial;
            stickRenderer.renderingLayerMask = LoungeRenderingLayer;
            stick.SetActive(false);
            sticks[seat] = stick.transform;
        }
    }

    // The layout's -Z (the observer) points at the observer's chair.
    public void Face(Vector3 observerDirection)
    {
        root.localRotation = Quaternion.LookRotation(-observerDirection, Vector3.up);
    }

    // Place every tile and stick for `time`; tiles in `lifted` (a hovered selection group) rise a little, tiles in
    // `highlighted` glow and tiles in `dimmed` are greyed.
    public void Apply(float time, ISet<int>? lifted, ISet<int>? highlighted = null, ISet<int>? dimmed = null)
    {
        for (var id = 0; id < TileCount; id++)
        {
            var pose = layout.GetTilePose(id, time);
            var position = ToUnity(pose.Position) * unit;
            if (lifted != null && lifted.Contains(id))
                position += Vector3.up * (HoverLift * unit);
            tiles[id].localPosition = position;
            tiles[id].localRotation = ToUnity(pose.Rotation);
            // The local player's own hand is the only set of tiles whose face leans toward -Z (the observer).
            var inOwnHand = (tiles[id].localRotation * Vector3.forward).z < -0.1f;
            var look = highlighted != null && highlighted.Contains(id) ? TileLook.Highlighted
                : dimmed != null && dimmed.Contains(id) ? TileLook.Dimmed
                : inOwnHand ? TileLook.OwnHand : TileLook.Table;
            var material = assets.MaterialFor(pose.Type, pose.RedFive, look);
            if (material != shownMaterial[id])
            {
                renderers[id].sharedMaterial = material;
                shownMaterial[id] = material;
            }
        }

        for (var seat = 0; seat < sticks.Length; seat++)
        {
            var pose = layout.GetRiichiStickPose(seat, time);
            sticks[seat].gameObject.SetActive(pose.Visible);
            if (!pose.Visible)
                continue;
            sticks[seat].localPosition = ToUnity(pose.Position) * unit;
            sticks[seat].localRotation = ToUnity(pose.Rotation);
        }
    }

    // The tile whose box the ray hits first, if any (tile boxes are triggers; other colliders in the way are ignored).
    public int? TileUnder(Ray ray, float maxDistance)
    {
        var hits = Physics.RaycastAll(ray, maxDistance, ~0, QueryTriggerInteraction.Collide);
        int? best = null;
        var bestDistance = float.MaxValue;
        for (var i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];
            if (hit.collider == null || !tileByCollider.TryGetValue(hit.collider.GetInstanceID(), out var id))
                continue;
            if (hit.distance < bestDistance)
            {
                best = id;
                bestDistance = hit.distance;
            }
        }
        return best;
    }

    // Diagnostics: where a few tiles are, as the layout says and as Unity draws them.
    public string Describe(float time)
    {
        var text = new System.Text.StringBuilder();
        text.Append($"root active={root.gameObject.activeInHierarchy} world={root.position} unit={unit:F4} children={root.childCount}");
        foreach (var id in new[] { 0, 1, 67, 135 })
        {
            var pose = layout.GetTilePose(id, time);
            var renderer = renderers[id];
            text.Append($"\n  tile {id}: pose {pose.Position} {pose.Type} -> local {tiles[id].localPosition} world {tiles[id].position}" +
                        $" scale {tiles[id].lossyScale} renderer enabled={renderer.enabled} visible={renderer.isVisible}" +
                        $" bounds {renderer.bounds.center} size {renderer.bounds.size} material {(renderer.sharedMaterial != null ? renderer.sharedMaterial.name : "none")}");
        }
        return text.ToString();
    }

    public void Dispose()
    {
        if (root != null)
            UnityEngine.Object.Destroy(root.gameObject);
    }

    private static Vector3 ToUnity(NVector3 v) => new(v.X, v.Y, v.Z);

    private static Quaternion ToUnity(NQuaternion q) => new(q.X, q.Y, q.Z, q.W);
}
