// Ported from Practice-Riichi-Mahjong source/Game/Rendering/RenderWall.vala (OpenRiichi, GPLv3).
using System;
using System.Collections.Generic;
using System.Numerics;

namespace MoonMahjong.Table.Layout;

internal sealed class RenderWall
{
    private readonly GameRenderContext context;
    private readonly WallPart[] walls = new WallPart[4];
    private readonly int split;

    private List<WallPart>? drawParts;
    private List<WallPart>? deadParts;

    public RenderWall(GameRenderContext context, SceneGraph graph, RenderTile[] tiles, SceneNode parent)
    {
        this.context = context;
        split = context.WallSplit;

        Node = new SceneNode(graph);
        parent.AddObject(Node);

        // added()
        for (int i = 0; i < 4; i++)
        {
            var wt = new RenderTile[34];
            for (int j = 0; j < 34; j++)
                wt[j] = tiles[i * 34 + j];

            var wrap = new SceneNode(graph);
            var rot = new SceneNode(graph);
            walls[i] = new WallPart(graph, wt, context.TileSize);
            Node.AddObject(rot);
            rot.AddObject(wrap);
            walls[i].AddTo(wrap);

            rot.Rotation = QuatMath.FromEuler(-i / 2.0f, 0, 0);
            wrap.Position = new Vector3(context.TileSize.X * 8, context.TileSize.Y / 2, 10 * context.TileSize.X);
        }
    }

    public SceneNode Node { get; }

    // Not in the Vala: lets tests see the wall structure. Empty until the split.
    public IReadOnlyList<WallPart> DrawParts => drawParts ?? (IReadOnlyList<WallPart>)Array.Empty<WallPart>();
    public IReadOnlyList<WallPart> DeadParts => deadParts ?? (IReadOnlyList<WallPart>)Array.Empty<WallPart>();

    public void SplitDeadWall(TableAnimationTime time)
    {
        float delta = context.TileSize.X / 2 * 3;

        int startWall = (4 - context.Dealer) % 4;
        var draw = new List<WallPart>();
        var dead = new List<WallPart>();

        for (int i = 0; i < 4; i++)
            draw.Add(walls[(startWall + i) % 4]);

        var left = draw[0];
        draw.RemoveAt(0);
        var p = left.DeadSplit(split);
        draw.Insert(0, p);
        p.AnimateMove(-delta, time);

        if (split > 7)
        {
            var part = left.DeadSplit(split - 7);
            part.ToDeadWall(4);
            dead.Add(part);
            draw.Add(left);

            left.AnimateMove(delta, time);
        }
        else
        {
            dead.Add(left);
            left.ToDeadWall(4);

            if (split != 7)
            {
                var part = draw[3].DeadSplit(10 + split);
                dead.Add(part);
                part.ToDeadWall(0);
                part.AnimateMove(-delta, time);
            }
        }

        drawParts = draw;
        deadParts = dead;
    }

    public RenderTile? DrawWall()
    {
        var parts = RequireSplit(drawParts);
        if (parts[0].Empty)
            parts.RemoveAt(0);
        if (parts.Count == 0)
            return null;
        return parts[0].Draw();
    }

    public RenderTile? DrawDeadWall()
    {
        var parts = RequireSplit(deadParts);
        if (parts[0].Empty)
            parts.RemoveAt(0);
        if (parts.Count == 0)
            return null;
        return parts[0].DeadDraw();
    }

    public void FlipDora()
    {
        var parts = RequireSplit(deadParts);
        // As in the Vala, a second part is assumed to exist when the first has no dora left to flip.
        if (!parts[0].FlipDora(context.ServerTimes.DoraFlip))
            parts[1].FlipDora(context.ServerTimes.DoraFlip);
    }

    public void FlipUraDora()
    {
        foreach (var part in RequireSplit(deadParts))
            part.FlipUraDora(context.ServerTimes.DoraFlip);
    }

    public void DeadTileAdd()
    {
        var draw = RequireSplit(drawParts);
        var dead = RequireSplit(deadParts);

        int i = draw.Count - 1;
        var last = draw[i];
        if (last.Empty)
            draw.RemoveAt(i--);

        dead[dead.Count - 1].DeadTileAdd(draw[i].RemoveLast(context.ServerTimes.DoraFlip), context.ServerTimes.DoraFlip);
    }

    private static List<WallPart> RequireSplit(List<WallPart>? parts) =>
        parts ?? throw new InvalidOperationException("The wall has not been split yet (the round start sequence splits it).");

    internal sealed class WallPart
    {
        private readonly SceneGraph graph;
        private readonly Vector3 tileSize;
        private readonly List<RenderTile> doras = new();
        private readonly List<RenderTile> uraDoras = new();
        private List<RenderTile> tiles = new();
        private int removedTiles;

        private int doraIndex;
        private int tilesAdded;
        private int deadDrawn;

