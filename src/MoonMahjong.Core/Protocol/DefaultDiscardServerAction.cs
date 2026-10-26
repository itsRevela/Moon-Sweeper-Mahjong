// Ported from Practice-Riichi-Mahjong source/Game/Logic/ServerAction.vala (OpenRiichi, GPLv3).
namespace MoonMahjong.Core;

public sealed class DefaultDiscardServerAction : ServerAction
{
    public DefaultDiscardServerAction(int client, int tile)
    {
        Client = client;
        Tile = tile;
    }

    public int Client { get; }
    public int Tile { get; }
}
