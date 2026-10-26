using System.Numerics;

namespace MoonMahjong.Table.Layout;

// The one conversion from OpenRiichi's table space to the plugin's space (documented on TableLayout and in
// docs/table-layout.md).
//
// OpenRiichi: right-handed, Y up, table centre at the origin, felt at y = 0, seat i turned i quarter turns about +Y
// (seat 0 sits at +Z, seat 1 at +X), each player looking at the centre with +X on their right. Units: their world units
// (one tile is TileSize.X = 0.5265 wide).
//
// Plugin: Unity's left-handed, Y up, table centre at the origin, felt at y = 0, the observer at -Z looking towards +Z
// (so +X is the observer's right), one unit = one tile width.
//
// Position: undo the observer seat's rotation (the observer now sits at +Z), mirror Z (the handedness change, which
// keeps the observer's right on +X), divide by the tile width.
// Rotation: conjugate by the same mirror (a quaternion (w, x, y, z) becomes (w, -x, -y, z)), then, for tiles, append the
// fixed change from the plugin's tile mesh to OpenRiichi's: the plugin mesh's face (+Z) is OpenRiichi's face (+Y), its
// image top (+Y) is OpenRiichi's image top (-Z), and its +X is OpenRiichi's -X. Mirror and mesh change are each a
// reflection; together they are the half turn about (0, 1, 1)/sqrt(2), quaternion (w 0, x 0, y sqrt(1/2), z sqrt(1/2)).
internal sealed class TableCoordinates
{
    private static readonly float HalfSqrt2 = (float)System.Math.Sqrt(0.5);

    // Plugin tile mesh axes -> OpenRiichi tile model axes, combined with the Z mirror (see above).
    public static readonly Quaternion TileMeshAlignment = new(0, HalfSqrt2, HalfSqrt2, 0);

    private readonly Quaternion inverseObserver;
    private readonly float tileWidth;

    public TableCoordinates(Quaternion observerRotation, float tileWidth)
    {
        inverseObserver = QuatMath.Inverse(observerRotation);
        this.tileWidth = tileWidth;
    }

    public Vector3 ToPluginPosition(Vector3 tablePosition)
    {
        var p = QuatMath.Rotate(inverseObserver, tablePosition);
        return new Vector3(p.X, p.Y, -p.Z) / tileWidth;
    }

    // For meshes modelled like OpenRiichi's (the riichi stick, the centre piece): only the frame changes.
    public Quaternion ToPluginObjectRotation(Quaternion tableRotation)
    {
        var q = QuatMath.Mul(inverseObserver, tableRotation);
        return Quaternion.Normalize(MirrorZ(q));
    }

    // For the plugin's tile mesh (face +Z, image top +Y, width along X).
    public Quaternion ToPluginTileRotation(Quaternion tableRotation)
    {
        var q = QuatMath.Mul(inverseObserver, tableRotation);
        return Quaternion.Normalize(QuatMath.Mul(MirrorZ(q), TileMeshAlignment));
    }

    // C R C for the reflection C = diag(1, 1, -1): the rotation axis is a pseudovector, so it maps to -C(axis).
    private static Quaternion MirrorZ(Quaternion q) => new(-q.X, -q.Y, q.Z, q.W);
}
