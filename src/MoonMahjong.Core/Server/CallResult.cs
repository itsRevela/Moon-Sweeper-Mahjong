// Ported from Practice-Riichi-Mahjong source/GameServer/GameState/ServerRoundStateValidator.vala (OpenRiichi, GPLv3).
using System.Collections.Generic;

namespace MoonMahjong.Core;

// The call that won priority on a discard. Callers has several entries only for (multiple) ron; Draw is set for a
// triple-ron draw. Tiles are the caller's hand tiles (null for ron).
public sealed class CallResult
{
    public CallResult
    (
        ServerRoundStatePlayer[] callers,
        ServerRoundStatePlayer discarder,
        Tile discardTile,
        List<Tile>? tiles,
        CallDecisionType callType,
        int riichiReturnIndex,
        bool draw
    )
    {
        Callers = callers;
        Discarder = discarder;
        DiscardTile = discardTile;
        Tiles = tiles;
        CallType = callType;
        RiichiReturnIndex = riichiReturnIndex;
        Draw = draw;
    }

    public ServerRoundStatePlayer[] Callers { get; private set; }
    public ServerRoundStatePlayer Discarder { get; private set; }
    public Tile DiscardTile { get; private set; }
    public List<Tile>? Tiles { get; private set; }
    public CallDecisionType CallType { get; private set; }
    public int RiichiReturnIndex { get; private set; }
    public bool Draw { get; private set; }
}
