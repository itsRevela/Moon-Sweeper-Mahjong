"""A rounded tile with a hairline gap, from NaoMahjong's sharp box (requested 2026-10-25: tiles sat perfectly flush).

The edges become quarter-circle arcs with smooth normals, built in the tile's real proportions so the rounding is
circular, and the width and length lose a hairline so neighbours never touch; the thickness keeps its size so the
walls' two layers still rest on each other. Every face keeps its texture mapping: the face image still spans the
whole front, running onto the start of each rounded edge.

Each face is a grid on the box's surface. A grid point q is pulled onto the rounded shape by the usual construction:
inner = q clamped to the box shrunk by the radius, p = inner + radius * normalize(q - inner), whose normal is that
direction. Grid lines near the edges are spaced so the arc's segments subtend equal angles.
"""

from __future__ import annotations

import math

PROPORTIONS = (1.0, 1.2903, 0.7279)  # TableLayout.TileSize: width, length, thickness (in tile widths)
RADIUS = 0.06       # edge rounding, in tile widths (about 1.6 mm on a 26 mm tile)
GAP = 0.03          # trimmed from the width and the length, in tile widths (about 0.8 mm between neighbours)
SEGMENTS = 3        # arc segments on each face's half of a rounded edge (45 degrees)

Vector = tuple[float, float, float]


def _face_maps(box: dict) -> dict[tuple[int, int], tuple]:
    """For each face (axis, sign) of a canonical unit box: the affine map from its in-plane coordinates to UV."""
    by_face: dict[tuple[int, int], dict[int, tuple]] = {}
    triangles = box["triangles"]
    for t in range(0, len(triangles), 3):
        for index in triangles[t:t + 3]:
            normal = box["normals"][index]
            axis = max(range(3), key=lambda a: abs(normal[a]))
            key = (axis, 1 if normal[axis] > 0 else -1)
            by_face.setdefault(key, {})[index] = (box["positions"][index], box["uvs"][index])

    maps = {}
    for axis in range(3):
        for sign in (-1, 1):
            corners = list(by_face.get((axis, sign), {}).values())
            if len(corners) < 3:
                raise ValueError(f"the tile mesh has no face on {'-+'[sign > 0]}{'XYZ'[axis]}")
            maps[(axis, sign)] = _solve_affine(corners, _plane_axes(axis))
    return maps


def _plane_axes(axis: int) -> tuple[int, int]:
    return tuple(a for a in range(3) if a != axis)  # type: ignore[return-value]


def _solve_affine(corners: list[tuple], plane: tuple[int, int]) -> tuple:
    """uv = (a*s + b*t + c) for in-plane coordinates (s, t), from three corners that span the face."""
    b0, b1 = plane
    for i in range(len(corners)):
        for j in range(i + 1, len(corners)):
            for k in range(j + 1, len(corners)):
                (p0, uv0), (p1, uv1), (p2, uv2) = corners[i], corners[j], corners[k]
                ds1, dt1 = p1[b0] - p0[b0], p1[b1] - p0[b1]
                ds2, dt2 = p2[b0] - p0[b0], p2[b1] - p0[b1]
                det = ds1 * dt2 - ds2 * dt1
                if abs(det) < 1e-9:
                    continue
                coefficients = []
                for c in range(2):
                    du1, du2 = uv1[c] - uv0[c], uv2[c] - uv0[c]
                    a = (du1 * dt2 - du2 * dt1) / det
                    b = (ds1 * du2 - ds2 * du1) / det
                    coefficients.append((a, b, uv0[c] - a * p0[b0] - b * p0[b1]))
                return tuple(coefficients)
    raise ValueError("a face of the tile mesh has collinear corners")


def _grid(half: float, radius: float, segments: int) -> list[float]:
    """Coordinates across one face axis: the flat middle, then arc steps at equal angles out to each edge."""
    flat = half - radius
    steps = [flat + radius * math.tan(math.radians(45 * k / segments)) for k in range(1, segments + 1)]
    return [-s for s in reversed(steps)] + [-flat, flat] + steps


def rounded_tile(box: dict, proportions: Vector = PROPORTIONS, radius: float = RADIUS, gap: float = GAP,
                 segments: int = SEGMENTS) -> dict:
    """The rounded tile mesh, in the same unit-box space and format as fbx_mesh.canonical_tile's `box`."""
    maps = _face_maps(box)
    half = ((proportions[0] - gap) / 2, (proportions[1] - gap) / 2, proportions[2] / 2)
    if not 0 < radius < min(half) or segments < 1:
        raise ValueError(f"radius {radius} must be positive and below every half extent {min(half):.3f}")

    mesh: dict = {"positions": [], "normals": [], "uvs": [], "triangles": []}
    for (axis, sign), uv_map in maps.items():
        s_axis, t_axis = _plane_axes(axis)
        s_grid, t_grid = _grid(half[s_axis], radius, segments), _grid(half[t_axis], radius, segments)
        base = len(mesh["positions"])
        for s in s_grid:
            for t in t_grid:
                q = [0.0, 0.0, 0.0]
                q[axis], q[s_axis], q[t_axis] = sign * half[axis], s, t
                inner = [max(-(h - radius), min(h - radius, c)) for c, h in zip(q, half)]
                offset = [c - i for c, i in zip(q, inner)]
                length = math.sqrt(sum(o * o for o in offset))
                normal = [o / length for o in offset]
                point = [i + radius * n for i, n in zip(inner, normal)]
                mesh["positions"].append([point[a] / proportions[a] for a in range(3)])
                mesh["normals"].append(normal)
                # The map was measured on the unit box, so q goes there too (the image spans the whole face).
                unit_s, unit_t = s / (2 * half[s_axis]), t / (2 * half[t_axis])
                mesh["uvs"].append([c[0] * unit_s + c[1] * unit_t + c[2] for c in uv_map])

        columns = len(t_grid)
        for i in range(len(s_grid) - 1):
            for j in range(columns - 1):
                a, b = base + i * columns + j, base + (i + 1) * columns + j
                c, d = base + (i + 1) * columns + j + 1, base + i * columns + j + 1
                mesh["triangles"] += _outward(mesh["positions"], (a, b, c), proportions)
                mesh["triangles"] += _outward(mesh["positions"], (a, c, d), proportions)
    return mesh


def _outward(positions: list, triangle: tuple[int, int, int], proportions: Vector) -> list[int]:
    """Wind a triangle so cross(b - a, c - a) points away from the tile's centre (front-facing in Unity)."""
    a, b, c = ([p[k] * proportions[k] for k in range(3)] for p in (positions[i] for i in triangle))
    ab = [b[k] - a[k] for k in range(3)]
    ac = [c[k] - a[k] for k in range(3)]
    cross = (ab[1] * ac[2] - ab[2] * ac[1], ab[2] * ac[0] - ab[0] * ac[2], ab[0] * ac[1] - ab[1] * ac[0])
    centroid = [(a[k] + b[k] + c[k]) / 3 for k in range(3)]
    if sum(cross[k] * centroid[k] for k in range(3)) < 0:
        return [triangle[0], triangle[2], triangle[1]]
    return list(triangle)
