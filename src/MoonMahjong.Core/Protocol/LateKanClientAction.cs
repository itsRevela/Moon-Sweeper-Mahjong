// Ported from Practice-Riichi-Mahjong source/Game/Logic/ClientAction.vala (OpenRiichi, GPLv3).
namespace MoonMahjong.Core;

// Tile is the ID of the tile added to the pon.
public sealed class LateKanClientAction : ClientAction
{
    public LateKanClientAction(int tile)
    {
        Tile = tile;
    }

    public int Tile { get; }
}
