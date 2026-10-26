// Ported from Practice-Riichi-Mahjong source/Game/Logic/TileRules.vala (OpenRiichi, GPLv3).
using System.Collections.Generic;

namespace MoonMahjong.Core;

public sealed class RoundStateContext
{
    public RoundStateContext
    (
        Wind roundWind,
        List<Tile> dora,
        List<Tile> uraDora,
        bool ron,
        Tile winTile,
        bool lastTile,
        bool rinshan,
        bool chankan,
        bool flowInterrupted
    )
    {
        RoundWind = roundWind;
        Dora = dora;
        UraDora = uraDora;
        Ron = ron;
        WinTile = winTile;
        LastTile = lastTile;
        Rinshan = rinshan;
        Chankan = chankan;
        FlowInterrupted = flowInterrupted;
    }

    public override string ToString()
    {
        // "win_wile" is a typo in the Vala source, kept as is.
        string str =
        "round_wind: " + RoundWind.ToString() + "\n" +
        "ron: " + Ron.ToString() + "\n" +
        "win_wile: " + WinTile.TileType.ToString() + "\n" +
        "last_tile: " + LastTile.ToString() + "\n" +
        "rinshan: " + Rinshan.ToString() + "\n" +
        "chankan: " + Chankan.ToString() + "\n" +
        "flow_interrupted: " + FlowInterrupted.ToString() + "\n";

        str += "dora: \n";
        foreach (Tile t in Dora)
            str += "\t" + t.TileType.ToString() + "\n";

        str += "ura_dora: \n";
        foreach (Tile t in UraDora)
            str += "\t" + t.TileType.ToString() + "\n";

        return str;
    }

    public Wind RoundWind { get; private set; }
    public List<Tile> Dora { get; private set; }
    public List<Tile> UraDora { get; private set; }
    public bool Ron { get; private set; }
    public Tile WinTile { get; private set; }
    public bool LastTile { get; private set; }
    public bool Rinshan { get; private set; }
    public bool Chankan { get; private set; }
    public bool FlowInterrupted { get; private set; }
}
