// Ported from Practice-Riichi-Mahjong source/Game/Rendering/RenderCalls.vala (OpenRiichi, GPLv3).
using System.Collections.Generic;
using System.Numerics;
using MoonMahjong.Core;

namespace MoonMahjong.Table.Layout;

// A player's called melds, laid out in a row leftwards from the corner at the player's right (mod change, see
// ModLayout.MeldGap; OpenRiichi stacked them towards the table centre). Each call is its own node, created at the calls
// origin and animated to its slot while its tiles animate into place inside it.
internal sealed class RenderCalls
{
    private readonly List<RenderCall> calls = new();
    private readonly GameRenderContext context;

    public RenderCalls(GameRenderContext context, SceneGraph graph)
    {
        this.context = context;
        Node = new SceneNode(graph);
    }

    public enum Alignment
    {
        Self = 0,
        Right = 1,
        Center = 2,
        Left = 3,
    }

    public SceneNode Node { get; }

    public IReadOnlyList<RenderCall> Calls => calls;

    public void AddCall(RenderCall call)
    {
        Node.AddObject(call.Node);
        calls.Add(call);
        Arrange();
    }

    public RenderCallPon? GetPon(TileType type)
    {
        foreach (var call in calls)
            if (call is RenderCallPon pon && pon.Tiles[0].Tile.TileType == type)
                return pon;

        return null;
    }

    public void LateKan(RenderCallPon pon, RenderCallLateKan kan)
    {
        int index = calls.IndexOf(pon);
        calls.RemoveAt(index);
        calls.Insert(index, kan);
        Node.AddObject(kan.Node);

        Arrange();
        Node.RemoveObject(pon.Node);
    }

    private void Arrange()
    {
        float offset = 0;

        foreach (var call in calls)
        {
            call.AnimateTo(new Vector3(-offset, 0, 0), context.ServerTimes.TileDiscard);
            offset += call.Width + ModLayout.MeldGap * context.TileSize.X;
            call.Arrange(context.ServerTimes.TileDiscard);
        }
    }

    public static Alignment PlayersToAlignment(RenderPlayer caller, RenderPlayer discarder)
    {
        int diff = (discarder.Seat - caller.Seat + 4) % 4;

        return diff switch
        {
            1 => Alignment.Right,
            3 => Alignment.Left,
            _ => Alignment.Center,
        };
    }

    internal abstract class RenderCall
    {
        protected RenderCall(SceneGraph graph, List<RenderTile> tiles, Vector3 tileSize)
        {
            Node = new SceneNode(graph);
            Tiles = tiles;
            TileSize = tileSize;
        }

        public SceneNode Node { get; }
        public List<RenderTile> Tiles { get; protected set; }
        public abstract float Height { get; }
        public abstract float Width { get; }

        protected Vector3 TileSize { get; }

        public abstract void Arrange(TableAnimationTime? time);

        public void AnimateTo(Vector3 position, TableAnimationTime time)
        {
            var animation = new NodeAnimation(time, SmoothApproachCurve.Instance);
            animation.DoAbsolutePosition(position);

            Node.CancelBufferedAnimations();
            Node.Animate(animation);
        }

        protected void ConvertTiles()
        {
            foreach (var tile in Tiles)
                Node.ConvertObject(tile.Node);
        }

        protected static void AnimateTile(RenderTile tile, Vector3 pos, Quaternion rot, TableAnimationTime? time)
        {
            if (time is { } t)
                tile.AnimateTowards(pos, rot, t);
            else
                tile.SetAbsoluteLocation(pos, rot);
        }

        // The shared layout of chii, pon and open kan: right to left, the tile at index `sideways` turned a quarter
        // turn and pushed to the near edge.
        protected void ArrangeInRow(int sideways, TableAnimationTime? time)
        {
            ConvertTiles();

            float width = 0;

            for (int i = 0; i < Tiles.Count; i++)
            {
                var tile = Tiles[i];

                float x = width;
                float z = 0;
                float rotation = 0;

                if (i == sideways)
                {
                    x += TileSize.Z / 2;
                    z += TileSize.X / 2;

                    rotation = 0.5f;
                    width += TileSize.Z;
                }
                else
                {
                    x += TileSize.X / 2;
                    z += TileSize.Z / 2;

                    width += TileSize.X;
                }

                var pos = new Vector3(-x, 0, -z);
                var rot = QuatMath.FromEuler(rotation, 0, 0);

                AnimateTile(tile, pos, rot, time);
            }
        }
    }

    internal sealed class RenderCallLateKan : RenderCall
    {
        private readonly Alignment alignment;

