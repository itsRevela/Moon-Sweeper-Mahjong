from pathlib import Path

import pytest

from PIL import Image

from scripts import fbx_mesh, prepare_assets, sound_assets, table_mesh, tile_index, tile_shape


def _fake_nao(root: Path, skip: str | None = None) -> Path:
    resources = root / "nao" / "Assets" / "Resources"
    tiles = resources / "Textures" / "TileTextures"
    tiles.mkdir(parents=True)
    for name in prepare_assets.TILE_NAMES:
        if name != skip:
            # Real 62 x 59 textures: the build now redraws each face with a corner index.
            Image.new("RGBA", (62, 59), (223, 223, 220, 255)).save(tiles / f"{name}.png")
    (resources / "Textures" / "Environment").mkdir(parents=True)
    (resources / "Textures" / "Environment" / "Table_Dif.png").write_bytes(b"table")
    (resources / "Fbx").mkdir()
    (resources / "Fbx" / "tile.fbx").write_bytes(b"fbx")
    return root / "nao"


def _fake_sounds(root: Path, skip: str | None = None) -> Path:
    import wave

    folder = root / "Sounds"
    folder.mkdir(exist_ok=True)
    for name in sound_assets.SOUNDS:
        if name != skip:
            with wave.open(str(folder / f"{name}.wav"), "wb") as w:
                w.setnchannels(1)
                w.setsampwidth(2)
                w.setframerate(22050)
                w.writeframes(bytes(4))
    return folder


@pytest.fixture
def stub_mesh(monkeypatch):
    monkeypatch.setattr(fbx_mesh, "convert", lambda data: {"triangles": []})
    monkeypatch.setattr(fbx_mesh, "canonical_tile",
                        lambda mesh, u: {"normals": [[0, 0, 1]], "uvs": [[42 / 62, 1.0]], "canonical": u})
    monkeypatch.setattr(tile_shape, "rounded_tile", lambda mesh: mesh)  # covered by test_tile_shape
    # The fake Table_Dif.png is not an image; texture splitting is covered by its own test below.
    monkeypatch.setattr(prepare_assets, "split_table_texture", lambda png, out: None)


def test_prepare_copies_all_37_tile_textures(tmp_path, stub_mesh):
    out = tmp_path / "out"
    prepare_assets.prepare(_fake_nao(tmp_path), out, _fake_sounds(tmp_path))
    assert len(list((out / "tiles").glob("*.png"))) == 37


def test_prepare_writes_upscaled_indexed_tile_textures(tmp_path, stub_mesh):
    out = tmp_path / "out"
    prepare_assets.prepare(_fake_nao(tmp_path), out, _fake_sounds(tmp_path))
    assert Image.open(out / "tiles" / "1z.png").size == (62 * tile_index.UPSCALE, 59 * tile_index.UPSCALE)


def test_prepare_manifest_lists_every_output_file(tmp_path, stub_mesh):
    out = tmp_path / "out"
    manifest = prepare_assets.prepare(_fake_nao(tmp_path), out, _fake_sounds(tmp_path))
    expected = ({"table.png", "tile_mesh.json", "sounds.json"} | set(table_mesh.MESHES)
                | {f"tiles/{n}.png" for n in prepare_assets.TILE_NAMES}
                | {f"sounds/{n}.f32" for n in sound_assets.SOUNDS})
    assert set(manifest) == expected


def test_split_table_texture_writes_the_four_derived_textures(tmp_path):
    from PIL import Image

    painting = tmp_path / "table.png"
    Image.new("RGB", (1024, 1024), (40, 70, 120)).save(painting)
    prepare_assets.split_table_texture(painting, tmp_path)
    sizes = {name: Image.open(tmp_path / name).size for name in
             ("table_frame.png", "table_rail.png", "table_felt.png", "table_console.png")}
    assert sizes["table_felt.png"] == (758, 758) and sizes["table_console.png"] == (501, 501)


def test_split_table_texture_rejects_other_sizes(tmp_path):
    from PIL import Image

    painting = tmp_path / "table.png"
    Image.new("RGB", (512, 512)).save(painting)
    with pytest.raises(prepare_assets.AssetError, match="1024"):
        prepare_assets.split_table_texture(painting, tmp_path)


def test_prepare_fails_on_missing_tile_without_writing(tmp_path, stub_mesh):
    out = tmp_path / "out"
    with pytest.raises(prepare_assets.AssetError, match="7z.png"):
        prepare_assets.prepare(_fake_nao(tmp_path, skip="7z"), out, _fake_sounds(tmp_path))
    assert not out.exists()


def test_tile_names_cover_red_fives_and_honors():
    assert {"0m", "0p", "0s", "1z", "7z"} <= set(prepare_assets.TILE_NAMES)


def test_prepare_fails_on_a_missing_sound_without_writing(tmp_path, stub_mesh):
    out = tmp_path / "out"
    with pytest.raises(prepare_assets.AssetError, match="ron.wav"):
        prepare_assets.prepare(_fake_nao(tmp_path), out, _fake_sounds(tmp_path, skip="ron"))
    assert not out.exists()
