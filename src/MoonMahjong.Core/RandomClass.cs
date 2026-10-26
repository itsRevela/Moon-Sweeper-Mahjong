// Ported from Practice-Riichi-Mahjong Engine/Helper/RandomClass.vala (GPLv3).
namespace MoonMahjong.Core;

public sealed class RandomClass
{
    private readonly System.Random rnd;

    public RandomClass()
    {
        rnd = new System.Random();
    }

    public RandomClass(int seed)
    {
        rnd = new System.Random(seed);
    }

    // Like GLib's Rand.int_range: min inclusive, max exclusive.
    public int IntRange(int min, int max) => rnd.Next(min, max);

    public float NextFloat() => (float)rnd.NextDouble();

    public float FloatRange(float min, float max) => min + NextFloat() * (max - min);

    public bool NextBool() => NextFloat() > 0.5f;
}
