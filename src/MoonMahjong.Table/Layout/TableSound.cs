namespace MoonMahjong.Table.Layout;

/// <summary>The table's sound effects, named after OpenRiichi's bin/Data/Audio/Sounds files.</summary>
public enum TableSound
{
    Slide,
    Draw,
    Discard,
    Flip,
    Riichi,
    Reveal,
    Chii,
    Pon,
    Kan,
    Ron,
    Tsumo,
}

/// <summary>A sound due at <see cref="Time"/>: when the action that makes it started.</summary>
public sealed record SoundCue(TableSound Sound, float Time);
