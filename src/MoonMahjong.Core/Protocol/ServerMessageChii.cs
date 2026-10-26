// Ported from Practice-Riichi-Mahjong source/Game/Logic/ServerMessage.vala (OpenRiichi, GPLv3).
namespace MoonMahjong.Core;

// PlayerIndex called chii on the current discard with two hand tiles.
public sealed class ServerMessageChii : ServerMessage
{
    public ServerMessageChii(int playerIndex, int tile1ID, int tile2ID)
    {
        PlayerIndex = playerIndex;
        Tile1ID = tile1ID;
        Tile2ID = tile2ID;
    }

    public int PlayerIndex { get; }
    public int Tile1ID { get; }
    public int Tile2ID { get; }
}
