// Ported from Practice-Riichi-Mahjong source/Game/Logic/TileRules.vala (OpenRiichi, GPLv3).
using System.Collections.Generic;

namespace MoonMahjong.Core;

public sealed class PlayerStateContext
{
    public PlayerStateContext
    (
        int index,
        List<Tile> hand, // Without the winning tile
        List<Tile> pond,
        List<RoundStateCall> calls,
        Wind wind,
        bool dealer,
        bool inRiichi,
        bool doubleRiichi,
        bool open,
        bool ippatsu,
        bool tilesCalledOn,
        bool firstTurn,
        int sekininIndex
    )
    {
        Index = index;
        Hand = hand;
        Pond = pond;
        Calls = calls;
        Wind = wind;
        Dealer = dealer;
        InRiichi = inRiichi;
        DoubleRiichi = doubleRiichi;
        Open = open;
        Ippatsu = ippatsu;
        TilesCalledOn = tilesCalledOn;
        FirstTurn = firstTurn;
        SekininIndex = sekininIndex;
    }

    public override string ToString()
    {
        string str =
        "index: " + Index.ToString() + "\n" +
        "wind: " + Wind.ToString() + "\n" +
        "dealer: " + Dealer.ToString() + "\n" +
        "in_riichi: " + InRiichi.ToString() + "\n" +
        "double_riichi: " + DoubleRiichi.ToString() + "\n" +
        "open: " + Open.ToString() + "\n" +
        "ippatsu: " + Ippatsu.ToString() + "\n" +
        "tiles_called_on: " + TilesCalledOn.ToString() + "\n" +
        "first_turn: " + FirstTurn.ToString() + "\n" +
        "sekinin_index: " + SekininIndex.ToString() + "\n";

        str += "hand: \n";
        foreach (Tile t in Hand)
            str += "\t" + t.TileType.ToString() + "\n";

        /*str += "pond: \n";
        foreach (Tile t in Pond)
            str += "\t" + t.TileType.ToString() + "\n";*/

        return str;
    }

    public int Index { get; private set; }
    public List<Tile> Hand { get; private set; }
    public List<Tile> Pond { get; private set; }
    public List<RoundStateCall> Calls { get; private set; }
    public Wind Wind { get; private set; }
    public bool Dealer { get; private set; }
    public bool InRiichi { get; private set; }
    public bool DoubleRiichi { get; private set; }
    public bool Open { get; private set; }
    public bool Ippatsu { get; private set; }
    public bool TilesCalledOn { get; private set; }
    public bool FirstTurn { get; private set; }
    public int SekininIndex { get; private set; }
}
