// Ported from Practice-Riichi-Mahjong Engine/Properties/Curve.vala (OpenRiichi, GPLv3).
// ExponentCurve is not ported: the table never uses it.
using System;

namespace MoonMahjong.Table.Layout;

// Maps an animation's linear progress (0..1) to the fraction of the path covered.
internal abstract class Curve
{
    public abstract float Map(float x);
}

internal sealed class LinearCurve : Curve
{
    public static readonly LinearCurve Instance = new();

    public override float Map(float x) => x;
}

internal sealed class SCurve : Curve
{
    private readonly float exp;

    public SCurve(float exp)
    {
        this.exp = exp;
    }

    public override float Map(float x)
    {
        if (x <= 0.5f)
            return F(2 * x, exp) * 0.5f;
        return 0.5f * F(2 * (x - 0.5f), -exp) + 0.5f;
    }

    private static float F(float x, float k)
    {
        float t = k * x;
        return (t - x) / (2 * t - k - 1);
    }
}

// Fast start, slow arrival. Every tile move (RenderTile.animate_towards) uses it.
internal sealed class SmoothApproachCurve : Curve
{
    public static readonly SmoothApproachCurve Instance = new();

    private readonly float exp = 0.01f;

    public override float Map(float x)
    {
        float mul = (float)Math.Pow(x, 0.5f);
        return (1 - mul) * x + (float)Math.Pow(x, exp) * mul;
    }
}

// Slow start, fast arrival: SmoothApproachCurve mirrored. Used to open and close hands.
internal sealed class SmoothDepartCurve : Curve
{
    public static readonly SmoothDepartCurve Instance = new();

    public override float Map(float x) => 1 - SmoothApproachCurve.Instance.Map(1 - x);
}
