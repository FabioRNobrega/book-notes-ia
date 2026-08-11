from __future__ import annotations

from pathlib import Path
import struct
import wave

import pytest

from app.settings import (
    PINNED_CHATTERBOX_SOURCE_REVISION,
    PINNED_MODEL_REVISION,
    Settings,
)


EN_PREVIEW_TEXT = """The book Jennifer Government by Max Barry (2003) explores a dystopian future where the U.S. government has been fully privatized and corporate power dominates society. This reflects the rise of neoliberalism in late‑20th/early‑21st century America, emphasizing themes like:

Corporate control over governance: The novel critiques how corporations exert influence over public institutions.
Loss of individual freedom: Citizens are surveilled and commodified under a system that prioritizes profit over people.
Government as an extension of business: Authority is wielded by corporate entities rather than traditional state structures.

Barry’s satire highlights the tensions between capitalism, power dynamics, and personal autonomy in contemporary culture."""

PT_PREVIEW_TEXT = """O livro *Jennifer Government*, de Max Barry (2003), explora um futuro distópico onde o governo dos EUA foi totalmente privatizado e o poder corporativo domina a sociedade. Isso reflete a ascensão do neoliberalismo na América do final do século XX e início do século XXI, enfatizando temas como:

* **Controle corporativo sobre a governança:** O romance critica a forma como as corporações exercem influência sobre as instituições públicas.
* **Perda da liberdade individual:** Os cidadãos são vigiados e transformados em mercadoria sob um sistema que prioriza o lucro em vez das pessoas.
* **O governo como uma extensão dos negócios:** A autoridade é exercida por entidades corporativas em vez de estruturas estatais tradicionais.

A sátira de Barry destaca as tensões entre o capitalismo, as dinâmicas de poder e a autonomia pessoal na cultura contemporânea."""


def write_wav(path: Path, duration_seconds: float = 3.1, sample: int = 1000) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    sample_rate = 16_000
    frame_count = int(sample_rate * duration_seconds)
    with wave.open(str(path), "wb") as audio:
        audio.setnchannels(1)
        audio.setsampwidth(2)
        audio.setframerate(sample_rate)
        audio.writeframes(struct.pack(f"<{frame_count}h", *([sample] * frame_count)))


@pytest.fixture
def settings(tmp_path: Path) -> Settings:
    data_dir = tmp_path / "data"
    config_dir = tmp_path / "config"
    data_dir.mkdir()
    config_dir.mkdir()
    (config_dir / "preview.txt").write_text(EN_PREVIEW_TEXT, encoding="utf-8")
    (config_dir / "pt-preview.txt").write_text(PT_PREVIEW_TEXT, encoding="utf-8")
    return Settings(
        data_dir=data_dir,
        config_dir=config_dir,
        voices_dir=data_dir / "voices",
        outputs_dir=data_dir / "outputs",
        model_revision=PINNED_MODEL_REVISION,
        source_revision=PINNED_CHATTERBOX_SOURCE_REVISION,
    ).validated()
