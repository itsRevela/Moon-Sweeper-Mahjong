// Ported from Practice-Riichi-Mahjong source/Game/Logic/ClientMessage.vala (OpenRiichi, GPLv3).
namespace MoonMahjong.Core;

public sealed class ClientMessageGameAction : ClientMessage
{
    public ClientMessageGameAction(ClientAction action)
    {
        Action = action;
    }

    public ClientAction Action { get; }
}
