import pytest

from scripts import table_mesh as tm


def _triangles(mesh):
    p, t = mesh["positions"], mesh["triangles"]
    for k in range(0, len(t), 3):
        yield p[t[k]], p[t[k + 1]], p[t[k + 2]], mesh["normals"][t[k]]


@pytest.mark.parametrize("build", list(tm.MESHES.values()), ids=list(tm.MESHES))
def test_every_triangle_is_wound_along_its_normal(build):
    for a, b, c, n in _triangles(build()):
        assert tm._dot(tm._cross(tm._sub(b, a), tm._sub(c, a)), n) > 0


@pytest.mark.parametrize("build", list(tm.MESHES.values()), ids=list(tm.MESHES))
def test_mesh_arrays_are_consistent(build):
    mesh = build()
    count = len(mesh["positions"])
    assert count == len(mesh["normals"]) == len(mesh["uvs"]) and len(mesh["triangles"]) % 3 == 0
    assert all(0 <= i < count for i in mesh["triangles"])


@pytest.mark.parametrize("build", [tm.frame_mesh, tm.rail_mesh, tm.felt_mesh], ids=["frame", "rail", "felt"])
def test_shell_stays_within_the_table_top(build):
    xs = [p[0] for p in build()["positions"]] + [p[2] for p in build()["positions"]]
    assert min(xs) >= -0.5 and max(xs) <= 0.5


def test_flat_tops_face_up():
    mesh = tm.felt_mesh()
    assert all(n == [0.0, 1.0, 0.0] for n in mesh["normals"])


def test_outer_apron_faces_outward():
    # The frame profile starts with a wall rising at the outer edge: its normals point away from the centre.
    mesh = tm.frame_mesh()
    apron = [(p, n) for p, n in zip(mesh["positions"], mesh["normals"]) if abs(n[1]) < 1e-6 and max(abs(p[0]), abs(p[2])) > 0.499]
    assert apron and all(p[0] * n[0] + p[2] * n[2] > 0 for p, n in apron)


def test_rail_rises_above_the_frame_and_felt():
    rail_top = max(p[1] for p in tm.rail_mesh()["positions"])
    assert rail_top > max(p[1] for p in tm.frame_mesh()["positions"]) > tm.felt_mesh()["positions"][0][1]


def test_felt_bed_matches_the_felt_texture_crop():
    xs = [p[0] for p in tm.felt_mesh()["positions"]]
    assert max(xs) == pytest.approx(0.5 - tm.FELT_INSET)


def test_console_is_unit_wide_and_stands_on_zero():
    positions = tm.console_mesh()["positions"]
    assert min(p[0] for p in positions) == pytest.approx(-0.5) and max(p[0] for p in positions) == pytest.approx(0.5)
    assert min(p[1] for p in positions) == pytest.approx(0.0)


def test_write_all_writes_every_mesh(tmp_path):
    assert sorted(tm.write_all(tmp_path)) == sorted(tm.MESHES)
