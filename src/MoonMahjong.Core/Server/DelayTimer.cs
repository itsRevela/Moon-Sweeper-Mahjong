// Ported from Practice-Riichi-Mahjong Engine/Helper/Timers.vala, class DelayTimer (GPLv3).
namespace MoonMahjong.Core;

// A one-shot timer on a caller-supplied clock. The countdown starts at the first Active(time) call after SetTime,
// not at SetTime itself.
public sealed class DelayTimer
{
    private bool timeSet;
    private float time;
    private float delay;

    public bool Active(float time)
    {
        if (!IsActive)
            return false;

        if (!timeSet)
        {
            this.time = time + Delay;
            timeSet = true;
        }

        if (time >= this.time)
        {
            IsActive = false;
            return true;
        }

        return false;
    }

    public void SetTime(float delay, bool add = false)
    {
        if (add && IsActive)
        {
            this.delay += delay;
            time += delay;
            return;
        }

        this.delay = delay;
        timeSet = false;
        IsActive = true;
    }

    public float Delay
    {
        get => delay;
        // As in the Vala source, the setter passes the current delay (the property), not the new value.
        set => SetTime(Delay);
    }

    public bool IsActive { get; private set; }
}
