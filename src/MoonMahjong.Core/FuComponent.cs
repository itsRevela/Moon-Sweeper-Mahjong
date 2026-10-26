// Ported from Practice-Riichi-Mahjong source/Game/Logic/TileRules.vala (OpenRiichi, GPLv3).
namespace MoonMahjong.Core;

public sealed class FuComponent
{
    public FuComponent(FuSource source, int fu, TileMeld? meld)
    {
        Source = source;
        Fu = fu;
        Meld = meld;
    }

    public FuSource Source { get; private set; }
    public int Fu { get; private set; }
    public TileMeld? Meld { get; private set; } // Only set for FuSource.Meld
}
