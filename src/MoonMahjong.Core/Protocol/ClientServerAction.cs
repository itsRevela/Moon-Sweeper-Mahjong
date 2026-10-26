// Ported from Practice-Riichi-Mahjong source/Game/Logic/ServerAction.vala (OpenRiichi, GPLv3).
namespace MoonMahjong.Core;

// Client is the seat index of the player who sent Action.
public sealed class ClientServerAction : ServerAction
{
    public ClientServerAction(int client, ClientAction action)
    {
        Client = client;
        Action = action;
    }

    public int Client { get; }
    public ClientAction Action { get; }
}
