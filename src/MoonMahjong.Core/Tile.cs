// Ported from Practice-Riichi-Mahjong source/Game/Logic/Tile.vala (OpenRiichi, GPLv3).
using System.Collections.Generic;
using System.Linq;

namespace MoonMahjong.Core;

public sealed class Tile
{
    public Tile(int id, TileType type, bool dora)
    {
        ID = id;
        TileType = type;
        Dora = dora;
    }

    // Mutable on purpose: a client's unseen tiles start as Blank and are revealed later (RoundState.TileAssign).
    public int ID { get; set; }
    public TileType TileType { get; set; }

    // True for a red five (aka dora), not for dora derived from indicators.
    public bool Dora { get; set; }

    public bool IsSameSort(Tile other)
    {
        if (TileType >= TileType.Man1 && TileType <= TileType.Man9 &&
            other.TileType >= TileType.Man1 && other.TileType <= TileType.Man9)
            return true;
        if (TileType >= TileType.Pin1 && TileType <= TileType.Pin9 &&
            other.TileType >= TileType.Pin1 && other.TileType <= TileType.Pin9)
            return true;
        if (TileType >= TileType.Sou1 && TileType <= TileType.Sou9 &&
            other.TileType >= TileType.Sou1 && other.TileType <= TileType.Sou9)
            return true;

        return TileType == other.TileType;
    }

    public bool IsSuitTile() =>
        (TileType >= TileType.Man1 && TileType <= TileType.Man9) ||
        (TileType >= TileType.Pin1 && TileType <= TileType.Pin9) ||
        (TileType >= TileType.Sou1 && TileType <= TileType.Sou9);

    public bool IsHonorTile() => IsWindTile() || IsDragonTile();

    public bool IsDragonTile() =>
        TileType == TileType.Haku || TileType == TileType.Hatsu || TileType == TileType.Chun;

    public bool IsWindTile() =>
        TileType == TileType.Ton || TileType == TileType.Nan || TileType == TileType.Shaa || TileType == TileType.Pei;

    public bool IsTerminalTile() =>
        TileType == TileType.Man1 || TileType == TileType.Man9 ||
        TileType == TileType.Pin1 || TileType == TileType.Pin9 ||
        TileType == TileType.Sou1 || TileType == TileType.Sou9;

    public bool IsWind(Wind wind) =>
        (TileType == TileType.Ton && wind == Wind.East) ||
        (TileType == TileType.Nan && wind == Wind.South) ||
        (TileType == TileType.Shaa && wind == Wind.West) ||
        (TileType == TileType.Pei && wind == Wind.North);

    public int GetNumberIndex()
    {
        if (TileType >= TileType.Man1 && TileType <= TileType.Man9)
            return TileType - TileType.Man1;
        if (TileType >= TileType.Pin1 && TileType <= TileType.Pin9)
            return TileType - TileType.Pin1;
        if (TileType >= TileType.Sou1 && TileType <= TileType.Sou9)
            return TileType - TileType.Sou1;

        return 0;
    }

    public bool IsNeighbour(Tile tile)
    {
        if (!IsSuitTile() || !tile.IsSuitTile() || !IsSameSort(tile))
            return false;

        return System.Math.Abs((int)TileType - (int)tile.TileType) == 1;
    }

    public bool IsSecondNeighbour(Tile tile)
    {
        if (!IsSuitTile() || !tile.IsSuitTile() || !IsSameSort(tile))
            return false;

        return System.Math.Abs((int)TileType - (int)tile.TileType) == 2;
    }

