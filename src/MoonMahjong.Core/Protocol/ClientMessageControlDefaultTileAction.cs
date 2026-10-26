// Ported from Practice-Riichi-Mahjong source/Game/Logic/ClientMessage.vala (OpenRiichi, GPLv3).
namespace MoonMahjong.Core;

// Unused by the Vala source as well; kept for protocol completeness.
public sealed class ClientMessageControlDefaultTileAction : ClientMessage
{
    public ClientMessageControlDefaultTileAction(int tileID)
    {
        TileID = tileID;
    }

    public int TileID { get; }
}
