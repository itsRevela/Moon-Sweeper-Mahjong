// Ported from Practice-Riichi-Mahjong source/Game/Rendering/RenderTable.vala (OpenRiichi, GPLv3).
// Dropped: the table and felt meshes (WorldTableObject), the centre piece's round-wind, name and score labels.
using System.Numerics;
using MoonMahjong.Core;

namespace MoonMahjong.Table.Layout;

internal sealed class RenderTable
{
    // Distance from the table centre to each hand (and to the corner of each call area), in world units.
    public const float HandOffset = 8;

    public RenderTable(GameRenderContext context, SceneGraph graph, RenderTile[] tiles, SceneNode parent)
    {
        Tiles = tiles;
        Node = new SceneNode(graph);
        parent.AddObject(Node);

        // added()
        CenterPiece = new SceneNode(graph);
        Node.AddObject(CenterPiece);
        float scale = context.TileSize.X * 2.9f;
        CenterPiece.Scale = scale;

        var centerSize = OpenRiichiModels.CenterPieceObb;
        // Mod change: OpenRiichi used (0, 1.1 * centre height, unscaled), which fits its centre piece's sloped edge; the
        // plugin's console is flat-topped to near its edge, so the stick lay inside it and vanished. It lies on the top.
        float stickHalfHeight = TableLayout.RiichiStickSize.Y / 2 * context.TileSize.X;
        RiichiOffset = new Vector3(0, centerSize.Y * scale + stickHalfHeight, centerSize.X / 2 * scale * 0.8f);

        Players = new RenderPlayer[4];
        for (int i = 0; i < Players.Length; i++)
        {
            Players[i] = new RenderPlayer(context, graph, i, i == context.Dealer, HandOffset, RiichiOffset,
                i == context.ObserverIndex, Winds.FromInt(i - context.Dealer));
            Node.AddObject(Players[i].Node);
            Players[i].Added();
            Players[i].Node.Rotation = QuatMath.FromEuler(i / 2.0f, 0, 0);
        }

        Wall = new RenderWall(context, graph, tiles, Node);
    }

    public SceneNode Node { get; }

    // RenderTableCenterPiece: origin at the centre of its base, scaled by 2.9 tile widths.
    public SceneNode CenterPiece { get; }

    public Vector3 RiichiOffset { get; }
    public RenderPlayer[] Players { get; }
    public RenderTile[] Tiles { get; }
    public RenderWall Wall { get; }

    public void SplitDeadWall(TableAnimationTime time) => Wall.SplitDeadWall(time);
}