        public WallPart(SceneGraph graph, IEnumerable<RenderTile> tiles, Vector3 tileSize)
        {
            this.graph = graph;
            this.tiles.AddRange(tiles);
            this.tileSize = tileSize;
            Node = new SceneNode(graph);
        }

        public SceneNode Node { get; }

        public bool Empty => tiles.Count == 0;

        // Not in the Vala: the tiles in this part, in its internal order (reversed for dead wall parts).
        public IReadOnlyList<RenderTile> Tiles => tiles;

        // parent.add_object(this) followed by added(): takes the tiles over and lays them out.
        public void AddTo(SceneNode parent)
        {
            parent.AddObject(Node);
            foreach (var tile in tiles)
                Node.AddObject(tile.Node);
            Order();
        }

        public void AnimateMove(float delta, TableAnimationTime time)
        {
            var animation = new NodeAnimation(time, SmoothApproachCurve.Instance);
            animation.DoRelativePosition(new Vector3(delta, 0, 0));
            Node.Animate(animation);
        }

        public void ToDeadWall(int doraIndex)
        {
            this.doraIndex = doraIndex + 1;

            var reversed = new List<RenderTile>();
            while (tiles.Count > 0)
            {
                reversed.Add(tiles[tiles.Count - 1]);
                tiles.RemoveAt(tiles.Count - 1);
            }
            tiles = reversed;
        }

        public WallPart DeadSplit(int index)
        {
            var split = new List<RenderTile>();

            index *= 2;

            while (index < tiles.Count)
            {
                split.Add(tiles[index]);
                tiles.RemoveAt(index);
            }

            var pos = new Vector3(split[0].Position.X, 0, 0);

            var wall = new WallPart(graph, split, tileSize);
            var parent = Node.Parent ?? throw new InvalidOperationException("A wall part must be attached before it is split.");
            wall.AddTo(parent);
            wall.Node.Position = Node.Position + pos;

            return wall;
        }

        public RenderTile RemoveLast(TableAnimationTime time)
        {
            if (tiles.Count == 0)
                throw new InvalidOperationException("No tile left to move to the dead wall.");

            var tile = tiles[tiles.Count - 1];
            tiles.RemoveAt(tiles.Count - 1);

            if (removedTiles % 2 == 0 && tiles.Count > 0)
            {
                var t = tiles[tiles.Count - 1];
                t.AnimateTowards(tile.Position, tile.Rotation, time);
            }

            removedTiles++;
            return tile;
        }

        public RenderTile? Draw()
        {
            if (Empty)
                return null;

            var tile = tiles[0];
            tiles.RemoveAt(0);
            return tile;
        }

        public RenderTile? DeadDraw()
        {
            int i = ++deadDrawn % 2;

            if (i >= tiles.Count)
                return null;

            doraIndex--;
            var tile = tiles[i];
            tiles.RemoveAt(i);
            return tile;
        }

        private void Order()
        {
            for (int i = 0; i < tiles.Count; i++)
            {
                var tile = tiles[i];

                var pos = new Vector3(
                    (i / 2) * -tileSize.X,
                    ((i + 1) % 2) * tileSize.Y,
                    0);
                var rot = QuatMath.FromEuler(0, 1, 0);

                tile.Node.Rotation = rot;
                tile.Node.Position = pos;
            }
        }

        public void DeadTileAdd(RenderTile tile, TableAnimationTime time)
        {
            var pos = new Vector3(((tilesAdded + 1) % 2) * tileSize.X, ((tilesAdded % 2) * 2 - 1) * tileSize.Y, 0);
            pos = tiles[tiles.Count - 1].Position + pos;
            var rot = QuatMath.FromEuler(0, 1, 0);

            Node.ConvertObject(tile.Node);
            tile.AnimateTowards(pos, rot, time);

            tiles.Add(tile);
            tilesAdded++;
        }

        public bool FlipDora(TableAnimationTime time)
        {
            if (doraIndex >= tiles.Count)
                return false;

            var t = tiles[doraIndex];
            doras.Add(t);
            uraDoras.Add(tiles[doraIndex - 1]);

            var rot = QuatMath.Mul(QuatMath.FromEuler(0, 1, 0), t.Rotation);

            t.AnimateTowards(t.Position, rot, time);

            doraIndex += 2;

            return true;
        }

        public void FlipUraDora(TableAnimationTime time)
        {
            foreach (var tile in doras)
            {
                var pos = new Vector3(0, -tileSize.Y, 0) + tile.Position;
                tile.AnimateTowards(pos, tile.Rotation, time);
            }

            foreach (var tile in uraDoras)
            {
                var pos = new Vector3(0, 0, tileSize.Z) + tile.Position;
                var rot = QuatMath.Mul(QuatMath.FromEuler(0, 1, 0), tile.Rotation);
                tile.AnimateTowards(pos, rot, time);
            }
        }
    }
}
