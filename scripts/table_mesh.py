"""Generate the 3D mahjong table top from NaoMahjong's flat table painting (see prepare_assets.split_table_texture).

The painting shows an automatic table from above: a dark wood frame, a rosewood rail, a dark lip, the felt and a
square centre console. This builds those as geometry, in the same JSON mesh format as tile_mesh.json:

  table_frame_mesh.json    outer frame band, its outer apron and the dark lip down to the felt  (table_frame.png)
  table_rail_mesh.json     the raised rosewood rail with a bevelled top and rounded inner edge   (table_rail.png)
  table_felt_mesh.json     the recessed felt bed                                                 (table_felt.png)
  table_console_mesh.json  the centre console, a bevelled box of unit width                      (table_console.png)

Coordinates: everything is a fraction of the table-top side, centred on the table, Y up from the table's own top;
the plugin scales uniformly by the real side (so normals stay correct). Heights below are given in metres for a
NOMINAL_SIDE table and converted. The console is 1 wide; the plugin scales it uniformly to the layout's centre
piece. Unity is left-handed: triangles are wound so cross(b - a, c - a) points along the
face normal (front-facing), as in fbx_mesh.canonical_tile.

Profile distances (from the outer edge, as a fraction of the side) are the painting's measured pixel bands / 1024.
"""
from __future__ import annotations

import json
import math
from pathlib import Path

NOMINAL_SIDE = 0.86        # the chess table top's width in its local units (measured in-game, 2026-10-25)

# (distance from the outer edge as a fraction of the side, height in metres)
FRAME_PROFILE = [(0.000, 0.000), (0.000, 0.010), (0.004, 0.012), (0.071, 0.012)]
RAIL_PROFILE = [(0.071, 0.012), (0.071, 0.019), (0.075, 0.022), (0.100, 0.022), (0.106, 0.020), (0.110, 0.016),
                (0.112, 0.011)]
LIP_PROFILE = [(0.112, 0.011), (0.126, 0.007), (0.130, 0.004)]
FELT_INSET = 0.130          # matches TABLE_FELT_BOX (133 / 1024) in prepare_assets
FELT_HEIGHT = 0.004
# OpenRiichi's centre piece is 5.798 tile widths wide and 0.242 high (TableLayout.CenterPieceSize); riichi sticks rest
# on it at 0.174, so the console keeps that height-to-width ratio.
NOMINAL_CONSOLE = 0.16      # metres: about OpenRiichi's centre piece with real-size tiles
CONSOLE_HEIGHT = NOMINAL_CONSOLE * 0.242 / 5.798   # metres above the felt
CONSOLE_BEVEL = 0.05        # fraction of the console's width
CONSOLE_EDGE_V = 0.03       # console sides sample this outer strip of the console texture


def _h(metres: float) -> float:
    return metres / NOMINAL_SIDE


def _sub(a, b):
    return [a[0] - b[0], a[1] - b[1], a[2] - b[2]]


def _cross(a, b):
    return [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]]


def _normalise(v):
    length = math.sqrt(v[0] ** 2 + v[1] ** 2 + v[2] ** 2)
    return [v[0] / length, v[1] / length, v[2] / length]


def _dot(a, b):
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]


class MeshBuilder:
    def __init__(self) -> None:
        self.positions: list[list[float]] = []
        self.normals: list[list[float]] = []
        self.uvs: list[list[float]] = []
        self.triangles: list[int] = []

    def quad(self, corners, uvs, outward) -> None:
        """Add a flat quad (corners in order around it) facing `outward` (a direction it must agree with)."""
        normal = _normalise(_cross(_sub(corners[1], corners[0]), _sub(corners[2], corners[0])))
        if _dot(normal, outward) < 0:
            corners = [corners[0], corners[3], corners[2], corners[1]]
            uvs = [uvs[0], uvs[3], uvs[2], uvs[1]]
            normal = [-n for n in normal]
        base = len(self.positions)
        for corner, uv in zip(corners, uvs):
            self.positions.append([round(c, 6) for c in corner])
            self.normals.append([round(n, 6) for n in normal])
            self.uvs.append([round(t, 6) for t in uv])
        self.triangles += [base, base + 1, base + 2, base, base + 2, base + 3]

    def to_json(self) -> dict:
        return {"positions": self.positions, "normals": self.normals, "uvs": self.uvs, "triangles": self.triangles}


# The four sides of the square, as (axis along the side, fixed-coordinate sign): side k's outer edge is at
# x = +/-half (along z) or z = +/-half (along x).
SIDES = [("x", -1), ("z", +1), ("x", +1), ("z", -1)]


def _ring_point(side, half, t, height):
    """A point on the square ring of half-size `half`, at parameter t in [-1, 1] along side `side`."""
    along, sign = side
    if along == "x":       # the side lies along X at z = sign * half
        return [t * half, height, sign * half]
    return [sign * half, height, t * half]


