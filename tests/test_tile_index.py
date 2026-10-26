from PIL import Image

from scripts import prepare_assets, tile_index

# A NaoMahjong-like texture: a 42 px face (u up to 42/62) left of a 20 px orange back.
WIDTH, HEIGHT, FACE_PX = 62, 59, 42
FACE_U = FACE_PX / WIDTH
FACE_BG = (223, 223, 220, 255)
BACK = (255, 157, 40, 255)
ART = (30, 60, 200, 255)
K = tile_index.UPSCALE


def _source(art_box=None) -> Image.Image:
    image = Image.new("RGBA", (WIDTH, HEIGHT), FACE_BG)
    image.paste(BACK, (FACE_PX, 0, WIDTH, HEIGHT))
    if art_box:
        image.paste(ART, art_box)
    return image


def _close(a, b, tolerance=12) -> bool:
    return all(abs(x - y) <= tolerance for x, y in zip(a[:3], b[:3]))


def _face_pixels(image):
    for y in range(HEIGHT * K):
        for x in range(FACE_PX * K):
            yield x, y, image.getpixel((x, y))


def test_every_tile_has_an_index_label():
    assert set(tile_index.LABELS) == set(prepare_assets.TILE_NAMES)


def test_suited_tiles_show_their_number():
    assert [tile_index.LABELS[f"{n}p"] for n in range(1, 10)] == [str(n) for n in range(1, 10)]


def test_red_fives_show_a_red_5():
    assert {tile_index.LABELS[f"0{suit}"] for suit in "mps"} == {"5"}
    r, g, b, _ = tile_index.label_colour("0s")
    assert r > 2 * g and r > 2 * b


def test_winds_show_their_compass_letter():
    assert [tile_index.LABELS[f"{n}z"] for n in range(1, 5)] == ["E", "S", "W", "N"]


def test_dragons_show_their_colour_without_clashing_with_west():
    assert [tile_index.LABELS[f"{n}z"] for n in range(5, 8)] == ["Wh", "G", "R"]


def test_indexed_tile_is_upscaled():
    assert tile_index.index_tile(_source(), "1m", FACE_U).size == (WIDTH * K, HEIGHT * K)


def test_blank_face_gets_ink_only_in_its_top_left_corner():
    indexed = tile_index.index_tile(_source(), "1z", FACE_U)
    ink = [(x, y) for x, y, p in _face_pixels(indexed) if sum(p[:3]) < 3 * 128]
    assert ink
    assert all(x < FACE_PX * K * 0.4 and y < HEIGHT * K * 0.3 for x, y in ink)


def test_art_keeps_its_size_and_place():
    # Art in the lower right of the face, clear of the index: it is all still there, corners included. (One pixel in
    # from each corner: Lanczos rings on the box's hard edge.)
    indexed = tile_index.index_tile(_source(art_box=(20, 30, 40, 57)), "1m", FACE_U)
    for x, y in ((21, 31), (38, 31), (21, 55), (38, 55), (30, 43)):
        assert _close(indexed.getpixel((x * K + K // 2, y * K + K // 2)), ART)
    assert _close(indexed.getpixel((19 * K, 29 * K)), FACE_BG)


def test_index_is_outlined_where_it_overlaps_the_art():
    # Art right up to the face's 1 px border, as the 一 and 9p's circles nearly are.
    indexed = tile_index.index_tile(_source(art_box=(1, 1, FACE_PX - 1, HEIGHT - 1)), "3z", FACE_U)
    corner = [p for x, y, p in _face_pixels(indexed) if x < FACE_PX * K * 0.4 and y < HEIGHT * K * 0.3]
    assert any(_close(p, tile_index.INK) for p in corner)
    assert sum(_close(p, FACE_BG) for p in corner) > 0.05 * len(corner)


def test_back_is_kept_and_does_not_bleed_into_the_face():
    indexed = tile_index.index_tile(_source(), "1m", FACE_U)
    assert _close(indexed.getpixel((FACE_PX * K + 10, HEIGHT * K // 2)), BACK)
    assert _close(indexed.getpixel((FACE_PX * K - 1, HEIGHT * K // 2)), FACE_BG)


def test_face_u_extent_reads_the_faces_uv_width():
    mesh = {"normals": [[0, 0, 1]] * 2 + [[0, 0, -1]], "uvs": [[0.0, 0.0], [FACE_U, 1.0], [0.95, 0.5]]}
    assert tile_index.face_u_extent(mesh) == FACE_U


def test_face_u_extent_rejects_a_mesh_without_a_face():
    mesh = {"normals": [[0, 1, 0]], "uvs": [[0.5, 0.5]]}
    try:
        tile_index.face_u_extent(mesh)
    except ValueError:
        return
    raise AssertionError("expected ValueError")
