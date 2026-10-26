"""Decompile named game methods with Ghidra (read-only) for reverse-engineering the hook points.

Prerequisites (see docs/reverse-engineering.md):
  extracted/rva/names.tsv     from scripts/RvaDump
  extracted/ghidra/project    from a one-time analyzeHeadless import of GameAssembly.dll

Usage:
    python -m scripts.decompile "MoonCleaners.MoonLoungePlayer$$LateUpdate" "MoonCleaners.MoonChessTable$$ViewFrom"
    python -m scripts.decompile --type MoonCleaners.MoonLoungePlayer     # every method of a type

Writes extracted/decompiled/<Type$$Method>.c (one file per method) and prints a summary line.
"""
from __future__ import annotations

import argparse
import os
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
NAMES = ROOT / "extracted" / "rva" / "names.tsv"
PROJECT = ROOT / "extracted" / "ghidra" / "project"
OUT = ROOT / "extracted" / "decompiled"
# Ghidra's headless analyser: set GHIDRA_HEADLESS to its analyzeHeadless.bat, or unpack Ghidra into tools/ghidra.
GHIDRA_DEFAULT = Path(os.environ.get("GHIDRA_HEADLESS", ROOT / "tools" / "ghidra" / "support" / "analyzeHeadless.bat"))


class DecompileError(Exception):
    """A refused or failed decompile run, with a message for the user."""


def load_names(path: Path) -> dict[str, str]:
    """Map every method name (including identical-code aliases) to its RVA string."""
    if not path.is_file():
        raise DecompileError(f"{path} is missing; run scripts/RvaDump first")
    by_name: dict[str, str] = {}
    for line in path.read_text(encoding="utf-8").splitlines():
        rva, _, names = line.partition("\t")
        for name in names.split(" | "):
            by_name.setdefault(name, rva)
    return by_name


def resolve(targets: list[str], types: list[str], by_name: dict[str, str]) -> dict[str, str]:
    """Return {method name: rva} for the requested methods and every method of the requested types."""
    wanted: dict[str, str] = {}
    missing = [t for t in targets if t not in by_name]
    if missing:
        raise DecompileError(f"no code address for: {', '.join(missing)}")
    for t in targets:
        wanted[t] = by_name[t]
    for type_name in types:
        prefix = type_name + "$$"
        found = {n: r for n, r in by_name.items() if n.startswith(prefix)}
        if not found:
            raise DecompileError(f"no methods with code found for type {type_name}")
        wanted.update(found)
    return wanted


def safe_file_name(name: str) -> str:
    return "".join(c if c.isalnum() or c in "._$-" else "_" for c in name)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("methods", nargs="*", help='full names like "MoonCleaners.MoonChessTable$$ViewFrom"')
    parser.add_argument("--type", action="append", default=[], help="decompile every method of this type")
    parser.add_argument("--ghidra", type=Path, default=GHIDRA_DEFAULT, help="path to analyzeHeadless.bat")
    parser.add_argument("--project", type=Path, default=PROJECT,
                        help="Ghidra project folder (default: the fully analysed one; the quick, unanalysed import "
                             "is extracted/ghidra/quick)")
    args = parser.parse_args(argv)
    try:
        if not args.methods and not args.type:
            raise DecompileError("name at least one method or --type")
        if not (args.project / "GameAssembly.gpr").is_file():
            raise DecompileError(f"{args.project} has no Ghidra project; see docs/reverse-engineering.md")
        wanted = resolve(args.methods, args.type, load_names(NAMES))
        raw = OUT / "_raw"
        raw.mkdir(parents=True, exist_ok=True)
        targets = raw / "targets.txt"
        targets.write_text("\n".join(sorted(set(wanted.values()))) + "\n", encoding="utf-8")
        command = [str(args.ghidra), str(args.project), "GameAssembly", "-process", "GameAssembly.dll", "-noanalysis",
                   "-readOnly", "-scriptPath", str(ROOT / "scripts" / "ghidra"),
                   "-postScript", "MsDecompile.java", str(NAMES), str(targets), str(raw)]
        result = subprocess.run(command, capture_output=True, text=True, check=False)
        summary = [line for line in result.stdout.splitlines() if "MS decompiled:" in line]
        if result.returncode != 0 or not summary:
            tail = "\n".join((result.stdout + result.stderr).splitlines()[-15:])
            raise DecompileError(f"Ghidra failed (exit {result.returncode}):\n{tail}")
        written = 0
        for name, rva in sorted(wanted.items()):
            source = raw / f"{rva}.c"
            if source.is_file():
                (OUT / f"{safe_file_name(name)}.c").write_text(source.read_text(encoding="utf-8"), encoding="utf-8")
                written += 1
        print(f"DECOMPILE OK: {written} of {len(wanted)} methods -> {OUT}")
    except DecompileError as exc:
        print(f"DECOMPILE ERROR: {exc}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
