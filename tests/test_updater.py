"""The updater (installer/MoonSweeperMahjong.ps1 -Action Update, packaged as "Update Moon Sweeper Mahjong.bat"): it asks
GitHub for the latest release and reinstalls from it. Here "GitHub" is a local HTTP server serving a release JSON and a
real packaged zip, and the game is a fake folder."""

import hashlib
import json
import subprocess
import threading
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

import pytest

from scripts import package_release

ROOT = Path(__file__).resolve().parent.parent
SCRIPT = ROOT / "installer" / "MoonSweeperMahjong.ps1"
MANIFEST = ".moonsweepers-mahjong-loader.json"
SETTINGS = "BepInEx/config/itsrevela.moonsweepers.mahjong.cfg"
LATEST = "9.9.9"


def run(*args: str, script: Path = SCRIPT) -> subprocess.CompletedProcess:
    return subprocess.run(
        ["powershell", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", str(script), *args,
         "-GameProcess", "MoonSweepersInstallerTest"],
        capture_output=True, text=True, timeout=300)


class QuietHandler(SimpleHTTPRequestHandler):
    def log_message(self, *args):
        pass


@pytest.fixture
def github(tmp_path):
    """Serves tmp_path/site over HTTP; returns (site folder, base URL)."""
    site = tmp_path / "site"
    site.mkdir()
    server = ThreadingHTTPServer(("127.0.0.1", 0), partial(QuietHandler, directory=str(site)))
    thread = threading.Thread(target=server.serve_forever, daemon=True)
    thread.start()
    yield site, f"http://127.0.0.1:{server.server_address[1]}"
    server.shutdown()


def release_zip(tmp_path, version: str, plugin_bytes: bytes) -> Path:
    """A real release zip (scripts/package_release) whose plugin DLL holds plugin_bytes."""
    tmp_path = tmp_path / f"build-{len(list(tmp_path.glob('build-*')))}"  # each build its own inputs
    loader = tmp_path / f"loader-{version}"
    (loader / "BepInEx" / "core").mkdir(parents=True)
    (loader / "BepInEx" / "core" / "BepInEx.Core.dll").write_bytes(b"core")
    (loader / "winhttp.dll").write_bytes(b"doorstop")
    plugin = tmp_path / f"bin-{version}"
    plugin.mkdir()
    for name in ("MoonMahjong.Core.dll", "MoonMahjong.Table.dll", "MoonMahjong.Net.dll"):
        (plugin / name).write_bytes(name.encode())
    (plugin / "MoonMahjong.dll").write_bytes(plugin_bytes)
    assets = tmp_path / f"assets-{version}"
    (assets / "tiles").mkdir(parents=True)
    (assets / "tiles" / "1m.png").write_bytes(b"png")
    return package_release.package(loader, plugin, assets, tmp_path / f"dist-{version}", version)


def publish(site: Path, base: str, archive: Path, digest: str | None = None) -> str:
    """Puts the zip and a GitHub-style "latest release" JSON on the server; returns the JSON's URL."""
    (site / archive.name).write_bytes(archive.read_bytes())
    asset = {"name": archive.name, "browser_download_url": f"{base}/{archive.name}"}
    asset["digest"] = digest or "sha256:" + hashlib.sha256(archive.read_bytes()).hexdigest()
    (site / "latest.json").write_text(json.dumps({"tag_name": "v" + archive.stem.split("-v")[1], "assets": [asset]}), encoding="utf-8")
    return f"{base}/latest.json"


@pytest.fixture
def game(tmp_path):
    folder = tmp_path / "Moon Sweepers"
    folder.mkdir()
    (folder / "MoonSweepers.exe").write_bytes(b"exe")
    return folder


def install_old(tmp_path, game: Path):
    """Installs a 0.1.0 release (its own .bat run as PowerShell), with a settings file."""
    archive = release_zip(tmp_path, "0.1.0", b"old plugin")
    import zipfile
    with zipfile.ZipFile(archive) as z:
        z.extractall(tmp_path / "old")
    folder = tmp_path / "old" / "MoonSweeperMahjong-v0.1.0"
    # The packaged .bat is also a PowerShell script (its batch part is a comment there): run it as 0.1.0's installer.
    script = tmp_path / "old-installer.ps1"
    script.write_bytes((folder / "Install Moon Sweeper Mahjong.bat").read_bytes())
    result = run("-Action", "Install", "-GamePath", str(game), "-Payload", str(folder / "payload"), "-Yes", script=script)
    assert result.returncode == 0, result.stdout + result.stderr
    (game / SETTINGS).parent.mkdir(parents=True, exist_ok=True)
    (game / SETTINGS).write_text("[Game]\nRoundCount = 4\n", encoding="utf-8")


def update(game: Path, api: str) -> subprocess.CompletedProcess:
    return run("-Action", "Update", "-GamePath", str(game), "-ReleaseApi", api)


def installed_version(game: Path) -> str:
    return json.loads((game / MANIFEST).read_text(encoding="utf-8"))["version"]


def test_update_installs_the_latest_release(tmp_path, github, game):
    site, base = github
    install_old(tmp_path, game)
    api = publish(site, base, release_zip(tmp_path, LATEST, b"new plugin"))

    result = update(game, api)
    assert result.returncode == 0, result.stdout + result.stderr
    assert installed_version(game) == LATEST


def test_update_replaces_the_mods_files(tmp_path, github, game):
    site, base = github
    install_old(tmp_path, game)
    api = publish(site, base, release_zip(tmp_path, LATEST, b"new plugin"))

    update(game, api)
    assert (game / "BepInEx/plugins/MoonMahjong/MoonMahjong.dll").read_bytes() == b"new plugin"


def test_update_keeps_the_settings(tmp_path, github, game):
    site, base = github
    install_old(tmp_path, game)
    api = publish(site, base, release_zip(tmp_path, LATEST, b"new plugin"))

    update(game, api)
    assert (game / SETTINGS).read_text(encoding="utf-8") == "[Game]\nRoundCount = 4\n"


def test_update_when_up_to_date_changes_nothing(tmp_path, github, game):
    site, base = github
    install_old(tmp_path, game)
    api = publish(site, base, release_zip(tmp_path, "0.1.0", b"same plugin"))

    result = update(game, api)
    assert result.returncode == 0, result.stdout + result.stderr
    assert "up to date" in result.stdout.lower()


def test_update_without_an_install_says_so(tmp_path, github, game):
    site, base = github
    api = publish(site, base, release_zip(tmp_path, LATEST, b"new plugin"))

    result = update(game, api)
    assert result.returncode == 1
    assert "not installed" in result.stdout.lower()


def test_a_download_that_fails_its_checksum_changes_nothing(tmp_path, github, game):
    site, base = github
    install_old(tmp_path, game)
    api = publish(site, base, release_zip(tmp_path, LATEST, b"new plugin"), digest="sha256:" + "0" * 64)

    result = update(game, api)
    assert result.returncode == 1
    assert installed_version(game) == "0.1.0"


def test_a_release_without_a_checksum_is_not_installed(tmp_path, github, game):
    site, base = github
    install_old(tmp_path, game)
    api = publish(site, base, release_zip(tmp_path, LATEST, b"new plugin"), digest="none")

    result = update(game, api)
    assert result.returncode == 1
    assert installed_version(game) == "0.1.0"


def test_github_unreachable_is_reported(tmp_path, game):
    install_old(tmp_path, game)

    result = update(game, "http://127.0.0.1:9/latest.json")
    assert result.returncode == 1
    assert "github" in result.stdout.lower()
