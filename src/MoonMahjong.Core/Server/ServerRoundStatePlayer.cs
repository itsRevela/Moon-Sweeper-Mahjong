// Ported from Practice-Riichi-Mahjong source/GameServer/GameState/ServerRoundStatePlayer.vala (OpenRiichi, GPLv3).
using System.Collections.Generic;

namespace MoonMahjong.Core;

// The server's per-seat bookkeeping around a RoundStatePlayer: call state and connection state.
public sealed class ServerRoundStatePlayer
{
    public ServerRoundStatePlayer(RoundStatePlayer player)
    {
        Player = player;
        CallDecision = null;
        State = PlayerState.Done;
    }

    public List<Tile>? GetOpenKanTiles(Tile tile)
    {
        var kan = new List<Tile>();

        foreach (var t in Hand)
            if (t.TileType == tile.TileType)
            {
                kan.Add(t);
                if (kan.Count == 3)
                    return kan;
            }

        return null;
    }

    public List<Tile>? GetPonTiles(Tile tile)
    {
        var pon = new List<Tile>();

        foreach (var t in Hand)
            if (t.TileType == tile.TileType)
            {
                pon.Add(t);
                if (pon.Count == 2)
                    return pon;
            }

        return null;
    }

    public RoundStateCallDecision? CallDecision { get; set; }
    public PlayerState State { get; set; }
    public RoundStatePlayer Player { get; private set; }
    public int Index => Player.Index;
    public bool InRiichi => Player.InRiichi;
    public Tile NewestTile => Player.NewestTile;
    public Tile DefaultDiscardTile => Player.GetDefaultDiscardTile();
    public List<Tile> Hand => Player.Hand;
    public bool Open => Player.Open;
    public bool Disconnected { get; set; }
}
