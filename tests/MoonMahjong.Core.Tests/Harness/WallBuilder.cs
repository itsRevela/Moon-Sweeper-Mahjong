using MoonMahjong.Core;

namespace MoonMahjong.Core.Tests.Harness;

// Builds a fixed 136-tile wall for RoundState.Custom / ServerRoundStateValidator with dealer 0 and wall index 0,
// where the live wall starts at tile 0. With that layout:
// - seat p's starting tiles are 16r + 4p + j for the three blocks (r = 0..2, j = 0..3), then 48 + p;
// - live-wall draws start at 52 and go round the table from the dealer (without calls: 52 + 4i + seat);
// - the first dora indicator is tile 130.
// Every unspecified slot (including a null hand) is filled from the remaining tiles in type order, so each type
// appears exactly 4 times.
internal static class WallBuilder
{
    public const int Dealer = 0;
    public const int WallIndex = 0;
    public const int DoraIndicatorSlot = 130;

    public static Tile[] Build(string?[] hands, string?[]? seatDraws = null, string? doraIndicator = null)
    {
        if (hands.Length != 4)
            throw new ArgumentException("Need four hands", nameof(hands));

        var slots = new Tile?[136];
        var used = new int[(int)TileType.Chun + 1];

        void Place(int slot, Tile tile)
        {
            if (slots[slot] != null)
                throw new InvalidOperationException($"Wall slot {slot} assigned twice");
            if (++used[(int)tile.TileType] > 4)
                throw new InvalidOperationException($"More than four {tile.TileType} in the wall");
            slots[slot] = new Tile(-1, tile.TileType, tile.Dora);
        }

        for (var p = 0; p < 4; p++)
        {
            if (hands[p] == null)
                continue;

            var tiles = TestHelpers.TestTiles(hands[p]!);
            if (tiles.Count != 13)
                throw new ArgumentException($"Hand {p} has {tiles.Count} tiles, need 13", nameof(hands));

            for (var k = 0; k < 13; k++)
                Place(StartingSlot(p, k), tiles[k]);
        }

        if (seatDraws != null)
        {
            for (var s = 0; s < seatDraws.Length; s++)
            {
                if (seatDraws[s] == null)
                    continue;

                var draws = TestHelpers.TestTiles(seatDraws[s]!);
                for (var i = 0; i < draws.Count; i++)
                    Place(52 + 4 * i + s, draws[i]);
            }
        }

        if (doraIndicator != null)
            Place(DoraIndicatorSlot, TestHelpers.TestTiles(doraIndicator)[0]);

        var pool = new List<TileType>();
        for (var type = (int)TileType.Man1; type <= (int)TileType.Chun; type++)
            for (var n = used[type]; n < 4; n++)
                pool.Add((TileType)type);

        var next = 0;
        var wall = new Tile[136];
        for (var i = 0; i < wall.Length; i++)
            wall[i] = slots[i] ?? new Tile(-1, pool[next++], false);

        return wall;
    }

    public static int StartingSlot(int seat, int k) => k < 12 ? 16 * (k / 4) + 4 * seat + k % 4 : 48 + seat;

    // The first tile of the given type in a player's hand.
    public static Tile Find(List<Tile> hand, TileType type) => hand.First(t => t.TileType == type);
}
