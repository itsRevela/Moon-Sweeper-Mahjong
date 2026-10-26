// Ported from Practice-Riichi-Mahjong source/Game/Logic/ServerMessage.vala (OpenRiichi, GPLv3).
namespace MoonMahjong.Core;

// PlayerIndex called kan on the current discard with three hand tiles.
public sealed class ServerMessageOpenKan : ServerMessage
{
    public ServerMessageOpenKan(int playerIndex, int tile1ID, int tile2ID, int tile3ID)
    {
        PlayerIndex = playerIndex;
        Tile1ID = tile1ID;
        Tile2ID = tile2ID;
        Tile3ID = tile3ID;
    }

    public int PlayerIndex { get; }
    public int Tile1ID { get; }
    public int Tile2ID { get; }
    public int Tile3ID { get; }
}
