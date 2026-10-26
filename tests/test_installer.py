"""The release installer (installer/MoonSweeperMahjong.ps1), run for real against fake Steam and game folders."""

import json
import subprocess
from pathlib import Path

import pytest

ROOT = Path(__file__).resolve().parent.parent
SCRIPT = ROOT / "installer" / "MoonSweeperMahjong.ps1"
MANIFEST = ".moonsweepers-mahjong-loader.json"
SETTINGS = "BepInEx/config/itsrevela.moonsweepers.mahjong.cfg"

PAYLOAD_FILES = {
    "winhttp.dll": b"doorstop",
    "doorstop_config.ini": b"[General]",
    "BepInEx/core/BepInEx.Core.dll": b"core",
    "BepInEx/plugins/MoonMahjong/MoonMahjong.dll": b"plugin",
    "BepInEx/plugins/MoonMahjong/assets/tiles/1m.png": b"png",
}


def run(*args: str) -> subprocess.CompletedProcess:
    return subprocess.run(
        ["powershell", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", str(SCRIPT), *args,
         "-GameProcess", "MoonSweepersInstallerTest"],  # a real running game must not refuse the tests
        capture_output=True, text=True, timeout=120)


@pytest.fixture
def env(tmp_path):
    """A Steam install whose second library holds the game, plus a payload to install."""
    steam = tmp_path / "Steam"
    library = tmp_path / "Library2"
    (steam / "steamapps").mkdir(parents=True)
    (library / "steamapps" / "common" / "Moon Sweepers").mkdir(parents=True)
    escaped = str(library).replace("\\", "\\\\")
    (steam / "steamapps" / "libraryfolders.vdf").write_text(
        '"libraryfolders"\n{\n\t"0"\n\t{\n\t\t"path"\t\t"' + str(steam).replace("\\", "\\\\") + '"\n\t}\n'
        '\t"1"\n\t{\n\t\t"path"\t\t"' + escaped + '"\n\t\t"apps"\n\t\t{\n\t\t\t"5335510"\t\t"123"\n\t\t}\n\t}\n}\n',
        encoding="utf-8")
    (library / "steamapps" / "appmanifest_5335510.acf").write_text(
        '"AppState"\n{\n\t"appid"\t\t"5335510"\n\t"installdir"\t\t"Moon Sweepers"\n}\n', encoding="utf-8")
    game = library / "steamapps" / "common" / "Moon Sweepers"
    (game / "MoonSweepers.exe").write_bytes(b"exe")

    payload = tmp_path / "payload"
    for rel, data in PAYLOAD_FILES.items():
        (payload / rel).parent.mkdir(parents=True, exist_ok=True)
        (payload / rel).write_bytes(data)
    return {"steam": steam, "game": game, "payload": payload}


def install(env, *extra: str) -> subprocess.CompletedProcess:
    return run("-Action", "Install", "-SteamPath", str(env["steam"]), "-Payload", str(env["payload"]), "-Yes", *extra)


def test_status_finds_the_game_in_another_steam_library(env):
    result = run("-Action", "Status", "-SteamPath", str(env["steam"]))
    assert result.returncode == 0, result.stdout + result.stderr
    assert str(env["game"]) in result.stdout
    assert "not installed" in result.stdout.lower()


def test_install_copies_the_payload_into_the_game(env):
    result = install(env)
    assert result.returncode == 0, result.stdout + result.stderr
    for rel, data in PAYLOAD_FILES.items():
        assert (env["game"] / rel).read_bytes() == data


def test_install_records_every_file_it_added(env):
    install(env)
    manifest = json.loads((env["game"] / MANIFEST).read_text(encoding="utf-8"))
    assert {f["path"] for f in manifest["files"]} == set(PAYLOAD_FILES)


def test_status_reports_an_intact_install(env):
    install(env)
    result = run("-Action", "Status", "-GamePath", str(env["game"]))
    assert "installed" in result.stdout.lower() and "intact" in result.stdout.lower()


def test_install_refuses_when_already_installed(env):
    install(env)
    result = install(env)
    assert result.returncode != 0
    assert "already installed" in (result.stdout + result.stderr).lower()


def test_uninstall_removes_everything_it_installed(env):
    install(env)
    result = run("-Action", "Uninstall", "-GamePath", str(env["game"]), "-Yes", "-Settings", "Remove")
    assert result.returncode == 0, result.stdout + result.stderr
    assert sorted(p.name for p in env["game"].iterdir()) == ["MoonSweepers.exe"]


def test_uninstall_also_removes_what_bepinex_made_at_runtime(env):
    install(env)
    for rel in ("BepInEx/interop/Assembly-CSharp.dll", "BepInEx/LogOutput.log", "BepInEx/config/BepInEx.cfg", "dotnet/x.dll"):
        (env["game"] / rel).parent.mkdir(parents=True, exist_ok=True)
        (env["game"] / rel).write_bytes(b"x")
    # dotnet/ ships in the real payload; record it like the installer would have.
    manifest = json.loads((env["game"] / MANIFEST).read_text(encoding="utf-8"))
    manifest["files"].append({"path": "dotnet/x.dll", "sha256": ""})
    manifest["dirs"].append("dotnet")
    (env["game"] / MANIFEST).write_text(json.dumps(manifest), encoding="utf-8")

    run("-Action", "Uninstall", "-GamePath", str(env["game"]), "-Yes", "-Settings", "Remove")
    assert sorted(p.name for p in env["game"].iterdir()) == ["MoonSweepers.exe"]


