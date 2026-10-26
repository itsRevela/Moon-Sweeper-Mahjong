"""Collect the plugin's run-time assets from the NaoMahjong clone into one folder.

Writes the 37 tile textures (0-9m, 0-9p, 0-9s, 1-7z; "0" is the red five) upscaled with a corner index on each face
(scripts.tile_index), the table textures, and converts
tile.fbx into the canonical tile mesh (scripts.fbx_mesh --tile-face-u-max 0.68), rounded with a hairline gap
(scripts.tile_shape). It writes a manifest with each
file's SHA-256 so a deploy can be checked against it.

Usage:
    python -m scripts.prepare_assets [--nao tools/NaoMahjong] [--sounds <OpenRiichi Sounds folder>] [--out build/assets]
"""
from __future__ import annotations

import argparse
import hashlib
import json
import shutil
import sys
from pathlib import Path

from PIL import Image

from scripts import fbx_mesh, sound_assets, table_mesh, tile_index, tile_shape

ROOT = Path(__file__).resolve().parent.parent
TILE_NAMES = [f"{n}{suit}" for suit in "mps" for n in range(10)] + [f"{n}z" for n in range(1, 8)]
FACE_U_MAX = 0.68


class AssetError(Exception):
    """A missing or unreadable source asset."""


def _require(path: Path) -> Path:
    if not path.is_file():
        raise AssetError(f"missing source asset {path}")
    return path


# OpenRiichi's sound effects, from the Practice-Riichi-Mahjong checkout the rules were ported from.
DEFAULT_SOUNDS = ROOT / "tools" / "Practice-Riichi-Mahjong" / "bin" / "Data" / "Audio" / "Sounds"


def prepare(nao: Path, out: Path, sounds: Path = DEFAULT_SOUNDS) -> dict[str, str]:
    resources = nao / "Assets" / "Resources"
    sources = {f"tiles/{name}.png": resources / "Textures" / "TileTextures" / f"{name}.png" for name in TILE_NAMES}
    sources["table.png"] = resources / "Textures" / "Environment" / "Table_Dif.png"
    for source in sources.values():
        _require(source)
    for name in sound_assets.SOUNDS:
        _require(sounds / f"{name}.wav")
    fbx = _require(resources / "Fbx" / "tile.fbx")

    if out.exists():
        shutil.rmtree(out)
    (out / "tiles").mkdir(parents=True)
    mesh = fbx_mesh.canonical_tile(fbx_mesh.convert(fbx.read_bytes()), FACE_U_MAX)
    face_u = tile_index.face_u_extent(mesh)
    for target, source in sources.items():
        if target.startswith("tiles/"):
            with Image.open(source) as image:
                tile_index.index_tile(image, Path(target).stem, face_u).save(out / target)
        else:
            shutil.copyfile(source, out / target)
    (out / "tile_mesh.json").write_text(json.dumps(tile_shape.rounded_tile(mesh), separators=(",", ":")), encoding="utf-8")
    sound_assets.prepare(sounds, out)
    split_table_texture(sources["table.png"], out)
    table_mesh.write_all(out)

    manifest = {p.relative_to(out).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest()
                for p in sorted(out.rglob("*")) if p.is_file()}
    (out / "manifest.json").write_text(json.dumps(manifest, indent=1), encoding="utf-8")
    return manifest


# Pixel bands of NaoMahjong's 1024 x 1024 Table_Dif.png (an automatic table painted from above), measured from the
# image: dark wood frame, rosewood rail, felt, and the centre console's notched square (with its four flap lines).
TABLE_FRAME_BAND = (8, 64)       # rows of the top frame band
TABLE_RAIL_BAND = (76, 112)      # rows of the top rosewood band
TABLE_BAND_COLUMNS = (130, 894)  # columns used for both bands (clear of the mitred corners)
TABLE_FELT_BOX = (133, 133, 891, 891)
TABLE_CONSOLE_BOX = (262, 262, 763, 763)
LINE_CLOSING_SIZE = 7            # wider than the painted 2-3 px lines
LINE_THRESHOLD = 6               # luminance steps darker than the closed image (lower catches the felt's grain)
LINE_MASK_GROW = 5               # px: also cover the lines' anti-aliased edges
LINE_FILL_RADIUS = 4.0           # px: Gaussian sigma of the fill from surrounding felt


