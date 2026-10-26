using System;
using MoonCleaners;
using UnityEngine;

namespace MoonMahjong.Plugin;

// The table's own audio sources, with the game's 3D setup (MoonOneShot.Configure). Settings > Audio > Mahjong sets
// their level on its own (requested: the game's Sound effects slider muted the table too). The game puts Master x
// Sound effects on the audio listener (seen in-game: master 1, effects 0, listener 0), so like its music these
// sources ignore the listener and apply Master x Mahjong themselves. (The game has no audio mixers:
// AudioSource.outputAudioMixerGroup can't even be read through the interop.)
internal static class MahjongAudio
{
    private static bool described;

    // The game's Master volume times Settings > Audio > Mahjong, 0 to 1.
    public static float Volume => Math.Clamp(Plugin.Settings.Volume.Value * (MoonUserSettings.Current?.masterVolume ?? 1f), 0f, 1f);

    public static AudioSource Source(GameObject host, float distance)
    {
        var source = host.AddComponent<AudioSource>();
        MoonOneShot.Configure(source, 1f, distance);
        if (!described)
        {
            described = true;
            var settings = MoonUserSettings.Current;
            var levels = settings != null ? $"master {settings.masterVolume:F2}, effects {settings.effectsVolume:F2}" : "unknown";
            Plugin.Logger.LogInfo($"Mahjong audio: the game's setup gave volume {source.volume:F2}, spatial blend " +
                $"{source.spatialBlend:F2}, range {source.minDistance:F1}-{source.maxDistance:F1} m; settings {levels}; " +
                $"listener volume {AudioListener.volume:F2}");
        }
        source.volume = 1f;
        source.ignoreListenerVolume = true;
        source.playOnAwake = false;
        source.loop = false;
        return source;
    }
}