def test_uninstall_can_keep_the_settings(env):
    install(env)
    (env["game"] / SETTINGS).parent.mkdir(parents=True, exist_ok=True)
    (env["game"] / SETTINGS).write_text("[Game]\nRoundCount = 4\n", encoding="utf-8")

    run("-Action", "Uninstall", "-GamePath", str(env["game"]), "-Yes", "-Settings", "Keep")
    assert (env["game"] / SETTINGS).read_text(encoding="utf-8") == "[Game]\nRoundCount = 4\n"
    assert not (env["game"] / "BepInEx" / "core").exists()
    assert not (env["game"] / MANIFEST).exists()


def test_a_foreign_bepinex_is_backed_up_and_restored(env):
    (env["game"] / "BepInEx" / "plugins").mkdir(parents=True)
    (env["game"] / "BepInEx" / "plugins" / "Other.dll").write_bytes(b"other mod")
    (env["game"] / "winhttp.dll").write_bytes(b"their doorstop")

    result = install(env)
    assert result.returncode == 0, result.stdout + result.stderr
    assert (env["game"] / "winhttp.dll").read_bytes() == b"doorstop"
    assert not (env["game"] / "BepInEx" / "plugins" / "Other.dll").exists()

    run("-Action", "Uninstall", "-GamePath", str(env["game"]), "-Yes", "-Settings", "Remove")
    assert (env["game"] / "winhttp.dll").read_bytes() == b"their doorstop"
    assert (env["game"] / "BepInEx" / "plugins" / "Other.dll").read_bytes() == b"other mod"
    assert not any(p.name.startswith("MoonSweeperMahjong-backup") for p in env["game"].iterdir())


def test_a_foreign_bepinex_is_left_alone_without_consent(env):
    (env["game"] / "winhttp.dll").write_bytes(b"their doorstop")

    result = run("-Action", "Install", "-SteamPath", str(env["steam"]), "-Payload", str(env["payload"]))
    assert result.returncode != 0
    assert (env["game"] / "winhttp.dll").read_bytes() == b"their doorstop"
    assert not (env["game"] / MANIFEST).exists()


def test_a_missing_game_is_reported(tmp_path):
    (tmp_path / "Steam" / "steamapps").mkdir(parents=True)
    result = run("-Action", "Status", "-SteamPath", str(tmp_path / "Steam"))
    assert result.returncode == 2
    assert "could not find moon sweepers" in (result.stdout + result.stderr).lower()


def test_a_game_path_without_the_game_is_rejected(tmp_path):
    result = run("-Action", "Status", "-GamePath", str(tmp_path))
    assert result.returncode == 2


def test_uninstall_without_an_install_says_so(env):
    result = run("-Action", "Uninstall", "-GamePath", str(env["game"]), "-Yes", "-Settings", "Remove")
    assert result.returncode != 0
    assert "not installed" in (result.stdout + result.stderr).lower()


def test_a_developer_install_from_loader_py_can_be_uninstalled(env):
    # scripts/loader.py writes the same manifest without the release fields (version, installer, backup).
    for rel, data in PAYLOAD_FILES.items():
        (env["game"] / rel).parent.mkdir(parents=True, exist_ok=True)
        (env["game"] / rel).write_bytes(data)
    manifest = {"dist": r"C:\dev\loader-dist", "files": [{"path": rel, "sha256": ""} for rel in PAYLOAD_FILES],
                "dirs": ["BepInEx", "BepInEx/core", "BepInEx/plugins", "BepInEx/plugins/MoonMahjong",
                         "BepInEx/plugins/MoonMahjong/assets", "BepInEx/plugins/MoonMahjong/assets/tiles"]}
    (env["game"] / MANIFEST).write_text(json.dumps(manifest), encoding="utf-8")

    result = run("-Action", "Uninstall", "-GamePath", str(env["game"]), "-Yes", "-Settings", "Remove")
    assert result.returncode == 0, result.stdout + result.stderr
    assert sorted(p.name for p in env["game"].iterdir()) == ["MoonSweepers.exe"]


def keep_settings_and_uninstall(env):
    install(env)
    (env["game"] / SETTINGS).parent.mkdir(parents=True, exist_ok=True)
    (env["game"] / SETTINGS).write_text("[Game]\nRoundCount = 4\n", encoding="utf-8")
    run("-Action", "Uninstall", "-GamePath", str(env["game"]), "-Yes", "-Settings", "Keep")


def test_reinstall_after_keeping_settings_uses_them(env):
    keep_settings_and_uninstall(env)

    install(env)
    assert (env["game"] / SETTINGS).read_text(encoding="utf-8") == "[Game]\nRoundCount = 4\n"


def test_reinstall_after_keeping_settings_is_not_mistaken_for_a_foreign_loader(env):
    keep_settings_and_uninstall(env)

    result = run("-Action", "Install", "-SteamPath", str(env["steam"]), "-Payload", str(env["payload"]))
    assert result.returncode == 0, result.stdout + result.stderr


def test_uninstall_after_a_reinstall_over_kept_settings_leaves_no_bepinex_folder(env):
    keep_settings_and_uninstall(env)
    install(env)

    run("-Action", "Uninstall", "-GamePath", str(env["game"]), "-Yes", "-Settings", "Remove")
    assert not (env["game"] / "BepInEx").exists()
