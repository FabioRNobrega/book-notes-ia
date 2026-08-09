from __future__ import annotations

from pathlib import Path
import struct
import wave

import pytest

from app.settings import PINNED_MODEL_REVISION, Settings


PREVIEW_TEXT = """The book Jennifer Government by Max Barry (2003) explores a dystopian future where the U.S. government has been fully privatized and corporate power dominates society. This reflects the rise of neoliberalism in late‑20th/early‑21st century America, emphasizing themes like:

Corporate control over governance: The novel critiques how corporations exert influence over public institutions.
Loss of individual freedom: Citizens are surveilled and commodified under a system that prioritizes profit over people.
Government as an extension of business: Authority is wielded by corporate entities rather than traditional state structures.

Barry’s satire highlights the tensions between capitalism, power dynamics, and personal autonomy in contemporary culture."""


def write_wav(path: Path, duration_seconds: float = 0.05) -> None:
    sample_rate = 16_000
    frame_count = int(sample_rate * duration_seconds)
    with wave.open(str(path), "wb") as audio:
        audio.setnchannels(1)
        audio.setsampwidth(2)
        audio.setframerate(sample_rate)
        audio.writeframes(struct.pack(f"<{frame_count}h", *([0] * frame_count)))


@pytest.fixture
def settings(tmp_path: Path) -> Settings:
    data_dir = tmp_path / "data"
    data_dir.mkdir()
    preview_path = tmp_path / "preview.txt"
    preview_path.write_text(PREVIEW_TEXT, encoding="utf-8")
    return Settings(
        data_dir=data_dir,
        preview_text_path=preview_path,
        reference_path=data_dir / "reference.wav",
        output_path=data_dir / "synthetic-preview.wav",
        model_revision=PINNED_MODEL_REVISION,
    ).validated()

