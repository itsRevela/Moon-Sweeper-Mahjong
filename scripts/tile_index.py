"""Corner indices for NaoMahjong's tile faces (requested 2026-10-25).

Each face gets a small index (the number, or a wind/dragon letter) in its top-left corner. The art keeps its size and
place (shrinking it to make room was tried and rejected), so the index may overlap it; a face-coloured outline keeps it
readable there. The textures are upscaled first so the index stays crisp: the source faces are only 42 x 59 px. The
back and sides (the rest of the texture) are upscaled separately so the orange never bleeds into the face.
"""

from __future__ import annotations

from collections import Counter

from PIL import Image, ImageDraw, ImageFont, features

UPSCALE = 4                 # 62 x 59 -> 248 x 236; a tile face is under 60 px tall on screen
LABEL_ORIGIN = (0.06, 0.03)  # the index's top-left corner (fractions of the face)
LABEL_SIZE = 0.20           # font size, as a fraction of the face's height
LABEL_WEIGHT = 1 / 28       # same-colour stroke, as a fraction of the font size: a touch bolder than Aileron Regular
OUTLINE = 1 / 8             # face-coloured outline around that, as a fraction of the font size

# Suited tiles show their number (red fives a red 5); winds show E S W N; dragons show their colour: G (green), R (red)
# and Wh (white: "W" is West).
LABELS = {
    **{f"{n}{suit}": str(n) for suit in "mps" for n in range(1, 10)},
    **{f"0{suit}": "5" for suit in "mps"},
    **dict(zip((f"{n}z" for n in range(1, 8)), ["E", "S", "W", "N", "Wh", "G", "R"])),
}

INK = (38, 38, 42, 255)
RED = (178, 34, 34, 255)
GREEN = (30, 110, 60, 255)


def label_colour(name: str) -> tuple[int, int, int, int]:
    if name.startswith("0") or name == "7z":
        return RED
    if name == "6z":
        return GREEN
    return INK


def face_u_extent(mesh: dict) -> float:
    """The face's UV width in a canonical tile mesh (fbx_mesh.canonical_tile turns the face to +Z)."""
    us = [uv[0] for normal, uv in zip(mesh["normals"], mesh["uvs"]) if normal[2] > 0.99]
    if not us:
        raise ValueError("the tile mesh has no face pointing at +Z")
    return max(us)


def _border_colour(source: Image.Image, face_px: int) -> tuple[int, ...]:
    height = source.height
    border = [(x, y) for x in range(face_px) for y in (0, height - 1)] + [(x, y) for x in (0, face_px - 1) for y in range(height)]
    return Counter(source.getpixel(p) for p in border).most_common(1)[0][0]


def _font(size: int) -> ImageFont.FreeTypeFont:
    if not features.check("freetype2"):
        raise RuntimeError("Pillow was built without FreeType, which the tile indices need")
    return ImageFont.load_default(size=size)  # Aileron Regular, bundled with Pillow


def index_tile(source: Image.Image, name: str, face_u: float) -> Image.Image:
    """Returns `source` upscaled by UPSCALE with the corner index for tile `name` drawn on its face."""
    if name not in LABELS:
        raise KeyError(f"no index label for tile {name!r}")
    source = source.convert("RGBA")
    face, back, face_px = _upscale(source, face_u)
    fw, fh = face.size

    size = round(fh * LABEL_SIZE)
    font = _font(size)
    colour = label_colour(name)
    weight = max(1, round(size * LABEL_WEIGHT))
    origin = (round(fw * LABEL_ORIGIN[0]), round(fh * LABEL_ORIGIN[1]))
    draw = ImageDraw.Draw(face)
    # The faces are a flat colour behind the art (measured: 223, 223, 220, all around every face's border), so the
    # outline in that colour reads as blank face cut around the index. anchor "lt": origin = the glyphs' top-left.
    draw.text(origin, LABELS[name], font=font, fill=colour, anchor="lt",
              stroke_width=weight + max(1, round(size * OUTLINE)), stroke_fill=_border_colour(source, face_px))
    draw.text(origin, LABELS[name], font=font, fill=colour, anchor="lt", stroke_width=weight, stroke_fill=colour)
    return _join(face, back)


def plain_tile(source: Image.Image, face_u: float) -> Image.Image:
    """Returns `source` upscaled like the indexed tiles but with no index: the face of a tile nobody has seen yet."""
    face, back, _ = _upscale(source.convert("RGBA"), face_u)
    return _join(face, back)


# The face (the left face_u of the texture) and the back are upscaled separately, so the orange never bleeds into it.
def _upscale(source: Image.Image, face_u: float) -> tuple[Image.Image, Image.Image, int]:
    width, height = source.size
    face_px = round(face_u * width)
    if not 0 < face_px < width:
        raise ValueError(f"face width {face_u} leaves no face or no back in a {width} px texture")
    face = source.crop((0, 0, face_px, height)).resize((face_px * UPSCALE, height * UPSCALE), Image.LANCZOS)
    back = source.crop((face_px, 0, width, height)).resize(((width - face_px) * UPSCALE, height * UPSCALE), Image.LANCZOS)
    return face, back, face_px


def _join(face: Image.Image, back: Image.Image) -> Image.Image:
    result = Image.new("RGBA", (face.width + back.width, face.height))
    result.paste(face, (0, 0))
    result.paste(back, (face.width, 0))
    return result
