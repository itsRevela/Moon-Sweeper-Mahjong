"""Install or remove the BepInEx loader in the Moon Sweepers game folder.

Install copies a built BepInEx distribution into the game folder. It refuses to
overwrite any existing file and records every file it adds in a manifest inside
the game folder. Uninstall removes exactly the files in that manifest, then any
directories it created that are now empty. Files that BepInEx or the mod create
later (interop assemblies, logs, plugins) are left alone unless --purge is given.

Stage merges a BepInEx IL2CPP distribution with a Cpp2IL CLI build into one tree, placing the CLI in
BepInEx/cpp2il/, where our BepInEx build runs it out of process (docs/loader.md explains why).

Usage:
    python -m scripts.loader stage --dist <BepInEx dist dir> --cpp2il <Cpp2IL net472 bin dir> --out <dir>
    python -m scripts.loader install --dist <BepInEx dist dir> [--game <dir>]
    python -m scripts.loader uninstall [--game <dir>] [--purge]
    python -m scripts.loader status [--game <dir>]
"""
from __future__ import annotations

import argparse
import hashlib
import json
import shutil
import sys
from dataclasses import dataclass
from pathlib import Path

DEFAULT_GAME = Path(r"C:\Program Files (x86)\Steam\steamapps\common\Moon Sweepers")
GAME_EXE = "MoonSweepers.exe"
MANIFEST_NAME = ".moonsweepers-mahjong-loader.json"
CPP2IL_SUBDIR = "BepInEx/cpp2il"
# Paths BepInEx creates at runtime; removed only with --purge.
RUNTIME_DIRS = ("BepInEx/interop", "BepInEx/unity-libs", "BepInEx/cache", "BepInEx/config", "BepInEx/plugins",
                "BepInEx/patchers")
RUNTIME_FILES = ("BepInEx/LogOutput.log", "BepInEx/ErrorLog.log")


class LoaderError(Exception):
    """A refused or failed loader operation, with a message for the user."""


@dataclass(frozen=True)
class InstallPlan:
    files: tuple[str, ...]  # relative POSIX paths, sorted
    dirs: tuple[str, ...]  # directories that do not exist yet and will be created, sorted


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for block in iter(lambda: handle.read(1 << 20), b""):
            digest.update(block)
    return digest.hexdigest()


def _check_game(game: Path) -> None:
    if not (game / GAME_EXE).is_file():
        raise LoaderError(f"{game} does not contain {GAME_EXE}; pass --game with the Moon Sweepers folder")


def _check_dist(dist: Path) -> None:
    for required in ("winhttp.dll", "doorstop_config.ini", "BepInEx/core/BepInEx.Unity.IL2CPP.dll"):
        if not (dist / required).is_file():
            raise LoaderError(f"{dist} is not a BepInEx IL2CPP distribution (missing {required})")


def stage(dist: Path, cpp2il: Path, out: Path) -> int:
    """Merge dist and the Cpp2IL CLI into out (which must not exist). Returns the number of files staged."""
    _check_dist(dist)
    if not (cpp2il / "Cpp2IL.exe").is_file() or not (cpp2il / "LibCpp2IL.dll").is_file():
        raise LoaderError(f"{cpp2il} is not a Cpp2IL CLI build (needs Cpp2IL.exe and LibCpp2IL.dll)")
    if out.exists():
        raise LoaderError(f"{out} already exists; choose a new staging folder")
    shutil.copytree(dist, out)
    shutil.copytree(cpp2il, out / CPP2IL_SUBDIR)
    return sum(1 for p in out.rglob("*") if p.is_file())


def plan_install(dist: Path, game: Path) -> InstallPlan:
    """List what install would add. Raises LoaderError if any target already exists."""
    _check_dist(dist)
    _check_game(game)
    files = sorted(p.relative_to(dist).as_posix() for p in dist.rglob("*") if p.is_file())
    clashes = [rel for rel in files if (game / rel).exists()]
    if clashes:
        shown = ", ".join(clashes[:5]) + (" ..." if len(clashes) > 5 else "")
        raise LoaderError(f"refusing to overwrite {len(clashes)} existing file(s) in the game folder: {shown}")
    dirs: set[str] = set()
    for rel in files:
        parent = Path(rel).parent
        while parent != Path("."):
            if not (game / parent).exists():
                dirs.add(parent.as_posix())
            parent = parent.parent
    return InstallPlan(files=tuple(files), dirs=tuple(sorted(dirs)))


