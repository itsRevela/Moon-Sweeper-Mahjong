// Ported from Practice-Riichi-Mahjong source/Game/Logic/TileRules.vala (OpenRiichi, GPLv3).
namespace MoonMahjong.Core;

public enum WaitPattern
{
    None,
    Ryanmen, // Two-sided sequence wait
    Kanchan, // Wait on the middle tile of a sequence
    Penchan, // Edge wait (12 waiting on 3, 89 waiting on 7)
    Shanpon, // Two pairs, one of which becomes a triplet
    Tanki,   // Single tile wait for the pair
}
