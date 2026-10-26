// Ported from Practice-Riichi-Mahjong source/Game/Logic/ServerMessage.vala (OpenRiichi, GPLv3).
namespace MoonMahjong.Core;

// The current player declared a closed kan of TileType.
public sealed class ServerMessageClosedKan : ServerMessage
{
    public ServerMessageClosedKan(TileType tileType)
    {
        TileType = tileType;
    }

    public TileType TileType { get; }
}
