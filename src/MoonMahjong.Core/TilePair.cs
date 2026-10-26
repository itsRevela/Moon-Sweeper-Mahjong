// Ported from Practice-Riichi-Mahjong source/Game/Logic/TileRules.vala (OpenRiichi, GPLv3).
namespace MoonMahjong.Core;

public sealed class TilePair
{
    public TilePair(Tile tile1, Tile tile2)
    {
        Tile1 = tile1;
        Tile2 = tile2;
    }

    public override string ToString()
    {
        string str =

        "tile_1: " + Tile1.ToString() + "\n" +
        "tile_2: " + Tile2.ToString();

        return str;
    }

    public bool Equals(TilePair pair)
    {
        return
            (Tile1.Equals(pair.Tile1) && Tile2.Equals(pair.Tile2)) ||
            (Tile1.Equals(pair.Tile2) && Tile2.Equals(pair.Tile1));
    }

    public Tile Tile1 { get; private set; }
    public Tile Tile2 { get; private set; }
}
