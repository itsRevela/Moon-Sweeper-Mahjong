namespace MoonMahjong.Table.Presentation;

/// <summary>How long a game runs, chosen on the start screen: East only (tonpuusen) or East + South (hanchan).</summary>
public enum GameLength
{
    EastOnly,
    EastSouth,
}

public static class GameLengths
{
    /// <summary>The game's rounds (GameStartInfo.RoundCount): four per wind.</summary>
    public static int RoundCount(GameLength length) => length == GameLength.EastOnly ? 4 : 8;

    public static string Name(GameLength length) => length == GameLength.EastOnly ? "East only" : "East + South";

    /// <summary>The choice closest to a configured round count ([Game] RoundCount), for the start screen's default.</summary>
    public static GameLength FromRoundCount(int rounds) => rounds <= 4 ? GameLength.EastOnly : GameLength.EastSouth;
}
