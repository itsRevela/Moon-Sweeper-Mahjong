// Ported from Practice-Riichi-Mahjong source/Game/Rendering/RenderTile.vala (OpenRiichi, GPLv3).
// Dropped: model loading, materials, colours, hover/indicated highlighting (rendering and picking are the plugin's job).
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using MoonMahjong.Core;

namespace MoonMahjong.Table.Layout;

internal sealed class RenderTile
{
    public RenderTile(SceneGraph graph, int index)
    {
        Index = index;
        Node = new SceneNode(graph);
        // As in the Vala: an unassigned tile is a Blank with ID 0 (not its own index).
        Tile = new Tile(0, TileType.Blank, false);
    }

    // Position in GameScene.tiles (= the tile ID); not part of the Vala class, used to report poses.
    public int Index { get; }

    public SceneNode Node { get; }

    // RenderTile.tile_type (a Tile, despite the name). Renamed to avoid clashing with the TileType enum.
    public Tile Tile { get; set; }

    public Vector3 Position => Node.Position;
    public Quaternion Rotation => Node.Rotation;

    public void SetAbsoluteLocation(Vector3 position, Quaternion rotation)
    {
        Node.CancelBufferedAnimations();
        Node.Rotation = rotation;
        Node.Position = position;
    }

    public void AnimateTowards(Vector3 position, Quaternion rotation, TableAnimationTime time)
    {
        var animation = new NodeAnimation(time, SmoothApproachCurve.Instance);
        animation.DoAbsolutePosition(position);
        animation.DoAbsoluteRotation(rotation);

        Node.CancelBufferedAnimations();
        Node.Animate(animation);
    }

    // RenderTile.sort_tiles: by tile type, stable (Gee's ArrayList.sort is a TimSort; List.Sort would not be stable).
    public static List<RenderTile> SortTiles(List<RenderTile> list) =>
        list.OrderBy(t => (int)t.Tile.TileType).ToList();
}
