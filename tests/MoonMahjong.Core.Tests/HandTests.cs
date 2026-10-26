// Ported from Practice-Riichi-Mahjong source/Tests/HandTests.vala (OpenRiichi, GPLv3).
using MoonMahjong.Core;
using Xunit.Abstractions;

namespace MoonMahjong.Core.Tests;

public sealed class HandTests
{
    private readonly ITestOutputHelper output;

    public HandTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    // Like the Vala original this only prints the readings and their yaku; it has no assertions,
    // so it passes as long as reading and yaku evaluation do not throw. (It was never registered
    // in TestMain.vala either.)
    [Fact]
    public void TestHands()
    {
        PlayerStateContext player = new PlayerStateContext(
            3,
            new List<Tile>(), // Without the winning tile
            new List<Tile>(),
            new List<RoundStateCall>(), // calls
            Wind.North,
            false,
            false,
            false,
            true,
            false,
            false,
            false,
            -1
        );

        RoundStateContext round = new RoundStateContext(
            Wind.East,
            new List<Tile>(), // dora
            new List<Tile>(), // ura_dora,
            true,
            new Tile(-1, TileType.Pei, false),
            false,
            false,
            false,
            true
        );

        List<Tile> tiles = new List<Tile>();
        tiles.Add(new Tile(0, TileType.Man9, false));
        tiles.Add(new Tile(1, TileType.Man8, false));
        tiles.Add(new Tile(2, TileType.Man7, false));
        tiles.Add(new Tile(3, TileType.Pin8, false));
        tiles.Add(new Tile(4, TileType.Pin7, false));
        tiles.Add(new Tile(5, TileType.Pin9, false));
        tiles.Add(new Tile(6, TileType.Ton, false));
        tiles.Add(new Tile(7, TileType.Ton, false));
        tiles.Add(new Tile(8, TileType.Nan, false));
        tiles.Add(new Tile(9, TileType.Nan, false));
        tiles.Add(new Tile(10, TileType.Nan, false));

        List<RoundStateCall> calls = new List<RoundStateCall>();

        List<Tile> chiiTiles = new List<Tile>();
        chiiTiles.Add(new Tile(11, TileType.Sou9, false));
        chiiTiles.Add(new Tile(12, TileType.Sou7, false));
        chiiTiles.Add(new Tile(13, TileType.Sou8, false));

        calls.Add(new RoundStateCall(RoundStateCall.CallType.Chii, chiiTiles, new Tile(13, TileType.Sou9, false), 1));

        List<HandReading> readings = TileRules.HandReadings(tiles, calls, false, false);

        foreach (var reading in readings)
        {
            output.WriteLine(reading.ToString());
            output.WriteLine("-----------------");

            foreach (Yaku yaku in Yaku.GetYaku(player, round, reading))
            {
                output.WriteLine(yaku.ToString());
            }
        }
    }
}
