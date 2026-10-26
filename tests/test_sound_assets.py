import array
import json
import wave
from pathlib import Path

import pytest

from scripts import sound_assets


def _wav(path: Path, channels: int, width: int, rate: int, frames: bytes) -> Path:
    with wave.open(str(path), "wb") as w:
        w.setnchannels(channels)
        w.setsampwidth(width)
        w.setframerate(rate)
        w.writeframes(frames)
    return path


def _floats(path: Path) -> list[float]:
    samples = array.array("f")
    samples.frombytes(path.read_bytes())
    return list(samples)


def test_names_match_the_tables_sounds():
    assert sound_assets.SOUNDS == ("slide", "draw", "discard", "flip", "riichi", "reveal", "chii", "pon", "kan", "ron", "tsumo")


def test_convert_turns_16_bit_stereo_into_interleaved_floats(tmp_path):
    pcm = array.array("h", [16384, -16384, 32767, -32768])
    source = _wav(tmp_path / "a.wav", 2, 2, 44100, pcm.tobytes())

    info = sound_assets.convert(source, tmp_path / "a.f32")

    assert info == {"channels": 2, "rate": 44100, "frames": 2}
    assert _floats(tmp_path / "a.f32") == pytest.approx([0.5, -0.5, 32767 / 32768, -1.0])


def test_convert_turns_8_bit_unsigned_mono_into_floats(tmp_path):
    source = _wav(tmp_path / "b.wav", 1, 1, 22050, bytes([128, 192, 64]))

    info = sound_assets.convert(source, tmp_path / "b.f32")

    assert info == {"channels": 1, "rate": 22050, "frames": 3}
    assert _floats(tmp_path / "b.f32") == pytest.approx([0.0, 0.5, -0.5])


def test_convert_rejects_unsupported_sample_widths(tmp_path):
    source = _wav(tmp_path / "c.wav", 1, 3, 44100, bytes(6))

    with pytest.raises(ValueError, match="24-bit"):
        sound_assets.convert(source, tmp_path / "c.f32")


def test_prepare_writes_every_sound_and_an_index(tmp_path):
    sounds = tmp_path / "Sounds"
    sounds.mkdir()
    for name in sound_assets.SOUNDS:
        _wav(sounds / f"{name}.wav", 2, 2, 44100, array.array("h", [0, 0]).tobytes())

    index = sound_assets.prepare(sounds, tmp_path / "out")

    assert set(index) == set(sound_assets.SOUNDS)
    assert json.loads((tmp_path / "out" / "sounds.json").read_text(encoding="utf-8")) == index
    assert all((tmp_path / "out" / "sounds" / f"{name}.f32").is_file() for name in sound_assets.SOUNDS)


def test_prepare_names_a_missing_sound(tmp_path):
    sounds = tmp_path / "Sounds"
    sounds.mkdir()

    with pytest.raises(FileNotFoundError, match="slide.wav"):
        sound_assets.prepare(sounds, tmp_path / "out")
