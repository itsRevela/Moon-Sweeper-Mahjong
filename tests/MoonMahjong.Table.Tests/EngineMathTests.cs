using System.Numerics;
using MoonMahjong.Table.Layout;
using static MoonMahjong.Table.Tests.TableTestHelpers;

namespace MoonMahjong.Table.Tests;

// The Vala engine's conventions, pinned down against System.Numerics (Hamilton product, q v q* rotation).
public sealed class EngineMathTests
{
    private static readonly Quaternion A = Quaternion.Normalize(new Quaternion(0.3f, -0.5f, 0.2f, 0.8f));
    private static readonly Quaternion B = Quaternion.Normalize(new Quaternion(-0.6f, 0.1f, 0.7f, 0.4f));

    [Fact]
    public void Mul_IsTheHamiltonProduct()
    {
        var expected = A * B;
        var actual = QuatMath.Mul(A, B);
        Assert.True(Math.Abs(Quaternion.Dot(expected, actual) - 1) < 1e-5f, $"{expected} vs {actual}");
    }

    [Fact]
    public void Rotate_MatchesQuaternionTransform()
    {
        var v = new Vector3(0.3f, -1.2f, 2.5f);
        AssertVector(Vector3.Transform(v, A), QuatMath.Rotate(A, v));
    }

    [Fact]
    public void FromEuler_YawHalfIsAQuarterTurnAboutY()
    {
        // Right-handed: +Z turns to +X.
        AssertVector(Vector3.UnitX, QuatMath.Rotate(QuatMath.FromEuler(0.5f, 0, 0), Vector3.UnitZ));
    }

    [Fact]
    public void FromEuler_PitchHalfIsAQuarterTurnAboutX()
    {
        // +Y turns to +Z: a flat tile stood up faces +Z.
        AssertVector(Vector3.UnitZ, QuatMath.Rotate(QuatMath.FromEuler(0, 0.5f, 0), Vector3.UnitY));
    }

    [Fact]
    public void FromEuler_RollHalfIsAQuarterTurnAboutZ()
    {
        AssertVector(Vector3.UnitY, QuatMath.Rotate(QuatMath.FromEuler(0, 0, 0.5f), Vector3.UnitX));
    }

    [Fact]
    public void FromEuler_AppliesPitchThenYawThenRoll()
    {
        float yaw = 0.3f, pitch = 0.2f, roll = 0.1f;
        var expected = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, roll * MathF.PI)
                       * Quaternion.CreateFromAxisAngle(Vector3.UnitY, yaw * MathF.PI)
                       * Quaternion.CreateFromAxisAngle(Vector3.UnitX, pitch * MathF.PI);
        Assert.True(AngleBetween(expected, QuatMath.FromEuler(yaw, pitch, roll)) < 1e-4f);
    }

    [Fact]
    public void Inverse_UndoesTheRotation()
    {
        var q = QuatMath.Mul(QuatMath.Inverse(A), A);
        Assert.True(AngleBetween(QuatMath.Identity, q) < 1e-4f);
    }

    [Fact]
    public void Slerp_HitsBothEnds()
    {
        Assert.True(AngleBetween(A, QuatMath.Slerp(A, B, 0)) < 1e-4f);
        Assert.True(AngleBetween(B, QuatMath.Slerp(A, B, 1)) < 1e-4f);
    }

    [Fact]
    public void Slerp_TakesTheShortWayWhenTheTargetSignIsFlipped()
    {
        var mid = QuatMath.Slerp(A, Quaternion.Negate(B), 0.5f);
        Assert.True(Math.Abs(AngleBetween(A, mid) - AngleBetween(A, B) / 2) < 1e-4f);
    }

    public static TheoryData<string> CurveNames => new() { "linear", "s", "approach", "depart" };

    private static Curve CurveByName(string name) => name switch
    {
        "linear" => LinearCurve.Instance,
        "s" => new SCurve(0.5f),
        "approach" => SmoothApproachCurve.Instance,
        _ => SmoothDepartCurve.Instance,
    };

    [Theory]
    [MemberData(nameof(CurveNames))]
    public void Curves_RunFromZeroToOneAndStayStrictlyBetween(string name)
    {
        var curve = CurveByName(name);
        Assert.Equal(0f, curve.Map(0), 5);
        Assert.Equal(1f, curve.Map(1), 5);
        for (float x = 0.05f; x < 1; x += 0.05f)
            Assert.InRange(curve.Map(x), 1e-6f, 1 - 1e-6f);
    }

    [Fact]
    public void SmoothApproach_IsMostlyThereHalfway()
    {
        // (1 - sqrt(0.5)) * 0.5 + 0.5^0.01 * sqrt(0.5)
        Assert.Equal(0.84866f, SmoothApproachCurve.Instance.Map(0.5f), 4);
    }
}
