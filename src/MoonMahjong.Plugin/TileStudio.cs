using System;
using System.Collections.Generic;
using MoonMahjong.Table.Layout;
using MoonMahjong.Table.Presentation;
using UnityEngine;

namespace MoonMahjong.Plugin;

// Films tiles in 3D for the screen (requested: the round result shows the winning hand as real tiles, not pictures).
// The studio sits at the world's origin, the moon's empty centre far from everything else, with its own light. An
// orthographic camera looks slightly down on the tiles standing in rows, so their tops show, and renders into a texture
// the result plate shows. It runs only while a result is up.
internal sealed class TileStudio : IDisposable
{
    private const float Pitch = 16f; // degrees the camera looks down
    private const float RowGap = 0.45f; // tile widths between stacked rows
    private const float Margin = 0.18f; // tile widths of tray around the tiles
    private const int PixelsPerTileWidth = 110;
    private const int MaxTextureWidth = 4096;
    // The key light stands about 1.2 m off, above, in front and to the left of the tiles: URP point lights fall off with
    // the square of the distance, and at a few centimetres (the first try) they washed the faces out.
    private static readonly Vector3 KeyOffset = new(-0.35f, 0.6f, -1.0f);
    private const float KeyIntensity = 1.4f;

    private readonly MahjongAssets assets;
    private readonly Transform root;
    private readonly Transform tilesRoot;
    private readonly Camera camera;
    private readonly Transform light;
    private RenderTexture? texture;

    public TileStudio(MahjongAssets assets, Color tray)
    {
        this.assets = assets;
        root = new GameObject("Mahjong tile studio").transform;
        root.position = Vector3.zero;
        tilesRoot = new GameObject("Tiles").transform;
        tilesRoot.SetParent(root, false);

        camera = new GameObject("Studio camera").AddComponent<Camera>();
        camera.transform.SetParent(root, false);
        camera.transform.localRotation = Quaternion.Euler(Pitch, 0f, 0f);
        camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = tray;
        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = 2f;
        camera.allowMSAA = true;
        camera.enabled = false;

        var key = new GameObject("Studio light").AddComponent<Light>();
        key.type = LightType.Point;
        key.range = 4f;
        key.intensity = KeyIntensity;
        key.shadows = LightShadows.None;
        light = key.transform;
        light.SetParent(root, false);
    }

    // Lays out `rows` (WinningHand rows, sideways tiles TableLayout.TileSize.Y wide) and starts filming them.
    public StudioShot Film(IReadOnlyList<IReadOnlyList<PlacedHandTile>> rows)
    {
        Clear();
        var unit = assets.TileSize.x;
        var length = TableLayout.TileSize.Y;
        var winners = new List<(Renderer Renderer, int Row)>();
        var renderers = new List<Renderer>();
        for (var r = 0; r < rows.Count; r++)
        {
            var baseline = -r * (length + RowGap);
            foreach (var placed in rows[r])
            {
                var renderer = PlaceTile(placed, baseline, unit, length);
                renderers.Add(renderer);
                if (placed.Tile.Winning)
                    winners.Add((renderer, r));
            }
        }
        return Frame(renderers, winners, unit);
    }

    public void Hide()
    {
        camera.enabled = false;
        Clear();
    }

    public void Dispose()
    {
        if (root != null)
            UnityEngine.Object.Destroy(root.gameObject);
        if (texture != null)
            texture.Release();
        texture = null;
    }

