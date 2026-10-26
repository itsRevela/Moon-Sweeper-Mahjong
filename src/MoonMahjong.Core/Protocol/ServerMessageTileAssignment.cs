// Ported from Practice-Riichi-Mahjong source/Game/Logic/ServerMessage.vala (OpenRiichi, GPLv3).
namespace MoonMahjong.Core;

// Reveals a tile's type to the receiver. The Vala message holds the server's Tile object (copied by serialisation
// on the network path); here the constructor takes a copy so receivers never share the server's tile.
public sealed class ServerMessageTileAssignment : ServerMessage
{
    public ServerMessageTileAssignment(Tile tile)
    {
        Tile = new Tile(tile.ID, tile.TileType, tile.Dora);
    }

    public Tile Tile { get; }
}
