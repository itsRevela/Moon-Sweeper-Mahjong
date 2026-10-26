// Ported from Practice-Riichi-Mahjong source/Game/Logic/TileRules.vala (OpenRiichi, GPLv3).
using System.Collections.Generic;

namespace MoonMahjong.Core;

public sealed class RoundStateCall
{
    public RoundStateCall(CallType callType, List<Tile> tiles, Tile? callTile, int discarderIndex)
    {
        Type = callType;
        Tiles = tiles;
        CallTile = callTile;
        DiscarderIndex = discarderIndex;
    }

    // Vala: call_type. The mechanical name CallType would clash with the nested enum below,
    // and C# does not allow a member and a nested type with the same name (CS0102).
    public CallType Type { get; private set; }
    public List<Tile> Tiles { get; private set; }
    public Tile? CallTile { get; private set; }
    public int DiscarderIndex { get; private set; }

    public enum CallType
    {
        Chii,
        Pon,
        OpenKan,
        ClosedKan,
        LateKan,
    }
}
