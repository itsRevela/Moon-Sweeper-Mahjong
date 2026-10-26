// Ported from Practice-Riichi-Mahjong source/Game/Logic/TileRules.vala (OpenRiichi, GPLv3).
using System.Collections.Generic;
using System.Linq;

namespace MoonMahjong.Core;

public sealed class HandReading
{
    public HandReading
    (
        List<TileMeld> melds,
        TilePair pair
    )
    {
        Tiles = new List<Tile>();
        Melds = new List<TileMeld>();
        Pairs = new List<TilePair>();

        foreach (TileMeld meld in melds)
            AddMeld(meld);

        Pairs.Add(pair);
        Tiles.Add(pair.Tile1);
        Tiles.Add(pair.Tile2);

        IsKokushi = false;
        ValidKeishiki = CheckKeishiki(Tiles);
    }

    // Body of the Vala named constructor HandReading.empty; the other named constructors start from it.
    private HandReading()
    {
        Tiles = new List<Tile>();
        Melds = new List<TileMeld>();
        Pairs = new List<TilePair>();
        IsKokushi = false;
        ValidKeishiki = false;
    }

    // Vala named constructor HandReading.chiitoi
    public static HandReading Chiitoi(List<TilePair> pairs)
    {
        HandReading reading = new HandReading();

        foreach (TilePair pair in pairs)
        {
            reading.Tiles.Add(pair.Tile1);
            reading.Tiles.Add(pair.Tile2);
        }

        reading.Pairs.AddRange(pairs);
        reading.IsKokushi = false;
        reading.ValidKeishiki = true;

        return reading;
    }

    // Vala named constructor HandReading.kokushi
    public static HandReading Kokushi(List<Tile> tiles)
    {
        HandReading reading = new HandReading();
        reading.Tiles.AddRange(tiles);

        reading.IsKokushi = true;
        reading.ValidKeishiki = true;

        return reading;
    }

    // Vala named constructor HandReading.empty
    public static HandReading Empty()
    {
        return new HandReading();
    }

    public void AddMeld(TileMeld meld)
    {
        Melds.Add(meld);
        Tiles.Add(meld.Tile1);
        Tiles.Add(meld.Tile2);
        Tiles.Add(meld.Tile3);

        if (meld.IsKan)
            Tiles.Add(meld.Tile4!);
    }

    private static bool CheckKeishiki(List<Tile> tiles)
    {
        // Double loop arguably faster than using mutations
        foreach (Tile tile in tiles)
        {
            int count = 0;
            foreach (Tile t in tiles)
                if (tile.TileType == t.TileType)
                    count++;
            if (count > 4)
                return false;
        }

        return true;
    }

    public override string ToString()
    {
        string str =

        "tiles:\n";
        foreach (Tile tile in Tiles)
            str += "\t" + tile.ToString() + "\n";
        str += "melds:";
        foreach (TileMeld meld in Melds)
            str += meld.ToString() + "\n";
        str += "pairs:";
        foreach (TilePair pair in Pairs)
            str += pair.ToString() + "\n";

        str +=
        "is_kokushi: " + IsKokushi.ToString() + "\n" +
        "valid_keishiki: " + ValidKeishiki.ToString();

        return str;
    }

    public bool Equals(HandReading reading)
    {
        if (ReferenceEquals(this, reading))
            return true;
        if (IsKokushi != reading.IsKokushi || ValidKeishiki != reading.ValidKeishiki || Melds.Count != reading.Melds.Count || Pairs.Count != reading.Pairs.Count)
            return false;
        // Melds.Count == 7 never holds (chiitoi readings have 7 pairs and no melds); kept as in the Vala source.
        if (IsKokushi || Melds.Count == 7)
            return Tile.TilesEqualUnordered(Tiles, reading.Tiles);
        if (!Pairs[0].Equals(reading.Pairs[0]))
            return false;

        List<TileMeld> s1 = GetSortedMelds();
        List<TileMeld> s2 = reading.GetSortedMelds();

        for (int i = 0; i < s1.Count; i++)
            if (!s1[i].Equals(s2[i]))
                return false;

        return true;
    }

    private List<TileMeld> GetSortedMelds()
    {
        // Gee's ArrayList.sort is a stable TimSort and List<T>.Sort is not, so use a stable ordering by min ID.
        return Melds.OrderBy(m => m.GetMinID()).ToList();
    }

    public List<Tile> Tiles { get; private set; }
    public List<TileMeld> Melds { get; private set; }
    public List<TilePair> Pairs { get; private set; }
    public bool IsKokushi { get; private set; }
    public bool ValidKeishiki { get; private set; }
}
