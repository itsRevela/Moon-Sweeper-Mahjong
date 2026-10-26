from pathlib import Path

import pytest

from scripts import loader


def _make_dist(root: Path) -> Path:
    dist = root / "dist"
    for rel in ("winhttp.dll", "doorstop_config.ini", "BepInEx/core/BepInEx.Unity.IL2CPP.dll",
                "dotnet/coreclr.dll"):
        (dist / rel).parent.mkdir(parents=True, exist_ok=True)
        (dist / rel).write_bytes(rel.encode())
    return dist


def _make_game(root: Path) -> Path:
    game = root / "game"
    game.mkdir()
    (game / loader.GAME_EXE).write_bytes(b"exe")
    (game / "UnityPlayer.dll").write_bytes(b"player")
    return game


@pytest.fixture
def dist_and_game(tmp_path):
    return _make_dist(tmp_path), _make_game(tmp_path)


def test_install_copies_every_dist_file(dist_and_game):
    dist, game = dist_and_game
    loader.install(dist, game)
    assert (game / "BepInEx/core/BepInEx.Unity.IL2CPP.dll").read_bytes() == b"BepInEx/core/BepInEx.Unity.IL2CPP.dll"


def test_install_refuses_to_overwrite_existing_file(dist_and_game):
    dist, game = dist_and_game
    (game / "winhttp.dll").write_bytes(b"someone else's proxy")
    with pytest.raises(loader.LoaderError, match="refusing to overwrite"):
        loader.install(dist, game)


def test_refused_install_leaves_game_folder_untouched(dist_and_game):
    dist, game = dist_and_game
    (game / "winhttp.dll").write_bytes(b"someone else's proxy")
    before = sorted(p.relative_to(game).as_posix() for p in game.rglob("*"))
    with pytest.raises(loader.LoaderError):
        loader.install(dist, game)
    assert sorted(p.relative_to(game).as_posix() for p in game.rglob("*")) == before


def test_install_rejects_folder_without_game_exe(tmp_path):
    dist = _make_dist(tmp_path)
    empty = tmp_path / "not-the-game"
    empty.mkdir()
    with pytest.raises(loader.LoaderError, match=loader.GAME_EXE):
        loader.install(dist, empty)


def test_install_rejects_non_bepinex_dist(tmp_path):
    game = _make_game(tmp_path)
    bogus = tmp_path / "bogus"
    bogus.mkdir()
    with pytest.raises(loader.LoaderError, match="not a BepInEx IL2CPP distribution"):
        loader.install(bogus, game)


def test_second_install_is_refused(dist_and_game):
    dist, game = dist_and_game
    loader.install(dist, game)
    with pytest.raises(loader.LoaderError, match="already recorded"):
        loader.install(dist, game)


def test_uninstall_restores_original_game_folder(dist_and_game):
    dist, game = dist_and_game
    before = sorted(p.relative_to(game).as_posix() for p in game.rglob("*"))
    loader.install(dist, game)
    loader.uninstall(game)
    assert sorted(p.relative_to(game).as_posix() for p in game.rglob("*")) == before


def test_uninstall_keeps_runtime_files_without_purge(dist_and_game):
    dist, game = dist_and_game
    loader.install(dist, game)
    (game / "BepInEx/LogOutput.log").write_text("log")
    kept = loader.uninstall(game)
    assert kept == ["BepInEx"] and (game / "BepInEx/LogOutput.log").is_file()


def test_uninstall_with_purge_removes_runtime_files(dist_and_game):
    dist, game = dist_and_game
    loader.install(dist, game)
    (game / "BepInEx/interop").mkdir()
    (game / "BepInEx/interop/Assembly-CSharp.dll").write_bytes(b"x")
    (game / "BepInEx/LogOutput.log").write_text("log")
    loader.uninstall(game, purge=True)
    assert not (game / "BepInEx").exists()


def test_uninstall_without_record_is_refused(tmp_path):
    game = _make_game(tmp_path)
    with pytest.raises(loader.LoaderError, match="no loader install"):
        loader.uninstall(game)


def _make_cpp2il(root: Path) -> Path:
    cli = root / "cpp2il-bin"
    cli.mkdir()
    for name in ("Cpp2IL.exe", "LibCpp2IL.dll", "Cpp2IL.Core.dll"):
        (cli / name).write_bytes(name.encode())
    return cli


def test_stage_places_cpp2il_under_bepinex(tmp_path):
    out = tmp_path / "staged"
    loader.stage(_make_dist(tmp_path), _make_cpp2il(tmp_path), out)
    assert (out / loader.CPP2IL_SUBDIR / "Cpp2IL.exe").read_bytes() == b"Cpp2IL.exe"


def test_stage_keeps_every_dist_file(tmp_path):
    out = tmp_path / "staged"
    count = loader.stage(_make_dist(tmp_path), _make_cpp2il(tmp_path), out)
    assert count == 4 + 3


def test_stage_refuses_existing_output(tmp_path):
    out = tmp_path / "staged"
    out.mkdir()
    with pytest.raises(loader.LoaderError, match="already exists"):
        loader.stage(_make_dist(tmp_path), _make_cpp2il(tmp_path), out)


def test_stage_rejects_folder_without_cpp2il(tmp_path):
    not_cli = tmp_path / "empty"
    not_cli.mkdir()
    with pytest.raises(loader.LoaderError, match="not a Cpp2IL CLI build"):
        loader.stage(_make_dist(tmp_path), not_cli, tmp_path / "staged")


def test_status_reports_changed_file(dist_and_game):
    dist, game = dist_and_game
    loader.install(dist, game)
    (game / "doorstop_config.ini").write_text("edited")
    assert "1 changed or missing: doorstop_config.ini" in loader.status(game)
