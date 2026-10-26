using BepInEx.Configuration;

namespace MoonMahjong.Plugin;

// BepInEx/config/itsrevela.moonsweepers.mahjong.cfg
internal sealed class MahjongConfig
{
    public MahjongConfig(ConfigFile file)
    {
        TileScale = file.Bind("Table", "TileScale", 1.0f,
            new ConfigDescription("Tile size relative to real tiles (26 x 35 x 20 mm).", new AcceptableValueRange<float>(0.6f, 1.6f)));
        ViewHeight = file.Bind("Table", "ViewHeight", 0.60f,
            new ConfigDescription("Seated camera height above the table top, in table units.", new AcceptableValueRange<float>(0.2f, 1.5f)));
        ViewDistance = file.Bind("Table", "ViewDistance", 0.62f,
            new ConfigDescription("Seated camera distance from the table centre toward the seat.", new AcceptableValueRange<float>(0.2f, 1.5f)));
        RoundCount = file.Bind("Game", "RoundCount", 8,
            new ConfigDescription("Rounds in a game: 8 is a hanchan (East and South), 4 is east-only.", new AcceptableValueRange<int>(1, 16)));
        DecisionTimeLimit = file.Bind("Game", "DecisionTimeLimit", 0,
            new ConfigDescription("Seconds a player has for each discard or call before the default is played for them; 0 means no limit.",
                new AcceptableValueRange<int>(0, 300)));
        DevAutoPlay = file.Bind("Developer", "DevAutoPlay", false,
            "Testing aid: from the main menu, continue the current save, sit at the nearest mahjong table and save screenshots to the plugin's dev folder.");
        DevScreenshotSeconds = file.Bind("Developer", "DevScreenshotSeconds", 6f,
            new ConfigDescription("Seconds between dev screenshots once seated.", new AcceptableValueRange<float>(1f, 120f)));
        DevScreenshotCount = file.Bind("Developer", "DevScreenshotCount", 5,
            new ConfigDescription("Dev screenshots to take before stopping.", new AcceptableValueRange<int>(0, 100)));
        DevTable = file.Bind("Developer", "DevTable", "",
            "Testing aid (with DevAutoPlay): the station whose lounge table to sit at (north, south, east or west); empty for the nearest.");
        DevPauseTest = file.Bind("Developer", "DevPauseTest", false,
            "Testing aid (with DevAutoPlay): 20 s after sitting, stand up for 10 s, then sit back down in the same chair, to check that leaving the table pauses the game.");
    }

    public ConfigEntry<float> TileScale { get; }
    public ConfigEntry<float> ViewHeight { get; }
    public ConfigEntry<float> ViewDistance { get; }
    public ConfigEntry<int> RoundCount { get; }
    public ConfigEntry<int> DecisionTimeLimit { get; }

    // The rules for a new game: OpenRiichi's defaults plus the decision time limit chosen here.
    public Core.ServerSettings GameSettings => DecisionTimeLimit.Value <= 0
        ? Core.ServerSettings.Default with { UnlimitedDecisionTime = true }
        : Core.ServerSettings.Default with { DecisionTime = DecisionTimeLimit.Value };
    public ConfigEntry<bool> DevAutoPlay { get; }
    public ConfigEntry<float> DevScreenshotSeconds { get; }
    public ConfigEntry<int> DevScreenshotCount { get; }
    public ConfigEntry<bool> DevPauseTest { get; }
    public ConfigEntry<string> DevTable { get; }
}