def sweep(builder: MeshBuilder, profile, v_range) -> None:
    """Sweep a (distance, height) profile around the square with mitred corners. v runs across the profile
    (v_range over the whole profile, by arc length), u along each side by absolute position."""
    lengths = [math.dist(profile[i], profile[i + 1]) for i in range(len(profile) - 1)]
    total = sum(lengths) or 1.0
    v_at = [v_range[0]]
    for length in lengths:
        v_at.append(v_at[-1] + (v_range[1] - v_range[0]) * length / total)

    for side in SIDES:
        along, sign = side
        outward_flat = [0.0, 0.0, float(sign)] if along == "x" else [float(sign), 0.0, 0.0]
        for i in range(len(profile) - 1):
            (d0, h0), (d1, h1) = profile[i], profile[i + 1]
            if math.isclose(d0, d1) and math.isclose(h0, h1):
                continue
            half0, half1 = 0.5 - d0, 0.5 - d1
            corners = [_ring_point(side, half0, -1, _h(h0)), _ring_point(side, half0, 1, _h(h0)),
                       _ring_point(side, half1, 1, _h(h1)), _ring_point(side, half1, -1, _h(h1))]

            def u_of(p):
                return (p[0] if along == "x" else p[2]) + 0.5

            uvs = [[u_of(corners[0]), v_at[i]], [u_of(corners[1]), v_at[i]],
                   [u_of(corners[2]), v_at[i + 1]], [u_of(corners[3]), v_at[i + 1]]]
            # The profile tangent (dd inward, dh up) turned a quarter turn toward the air: flat parts face up, a
            # wall rising as it goes inward faces outward, a slope falling inward faces the centre and up.
            dd, dh = d1 - d0, h1 - h0
            outward = [outward_flat[0] * dh, dd, outward_flat[2] * dh]
            builder.quad(corners, uvs, outward)


def frame_mesh() -> dict:
    builder = MeshBuilder()
    sweep(builder, FRAME_PROFILE, (0.0, 1.0))
    sweep(builder, LIP_PROFILE, (0.0, 1.0))
    return builder.to_json()


def rail_mesh() -> dict:
    builder = MeshBuilder()
    sweep(builder, RAIL_PROFILE, (0.0, 1.0))
    return builder.to_json()


def felt_mesh() -> dict:
    builder = MeshBuilder()
    half = 0.5 - FELT_INSET
    y = _h(FELT_HEIGHT)
    corners = [[-half, y, -half], [half, y, -half], [half, y, half], [-half, y, half]]
    # Texture rows run from the far side (image top) toward the near side, as the painting is seen from seat 0.
    uvs = [[0, 0], [1, 0], [1, 1], [0, 1]]
    builder.quad(corners, uvs, [0, 1, 0])
    return builder.to_json()


def console_mesh() -> dict:
    """A unit-wide bevelled box standing on y = 0 (the plugin puts it on the felt and scales it uniformly to the
    layout's centre piece; its height is CONSOLE_HEIGHT for a NOMINAL_CONSOLE-wide console)."""
    builder = MeshBuilder()
    top, bottom = CONSOLE_HEIGHT / NOMINAL_CONSOLE, 0.0
    bevel_drop = top * 0.35
    inner = 0.5 - CONSOLE_BEVEL
    # Top face: the painted console.
    corners = [[-inner, top, -inner], [inner, top, -inner], [inner, top, inner], [-inner, top, inner]]
    margin = CONSOLE_BEVEL
    builder.quad(corners, [[margin, margin], [1 - margin, margin], [1 - margin, 1 - margin], [margin, 1 - margin]], [0, 1, 0])
    # Bevel ring and vertical sides, swept like the shell (distance measured from the console's own edge).
    profile = [(CONSOLE_BEVEL, top), (0.0, top - bevel_drop), (0.0, bottom)]
    for side in SIDES:
        along, sign = side
        outward_flat = [0.0, 0.0, float(sign)] if along == "x" else [float(sign), 0.0, 0.0]
        for i in range(len(profile) - 1):
            (d0, h0), (d1, h1) = profile[i], profile[i + 1]
            half0, half1 = 0.5 - d0, 0.5 - d1
            pts = [_ring_point(side, half0, -1, h0), _ring_point(side, half0, 1, h0),
                   _ring_point(side, half1, 1, h1), _ring_point(side, half1, -1, h1)]
            uvs = [[(p[0] if along == "x" else p[2]) + 0.5, CONSOLE_EDGE_V * (0.0 if k < 2 else 1.0)] for k, p in enumerate(pts)]
            outward = outward_flat[:] if i == 1 else [outward_flat[0], 1.0, outward_flat[2]]
            builder.quad(pts, uvs, outward)
    return builder.to_json()


MESHES = {
    "table_frame_mesh.json": frame_mesh,
    "table_rail_mesh.json": rail_mesh,
    "table_felt_mesh.json": felt_mesh,
    "table_console_mesh.json": console_mesh,
}


def write_all(out: Path) -> list[str]:
    written = []
    for name, build in MESHES.items():
        (out / name).write_text(json.dumps(build(), separators=(",", ":")), encoding="utf-8")
        written.append(name)
    return written