    private Renderer PlaceTile(PlacedHandTile placed, float baseline, float unit, float length)
    {
        var tile = placed.Tile;
        var go = new GameObject("Studio tile");
        go.transform.SetParent(tilesRoot, false);
        go.AddComponent<MeshFilter>().sharedMesh = assets.TileMesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = assets.MaterialFor(tile.Type, tile.RedFive);
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // The tile mesh faces local +Z; turned half round it faces the camera (which looks along +Z) unmirrored.
        Quaternion rotation;
        Vector3 centre;
        if (tile.FaceDown)
        {
            rotation = Quaternion.identity;
            centre = new Vector3(placed.X + 0.5f, baseline + length / 2f, 0f);
        }
        else if (tile.Sideways)
        {
            rotation = Quaternion.Euler(0f, 180f, 0f) * Quaternion.Euler(0f, 0f, 90f);
            centre = new Vector3(placed.X + length / 2f, baseline + 0.5f, 0f);
        }
        else
        {
            rotation = Quaternion.Euler(0f, 180f, 0f);
            centre = new Vector3(placed.X + 0.5f, baseline + length / 2f, 0f);
        }
        go.transform.localRotation = rotation;
        go.transform.localPosition = centre * unit;
        return renderer;
    }

    // Fit the camera around the tiles and size the texture to them; report where the winning tiles landed.
    private StudioShot Frame(List<Renderer> renderers, List<(Renderer Renderer, int Row)> winners, float unit)
    {
        var view = camera.transform;
        float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue, minZ = float.MaxValue;
        foreach (var renderer in renderers)
            foreach (var corner in Corners(renderer.bounds))
            {
                var p = view.InverseTransformDirection(corner - root.position);
                minX = Math.Min(minX, p.x);
                maxX = Math.Max(maxX, p.x);
                minY = Math.Min(minY, p.y);
                maxY = Math.Max(maxY, p.y);
                minZ = Math.Min(minZ, p.z);
            }
        var margin = Margin * unit;
        minX -= margin;
        maxX += margin;
        minY -= margin;
        maxY += margin;

        var width = maxX - minX;
        var height = maxY - minY;
        view.localPosition = view.localRotation * new Vector3((minX + maxX) / 2f, (minY + maxY) / 2f, minZ - 0.5f);
        camera.orthographicSize = height / 2f;

        var pixelsWide = Mathf.Clamp(Mathf.RoundToInt(width / unit * PixelsPerTileWidth), 64, MaxTextureWidth);
        var pixelsHigh = Mathf.Max(16, Mathf.RoundToInt(pixelsWide * height / width));
        if (texture == null || texture.width != pixelsWide || texture.height != pixelsHigh)
        {
            if (texture != null)
                texture.Release();
            texture = new RenderTexture(pixelsWide, pixelsHigh, 24, RenderTextureFormat.ARGB32)
            {
                name = "MoonMahjong tile studio",
                antiAliasing = 4,
                hideFlags = HideFlags.DontUnloadUnusedAsset,
            };
        }
        camera.targetTexture = texture;
        camera.aspect = width / height;

        light.position = root.position + view.rotation * (new Vector3((minX + maxX) / 2f, (minY + maxY) / 2f, minZ) + KeyOffset);
        camera.enabled = true;

        var marks = new List<StudioMark>();
        foreach (var (renderer, _) in winners)
        {
            var bounds = renderer.bounds;
            var centre = view.InverseTransformDirection(bounds.center - root.position);
            var bottom = float.MaxValue;
            foreach (var corner in Corners(bounds))
                bottom = Math.Min(bottom, view.InverseTransformDirection(corner - root.position).y);
            marks.Add(new StudioMark((centre.x - minX) / width, (maxY - bottom) / height, 1f * unit / width));
        }
        return new StudioShot(texture, height / width, marks);
    }

    private void Clear()
    {
        for (var i = tilesRoot.childCount - 1; i >= 0; i--)
            UnityEngine.Object.Destroy(tilesRoot.GetChild(i).gameObject);
    }

    private static IEnumerable<Vector3> Corners(Bounds b)
    {
        for (var i = 0; i < 8; i++)
            yield return new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
    }
}

// The filmed rows: the texture, its height over its width, and where each winning tile is (fractions of the image:
// U its centre from the left, V its bottom edge from the top, Width its width).
internal sealed record StudioShot(RenderTexture Texture, float Aspect, IReadOnlyList<StudioMark> Winners);

internal sealed record StudioMark(float U, float V, float Width);
