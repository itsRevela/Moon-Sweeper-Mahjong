using System;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MoonCleaners;
using MoonMahjong.Net;
using MoonMahjong.Table.Presentation;
using UnityEngine;

namespace MoonMahjong.Plugin;

// The Reset table sounds at the table (requested: everyone nearby hears a reset coming, not too loud): the charge
// while anyone holds, a falling blip when the last holder lets go, and a chime when the game ends from a reset. They
// play from the table's own sources (MahjongAudio), at the Settings > Audio > Mahjong level.
internal sealed class ResetAudio : IDisposable
{
    private const float Volume = 0.7f; // the clips themselves peak at ResetSounds.Peak
    private const float Distance = 14f; // metres: heard around the table, not across the lounge

    private readonly Transform table;
    private readonly AudioSource source; // the charge, which can be stopped
    private readonly AudioSource shots; // the cancel and done sounds
    private readonly AudioClip charge;
    private readonly AudioClip cancel;
    private readonly AudioClip done;
    private bool charging;
    private bool resetSent;

    public ResetAudio(GameObject table)
    {
        this.table = table.transform;
        charge = Clip("MoonMahjong reset charge", ResetSounds.Charge());
        cancel = Clip("MoonMahjong reset cancel", ResetSounds.Cancel());
        done = Clip("MoonMahjong reset done", ResetSounds.Done());
        // The table's own sources (MahjongAudio): Settings > Audio > Mahjong sets their level, not Sound effects.
        source = MahjongAudio.Source(table, Distance);
        source.clip = charge;
        shots = MahjongAudio.Source(table, Distance);
    }

    // `holdersLeft`: how many are still holding after this signal.
    public void Signal(ResetSignal signal, int holdersLeft)
    {
        switch (signal)
        {
            case ResetSignal.HoldStarted:
                if (!charging)
                {
                    source.volume = Volume * MahjongAudio.Volume;
                    source.Play();
                }
                charging = true;
                break;
            case ResetSignal.HoldStopped when holdersLeft == 0 && charging:
                source.Stop();
                Play(cancel);
                charging = false;
                break;
            case ResetSignal.Reset:
                resetSent = true; // the charge runs on until the game ends
                break;
        }
    }

    // The game being watched ended: a chime if a reset ended it.
    public void GameEnded(bool cancelled)
    {
        if (cancelled && (charging || resetSent))
        {
            source.Stop();
            Play(done);
        }
        charging = false;
        resetSent = false;
    }

    public void Dispose()
    {
        if (source != null)
            UnityEngine.Object.Destroy(source);
        if (shots != null)
            UnityEngine.Object.Destroy(shots);
    }

    private void Play(AudioClip clip)
    {
        if (MahjongAudio.Volume > 0f && MoonOneShot.Hears(table.position, Distance))
            shots.PlayOneShot(clip, Volume * MahjongAudio.Volume);
    }

    private static AudioClip Clip(string name, float[] samples)
    {
        var clip = AudioClip.Create(name, samples.Length, 1, ResetSounds.SampleRate, false);
        clip.SetData(new Il2CppStructArray<float>(samples), 0);
        clip.hideFlags = HideFlags.DontUnloadUnusedAsset; // see MahjongAssets: scene loads unload unused runtime assets
        return clip;
    }
}
