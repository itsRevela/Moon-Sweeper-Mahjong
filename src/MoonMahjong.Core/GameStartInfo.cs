// Ported from Practice-Riichi-Mahjong source/Game/Logic/GameStartInfo.vala (OpenRiichi, GPLv3).
// AnimationTimings is dropped: render pacing belongs to the plugin, not the rules.
namespace MoonMahjong.Core;

public sealed record GameStartInfo(
    GamePlayer[] Players,
    int StartingDealer,
    int StartingScore,
    int RoundCount,
    int HanchanCount,
    int UmaHigher,
    int UmaLower)
{
    public GamePlayer[] GetPlayers() => Players;
}
