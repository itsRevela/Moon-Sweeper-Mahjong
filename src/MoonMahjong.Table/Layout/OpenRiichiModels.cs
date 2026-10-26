// Bounding boxes of Practice-Riichi-Mahjong bin/Data/Models/*.obj (OpenRiichi, GPLv3), as the engine measures them:
// ObjParser computes size = max - min (in float) over the vertices its triangles use, and does not recentre models.
using System.Numerics;

namespace MoonMahjong.Table.Layout;

internal static class OpenRiichiModels
{
    // RenderTile.obb = (front.size.x, front.size.y + back.size.y, front.size.z). GameRenderView measures a fresh
    // RenderTile, whose model_quality defaults to LOW, so the layout's tile_size comes from tile_low.obj:
    // "Top_Cube.001" (front) x [-0.175507, 0.175507], y [-0.061479, 0.129458], z [-0.226458, 0.226458];
    // "Bottom_Cube" (back) y [-0.126038, -0.061479].
    // The tile lies flat: width along X, thickness along Y (face on +Y), length along Z (image top towards -Z, since
    // the parser flips v and stb loads images top row first).
    public static readonly Vector3 TileObb = new(
        0.175507f - -0.175507f,
        (0.129458f - -0.061479f) + (-0.061479f - -0.126038f),
        0.226458f - -0.226458f);

    // stick.obj "Cylinder": x [-3.443488, 3.44757], y [-0.037247, 0.108675], z [-0.320301, 0.254574]. Long along X.
    public static readonly Vector3 StickObb = new(
        3.44757f - -3.443488f,
        0.108675f - -0.037247f,
        0.254574f - -0.320301f);

    // table_center.obj "Cube": x and z [-0.999595, 0.999595], y [0, 0.083347]; its origin is the centre of its base.
    public static readonly Vector3 CenterPieceObb = new(
        0.999595f - -0.999595f,
        0.083347f - 0f,
        0.999595f - -0.999595f);
}
