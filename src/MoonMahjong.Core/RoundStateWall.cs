// Ported from Practice-Riichi-Mahjong source/Game/Logic/RoundState.vala, class RoundStateWall (OpenRiichi, GPLv3).
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace MoonMahjong.Core;

public sealed class RoundStateWall
{
    private readonly List<Tile> wallTiles = new();
    private readonly List<Tile> deadWallTiles = new();
    private int doraIndex = 4;

    // An unrevealed wall (a client's view): every tile Blank until assigned.
    public RoundStateWall(int dealer, int wallIndex)
    {
        Init(dealer, wallIndex, false, null, false, false, null, null, null, null, null, null, null);
    }

    private RoundStateWall()
    {
    }

    public static RoundStateWall Shuffled(int dealer, int wallIndex, bool akaDora, RandomClass rnd)
    {
        var wall = new RoundStateWall();
        wall.Init(dealer, wallIndex, akaDora, rnd, true, false, null, null, null, null, null, null, null);
        return wall;
    }

    // Places the given tile types in each player's starting hand, the live wall and the dead wall; Blank entries
    // and every other tile are filled from the remaining set at random. Used for tests.
    public static RoundStateWall Seeded(int dealer, int wallIndex, bool akaDora, bool shuffle, RandomClass? rnd,
        TileType[] p1Tiles, TileType[] p2Tiles, TileType[] p3Tiles, TileType[] p4Tiles, TileType[] drawTiles, TileType[] deadWall)
    {
        var wall = new RoundStateWall();
        wall.Init(dealer, wallIndex, akaDora, rnd, shuffle, true, null, p1Tiles, p2Tiles, p3Tiles, p4Tiles, drawTiles, deadWall);
        return wall;
    }

    public static RoundStateWall Custom(int dealer, int wallIndex, Tile[] tiles)
    {
        var wall = new RoundStateWall();
        wall.Init(dealer, wallIndex, false, null, false, false, tiles, null, null, null, null, null, null);
        return wall;
    }

    private void Init(int dealer, int wallIndex, bool akaDora, RandomClass? rnd, bool shuffled, bool seeded, Tile[]? customTiles,
        TileType[]? p1Tiles, TileType[]? p2Tiles, TileType[]? p3Tiles, TileType[]? p4Tiles, TileType[]? drawTiles, TileType[]? deadWall)
    {
        Tiles = customTiles ?? new Tile[136];

        Dora = new List<Tile>();
        UraDora = new List<Tile>();

        if (customTiles == null)
        {
            for (var i = 0; i < Tiles.Length; i++)
            {
                var type = (shuffled && !seeded) ? (TileType)((i / 4) + 1) : TileType.Blank;
                Tiles[i] = new Tile(-1, type, false);
            }
        }

        var startWall = (4 - dealer) % 4;
        var index = startWall * 34 + wallIndex * 2;

        if (seeded)
            Seed(index, rnd!, Tiles, p1Tiles!, p2Tiles!, p3Tiles!, p4Tiles!, drawTiles!, deadWall!);
        else if (shuffled)
            Shuffle(Tiles, rnd!);

        if ((shuffled || seeded) && akaDora)
        {
            var fiveMan = new List<Tile>();
            var fivePin = new List<Tile>();
            var fiveSou = new List<Tile>();

            foreach (var tile in Tiles)
            {
                if (tile.TileType == TileType.Man5)
                    fiveMan.Add(tile);
                else if (tile.TileType == TileType.Pin5)
                    fivePin.Add(tile);
                else if (tile.TileType == TileType.Sou5)
                    fiveSou.Add(tile);
            }

            // As in the Vala source: the max is exclusive, so the last five of each suit is never picked.
            fiveMan[rnd!.IntRange(0, fiveMan.Count - 1)].Dora = true;
            fivePin[rnd.IntRange(0, fivePin.Count - 1)].Dora = true;
            fiveSou[rnd.IntRange(0, fiveSou.Count - 1)].Dora = true;
        }

        for (var i = 0; i < Tiles.Length; i++)
            Tiles[i].ID = i;

        for (var i = 0; i < 122; i++)
        {
            var t = (index + i) % 136;
            wallTiles.Add(Tiles[t]);
        }

        for (var i = 0; i < 14; i++)
        {
            var t = (index + i + 122) % 136;
            if (i % 2 == 0)
                t++;
            else
                t--;

            deadWallTiles.Insert(0, Tiles[t]);
        }

        FlipDora();
    }

