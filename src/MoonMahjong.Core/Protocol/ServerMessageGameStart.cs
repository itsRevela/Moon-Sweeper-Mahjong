// Ported from Practice-Riichi-Mahjong source/Game/Logic/ServerMessage.vala (OpenRiichi, GPLv3).
namespace MoonMahjong.Core;

// PlayerIndex is the receiver's seat, or -1 for a spectator.
public sealed class ServerMessageGameStart : ServerMessage
{
    public ServerMessageGameStart(GameStartInfo info, ServerSettings settings, int playerIndex)
    {
        Info = info;
        Settings = settings;
        PlayerIndex = playerIndex;
    }

    public GameStartInfo Info { get; }
    public ServerSettings Settings { get; }
    public int PlayerIndex { get; }
}
