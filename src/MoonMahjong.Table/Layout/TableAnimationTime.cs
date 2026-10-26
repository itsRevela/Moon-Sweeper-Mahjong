// Ported from Practice-Riichi-Mahjong Engine/Properties/Animation.vala, class AnimationTime (OpenRiichi, GPLv3).
using System;

namespace MoonMahjong.Table.Layout;

/// <summary>
/// One animation's timing, in seconds: <see cref="Pre"/> (a delay before anything moves), <see cref="Time"/> (the
/// movement) and <see cref="Post"/> (a hold afterwards, which delays whatever is queued next). OpenRiichi's
/// <c>AnimationTime</c>; renamed so it cannot clash with a server-side port of the Vala type.
/// </summary>
public readonly record struct TableAnimationTime
{
    public TableAnimationTime(float pre, float time, float post)
    {
        Pre = Check(pre, nameof(pre));
        Time = Check(time, nameof(time));
        Post = Check(post, nameof(post));
    }

    /// <summary><c>AnimationTime.zero()</c>: instant, and holds nothing up.</summary>
    public static TableAnimationTime Zero => default;

    public float Pre { get; }
    public float Time { get; }
    public float Post { get; }

    public float Total => Pre + Time + Post;

    /// <summary><c>AnimationTime.preset(time)</c>: no delay, no hold.</summary>
    public static TableAnimationTime Preset(float time) => new(0, time, 0);

    private static float Check(float value, string name)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value < 0)
            throw new ArgumentOutOfRangeException(name, value, "Animation times must be finite and non-negative.");
        return value;
    }
}
