// Ported from Practice-Riichi-Mahjong source/Tests/TestHelpers.vala (OpenRiichi, GPLv3).
using MoonMahjong.Core;

namespace MoonMahjong.Core.Tests;

// Shared helpers for the unit tests.
// expect_int/expect_true became xUnit asserts; expect_string is only used by the unported PointCalcTests.
internal static class TestHelpers
{
    private static int testNextTileId = 1000;

    // Parses compact tile notation into tiles with unique IDs.
    // Digits are followed by their suit: m = man, p = pin, s = sou, z = honors.
    // Honors: 1z-4z = East, South, West, North; 5z-7z = Haku, Hatsu, Chun.
    // A 0 in a suit means a red five (aka dora), e.g. "0p" is a red 5-pin.
    public static List<Tile> TestTiles(string notation)
    {
        List<Tile> tiles = new List<Tile>();
        List<int> pending = new List<int>();

        for (int i = 0; i < notation.Length; i++)
        {
            char c = notation[i];

            if (c >= '0' && c <= '9')
            {
                pending.Add(c - '0');
                continue;
            }

            TileType first;
            if (c == 'm')
                first = TileType.Man1;
            else if (c == 'p')
                first = TileType.Pin1;
            else if (c == 's')
                first = TileType.Sou1;
            else if (c == 'z')
                first = TileType.Ton;
            else
                throw new ArgumentException($"Invalid tile notation '{notation}'", nameof(notation));

            foreach (int n in pending)
            {
                bool red = n == 0;
                int number = red ? 5 : n;
                if (c == 'z' && (red || number > 7))
                    throw new ArgumentException($"Invalid honor tile in '{notation}'", nameof(notation));

                TileType type = (TileType)((int)first + number - 1);
                tiles.Add(new Tile(NextTileId(), type, red));
            }

            pending.Clear();
        }

        if (pending.Count != 0)
            throw new ArgumentException($"Tile notation '{notation}' is missing a trailing suit letter", nameof(notation));

        return tiles;
    }

    public static RoundStateCall TestCall(RoundStateCall.CallType type, string notation, int discarderIndex)
    {
        List<Tile> tiles = TestTiles(notation);
        Tile? callTile = type == RoundStateCall.CallType.ClosedKan ? null : tiles[0];
        return new RoundStateCall(type, tiles, callTile, discarderIndex);
    }

    public static bool ScoringHasYaku(Scoring score, YakuType type)
    {
        if (score.Yaku == null)
            return false;

        foreach (Yaku y in score.Yaku)
            if (y.YakuType == type)
                return true;

        return false;
    }

    // test_next_tile_id++, made atomic because xUnit may run test classes in parallel.
    private static int NextTileId() => Interlocked.Increment(ref testNextTileId) - 1;
}

// Describes a finished hand for the scoring engine. The winner is always
// player 0; "concealed" excludes the winning tile.
internal sealed class TestHand
{
    public string Concealed { get; set; } = "";
    public string Win { get; set; } = "";
    public bool Ron { get; set; } = true;
    public bool Dealer { get; set; } = false;
    public Wind SeatWind { get; set; } = Wind.West;
    public Wind RoundWind { get; set; } = Wind.East;
    public bool Riichi { get; set; } = false;
    public string DoraIndicators { get; set; } = "";
    public List<RoundStateCall> Calls { get; } = new List<RoundStateCall>();

    public Scoring Score()
    {
        PlayerStateContext player = new PlayerStateContext
        (
            0,
            TestHelpers.TestTiles(Concealed),
            new List<Tile>(),
            Calls,
            SeatWind,
            Dealer,
            Riichi,
            false,
            false,
            false,
            false,
            false,
            -1
        );

        RoundStateContext round = new RoundStateContext
        (
            RoundWind,
            TestHelpers.TestTiles(DoraIndicators),
            new List<Tile>(),
            Ron,
            TestHelpers.TestTiles(Win)[0],
            false,
            false,
            false,
            true
        );

        return TileRules.GetScore(player, round);
    }
}
