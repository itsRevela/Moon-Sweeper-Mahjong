using System;
using HarmonyLib;
using MoonCleaners;

namespace MoonMahjong.Plugin;

// Settings > Audio gets a Mahjong volume slider (requested), after the game's Master volume, Music and Sound effects:
// the game's own slider row, kept in this mod's config (Audio.Volume). Added once the whole screen is built, since
// Build is what the menus call (BuildAudio, called only from Build, might not be patchable on its own).
[HarmonyPatch(typeof(MoonSettingsScreen), nameof(MoonSettingsScreen.Build))]
internal static class AudioSettingsRow
{
    private const string Label = "Mahjong";

    private static void Postfix(MoonSettingsScreen __instance)
    {
        try
        {
            var volume = Plugin.Settings.Volume;
            __instance.SliderRow(MoonSettingsScreen.Tab.Audio, Label, 0f, 1f,
                (Func<float>)(() => volume.Value),
                (Action<float>)(value => volume.Value = Math.Clamp(value, 0f, 1f)),
                (Func<float, string>)MoonSettingsScreen.Percent,
                notify: false);
            Plugin.Logger.LogInfo($"Settings > Audio: added the {Label} volume slider ({volume.Value:P0})");
        }
        catch (Exception e)
        {
            // The game's settings must keep working whatever happens here.
            Plugin.Logger.LogError($"Could not add the Mahjong volume slider to Settings > Audio: {e}");
        }
    }
}
