// Ported from Practice-Riichi-Mahjong Engine/Properties/Quat.vala, Vec3.vala and Helper/Calculations.vala
// (OpenRiichi, GPLv3). Values are stored in System.Numerics types; every operation is the Vala formula, verbatim.
using System;
using System.Numerics;

namespace MoonMahjong.Table.Layout;

// OpenRiichi's quaternion maths. Their Quat is (w, x, y, z); System.Numerics.Quaternion is (X, Y, Z, W).
// Conventions (verified in EngineMathTests): Mul is the Hamilton product (a.Mul(b) applies b first, then a), vectors
// are rotated as column vectors by the standard matrix (q v q*), and the world is right-handed with Y up.
internal static class QuatMath
{
    private const float SlerpThreshold = 0.9995f;

    // Quat() in Vala: w = 1.
    public static readonly Quaternion Identity = new(0, 0, 0, 1);

    // Quat.from_euler(yaw, pitch, roll). Angles are in units of pi (0.5 = a quarter turn), yaw is about +Y, pitch
    // about +X and roll about +Z (right-handed). The formula equals Rz(roll) * Ry(yaw) * Rx(pitch).
    public static Quaternion FromEuler(float yaw, float pitch, float roll)
    {
        // "Multiply with pi to simplify use": half-angles, hence pi / 2.
        float tau = (float)Math.PI / 2;
        yaw *= tau;
        pitch *= tau;
        roll *= tau;

        float t0 = (float)Math.Cos(roll);
        float t1 = (float)Math.Sin(roll);
        float t2 = (float)Math.Cos(pitch);
        float t3 = (float)Math.Sin(pitch);
        float t4 = (float)Math.Cos(yaw);
        float t5 = (float)Math.Sin(yaw);

        float w = t0 * t2 * t4 + t1 * t3 * t5;
        float x = t0 * t3 * t4 - t1 * t2 * t5;
        float y = t0 * t2 * t5 + t1 * t3 * t4;
        float z = t1 * t2 * t4 - t0 * t3 * t5;

        return new Quaternion(x, y, z, w);
    }

    // Quat.mul: an eight-multiplication form of the Hamilton product a * b.
    public static Quaternion Mul(Quaternion a, Quaternion b)
    {
        float w = a.W, x = a.X, y = a.Y, z = a.Z;

        float pA = (w + x) * (b.W + b.X);
        float pB = (z - y) * (b.Y - b.Z);
        float pC = (w - x) * (b.Y + b.Z);
        float pD = (y + z) * (b.W - b.X);
        float pE = (x + z) * (b.X + b.Y);
        float pF = (x - z) * (b.X - b.Y);
        float pG = (w + y) * (b.W - b.Z);
        float pH = (w - y) * (b.W + b.Z);

        float rw = pB + (-pE - pF + pG + pH) / 2;
        float rx = pA - (pE + pF + pG + pH) / 2;
        float ry = pC + (pE - pF + pG - pH) / 2;
        float rz = pD + (pE - pF - pG + pH) / 2;

        return new Quaternion(rx, ry, rz, rw);
    }

    public static float Dot(Quaternion a, Quaternion b) => a.W * b.W + a.X * b.X + a.Y * b.Y + a.Z * b.Z;

    public static Quaternion Neg(Quaternion q) => new(-q.X, -q.Y, -q.Z, -q.W);

    public static Quaternion Conjugate(Quaternion q) => new(-q.X, -q.Y, -q.Z, q.W);

    // Quat.inv: conjugate divided by the squared length.
    public static Quaternion Inverse(Quaternion q)
    {
        float l = q.W * q.W + q.X * q.X + q.Y * q.Y + q.Z * q.Z;
        var c = Conjugate(q);
        return new Quaternion(c.X / l, c.Y / l, c.Z / l, c.W / l);
    }

    // Quat.slerp, including its sign flip and its linear fallback for nearly equal rotations (the result is not
    // renormalised there, as in the Vala).
    public static Quaternion Slerp(Quaternion from, Quaternion to, float t)
    {
        float cosom = Dot(from, to);

        Quaternion nto = to;
        if (cosom < 0.0)
        {
            cosom = -cosom;
            nto = Neg(to);
        }

        float scale0, scale1;
        if (cosom < SlerpThreshold)
        {
            float omega = (float)Math.Acos(cosom);
            float sinom = (float)Math.Sin(omega);
            scale0 = (float)Math.Sin((1.0 - t) * omega) / sinom;
            scale1 = (float)Math.Sin(t * omega) / sinom;
        }
        else
        {
            scale0 = 1 - t;
            scale1 = t;
        }

        return new Quaternion(
            scale0 * from.X + scale1 * nto.X,
            scale0 * from.Y + scale1 * nto.Y,
            scale0 * from.Z + scale1 * nto.Z,
            scale0 * from.W + scale1 * nto.W);
    }

    // Rotates v by the matrix of Calculations.rotation_matrix_quat (column-vector convention: v' = R v).
    public static Vector3 Rotate(Quaternion q, Vector3 v)
    {
        float x = q.X, y = q.Y, z = q.Z, w = q.W;
        float x2 = x + x, y2 = y + y, z2 = z + z;
        float xx = x * x2, xy = x * y2, xz = x * z2;
        float yy = y * y2, yz = y * z2, zz = z * z2;
        float wx = w * x2, wy = w * y2, wz = w * z2;

        return new Vector3(
            (1 - (yy + zz)) * v.X + (xy - wz) * v.Y + (xz + wy) * v.Z,
            (xy + wz) * v.X + (1 - (xx + zz)) * v.Y + (yz - wx) * v.Z,
            (xz - wy) * v.X + (yz + wx) * v.Y + (1 - (xx + yy)) * v.Z);
    }
}

internal static class Vec3Math
{
    // Vec3.lerp, per component as in the Vala.
    public static Vector3 Lerp(Vector3 start, Vector3 end, float lerp) => new(
        start.X + (end.X - start.X) * lerp,
        start.Y + (end.Y - start.Y) * lerp,
        start.Z + (end.Z - start.Z) * lerp);
}
