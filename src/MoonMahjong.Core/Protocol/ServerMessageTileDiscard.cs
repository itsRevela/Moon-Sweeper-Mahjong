// Ported from Practice-Riichi-Mahjong source/Game/Logic/ServerMessage.vala (OpenRiichi, GPLv3).
namespace MoonMahjong.Core;

// The current player discards TileID.
public sealed class ServerMessageTileDiscard : ServerMessage
{
    public ServerMessageTileDiscard(int tileID)
    {
        TileID = tileID;
    }

    public int TileID { get; }
}
