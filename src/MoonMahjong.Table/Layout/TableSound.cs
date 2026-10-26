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

/// <summary>Which table sounds are spoken.</summary>
public static class TableSounds
{
    /// <summary>
    /// A call said aloud ("Riichi!", "Ron!"), not a tile noise. The plugin plays these on a voice of the table's own,
    /// which the game's shared sound pool can't take over halfway through.
    /// </summary>
    public static bool IsSpoken(this TableSound sound) => sound is TableSound.Riichi or TableSound.Chii
        or TableSound.Pon or TableSound.Kan or TableSound.Ron or TableSound.Tsumo;
}

/// <summary>A sound due at <see cref="Time"/>: when the action that makes it started.</summary>
public sealed record SoundCue(TableSound Sound, float Time);
