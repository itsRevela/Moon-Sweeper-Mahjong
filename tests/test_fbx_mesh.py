import pytest

from scripts import fbx_mesh


def _quad_mesh(face_normal_axis: int, u_axis: int, v_axis: int, degenerate: bool = False) -> dict:
    """One textured quad lying on the +face_normal_axis side of a unit box, plus an optional zero-area sliver."""
    positions, normals, uvs = [], [], []
    for u, v in ((0.0, 0.0), (1.0, 0.0), (1.0, 1.0), (0.0, 1.0)):
        p = [0.0, 0.0, 0.0]
        p[face_normal_axis] = 1.0
        p[u_axis] = -1.0 + 2.0 * u
        p[v_axis] = -1.0 + 2.0 * v
        n = [0.0, 0.0, 0.0]
        n[face_normal_axis] = 1.0
        positions.append(p)
        normals.append(n)
        uvs.append([u * 0.5, v])  # face lives in u < 0.5
    triangles = [0, 1, 2, 0, 2, 3]
    if degenerate:
        positions += [[1.0, 1.0, 1.0]] * 3
        normals += [[0.0, 0.0, 1.0]] * 3
        uvs += [[0.0, 0.0]] * 3
        triangles += [4, 5, 6]
    # A back quad so every axis has extent (positions are normalised per axis).
    for u, v in ((0.0, 0.0), (1.0, 0.0), (1.0, 1.0)):
        p = [0.0, 0.0, 0.0]
        p[face_normal_axis] = -1.0
        p[u_axis] = -1.0 + 2.0 * u
        p[v_axis] = -1.0 + 2.0 * v
        n = [0.0, 0.0, 0.0]
        n[face_normal_axis] = -1.0
        positions.append(p)
        normals.append(n)
        uvs.append([0.9, 0.5])
    base = len(positions) - 3
    triangles += [base, base + 1, base + 2]
    return {"positions": positions, "normals": normals, "uvs": uvs, "triangles": triangles}


def _face_corners(mesh: dict) -> list[int]:
    return [i for i in mesh["triangles"] if mesh["normals"][i][2] > 0.9]


def test_canonical_tile_points_face_at_plus_z():
    out = fbx_mesh.canonical_tile(_quad_mesh(face_normal_axis=0, u_axis=2, v_axis=1), 0.6)
    assert all(out["positions"][i][2] == pytest.approx(0.5) for i in _face_corners(out))


def test_canonical_tile_runs_u_along_minus_x():
    out = fbx_mesh.canonical_tile(_quad_mesh(face_normal_axis=0, u_axis=2, v_axis=1), 0.6)
    for i in _face_corners(out):
        expected_x = 0.5 - out["uvs"][i][0] * 2  # u 0..0.5 maps to x +0.5..-0.5
        assert out["positions"][i][0] == pytest.approx(expected_x)


def test_canonical_tile_runs_v_along_plus_y():
    out = fbx_mesh.canonical_tile(_quad_mesh(face_normal_axis=1, u_axis=0, v_axis=2), 0.6)
    for i in _face_corners(out):
        assert out["positions"][i][1] == pytest.approx(out["uvs"][i][1] - 0.5)


def test_canonical_tile_winds_triangles_along_their_normals():
    out = fbx_mesh.canonical_tile(_quad_mesh(face_normal_axis=2, u_axis=0, v_axis=1), 0.6)
    p, n, t = out["positions"], out["normals"], out["triangles"]
    for k in range(0, len(t), 3):
        a, b, c = t[k:k + 3]
        cross = fbx_mesh._cross(fbx_mesh._sub(p[b], p[a]), fbx_mesh._sub(p[c], p[a]))
        assert fbx_mesh._dot(cross, n[a]) > 0


def test_canonical_tile_drops_zero_area_triangles():
    out = fbx_mesh.canonical_tile(_quad_mesh(face_normal_axis=0, u_axis=2, v_axis=1, degenerate=True), 0.6)
    assert len(out["triangles"]) == 9


def test_canonical_tile_requires_a_face():
    with pytest.raises(fbx_mesh.FbxError, match="no face"):
        fbx_mesh.canonical_tile(_quad_mesh(face_normal_axis=0, u_axis=2, v_axis=1), 0.0)


def test_parse_rejects_non_fbx_bytes():
    with pytest.raises(fbx_mesh.FbxError, match="not a binary FBX"):
        fbx_mesh.parse(b"; FBX 7.4.0 project file (ASCII)")
