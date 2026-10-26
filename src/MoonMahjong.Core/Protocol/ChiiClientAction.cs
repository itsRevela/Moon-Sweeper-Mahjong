// Ported from Practice-Riichi-Mahjong source/Game/Logic/ClientAction.vala (OpenRiichi, GPLv3).
namespace MoonMahjong.Core;

// Tile1 and Tile2 are the IDs of the two hand tiles used for the chii.
public sealed class ChiiClientAction : ClientAction
{
    public ChiiClientAction(int tile1, int tile2)
    {
        Tile1 = tile1;
        Tile2 = tile2;
    }

    public int Tile1 { get; }
    public int Tile2 { get; }
}
