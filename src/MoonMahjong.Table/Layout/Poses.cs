using System.Numerics;
using MoonMahjong.Core;

namespace MoonMahjong.Table.Layout;

/// <summary>
/// Where to draw one tile, in table-local plugin space (see <see cref="TableLayout"/>): Unity's left-handed, Y-up axes,
/// table centre at the origin, felt at Y = 0, observer at -Z, one unit = one tile width.
/// </summary>
/// <param name="Position">Centre of the tile, in tile widths. Multiply by the real tile width.</param>
/// <param name="Rotation">
/// Rotation for a tile mesh whose face points along local +Z, whose face image is upright along local +Y and whose width
/// is along local X (image right on -X, as an unmirrored face seen from the front in Unity).
/// </param>
/// <param name="Type">The tile's type, or <see cref="TileType.Blank"/> until <see cref="TableLayout.TileAssignment"/> reveals it.</param>
/// <param name="RedFive">Whether the tile is a red five (aka dora), once revealed.</param>
public readonly record struct TilePose(Vector3 Position, Quaternion Rotation, TileType Type, bool RedFive)
{
    public bool TypeKnown => Type != TileType.Blank;
}

/// <summary>
/// Where to draw a seat's riichi stick, in the same space as <see cref="TilePose"/>. The rotation is for a stick mesh
/// whose length is along local X and whose printed face is local +Y (<see cref="TableLayout.RiichiStickSize"/>).
/// </summary>
public readonly record struct StickPose(Vector3 Position, Quaternion Rotation, bool Visible);

/// <summary>A pose for a non-tile object (the centre piece), in the same space as <see cref="TilePose"/>.</summary>
public readonly record struct ObjectPose(Vector3 Position, Quaternion Rotation);