        public RenderCallLateKan(SceneGraph graph, List<RenderTile> tiles, RenderTile kanTile, Vector3 tileSize, Alignment alignment)
            : base(graph, tiles, tileSize)
        {
            this.alignment = alignment;

            int n = alignment switch
            {
                Alignment.Right => 1,
                Alignment.Left => 3,
                _ => 2,
            };

            // As in the Vala, this inserts into the pon's own list.
            tiles.Insert(n, kanTile);
        }

        public override float Height => System.Math.Max(TileSize.Z, 2 * TileSize.X);
        public override float Width => TileSize.X * 2 + TileSize.Z;

        public override void Arrange(TableAnimationTime? time)
        {
            ConvertTiles();

            float width = 0;
            float bottom = 0;
            int n = alignment switch
            {
                Alignment.Right => 0,
                Alignment.Left => 2,
                _ => 1,
            };

            for (int i = 0; i < Tiles.Count; i++)
            {
                var tile = Tiles[i];

                float x = width;
                float z = bottom;
                float rotation = 0;

                if (i == n)
                {
                    x += TileSize.Z / 2;
                    z += TileSize.X / 2;

                    rotation += 0.5f;
                }
                else if (i == n + 1)
                {
                    x += TileSize.Z / 2;
                    z += TileSize.X / 2 * 3;

                    rotation += 0.5f;
                    width += TileSize.Z;
                }
                else
                {
                    x += TileSize.X / 2;
                    z += TileSize.Z / 2;

                    width += TileSize.X;
                }

                var pos = new Vector3(-x, 0, -z);
                var rot = QuatMath.FromEuler(rotation, 0, 0);

                AnimateTile(tile, pos, rot, time);
            }
        }
    }

    internal sealed class RenderCallClosedKan : RenderCall
    {
        public RenderCallClosedKan(SceneGraph graph, List<RenderTile> tiles, Vector3 tileSize)
            : base(graph, tiles, tileSize)
        {
        }

        public override float Height => TileSize.Z;
        public override float Width => TileSize.X * 4;

        public override void Arrange(TableAnimationTime? time)
        {
            ConvertTiles();

            for (int i = 0; i < Tiles.Count; i++)
            {
                var tile = Tiles[Tiles.Count - i - 1];

                var pos = new Vector3(-TileSize.X * (i + 0.5f), 0, -TileSize.Z / 2);
                var rot = QuatMath.FromEuler(0, (i == 1 || i == 2) ? 1 : 0, 0);

                AnimateTile(tile, pos, rot, time);
            }
        }
    }

    internal sealed class RenderCallOpenKan : RenderCall
    {
        private readonly int sideways;

        public RenderCallOpenKan(SceneGraph graph, List<RenderTile> tiles, RenderTile discardTile, Vector3 tileSize, Alignment alignment)
            : base(graph, tiles, tileSize)
        {
            sideways = alignment switch
            {
                Alignment.Right => 0,
                Alignment.Left => 3,
                _ => 1,
            };

            tiles.Insert(sideways, discardTile);
        }

        public override float Height => TileSize.Z;
        public override float Width => TileSize.X * 3 + TileSize.Z;

        public override void Arrange(TableAnimationTime? time) => ArrangeInRow(sideways, time);
    }

    internal sealed class RenderCallPon : RenderCall
    {
        private readonly int sideways;

        public RenderCallPon(SceneGraph graph, List<RenderTile> tiles, RenderTile discardTile, Vector3 tileSize, Alignment alignment)
            : base(graph, tiles, tileSize)
        {
            Alignment = alignment;

            sideways = alignment switch
            {
                Alignment.Right => 0,
                Alignment.Left => 2,
                _ => 1,
            };

            tiles.Insert(sideways, discardTile);
        }

        public override float Height => TileSize.Z;
        public override float Width => TileSize.X * 2 + TileSize.Z;
        public Alignment Alignment { get; }

        public override void Arrange(TableAnimationTime? time) => ArrangeInRow(sideways, time);
    }

    internal sealed class RenderCallChii : RenderCall
    {
        public RenderCallChii(SceneGraph graph, List<RenderTile> tiles, RenderTile discardTile, Vector3 tileSize)
            : base(graph, tiles, tileSize)
        {
            var sort = RenderTile.SortTiles(tiles);
            Tiles = new List<RenderTile>();

            for (int i = sort.Count - 1; i >= 0; i--)
                Tiles.Add(sort[i]);

            Tiles.Add(discardTile);
        }

        public override float Height => TileSize.Z;
        public override float Width => TileSize.X * 2 + TileSize.Z;

        public override void Arrange(TableAnimationTime? time) => ArrangeInRow(2, time);
    }
}
