"""The table's sound effects, from OpenRiichi's bin/Data/Audio/Sounds (via Practice-Riichi-Mahjong, GPLv3).

Each WAV becomes raw little-endian float32 samples (interleaved by channel, in [-1, 1]) that the plugin hands
straight to AudioClip.SetData, plus an index (sounds.json) with each sound's channels, sample rate and frame count.
Converting here keeps a WAV parser out of the plugin; the standard library's `wave` reads the PCM.
"""

from __future__ import annotations

import array
import json
import sys
import wave
from pathlib import Path

# TableSound's members, lower-cased: the names of OpenRiichi's sound files.
SOUNDS = ("slide", "draw", "discard", "flip", "riichi", "reveal", "chii", "pon", "kan", "ron", "tsumo")


def convert(source: Path, target: Path) -> dict[str, int]:
    """Writes `source` (8- or 16-bit PCM WAV) to `target` as float32 samples; returns its format."""
    with wave.open(str(source), "rb") as w:
        channels, width, rate, frames = w.getnchannels(), w.getsampwidth(), w.getframerate(), w.getnframes()
        data = w.readframes(frames)

    if width == 2:
        pcm = array.array("h")
        pcm.frombytes(data)
        if sys.byteorder == "big":
            pcm.byteswap()
        samples = array.array("f", (s / 32768.0 for s in pcm))
    elif width == 1:
        samples = array.array("f", ((b - 128) / 128.0 for b in data))
    else:
        raise ValueError(f"{source.name}: {width * 8}-bit samples are not supported (8- or 16-bit PCM only)")

    if sys.byteorder == "big":
        samples.byteswap()
    target.write_bytes(samples.tobytes())
    return {"channels": channels, "rate": rate, "frames": frames}


def prepare(sounds: Path, out: Path) -> dict[str, dict[str, int]]:
    """Converts every table sound from `sounds` into `out`/sounds and writes `out`/sounds.json."""
    sources = {name: sounds / f"{name}.wav" for name in SOUNDS}
    for source in sources.values():
        if not source.is_file():
            raise FileNotFoundError(f"missing sound {source}")

    (out / "sounds").mkdir(parents=True, exist_ok=True)
    index = {name: convert(source, out / "sounds" / f"{name}.f32") for name, source in sources.items()}
    (out / "sounds.json").write_text(json.dumps(index, indent=1), encoding="utf-8")
    return index
