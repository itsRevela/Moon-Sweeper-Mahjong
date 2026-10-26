// Ported from Practice-Riichi-Mahjong source/GameServer/GameState/ServerRoundStatePlayer.vala (OpenRiichi, GPLv3).
using System.Collections.Generic;

namespace MoonMahjong.Core;

// A player's pending call on the current discard; Tiles are the hand tiles used (null for ron).
public sealed class RoundStateCallDecision
{
    public RoundStateCallDecision(CallDecisionType type, List<Tile>? tiles)
    {
        CallType = type;
        Tiles = tiles;
    }

    public CallDecisionType CallType { get; private set; }
    public List<Tile>? Tiles { get; private set; }
}
