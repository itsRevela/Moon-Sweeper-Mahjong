using MoonMahjong.Core;
using Xunit.Abstractions;

namespace MoonMahjong.Core.Tests;

// Riichi is offered when some discard leaves the hand tenpai (TileRules.TenpaiTiles over InTenpai). Checked here
// against a plain definition: a 13-tile hand is tenpai when some tile type would complete it (WinningHand).
public sealed class TenpaiTests
{
    private static readonly TileType[] Types = Enumerable.Range((int)TileType.Man1, 34).Select(t => (TileType)t).ToArray();
    private readonly ITestOutputHelper output;

    public TenpaiTests(ITestOutputHelper output) => this.output = output;

    private static bool CompletedBySomeTile(List<Tile> hand) =>
        Types.Any(type => TileRules.WinningHand(hand.Append(new Tile(-1, type, false)).ToList(), null));

    private static string Notation(IEnumerable<Tile> tiles) =>
        string.Join(" ", tiles.OrderBy(t => t.TileType).Select(t => t.TileType));

    // A winning shape (four sets and a pair, seven pairs or thirteen orphans) less one tile: always tenpai.
    private static List<Tile> TenpaiHand(Random random)
    {
        var counts = new int[Types.Length];
        var tiles = new List<TileType>();

        bool Take(TileType type)
        {
            var i = Array.IndexOf(Types, type);
            if (counts[i] == 4)
                return false;
            counts[i]++;
            tiles.Add(type);
            return true;
        }

        var shape = random.Next(10);
        if (shape == 0)
        {
            foreach (var pair in Types.OrderBy(_ => random.Next()).Take(7))
            {
                Take(pair);
                Take(pair);
            }
        }
        else if (shape == 1)
        {
            var orphans = Types.Where(t => t >= TileType.Ton || (int)t % 9 is 1 or 0).ToList();
            foreach (var orphan in orphans)
                Take(orphan);
            Take(orphans[random.Next(orphans.Count)]);
        }
        else
        {
            while (tiles.Count < 12)
            {
                var start = Types[random.Next(Types.Length)];
                var suited = start < TileType.Ton && ((int)start - 1) % 9 <= 6;
                var set = suited && random.Next(2) == 0
                    ? new[] { start, start + 1, start + 2 }
                    : new[] { start, start, start };
                var before = tiles.Count;
                if (!set.All(Take))
                {
                    foreach (var t in tiles.Skip(before).ToList())
                        counts[Array.IndexOf(Types, t)]--;
                    tiles.RemoveRange(before, tiles.Count - before);
                }
            }
            while (true)
            {
                var pair = Types[random.Next(Types.Length)];
                if (counts[Array.IndexOf(Types, pair)] <= 2)
                {
                    Take(pair);
                    Take(pair);
                    break;
                }
            }
        }

        tiles.RemoveAt(random.Next(tiles.Count));
        return tiles.Select((type, id) => new Tile(id, type, false)).ToList();
    }

    [Fact]
    public void InTenpai_FindsEveryHandOneTileFromWinning()
    {
        var random = new Random(12345);
        var missed = new List<string>();
        for (var i = 0; i < 3000; i++)
        {
            var hand = TenpaiHand(random);
            if (!TileRules.InTenpai(hand, null))
                missed.Add(Notation(hand));
        }
        foreach (var hand in missed.Distinct().Take(20))
            output.WriteLine(hand);

        Assert.Empty(missed);
    }

    [Fact]
    public void InTenpai_AgreesWithTheWinningTileSearch_OnRandomHands()
    {
        var random = new Random(54321);
        var all = Types.SelectMany(t => Enumerable.Repeat(t, 4)).ToList();
        var wrong = new List<string>();
        for (var i = 0; i < 3000; i++)
        {
            var hand = all.OrderBy(_ => random.Next()).Take(13).Select((type, id) => new Tile(id, type, false)).ToList();
            if (TileRules.InTenpai(hand, null) != CompletedBySomeTile(hand))
                wrong.Add(Notation(hand));
        }
        foreach (var hand in wrong.Take(20))
            output.WriteLine(hand);

        Assert.Empty(wrong);
    }
}
