// Ported from Practice-Riichi-Mahjong Engine/Properties/Animation.vala, class AnimationTime (GPLv3).
// The Serializable base is dropped; wire serialisation is designed separately.
namespace MoonMahjong.Core;

// The length of an animation in seconds: a delay before it, its own duration, and a delay after it.
public sealed class AnimationTime
{
    public AnimationTime(float pre, float time, float post)
    {
        Pre = pre;
        Time = time;
        Post = post;
    }

    // Vala named constructor AnimationTime.preset
    public static AnimationTime Preset(float time) => new(0, time, 0);

    // Vala named constructor AnimationTime.zero
    public static AnimationTime Zero() => new(0, 0, 0);

    public float Total()
    {
        return Pre + Time + Post;
    }

    public float Pre { get; }
    public float Time { get; }
    public float Post { get; }
}
