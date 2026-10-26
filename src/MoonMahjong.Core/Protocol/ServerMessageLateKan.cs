// Ported from Practice-Riichi-Mahjong source/Game/Logic/ServerMessage.vala (OpenRiichi, GPLv3).
namespace MoonMahjong.Core;

// The current player added TileID to a pon.
public sealed class ServerMessageLateKan : ServerMessage
{
    public ServerMessageLateKan(int tileID)
    {
        TileID = tileID;
    }

    public int TileID { get; }
}
