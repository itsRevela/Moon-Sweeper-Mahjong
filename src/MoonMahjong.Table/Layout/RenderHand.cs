// Ported from Practice-Riichi-Mahjong source/Game/Rendering/RenderHand.vala (OpenRiichi, GPLv3).
// Dropped: animate_angle (only used when a spectator switches the observed seat, which this model does not do).
using System.Collections.Generic;
using System.Numerics;
using MoonMahjong.Core;

namespace MoonMahjong.Table.Layout;

internal sealed class RenderHand
{
    private readonly GameRenderContext context;
    private readonly Vector3 tileSize;
    private readonly SceneNode wrap;
    private int drawn;

    public RenderHand(GameRenderContext context, SceneGraph graph, float viewAngle)
    {
        this.context = context;
        tileSize = context.TileSize;
        ViewAngle = viewAngle;
        Node = new SceneNode(graph);
        wrap = new SceneNode(graph);
    }

    public SceneNode Node { get; }

    public List<RenderTile> Tiles { get; private set; } = new();
    public float ViewAngle { get; }
    public RenderTile? LastDrawnTile { get; private set; }
    public bool Open { get; set; } // Open riichi

    // added(), run once the hand is attached to its player.
    public void Added()
    {
        Node.Rotation = QuatMath.FromEuler(0, 0.5f - ViewAngle, 0);
        Node.AddObject(wrap);
        wrap.Position = new Vector3(0, tileSize.Y / 2, -tileSize.Z / 2);
    }

    public void DrawTile(RenderTile tile)
    {
        wrap.ConvertObject(tile.Node);
        drawn++;

        if (Tiles.Count > 1 && drawn >= 14)
        {
            SortHand();
            OrderHand(true);
            OrderDrawTile(tile);
            Tiles.Add(tile);
        }
        else
        {
            Tiles.Add(tile);
            SortHand();
            OrderHand(true);
        }

        LastDrawnTile = tile;
    }

    public void Remove(RenderTile tile)
    {
        Tiles.Remove(tile);
        SortHand();
        OrderHand(true);
    }

    public List<RenderTile> GetTilesType(TileType type)
    {
        var tiles = new List<RenderTile>();

        foreach (var tile in Tiles)
            if (tile.Tile.TileType == type)
                tiles.Add(tile);

        return tiles;
    }

    public void SortHand()
    {
        Tiles = RenderTile.SortTiles(Tiles);
    }

    public void OrderHand(bool animate)
    {
        for (int i = 0; i < Tiles.Count; i++)
            OrderTile(Tiles[i], i, animate);
    }

    public void Ron(RenderTile tile)
    {
        // Empty in the Vala (its animate_open() call is commented out): the ron tile stays where it was discarded.
    }

    public void Tsumo()
    {
        var tile = LastDrawnTile ?? throw new System.InvalidOperationException("Tsumo without a drawn tile.");
        OrderTile(tile, Tiles.Count + 0.5f, true);
    }

    public void OpenHand()
    {
        Open = true;
        AnimateOpen();
    }

    private void AnimateOpen()
    {
        var animation = new NodeAnimation(context.ServerTimes.HandReveal, SmoothDepartCurve.Instance);
        animation.DoAbsoluteRotation(QuatMath.Identity);

        Node.CancelBufferedAnimations();
        Node.Animate(animation);
    }

    public void CloseHand()
    {
        var animation = new NodeAnimation(context.ServerTimes.HandReveal, SmoothDepartCurve.Instance);
        animation.DoAbsoluteRotation(QuatMath.FromEuler(0, 1, 0));
        Node.CancelBufferedAnimations();
        Node.Animate(animation);

        animation = new NodeAnimation(context.ServerTimes.HandReveal, SmoothDepartCurve.Instance);
        animation.DoAbsolutePosition(new Vector3(0, -tileSize.Y / 2, -tileSize.Z / 2));
        wrap.Animate(animation);
    }

    private void OrderTile(RenderTile tile, float tilePosition, bool animate)
    {
        var pos = new Vector3((tilePosition - (Tiles.Count - 1) / 2.0f) * tileSize.X, 0, 0);

        if (animate)
            tile.AnimateTowards(pos, QuatMath.Identity, context.ServerTimes.HandOrder);
        else
            tile.SetAbsoluteLocation(pos, QuatMath.Identity);
    }

    // After the deal, a drawn tile waits on its side, one row beyond the hand, until the player discards.
    // Mod change (ModLayout.DrawnTileGap): the drawn tile stands upright just right of the hand. OpenRiichi put it at
    // ((n / 2 - 1) * width, 0, -(depth + width) / 2) turned a quarter, beyond the hand, where the table's rail clipped it.
    // Called before the tile joins Tiles, so the hand's rightmost tile is at (Tiles.Count - 1) / 2 widths.
    private void OrderDrawTile(RenderTile tile)
    {
        var pos = new Vector3(
            ((Tiles.Count - 1) / 2.0f + 1 + ModLayout.DrawnTileGap) * tileSize.X,
            0,
            0);

        tile.AnimateTowards(pos, Quaternion.Identity, context.ServerTimes.TileDraw);
    }
}
