using System.Numerics;
using MoonMahjong.Core;
using MoonMahjong.Table.Layout;

namespace MoonMahjong.Table.Tests;

// Builds tables and measures poses in plugin space (Unity axes, observer at -Z, one unit = one tile width).
internal static class TableTestHelpers
{
    public const float Start = 10f;

    // Round start: 0.5 s pause + 0.5 s split + 16 x 0.15 s deal = 3.4 s, then the dora flip (0.2 s) and hand
    // ordering (0.15 s) finish. Everything has settled well before this.
    public const float AfterDeal = Start + 5f;

    public const float Tolerance = 1e-4f;

    // OpenRiichi's world units per tile width; the hands sit 8 world units from the centre.
    public static readonly float TileWidthWorld = OpenRiichiModels.TileObb.X * 1.5f;
    public static float HandDistance => 8 / TileWidthWorld;

    public static TableLayout NewTable(int observer = 0, int dealer = 0, int wallIndex = 5, TableTimings? timings = null, float time = Start,
        float handAreaRise = 0)
    {
        var game = NewGame(dealer);
        return new TableLayout(observer, dealer, game, new RoundStartInfo(wallIndex), timings ?? TableTimings.Default, time, handAreaRise);
    }

    public static GameStartInfo NewGame(int dealer)
    {
        var players = new[] { new GamePlayer(0, "A"), new GamePlayer(1, "B"), new GamePlayer(2, "C"), new GamePlayer(3, "D") };
        return new GameStartInfo(players, dealer, 25000, 8, 1, 20, 10);
    }

    // Reveals every tile as the unshuffled set: IDs 4k..4k+3 share a type.
    public static void AssignAll(TableLayout table, float time)
    {
        for (int id = 0; id < 136; id++)
            table.TileAssignment(new Tile(id, (TileType)(id / 4 + 1), false), time);
    }

    // The tile IDs each seat holds after the deal, according to the rules engine's wall (Core.RoundStateWall).
    public static List<int>[] ExpectedDeal(int dealer, int wallIndex, out RoundStateWall wall)
    {
        wall = new RoundStateWall(dealer, wallIndex);
        var hands = new[] { new List<int>(), new List<int>(), new List<int>(), new List<int>() };
        for (int i = 0; i < 16; i++)
        {
            int player = (i + dealer) % 4;
            for (int j = 0; j < (i < 12 ? 4 : 1); j++)
                hands[player].Add(wall.DrawWall().ID);
        }
        return hands;
    }

    public static List<int>[] ExpectedDeal(int dealer, int wallIndex) => ExpectedDeal(dealer, wallIndex, out _);

    public static TilePose[] AllPoses(TableLayout table, float time) =>
        Enumerable.Range(0, 136).Select(id => table.GetTilePose(id, time)).ToArray();

    public static Vector3 FaceNormal(TilePose pose) => Vector3.Transform(Vector3.UnitZ, pose.Rotation);
    public static Vector3 ImageUp(TilePose pose) => Vector3.Transform(Vector3.UnitY, pose.Rotation);
    public static Vector3 WidthAxis(TilePose pose) => Vector3.Transform(Vector3.UnitX, pose.Rotation);

    // A point in a seat's own frame: depth = distance from the centre towards that seat, lateral = along the seat's right.
    // seatOffset = (seat - observer + 4) % 4; the observer sits at -Z, the next seat at +X.
    public static (float Lateral, float Depth) SeatCoords(Vector3 p, int seatOffset) => seatOffset switch
    {
        0 => (p.X, -p.Z),
        1 => (p.Z, p.X),
        2 => (-p.X, p.Z),
        _ => (-p.Z, -p.X),
    };

    // The seat's "towards the player" direction in plugin space.
    public static Vector3 SeatDirection(int seatOffset) => seatOffset switch
    {
        0 => -Vector3.UnitZ,
        1 => Vector3.UnitX,
        2 => Vector3.UnitZ,
        _ => -Vector3.UnitX,
    };

    public static void AssertVector(Vector3 expected, Vector3 actual, float tolerance = Tolerance)
    {
        Assert.True(Vector3.Distance(expected, actual) <= tolerance, $"Expected {expected}, got {actual}");
    }

    public static float AngleBetween(Quaternion a, Quaternion b)
    {
        float dot = Math.Abs(Quaternion.Dot(Quaternion.Normalize(a), Quaternion.Normalize(b)));
        return 2 * MathF.Acos(Math.Min(1f, dot));
    }

    // Separating-axis test for two tiles (boxes of TableLayout.TileSize), each shrunk by `margin` per side so that
    // tiles that merely touch do not count.
    public static bool Overlap(TilePose a, TilePose b, float margin = 0.01f)
    {
        var half = TableLayout.TileSize / 2 - new Vector3(margin);
        Vector3[] axesA = { WidthAxis(a), ImageUp(a), FaceNormal(a) };
        Vector3[] axesB = { WidthAxis(b), ImageUp(b), FaceNormal(b) };
        var d = b.Position - a.Position;

        var candidates = new List<Vector3>(axesA.Concat(axesB));
        foreach (var u in axesA)
            foreach (var v in axesB)
            {
                var c = Vector3.Cross(u, v);
                if (c.LengthSquared() > 1e-6f)
                    candidates.Add(Vector3.Normalize(c));
            }

        foreach (var axis in candidates)
        {
            float ra = Math.Abs(Vector3.Dot(axesA[0], axis)) * half.X + Math.Abs(Vector3.Dot(axesA[1], axis)) * half.Y + Math.Abs(Vector3.Dot(axesA[2], axis)) * half.Z;
            float rb = Math.Abs(Vector3.Dot(axesB[0], axis)) * half.X + Math.Abs(Vector3.Dot(axesB[1], axis)) * half.Y + Math.Abs(Vector3.Dot(axesB[2], axis)) * half.Z;
            if (Math.Abs(Vector3.Dot(d, axis)) > ra + rb)
                return false;
        }

        return true;
    }

    public static List<(int A, int B)> OverlappingPairs(TilePose[] poses)
    {
        var pairs = new List<(int, int)>();
        for (int i = 0; i < poses.Length; i++)
            for (int j = i + 1; j < poses.Length; j++)
                if (Overlap(poses[i], poses[j]))
                    pairs.Add((i, j));
        return pairs;
    }
}
