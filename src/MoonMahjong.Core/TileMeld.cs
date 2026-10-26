// Ported from Practice-Riichi-Mahjong source/Game/Logic/TileRules.vala (OpenRiichi, GPLv3).
using System;
using System.Collections.Generic;

namespace MoonMahjong.Core;

public sealed class TileMeld
{
    public TileMeld(Tile tile1, Tile tile2, Tile tile3, bool isClosed)
        : this(tile1, tile2, tile3, null, isClosed, false)
    {
    }

    private TileMeld(Tile tile1, Tile tile2, Tile tile3, Tile? tile4, bool isClosed, bool isKan)
    {
        Tile1 = tile1;
        Tile2 = tile2;
        Tile3 = tile3;
        Tile4 = tile4;
        IsClosed = isClosed;
        IsKan = isKan;

        IsTriplet = tile1.TileType == tile2.TileType && tile2.TileType == tile3.TileType;
    }

    // Vala named constructor TileMeld.kan
    public static TileMeld Kan(Tile tile1, Tile tile2, Tile tile3, Tile tile4, bool isClosed)
    {
        return new TileMeld(tile1, tile2, tile3, tile4, isClosed, true);
    }

    public override string ToString()
    {
        // Prints Tile2 in the tile_4 slot: kept as in the Vala source.
        string str =

        "tile_1: " + Tile1.ToString() + "\n" +
        "tile_2: " + Tile2.ToString() + "\n" +
        "tile_3: " + Tile3.ToString() + "\n" +
        "tile_4: " + (Tile4 == null ? "(null)" : Tile2.ToString()) + "\n" +
        "is_triplet: " + IsTriplet.ToString() + "\n" +
        "is_kan: " + IsKan.ToString() + "\n" +
        "is_closed: " + IsClosed.ToString();

        return str;
    }

    public int GetMinID()
    {
        int min = Math.Min(Tile1.ID, Math.Min(Tile2.ID, Tile3.ID));
        if (Tile4 != null)
            min = Math.Min(min, Tile4.ID);

        return min;
    }

    public bool Equals(TileMeld meld)
    {
        if (IsTriplet != meld.IsTriplet || IsKan != meld.IsKan || IsClosed != meld.IsClosed)
            return false;

        return Tile.TilesEqualUnordered(GetTiles(), meld.GetTiles());
    }

    private List<Tile> GetTiles()
    {
        List<Tile> tiles = new List<Tile>();
        tiles.Add(Tile1);
        tiles.Add(Tile2);
        tiles.Add(Tile3);
        if (Tile4 != null)
            tiles.Add(Tile4);

        return tiles;
    }

    public Tile Tile1 { get; private set; }
    public Tile Tile2 { get; private set; }
    public Tile Tile3 { get; private set; }
    public Tile? Tile4 { get; private set; }
    public bool IsTriplet { get; private set; } // This is also true for kans
    public bool IsKan { get; private set; }
    public bool IsClosed { get; set; }
}
