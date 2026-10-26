"""Convert a binary FBX (7.x) single-mesh model to a small JSON mesh the plugin loads at run time.

Only what the mahjong tile needs: the first Geometry's vertices, polygons, one UV set and normals. Polygons are
fan-triangulated and every polygon corner becomes its own vertex (so per-corner UVs and normals survive).
Coordinates are written as in the file; the plugin normalises size and orientation.

With --tile-face-u-max U the mesh is also re-axised for a mahjong tile (see canonical_tile): NaoMahjong's tile
textures put the face in u < 0.677 and the back/sides in the orange strip to its right.

Usage:
    python -m scripts.fbx_mesh [--tile-face-u-max 0.68] <in.fbx> <out.json>
"""
from __future__ import annotations

import json
import struct
import sys
import zlib
from dataclasses import dataclass, field
from pathlib import Path

MAGIC = b"Kaydara FBX Binary  \x00"


class FbxError(Exception):
    """The file is not a binary FBX this converter understands."""


@dataclass
class Node:
    name: str
    props: list = field(default_factory=list)
    children: list["Node"] = field(default_factory=list)

    def child(self, name: str) -> "Node | None":
        return next((c for c in self.children if c.name == name), None)


_ARRAY_TYPES = {"f": ("f", 4), "d": ("d", 8), "l": ("q", 8), "i": ("i", 4), "b": ("?", 1)}


def _read_prop(data: bytes, pos: int) -> tuple[object, int]:
    code = chr(data[pos])
    pos += 1
    scalars = {"Y": ("<h", 2), "C": ("<?", 1), "I": ("<i", 4), "F": ("<f", 4), "D": ("<d", 8), "L": ("<q", 8)}
    if code in scalars:
        fmt, size = scalars[code]
        return struct.unpack_from(fmt, data, pos)[0], pos + size
    if code in _ARRAY_TYPES:
        length, encoding, comp_len = struct.unpack_from("<III", data, pos)
        pos += 12
        raw = data[pos:pos + comp_len]
        pos += comp_len
        if encoding == 1:
            raw = zlib.decompress(raw)
        elif encoding != 0:
            raise FbxError(f"unknown array encoding {encoding}")
        fmt, size = _ARRAY_TYPES[code]
        if len(raw) != length * size:
            raise FbxError(f"array of {length} '{code}' has {len(raw)} bytes")
        return list(struct.unpack(f"<{length}{fmt}", raw)), pos
    if code in ("S", "R"):
        (length,) = struct.unpack_from("<I", data, pos)
        pos += 4
        raw = data[pos:pos + length]
        return (raw.decode("utf-8", "replace") if code == "S" else raw), pos + length
    raise FbxError(f"unknown property type {code!r} at {pos - 1}")


def _read_node(data: bytes, pos: int, wide: bool) -> tuple[Node | None, int]:
    if wide:
        end, num_props, _plen = struct.unpack_from("<QQQ", data, pos)
        pos += 24
    else:
        end, num_props, _plen = struct.unpack_from("<III", data, pos)
        pos += 12
    name_len = data[pos]
    pos += 1
    if end == 0:
        return None, pos + name_len
    node = Node(data[pos:pos + name_len].decode("ascii", "replace"))
    pos += name_len
    for _ in range(num_props):
        value, pos = _read_prop(data, pos)
        node.props.append(value)
    sentinel = 25 if wide else 13
    while pos < end - sentinel:
        child, pos = _read_node(data, pos, wide)
        if child is None:
            break
        node.children.append(child)
    return node, end


def parse(data: bytes) -> list[Node]:
    if not data.startswith(MAGIC):
        raise FbxError("not a binary FBX file")
    (version,) = struct.unpack_from("<I", data, 23)
    wide = version >= 7500
    pos, roots = 27, []
    while pos < len(data):
        node, pos = _read_node(data, pos, wide)
        if node is None:
            break
        roots.append(node)
    return roots


def _layer(geometry: Node, layer_name: str, data_name: str, index_name: str) -> tuple[list[float], list[int] | None, str, str]:
    layer = geometry.child(layer_name)
    if layer is None:
        raise FbxError(f"geometry has no {layer_name}")
    values = layer.child(data_name).props[0]
    index_node = layer.child(index_name)
    mapping = layer.child("MappingInformationType").props[0]
    reference = layer.child("ReferenceInformationType").props[0]
    return values, (index_node.props[0] if index_node else None), mapping, reference


def _corner_value(values: list[float], indices: list[int] | None, mapping: str, reference: str,
                  corner: int, vertex: int, width: int) -> list[float]:
    if mapping == "ByPolygonVertex":
        key = corner
    elif mapping in ("ByVertice", "ByVertex"):
        key = vertex
    else:
        raise FbxError(f"unsupported mapping {mapping}")
    if reference == "IndexToDirect":
        key = indices[key]
    elif reference != "Direct":
        raise FbxError(f"unsupported reference {reference}")
    return values[key * width:(key + 1) * width]


