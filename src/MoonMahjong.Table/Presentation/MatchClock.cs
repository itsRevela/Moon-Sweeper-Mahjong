using System;

namespace MoonMahjong.Table.Presentation;

/// <summary>
/// The game's own time, in seconds: it advances only while it runs, so pausing it (the player left the table) freezes
/// the server, the bots and the table's animations together, and resuming carries on from the same moment.
/// </summary>
public sealed class MatchClock
{
    public MatchClock(float start)
    {
        if (!float.IsFinite(start))
            throw new ArgumentOutOfRangeException(nameof(start), start, "The clock must start at a finite time.");
        Now = start;
    }

    public float Now { get; private set; }
    public bool Paused { get; private set; }

    public void Pause() => Paused = true;

    public void Resume() => Paused = false;

    // Advance by a frame's duration; a negative or non-finite one (a hitch or a bad clock) is ignored.
    public void Tick(float delta)
    {
        if (!Paused && float.IsFinite(delta) && delta > 0)
            Now += delta;
    }
}
