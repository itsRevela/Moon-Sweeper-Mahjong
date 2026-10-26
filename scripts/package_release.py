"""Build the release zip players download: dist/MoonSweeperMahjong-v<version>.zip.

The zip holds one folder with:
  Install Moon Sweeper Mahjong.bat  installer/header.bat + installer/MoonSweeperMahjong.ps1, stamped with the version
  Update Moon Sweeper Mahjong.bat   the same, run with -Action Update (reinstalls from the latest GitHub release)
  payload/                          what the installer copies into the game folder: the patched BepInEx loader
                                    (scripts.loader stage output) plus BepInEx/plugins/MoonMahjong (the plugin's
                                    MoonMahjong*.dll/.pdb and build/assets), as `dotnet build -p:DeployToGame=true` lays it out
  LICENSE.txt, THIRD-PARTY-NOTICES.txt (installer/THIRD-PARTY-NOTICES.txt + installer/licenses/*), README.txt

Build the inputs first: the plugin (dotnet build src/MoonMahjong.Plugin -c Release), the assets
(python -m scripts.prepare_assets) and the loader (python -m scripts.loader stage ..., see docs/loader.md).

Usage:
    python -m scripts.package_release [--loader-dist .run/loader-dist] [--plugin-bin <Release bin>] [--assets build/assets]
                                      [--out dist] [--version X.Y.Z]
"""
from __future__ import annotations

import argparse
import re
import shutil
import sys
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
INSTALLER = ROOT / "installer"
BAT_NAME = "Install Moon Sweeper Mahjong.bat"
UPDATER_NAME = "Update Moon Sweeper Mahjong.bat"
ARGUMENTS = " %*\""  # where installer/header.bat hands the .bat's arguments to the script
PLUGIN_DIR = "BepInEx/plugins/MoonMahjong"
REQUIRED_PLUGIN_FILES = ("MoonMahjong.dll", "MoonMahjong.Core.dll", "MoonMahjong.Table.dll", "MoonMahjong.Net.dll")
VERSION_LINE = "$ModVersion = '0.0.0-dev'"


class PackageError(Exception):
    """A missing or unusable input, with a message for the user."""


def plugin_version(source: Path = ROOT / "src" / "MoonMahjong.Plugin" / "Plugin.cs") -> str:
    match = re.search(r'const string Version = "([^"]+)"', source.read_text(encoding="utf-8"))
    if not match:
        raise PackageError(f"no Version constant in {source}")
    return match.group(1)


def build_bat(version: str, action: str | None = None) -> bytes:
    """The installer: a batch header that hands the rest of the file to PowerShell, with Windows line endings. With
    `action`, the .bat runs that action (the updater is the same script run with -Action Update)."""
    script = (INSTALLER / "MoonSweeperMahjong.ps1").read_text(encoding="utf-8")
    if script.count(VERSION_LINE) != 1:
        raise PackageError(f"installer/MoonSweeperMahjong.ps1 must contain exactly one {VERSION_LINE!r}")
    script = script.replace(VERSION_LINE, f"$ModVersion = '{version}'")
    header = (INSTALLER / "header.bat").read_text(encoding="utf-8")
    if action:
        if header.count(ARGUMENTS) != 1:
            raise PackageError(f"installer/header.bat must pass its arguments exactly once ({ARGUMENTS!r})")
        header = header.replace(ARGUMENTS, f" -Action {action}{ARGUMENTS}")
    text = header + script
    if not text.isascii():
        raise PackageError("the installer must be plain ASCII (cmd reads the .bat in the console's code page)")
    return "\r\n".join(text.replace("\r\n", "\n").split("\n")).encode("ascii")


def notices() -> str:
    parts = [(INSTALLER / "THIRD-PARTY-NOTICES.txt").read_text(encoding="utf-8")]
    for licence in sorted((INSTALLER / "licenses").glob("*.txt")):
        parts.append(f"\n\n{'=' * 118}\n{licence.stem}\n{'=' * 118}\n\n" + licence.read_text(encoding="utf-8"))
    return "".join(parts)


def _require(path: Path, what: str) -> Path:
    if not path.exists():
        raise PackageError(f"{what} is missing: {path}")
    return path


def package(loader_dist: Path, plugin_bin: Path, assets: Path, out: Path, version: str | None = None) -> Path:
    version = version or plugin_version()
    _require(loader_dist / "winhttp.dll", "the staged loader (python -m scripts.loader stage)")
    for name in REQUIRED_PLUGIN_FILES:
        _require(plugin_bin / name, "the built plugin (dotnet build src/MoonMahjong.Plugin -c Release)")
    _require(assets, "the plugin assets (python -m scripts.prepare_assets)")

    name = f"MoonSweeperMahjong-v{version}"
    stage = out / name
    if stage.exists():
        shutil.rmtree(stage)  # our own earlier output for this version
    payload = stage / "payload"
    shutil.copytree(loader_dist, payload)
    plugin = payload / PLUGIN_DIR
    plugin.mkdir(parents=True, exist_ok=True)
    for file in sorted(plugin_bin.glob("MoonMahjong*")):
        if file.suffix in (".dll", ".pdb"):
            shutil.copy2(file, plugin / file.name)
    shutil.copytree(assets, plugin / "assets")

    (stage / BAT_NAME).write_bytes(build_bat(version))
    (stage / UPDATER_NAME).write_bytes(build_bat(version, "Update"))
    shutil.copy2(ROOT / "LICENSE", stage / "LICENSE.txt")
    (stage / "THIRD-PARTY-NOTICES.txt").write_text(notices(), encoding="utf-8")
    readme = (INSTALLER / "README.txt").read_text(encoding="utf-8").replace("{version}", version)
    (stage / "README.txt").write_text(readme, encoding="utf-8")

    archive = out / f"{name}.zip"
    with zipfile.ZipFile(archive, "w", zipfile.ZIP_DEFLATED) as z:
        for path in sorted(stage.rglob("*")):
            if path.is_file():
                z.write(path, path.relative_to(out).as_posix())
    return archive


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--loader-dist", type=Path, default=ROOT / ".run" / "loader-dist")
    parser.add_argument("--plugin-bin", type=Path, default=ROOT / "src" / "MoonMahjong.Plugin" / "bin" / "Release" / "net6.0")
    parser.add_argument("--assets", type=Path, default=ROOT / "build" / "assets")
    parser.add_argument("--out", type=Path, default=ROOT / "dist")
    parser.add_argument("--version")
    args = parser.parse_args(argv)
    try:
        archive = package(args.loader_dist, args.plugin_bin, args.assets, args.out, args.version)
    except (PackageError, OSError) as exc:
        print(f"PACKAGE ERROR: {exc}", file=sys.stderr)
        return 1
    print(f"PACKAGE OK: {archive} ({archive.stat().st_size / 1e6:.1f} MB)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
