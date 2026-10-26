// Ported from Practice-Riichi-Mahjong source/Game/Logic/ServerMessage.vala (OpenRiichi, GPLv3).
namespace MoonMahjong.Core;

public sealed class ServerMessagePlayerLeft : ServerMessage
{
    public ServerMessagePlayerLeft(int playerIndex)
    {
        PlayerIndex = playerIndex;
    }

    public int PlayerIndex { get; }
}