def convert(data: bytes) -> dict:
    roots = parse(data)
    objects = next((n for n in roots if n.name == "Objects"), None)
    geometry = objects and next((c for c in objects.children if c.name == "Geometry"), None)
    if geometry is None:
        raise FbxError("no Geometry node")
    points = geometry.child("Vertices").props[0]
    polygon_index = geometry.child("PolygonVertexIndex").props[0]
    uv = _layer(geometry, "LayerElementUV", "UV", "UVIndex")
    normals = _layer(geometry, "LayerElementNormal", "Normals", "NormalsIndex")

    positions, out_uvs, out_normals, triangles = [], [], [], []
    polygon: list[int] = []
    for corner, raw in enumerate(polygon_index):
        vertex = raw if raw >= 0 else ~raw
        positions.append(points[vertex * 3:vertex * 3 + 3])
        out_uvs.append(_corner_value(*uv, corner, vertex, 2))
        out_normals.append(_corner_value(*normals, corner, vertex, 3))
        polygon.append(len(positions) - 1)
        if raw < 0:  # last corner of the polygon
            for i in range(1, len(polygon) - 1):
                triangles.extend((polygon[0], polygon[i], polygon[i + 1]))
            polygon = []
    if polygon:
        raise FbxError("polygon list does not end with a closing (negative) index")
    return {"positions": positions, "normals": out_normals, "uvs": out_uvs, "triangles": triangles}


def _sub(a: list[float], b: list[float]) -> list[float]:
    return [a[0] - b[0], a[1] - b[1], a[2] - b[2]]


def _cross(a: list[float], b: list[float]) -> list[float]:
    return [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]]


def _dot(a: list[float], b: list[float]) -> float:
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]


def canonical_tile(mesh: dict, face_u_max: float) -> dict:
    """Re-axis a box-shaped tile so the face (triangles with every u < face_u_max) points at +Z, with v along +Y and
    u along -X: Unity is left-handed, so a viewer on +Z looking at the face has -X on their right and sees the image
    unmirrored. Positions are scaled into [-0.5, 0.5]. Triangles are wound so cross(b - a, c - a) points along the
    vertex normal, which is front-facing in Unity."""
    positions, normals, uvs = mesh["positions"], mesh["normals"], mesh["uvs"]
    # Drop zero-area slivers (NaoMahjong's tile.fbx has eight, all with UV 0,0) so they can't skew the face search.
    kept: list[int] = []
    for t in range(0, len(mesh["triangles"]), 3):
        a, b, c = mesh["triangles"][t:t + 3]
        area = _cross(_sub(positions[b], positions[a]), _sub(positions[c], positions[a]))
        if _dot(area, area) > 1e-12:
            kept.extend((a, b, c))
    face = sorted({i for t in range(0, len(kept), 3)
                   if all(uvs[i][0] < face_u_max for i in kept[t:t + 3]) for i in kept[t:t + 3]})
    if not face:
        raise FbxError("no face triangles found below the given u")
    # The face normal is the axis the face corners share; it becomes +Z.
    face_axis = max(range(3), key=lambda k: abs(sum(normals[i][k] for i in face)))
    face_sign = 1.0 if sum(normals[i][face_axis] for i in face) > 0 else -1.0
    others = [k for k in range(3) if k != face_axis]

    def best_axis(uv_index: int) -> tuple[int, float]:
        # Which in-plane axis moves with this UV coordinate, and in which direction.
        scores = {}
        for k in others:
            cov = sum((positions[i][k]) * (uvs[i][uv_index] - 0.5) for i in face)
            scores[k] = cov
        axis = max(scores, key=lambda k: abs(scores[k]))
        return axis, (1.0 if scores[axis] > 0 else -1.0)

    u_axis, u_sign = best_axis(0)
    v_axis, v_sign = best_axis(1)
    if u_axis == v_axis:
        raise FbxError("could not separate the face's u and v axes")
    axes = ((u_axis, -u_sign), (v_axis, v_sign), (face_axis, face_sign))

    def remap(vec: list[float]) -> list[float]:
        return [vec[axis] * sign for axis, sign in axes]

    new_positions = [remap(p) for p in positions]
    new_normals = [remap(n) for n in normals]
    lows = [min(p[k] for p in new_positions) for k in range(3)]
    highs = [max(p[k] for p in new_positions) for k in range(3)]
    new_positions = [[(p[k] - lows[k]) / (highs[k] - lows[k]) - 0.5 for k in range(3)] for p in new_positions]

    triangles = kept
    for t in range(0, len(triangles), 3):
        a, b, c = triangles[t:t + 3]
        geometric = _cross(_sub(new_positions[b], new_positions[a]), _sub(new_positions[c], new_positions[a]))
        if _dot(geometric, new_normals[a]) < 0:
            triangles[t + 1], triangles[t + 2] = c, b
    return {"positions": new_positions, "normals": new_normals, "uvs": uvs, "triangles": triangles}


def main(argv: list[str] | None = None) -> int:
    args = sys.argv[1:] if argv is None else argv
    tile_u = None
    if len(args) == 4 and args[0] == "--tile-face-u-max":
        tile_u, args = float(args[1]), args[2:]
    if len(args) != 2:
        print(__doc__, file=sys.stderr)
        return 2
    try:
        mesh = convert(Path(args[0]).read_bytes())
        if tile_u is not None:
            mesh = canonical_tile(mesh, tile_u)
    except (FbxError, OSError, zlib.error, struct.error) as exc:
        print(f"FBX MESH ERROR: {exc}", file=sys.stderr)
        return 1
    Path(args[1]).write_text(json.dumps(mesh, separators=(",", ":")), encoding="utf-8")
    print(f"FBX MESH OK: {len(mesh['positions'])} vertices, {len(mesh['triangles']) // 3} triangles -> {args[1]}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
