import math

import pytest

from scripts import tile_shape

W, L, T = tile_shape.PROPORTIONS


def _box() -> dict:
    """fbx_mesh.canonical_tile's output for NaoMahjong's tile: a unit box, face on +Z (measured UVs)."""
    side = {"near": 0.6774, "far": 0.9839, "lo": 0.0169, "hi": 0.6949}
    quads = [
        # normal, four corners as (position, uv)
        ((0, 0, 1), [((-0.5, -0.5, 0.5), (0.6774, 0.0)), ((-0.5, 0.5, 0.5), (0.6774, 1.0)),
                     ((0.5, 0.5, 0.5), (0.0, 1.0)), ((0.5, -0.5, 0.5), (0.0, 0.0))]),
        ((0, 0, -1), [((0.5, -0.5, -0.5), (0.9839, 0.9322)), ((0.5, 0.5, -0.5), (0.6935, 0.9322)),
                      ((-0.5, 0.5, -0.5), (0.6935, 0.7288)), ((-0.5, -0.5, -0.5), (0.9839, 0.7288))]),
        ((-1, 0, 0), [((-0.5, 0.5, 0.5), (side["near"], side["lo"])), ((-0.5, -0.5, 0.5), (side["near"], side["hi"])),
                      ((-0.5, -0.5, -0.5), (side["far"], side["hi"])), ((-0.5, 0.5, -0.5), (side["far"], side["lo"]))]),
        ((1, 0, 0), [((0.5, -0.5, 0.5), (side["near"], side["lo"])), ((0.5, 0.5, 0.5), (side["near"], side["hi"])),
                     ((0.5, 0.5, -0.5), (side["far"], side["hi"])), ((0.5, -0.5, -0.5), (side["far"], side["lo"]))]),
        ((0, -1, 0), [((-0.5, -0.5, 0.5), (side["near"], side["lo"])), ((0.5, -0.5, 0.5), (side["near"], side["hi"])),
                      ((0.5, -0.5, -0.5), (side["far"], side["hi"])), ((-0.5, -0.5, -0.5), (side["far"], side["lo"]))]),
        ((0, 1, 0), [((-0.5, 0.5, 0.5), (side["near"], side["hi"])), ((-0.5, 0.5, -0.5), (side["far"], side["hi"])),
                     ((0.5, 0.5, -0.5), (side["far"], side["lo"])), ((0.5, 0.5, 0.5), (side["near"], side["lo"]))]),
    ]
    mesh = {"positions": [], "normals": [], "uvs": [], "triangles": []}
    for normal, corners in quads:
        base = len(mesh["positions"])
        for position, uv in corners:
            mesh["positions"].append(list(position))
            mesh["normals"].append(list(normal))
            mesh["uvs"].append(list(uv))
        mesh["triangles"] += [base, base + 1, base + 2, base, base + 2, base + 3]
    return mesh


@pytest.fixture(scope="module")
def tile() -> dict:
    return tile_shape.rounded_tile(_box())


def _real(position):
    return position[0] * W, position[1] * L, position[2] * T


def test_tile_fits_the_gap_trimmed_box(tile):
    xs, ys, zs = zip(*(_real(p) for p in tile["positions"]))
    gap = tile_shape.GAP * W
    assert max(map(abs, xs)) == pytest.approx((W - gap) / 2)
    assert max(map(abs, ys)) == pytest.approx((L - gap) / 2)


def test_thickness_is_not_trimmed_so_wall_stacks_still_touch(tile):
    assert max(abs(p[2]) for p in tile["positions"]) == pytest.approx(0.5)


def test_edges_are_rounded_with_smooth_normals(tile):
    # The front-right edge's middle: a normal halfway between the face (+Z) and the side (+X).
    diagonal = [n for n in tile["normals"] if n[0] > 0.6 and n[2] > 0.6 and abs(n[1]) < 1e-6]
    assert any(n[0] == pytest.approx(math.sqrt(0.5), abs=1e-6) for n in diagonal)


def test_rounded_corners_are_circular_in_real_proportions(tile):
    # Every vertex on the front-right edge's arc is one radius from the arc's axis.
    gap = tile_shape.GAP * W
    cx, cz = (W - gap) / 2 - tile_shape.RADIUS, T / 2 - tile_shape.RADIUS
    for p, n in zip(tile["positions"], tile["normals"]):
        if n[0] > 0.01 and n[2] > 0.01 and abs(n[1]) < 1e-6:
            x, _, z = _real(p)
            assert math.hypot(x - cx, z - cz) == pytest.approx(tile_shape.RADIUS, abs=1e-6)


def test_front_keeps_the_whole_face_image(tile):
    front = [uv for uv, n in zip(tile["uvs"], tile["normals"]) if n[2] > 0.5 and abs(n[0]) < 0.75 and abs(n[1]) < 0.75]
    us, vs = zip(*front)
    assert (min(us), max(us), min(vs), max(vs)) == pytest.approx((0.0, 0.6774, 0.0, 1.0), abs=1e-4)


def test_back_keeps_its_texture_block(tile):
    back = [uv for uv, n in zip(tile["uvs"], tile["normals"]) if n[2] < -0.99]
    us, vs = zip(*back)
    assert min(us) >= 0.6935 - 1e-4 and max(us) <= 0.9839 + 1e-4
    assert min(vs) >= 0.7288 - 1e-4 and max(vs) <= 0.9322 + 1e-4


def test_triangles_face_outward(tile):
    p = tile["positions"]
    t = tile["triangles"]
    for i in range(0, len(t), 3):
        a, b, c = (p[k] for k in t[i:i + 3])
        ab = [b[j] - a[j] for j in range(3)]
        ac = [c[j] - a[j] for j in range(3)]
        cross = (ab[1] * ac[2] - ab[2] * ac[1], ab[2] * ac[0] - ab[0] * ac[2], ab[0] * ac[1] - ab[1] * ac[0])
        centroid = [(a[j] + b[j] + c[j]) / 3 for j in range(3)]
        area = math.sqrt(sum(x * x for x in cross))
        if area > 1e-12:
            assert sum(cross[j] * centroid[j] for j in range(3)) > 0


def test_mesh_arrays_are_consistent(tile):
    n = len(tile["positions"])
    assert len(tile["normals"]) == n and len(tile["uvs"]) == n
    assert len(tile["triangles"]) % 3 == 0 and max(tile["triangles"]) < n


def test_rejects_a_radius_that_does_not_fit_the_thickness():
    with pytest.raises(ValueError, match="radius"):
        tile_shape.rounded_tile(_box(), radius=T / 2)


def test_rejects_a_mesh_missing_a_face():
    box = _box()
    for key in ("positions", "normals", "uvs"):
        box[key] = box[key][4:]
    box["triangles"] = [i - 4 for i in box["triangles"][6:]]
    with pytest.raises(ValueError, match="face"):
        tile_shape.rounded_tile(box)