    public Tile FlipDora()
    {
        Debug.Assert(deadWallTiles.Count > doraIndex + 1);

        var tile = deadWallTiles[doraIndex];
        Dora.Add(tile);
        UraDora.Add(deadWallTiles[doraIndex + 1]);

        doraIndex += 2;

        NewestDora = tile;
        return tile;
    }

    public Tile DrawWall()
    {
        Debug.Assert(wallTiles.Count > 0);

        var tile = wallTiles[0];
        wallTiles.RemoveAt(0);
        return tile;
    }

    public Tile DrawDeadWall()
    {
        Debug.Assert(deadWallTiles.Count > 0);

        var tile = deadWallTiles[0];
        deadWallTiles.RemoveAt(0);
        DeadTileAdd();
        doraIndex--;
        return tile;
    }

    private Tile DeadTileAdd()
    {
        var tile = wallTiles[wallTiles.Count - 1];
        wallTiles.RemoveAt(wallTiles.Count - 1);
        deadWallTiles.Insert(deadWallTiles.Count, tile);
        return tile;
    }

    // The Vala version returns null for an unknown ID and lets the caller crash; this fails at the source instead.
    public Tile GetTile(int tileID)
    {
        if (tileID < 0 || tileID >= Tiles.Length)
            throw new ArgumentOutOfRangeException(nameof(tileID), tileID, "No such tile in the wall");

        foreach (var tile in Tiles)
            if (tile.ID == tileID)
                return tile;
        throw new ArgumentOutOfRangeException(nameof(tileID), tileID, "No such tile in the wall");
    }

    // As in the Vala source: a swap with a random position, not a Fisher-Yates shuffle.
    private static void Shuffle(Tile[] tiles, RandomClass rnd)
    {
        for (var i = 0; i < tiles.Length; i++)
        {
            var tmp = rnd.IntRange(0, tiles.Length);
            (tiles[i], tiles[tmp]) = (tiles[tmp], tiles[i]);
        }
    }

    private static void Seed(int index, RandomClass rnd, Tile[] tiles, TileType[] p1Tiles, TileType[] p2Tiles, TileType[] p3Tiles,
        TileType[] p4Tiles, TileType[] drawTiles, TileType[] deadWall)
    {
        var unassigned = new List<TileType>();
        for (var i = 0; i < tiles.Length; i++)
            unassigned.Add((TileType)((i / 4) + 1));

        var length = tiles.Length;

        for (var i = 0; i < 4; i++)
        {
            foreach (var playerTiles in new[] { p1Tiles, p2Tiles, p3Tiles, p4Tiles })
            {
                for (var j = 0; j < 4; j++)
                {
                    var a = i * 4 + j;

                    if (playerTiles.Length > a)
                        Replace(tiles, playerTiles[a], unassigned, index);
                    index++;
                    length--;

                    if (a >= 12)
                        break;
                }
            }
        }

        for (var i = 0; i < drawTiles.Length; i++)
        {
            Replace(tiles, drawTiles[i], unassigned, index++);
            length--;
        }

        index += length - 14;

        for (var i = 0; i < deadWall.Length; i++)
            Replace(tiles, deadWall[i], unassigned, index++);

        foreach (var t in tiles)
            if (t.TileType == TileType.Blank)
            {
                Debug.Assert(unassigned.Count > 0);
                var pick = rnd.IntRange(0, unassigned.Count);
                t.TileType = unassigned[pick];
                unassigned.RemoveAt(pick);
            }
    }

    private static void Replace(Tile[] tiles, TileType tileType, List<TileType> unassigned, int index)
    {
        if (tileType == TileType.Blank)
            return;

        index %= tiles.Length;

        for (var i = 0; i < unassigned.Count; i++)
            if (unassigned[i] == tileType)
            {
                tiles[index].TileType = unassigned[i];
                unassigned.RemoveAt(i);
                return;
            }

        Debug.WriteLine("RoundStateWall: RoundState seed, did not find " + tileType);
    }

    public bool Empty => wallTiles.Count == 0;
    public int Remaining => wallTiles.Count;
    public bool CanKan => Dora.Count < 5;
    public bool CanCall => wallTiles.Count > 0;
    public bool CanRiichi => wallTiles.Count >= 4;
    public Tile? NewestDora { get; private set; }
    public List<Tile> Dora { get; private set; } = null!;
    public List<Tile> UraDora { get; private set; } = null!;
    public Tile[] Tiles { get; private set; } = null!;
}

public enum GameDrawType
{
    None,
    EmptyWall,
    FourWinds,
    FourKans,
    FourRiichi,
    VoidHand,
    TripleRon,
}

public enum ChankanCall
{
    None,
    Late,
    Closed,
}
