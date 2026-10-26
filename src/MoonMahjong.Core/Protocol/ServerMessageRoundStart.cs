// Ported from Practice-Riichi-Mahjong source/Game/Logic/ServerMessage.vala (OpenRiichi, GPLv3).
namespace MoonMahjong.Core;

public sealed class ServerMessageRoundStart : ServerMessage
{
    public ServerMessageRoundStart(RoundStartInfo info)
    {
        Info = info;
    }

    public RoundStartInfo Info { get; }
}
