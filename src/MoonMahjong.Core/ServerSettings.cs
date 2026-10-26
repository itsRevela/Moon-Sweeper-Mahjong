// Ported from Practice-Riichi-Mahjong source/Game/ServerSettings.vala (OpenRiichi, GPLv3).
// The on-disk config handling was dropped; the plugin's BepInEx config supplies these values.
namespace MoonMahjong.Core;

public sealed record ServerSettings
{
    public const int MinDecisionTime = 2;
    public const int MaxDecisionTime = 300;

    private readonly int decisionTime = 10;

    // OnOffEnum fields from the Vala source become bools (ON == true). Defaults match ServerSettings.default().
    public bool OpenRiichi { get; init; }
    public bool AkaDora { get; init; } = true;
    public bool MultipleRon { get; init; } = true;
    public bool TripleRonDraw { get; init; } = true;

    // Mod addition (not in OpenRiichi): no decision timer at all, so the server waits for a player's own decision and
    // never plays the default discard/no-call for them. Bots answer at once either way. Off by default for parity.
    public bool UnlimitedDecisionTime { get; init; }

    // Seconds a player has to decide on a call or discard (ignored with UnlimitedDecisionTime).
    public int DecisionTime
    {
        get => decisionTime;
        init => decisionTime = System.Math.Clamp(value, MinDecisionTime, MaxDecisionTime);
    }

    public static ServerSettings Default { get; } = new();
}
