"""The release zip (scripts/package_release.py): layout, payload, the installer .bat, and the .bat actually running."""

import os
import subprocess
import zipfile
from pathlib import Path

import pytest

from scripts import package_release

ROOT = Path(__file__).resolve().parent.parent


@pytest.fixture
def inputs(tmp_path):
    loader = tmp_path / "loader"
    (loader / "BepInEx" / "core").mkdir(parents=True)
    (loader / "BepInEx" / "core" / "BepInEx.Core.dll").write_bytes(b"core")
    (loader / "winhttp.dll").write_bytes(b"doorstop")
    plugin = tmp_path / "bin"
    plugin.mkdir()
    for name in ("MoonMahjong.dll", "MoonMahjong.pdb", "MoonMahjong.Core.dll", "MoonMahjong.Table.dll", "MoonMahjong.deps.json", "Other.dll"):
        (plugin / name).write_bytes(name.encode())
    assets = tmp_path / "assets"
    (assets / "tiles").mkdir(parents=True)
    (assets / "tiles" / "1m.png").write_bytes(b"png")
    return {"loader": loader, "plugin": plugin, "assets": assets, "out": tmp_path / "dist"}


def package(inputs, version="1.2.3") -> Path:
    return package_release.package(inputs["loader"], inputs["plugin"], inputs["assets"], inputs["out"], version)


def names(zip_path: Path) -> set[str]:
    with zipfile.ZipFile(zip_path) as z:
        return set(z.namelist())


def test_version_is_read_from_the_plugin():
    assert package_release.plugin_version() == "0.1.0"


def test_zip_is_named_for_the_version(inputs):
    assert package(inputs).name == "MoonSweeperMahjong-v1.2.3.zip"


def test_zip_holds_the_installer_licences_and_readme(inputs):
    top = "MoonSweeperMahjong-v1.2.3/"
    assert {top + "Install Moon Sweeper Mahjong.bat", top + "LICENSE.txt", top + "THIRD-PARTY-NOTICES.txt", top + "README.txt"} <= names(package(inputs))


def test_payload_has_the_loader_and_the_plugin_with_its_assets(inputs):
    payload = "MoonSweeperMahjong-v1.2.3/payload/"
    expected = {payload + p for p in ("winhttp.dll", "BepInEx/core/BepInEx.Core.dll",
                                      "BepInEx/plugins/MoonMahjong/MoonMahjong.dll", "BepInEx/plugins/MoonMahjong/MoonMahjong.pdb",
                                      "BepInEx/plugins/MoonMahjong/MoonMahjong.Core.dll", "BepInEx/plugins/MoonMahjong/MoonMahjong.Table.dll",
                                      "BepInEx/plugins/MoonMahjong/assets/tiles/1m.png")}
    got = {n for n in names(package(inputs)) if n.startswith(payload) and not n.endswith("/")}
    assert got == expected


def test_installer_bat_carries_the_version(inputs):
    with zipfile.ZipFile(package(inputs)) as z:
        bat = z.read("MoonSweeperMahjong-v1.2.3/Install Moon Sweeper Mahjong.bat").decode("ascii")
    assert "$ModVersion = '1.2.3'" in bat
    assert bat.startswith("<# :")


def test_installer_bat_uses_windows_line_endings(inputs):
    with zipfile.ZipFile(package(inputs)) as z:
        bat = z.read("MoonSweeperMahjong-v1.2.3/Install Moon Sweeper Mahjong.bat")
    assert b"\r\n" in bat and b"\n" not in bat.replace(b"\r\n", b"")


def test_notices_name_every_upstream_project(inputs):
    with zipfile.ZipFile(package(inputs)) as z:
        notices = z.read("MoonSweeperMahjong-v1.2.3/THIRD-PARTY-NOTICES.txt").decode("utf-8")
    for project in ("BepInEx", "Il2CppInterop", "Cpp2IL", "OpenRiichi", "NaoMahjong", "HarmonyX", "UnityDoorstop", ".NET"):
        assert project in notices


def test_missing_inputs_are_named(inputs):
    (inputs["plugin"] / "MoonMahjong.dll").unlink()
    with pytest.raises(package_release.PackageError, match="MoonMahjong.dll"):
        package(inputs)


def test_packaged_bat_runs_the_installer(inputs, tmp_path):
    with zipfile.ZipFile(package(inputs)) as z:
        z.extractall(tmp_path / "release")
    game = tmp_path / "Game"
    game.mkdir()
    (game / "MoonSweepers.exe").write_bytes(b"exe")
    bat = tmp_path / "release" / "MoonSweeperMahjong-v1.2.3" / "Install Moon Sweeper Mahjong.bat"

    result = subprocess.run(["cmd", "/c", str(bat), "-Action", "Status", "-GamePath", str(game)],
                            capture_output=True, text=True, timeout=120, env={**os.environ, "MSM_NOPAUSE": "1"})
    assert result.returncode == 0, result.stdout + result.stderr
    assert "Moon Sweeper Mahjong: not installed" in result.stdout