def install(dist: Path, game: Path) -> InstallPlan:
    if (game / MANIFEST_NAME).exists():
        raise LoaderError("a loader install is already recorded; run uninstall first")
    plan = plan_install(dist, game)
    copied: list[dict[str, str]] = []
    try:
        for rel in plan.files:
            target = game / rel
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(dist / rel, target)
            copied.append({"path": rel, "sha256": _sha256(target)})
    except OSError as exc:
        # Roll back the partial copy so the game folder is left as it was.
        _remove_recorded(game, [c["path"] for c in copied], list(plan.dirs))
        raise LoaderError(f"copy failed and was rolled back: {exc}") from exc
    manifest = {"dist": str(dist), "files": copied, "dirs": list(plan.dirs)}
    (game / MANIFEST_NAME).write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    return plan


def _remove_recorded(game: Path, files: list[str], dirs: list[str]) -> list[str]:
    """Remove recorded files, then recorded dirs that are empty. Returns paths kept back."""
    kept: list[str] = []
    for rel in files:
        target = game / rel
        if target.is_file():
            target.unlink()
    for rel in sorted(dirs, key=lambda d: d.count("/"), reverse=True):
        target = game / rel
        if target.is_dir():
            if any(target.iterdir()):
                kept.append(rel)
            else:
                target.rmdir()
    return kept


def uninstall(game: Path, purge: bool = False) -> list[str]:
    """Remove the recorded install. Returns directories kept because they still hold other files."""
    _check_game(game)
    manifest_path = game / MANIFEST_NAME
    if not manifest_path.is_file():
        raise LoaderError("no loader install is recorded in this game folder")
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    if purge:
        for rel in RUNTIME_DIRS:
            if (game / rel).is_dir():
                shutil.rmtree(game / rel)
        for rel in RUNTIME_FILES:
            if (game / rel).is_file():
                (game / rel).unlink()
    kept = _remove_recorded(game, [f["path"] for f in manifest["files"]], manifest["dirs"])
    manifest_path.unlink()
    return kept


def status(game: Path) -> str:
    _check_game(game)
    manifest_path = game / MANIFEST_NAME
    if not manifest_path.is_file():
        return "LOADER STATUS: not installed"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    changed = [f["path"] for f in manifest["files"]
               if not (game / f["path"]).is_file() or _sha256(game / f["path"]) != f["sha256"]]
    line = f"LOADER STATUS: installed from {manifest['dist']}: {len(manifest['files'])} files"
    return line + (f", {len(changed)} changed or missing: {', '.join(changed[:5])}" if changed else ", all intact")


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest="command", required=True)
    p_stage = sub.add_parser("stage")
    p_stage.add_argument("--dist", type=Path, required=True)
    p_stage.add_argument("--cpp2il", type=Path, required=True)
    p_stage.add_argument("--out", type=Path, required=True)
    p_install = sub.add_parser("install")
    p_install.add_argument("--dist", type=Path, required=True)
    p_install.add_argument("--game", type=Path, default=DEFAULT_GAME)
    p_uninstall = sub.add_parser("uninstall")
    p_uninstall.add_argument("--game", type=Path, default=DEFAULT_GAME)
    p_uninstall.add_argument("--purge", action="store_true", help="also remove files BepInEx generated at runtime")
    p_status = sub.add_parser("status")
    p_status.add_argument("--game", type=Path, default=DEFAULT_GAME)
    args = parser.parse_args(argv)
    try:
        if args.command == "stage":
            count = stage(args.dist.resolve(), args.cpp2il.resolve(), args.out.resolve())
            print(f"LOADER STAGE OK: {count} files in {args.out}")
        elif args.command == "install":
            plan = install(args.dist.resolve(), args.game)
            print(f"LOADER INSTALL OK: {len(plan.files)} files, {len(plan.dirs)} new directories in {args.game}")
        elif args.command == "uninstall":
            kept = uninstall(args.game, purge=args.purge)
            suffix = f"; kept non-empty: {', '.join(kept)}" if kept else ""
            print(f"LOADER UNINSTALL OK{suffix}")
        else:
            print(status(args.game))
    except LoaderError as exc:
        print(f"LOADER ERROR: {exc}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
