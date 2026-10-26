// Ported from Practice-Riichi-Mahjong source/Game/Logic/ClientAction.vala (OpenRiichi, GPLv3).
namespace MoonMahjong.Core;

// Tile is the ID of the tile to discard.
public sealed class TileDiscardClientAction : ClientAction
{
    public TileDiscardClientAction(int tile)
    {
        Tile = tile;
    }

    public int Tile { get; }
}
