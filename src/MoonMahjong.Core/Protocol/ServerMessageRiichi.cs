// Ported from Practice-Riichi-Mahjong source/Game/Logic/ServerMessage.vala (OpenRiichi, GPLv3).
namespace MoonMahjong.Core;

// The current player declared riichi.
public sealed class ServerMessageRiichi : ServerMessage
{
    public ServerMessageRiichi(bool open)
    {
        Open = open;
    }

    public bool Open { get; }
}