    // The tile this one indicates as dora when it is a dora indicator.
    public TileType DoraIndicator()
    {
        var type = (int)TileType;

        if (type >= (int)TileType.Man1 && type <= (int)TileType.Man8)
            return (TileType)(type + 1);
        if (TileType == TileType.Man9)
            return TileType.Man1;
        if (type >= (int)TileType.Pin1 && type <= (int)TileType.Pin8)
            return (TileType)(type + 1);
        if (TileType == TileType.Pin9)
            return TileType.Pin1;
        if (type >= (int)TileType.Sou1 && type <= (int)TileType.Sou8)
            return (TileType)(type + 1);
        if (TileType == TileType.Sou9)
            return TileType.Sou1;

        return TileType switch
        {
            TileType.Ton => TileType.Nan,
            TileType.Nan => TileType.Shaa,
            TileType.Shaa => TileType.Pei,
            TileType.Pei => TileType.Ton,
            TileType.Haku => TileType.Hatsu,
            TileType.Hatsu => TileType.Chun,
            TileType.Chun => TileType.Haku,
            _ => TileType.Blank,
        };
    }

    public override string ToString() => $"({ID}) {TileType}{(Dora ? " dora" : " not dora")}";

    public bool Equals(Tile tile) => ID == tile.ID && TileType == tile.TileType && Dora == tile.Dora;

    public static List<Tile> SortTilesID(List<Tile> list) => list.OrderBy(t => t.ID).ToList();

    public static List<Tile> SortTilesType(List<Tile> list) => list.OrderBy(t => (int)t.TileType).ToList();

    public static bool TilesEqualUnordered(List<Tile> list1, List<Tile> list2) =>
        TilesEqualOrdered(SortTilesID(list1), SortTilesID(list2));

    public static bool TilesEqualOrdered(List<Tile> list1, List<Tile> list2)
    {
        if (list1.Count != list2.Count)
            return false;

        for (var i = 0; i < list1.Count; i++)
            if (!list1[i].Equals(list2[i]))
                return false;

        return true;
    }
}

public enum Wind
{
    East,
    South,
    West,
    North,
}

public static class Winds
{
    // INT_TO_WIND: wraps any integer (including negatives) onto the four winds.
    public static Wind FromInt(int wind)
    {
        if (wind < 0)
            wind = 4 - (-wind % 4);
        return (Wind)(wind % 4);
    }

    public static Wind Next(Wind wind) => (Wind)(((int)wind + 1) % 4);

    public static Wind Previous(Wind wind) => (Wind)(((int)wind + 3) % 4);

    public static string ToKanji(Wind wind) => wind switch
    {
        Wind.South => "南",
        Wind.West => "西",
        Wind.North => "北",
        _ => "東",
    };

    public static string ToDisplayString(Wind wind) => wind switch
    {
        Wind.South => "South",
        Wind.West => "West",
        Wind.North => "North",
        _ => "East",
    };
}

public enum TileType
{
    Blank,
    Man1,
    Man2,
    Man3,
    Man4,
    Man5,
    Man6,
    Man7,
    Man8,
    Man9,
    Pin1,
    Pin2,
    Pin3,
    Pin4,
    Pin5,
    Pin6,
    Pin7,
    Pin8,
    Pin9,
    Sou1,
    Sou2,
    Sou3,
    Sou4,
    Sou5,
    Sou6,
    Sou7,
    Sou8,
    Sou9,
    Ton,
    Nan,
    Shaa,
    Pei,
    Haku,
    Hatsu,
    Chun,
}

public static class TileTypes
{
    // TILE_TYPE_TO_STRING
    public static string ToDisplayString(TileType type)
    {
        var t = (int)type;
        if (t >= (int)TileType.Man1 && t <= (int)TileType.Man9)
            return "Man" + (t - (int)TileType.Man1 + 1);
        if (t >= (int)TileType.Pin1 && t <= (int)TileType.Pin9)
            return "Pin" + (t - (int)TileType.Pin1 + 1);
        if (t >= (int)TileType.Sou1 && t <= (int)TileType.Sou9)
            return "Sou" + (t - (int)TileType.Sou1 + 1);
        return type switch
        {
            TileType.Ton => "Ton",
            TileType.Nan => "Nan",
            TileType.Shaa => "Shaa",
            TileType.Pei => "Pei",
            TileType.Haku => "Haku",
            TileType.Hatsu => "Hatsu",
            TileType.Chun => "Chun",
            _ => "Blank",
        };
    }
}
