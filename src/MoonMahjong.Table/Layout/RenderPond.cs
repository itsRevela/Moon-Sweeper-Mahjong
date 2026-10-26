// Ported from Practice-Riichi-Mahjong source/Game/Rendering/RenderPond.vala (OpenRiichi, GPLv3).
using System.Collections.Generic;
using System.Numerics;

namespace MoonMahjong.Table.Layout;

internal sealed class RenderPond
{
    private readonly GameRenderContext context;
    private readonly Vector3 tileSize;
    private readonly List<RenderTile> tiles = new();
    private RenderTile? riichiTile;
    private bool doRiichi;

    public RenderPond(GameRenderContext context, SceneGraph graph)
    {
        this.context = context;
        tileSize = context.TileSize;
        Node = new SceneNode(graph);
        Wrap = new SceneNode(graph);
    }

    public SceneNode Node { get; }

    // Created and positioned as in the Vala, but unused there too: add_tile converts tiles into the pond itself.
    public SceneNode Wrap { get; }

    public IReadOnlyList<RenderTile> Tiles => tiles;

    public void Added()
    {
        Node.AddObject(Wrap);
        Wrap.Position = new Vector3(-tileSize.X / 2, tileSize.Y / 2, tileSize.Z / 2);
    }

    public void AddTile(RenderTile tile)
    {
        Node.ConvertObject(tile.Node);

        if (doRiichi)
        {
            riichiTile = tile;
            doRiichi = false;
        }

        tiles.Add(tile);
        ArrangePond();
    }

    public void Remove(RenderTile tile)
    {
        tiles.Remove(tile);
        ArrangePond();

        if (tile == riichiTile)
            doRiichi = true;
    }

    public void Riichi()
    {
        doRiichi = true;
    }

    private void ArrangePond()
    {
        // Top left corner
        float width = -3 * tileSize.X;
        float height = 0;

        for (int i = 0; i < tiles.Count; i++)
        {
            var tile = tiles[i];

            if (i == 6)
            {
                width = -3 * tileSize.X;
                height = tileSize.Z;
            }
            else if (i == 12)
            {
                width = -3 * tileSize.X;
                height = 2 * tileSize.Z;
            }

            float x;
            float y;
            float r = 0;

            if (tile == riichiTile)
            {
                x = width + tileSize.Z / 2;
                y = height + tileSize.Z - tileSize.X / 2;
                r = 0.5f;

                width += tileSize.Z;
            }
            else
            {
                x = width + tileSize.X / 2;
                y = height + tileSize.Z / 2;

                width += tileSize.X;
            }

            var pos = new Vector3(x, 0, y);
            var rot = QuatMath.FromEuler(r, 0, 0);

            tile.AnimateTowards(pos, rot, context.ServerTimes.TileDiscard);
        }
    }
}