def split_table_texture(table_png: Path, out: Path) -> None:
    """Cut the flat table painting into textures for the 3D table: frame and rail wood strips, the felt with the
    console's painted outline removed (the console becomes geometry), and the console top itself."""
    from PIL import Image, ImageFilter  # local: only the asset step needs Pillow
    import numpy as np

    image = Image.open(table_png).convert("RGB")
    if image.size != (1024, 1024):
        raise AssetError(f"{table_png} is {image.size}, expected 1024 x 1024 (the bands are measured for that)")
    left, right = TABLE_BAND_COLUMNS
    image.crop((left, TABLE_FRAME_BAND[0], right, TABLE_FRAME_BAND[1])).save(out / "table_frame.png")
    image.crop((left, TABLE_RAIL_BAND[0], right, TABLE_RAIL_BAND[1])).save(out / "table_rail.png")
    image.crop(TABLE_CONSOLE_BOX).save(out / "table_console.png")

    felt = image.crop(TABLE_FELT_BOX)
    original = np.asarray(felt, dtype=np.float32)
    # Find the painted lines (darker than a grey closing of the image), widen the mask over their anti-aliased edges,
    # then fill it from the surrounding felt only: blur(felt * keep) / blur(keep) (normalised convolution).
    closed = felt.filter(ImageFilter.MaxFilter(LINE_CLOSING_SIZE)).filter(ImageFilter.MinFilter(LINE_CLOSING_SIZE))
    line = (np.asarray(closed, dtype=np.float32).mean(axis=2) - original.mean(axis=2)) > LINE_THRESHOLD
    # Only where the console's outline and diagonals are painted; elsewhere a "line" is just the felt's grain.
    line &= _console_line_band(original.shape[0], TABLE_FELT_BOX[0])
    line_image = Image.fromarray((line * 255).astype(np.uint8), "L").filter(ImageFilter.MaxFilter(LINE_MASK_GROW))
    line = np.asarray(line_image) > 0
    keep = (~line).astype(np.float32)
    filled = original.copy()
    unfilled = line.copy()
    # Narrow fill first (keeps the grain's local tone); wider passes only where the mask was too wide to reach felt.
    for sigma in (LINE_FILL_RADIUS, LINE_FILL_RADIUS * 4, LINE_FILL_RADIUS * 16):
        weight = _gaussian_blur(keep, sigma)
        reached = unfilled & (weight > 0.05)
        safe = np.maximum(weight, 1e-6)
        estimate = np.stack([_gaussian_blur(original[..., c] * keep, sigma) / safe for c in range(3)], axis=2)
        filled = np.where(reached[..., None], estimate, filled)
        unfilled &= ~reached
    if unfilled.any():
        raise AssetError(f"could not fill {int(unfilled.sum())} felt pixels under the painted console lines")
    cleaned = filled
    Image.fromarray(np.clip(np.rint(cleaned), 0, 255).astype(np.uint8), "RGB").save(out / "table_felt.png")


CONSOLE_OUTLINE = (269, 756)     # table px: the console square's painted outline (both axes)
CONSOLE_CORNER = 32              # px: the notched corners reach this far in from the outline
CONSOLE_BAND = 7                 # px: half-width of the band searched around each painted line


def _console_line_band(size: int, offset: int):
    """Boolean mask (size x size, in felt-crop pixels) of the band around the console's painted outline, notched
    corners and two diagonals."""
    import numpy as np

    lo, hi = CONSOLE_OUTLINE
    y, x = np.mgrid[0:size, 0:size].astype(np.float32) + offset  # back to table pixels
    inside = (x >= lo - CONSOLE_BAND) & (x <= hi + CONSOLE_BAND) & (y >= lo - CONSOLE_BAND) & (y <= hi + CONSOLE_BAND)
    near_edge = (np.minimum(np.abs(x - lo), np.abs(x - hi)) <= CONSOLE_BAND) | \
                (np.minimum(np.abs(y - lo), np.abs(y - hi)) <= CONSOLE_BAND)
    near_corner = (np.minimum(x - lo, hi - x) <= CONSOLE_CORNER) & (np.minimum(y - lo, hi - y) <= CONSOLE_CORNER)
    # Diagonals y = x and y = lo + hi - x, distance measured perpendicular to the line.
    near_diagonal = (np.abs(y - x) / np.sqrt(2) <= CONSOLE_BAND) | (np.abs(y + x - (lo + hi)) / np.sqrt(2) <= CONSOLE_BAND)
    return inside & (near_edge | near_corner | near_diagonal)


def _gaussian_blur(values, sigma: float):
    """Separable Gaussian blur of a 2-D float array, in float (no 8-bit round trips), edges held."""
    import numpy as np

    radius = int(3 * sigma)
    offsets = np.arange(-radius, radius + 1, dtype=np.float32)
    kernel = np.exp(-(offsets ** 2) / (2 * sigma * sigma))
    kernel /= kernel.sum()
    padded = np.pad(values, radius, mode="edge")
    rows = np.apply_along_axis(lambda r: np.convolve(r, kernel, mode="valid"), 1, padded)
    return np.apply_along_axis(lambda c: np.convolve(c, kernel, mode="valid"), 0, rows)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--nao", type=Path, default=ROOT / "tools" / "NaoMahjong")
    parser.add_argument("--sounds", type=Path, default=DEFAULT_SOUNDS)
    parser.add_argument("--out", type=Path, default=ROOT / "build" / "assets")
    args = parser.parse_args(argv)
    try:
        manifest = prepare(args.nao, args.out, args.sounds)
    except (AssetError, fbx_mesh.FbxError, OSError, ValueError, RuntimeError) as exc:
        print(f"ASSETS ERROR: {exc}", file=sys.stderr)
        return 1
    print(f"ASSETS OK: {len(manifest)} files -> {args.out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
