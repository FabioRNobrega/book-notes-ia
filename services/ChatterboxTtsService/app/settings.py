from __future__ import annotations

from dataclasses import dataclass
import os
from pathlib import Path


PINNED_MODEL_REVISION = "5bb1f6ee58e50c3b8d408bc82a6d3740c2db6e18"


@dataclass(frozen=True)
class Settings:
    data_dir: Path
    preview_text_path: Path
    reference_path: Path
    output_path: Path
    device: str = "cpu"
    language_id: str = "en"
    model: str = "v3"
    model_revision: str = PINNED_MODEL_REVISION
    max_chunk_chars: int = 280
    sentence_silence_ms: int = 180
    paragraph_silence_ms: int = 420

    @classmethod
    def from_environment(cls) -> "Settings":
        data_dir = Path(os.getenv("CHATTERBOX_DATA_DIR", "/data")).resolve()
        return cls(
            data_dir=data_dir,
            preview_text_path=Path(
                os.getenv("CHATTERBOX_PREVIEW_TEXT_PATH", "/app/config/preview.txt")
            ).resolve(),
            reference_path=data_dir / "reference.wav",
            output_path=data_dir / "synthetic-preview.wav",
            device=os.getenv("CHATTERBOX_DEVICE", "cpu"),
            language_id=os.getenv("CHATTERBOX_LANGUAGE_ID", "en"),
            model=os.getenv("CHATTERBOX_MODEL", "v3"),
            model_revision=os.getenv(
                "CHATTERBOX_MODEL_REVISION", PINNED_MODEL_REVISION
            ),
        ).validated()

    def validated(self) -> "Settings":
        data_dir = self.data_dir.resolve()
        for path in (self.reference_path.resolve(), self.output_path.resolve()):
            if path.parent != data_dir:
                raise ValueError("Audio paths must be direct children of the data directory")
        if self.device != "cpu":
            raise ValueError("This proof of concept supports only CPU inference")
        if self.language_id != "en":
            raise ValueError("This proof of concept supports only English")
        if self.model != "v3":
            raise ValueError("This proof of concept requires Chatterbox Multilingual V3")
        if len(self.model_revision) != 40 or any(
            character not in "0123456789abcdef" for character in self.model_revision
        ):
            raise ValueError("The model revision must be a full lowercase Git commit SHA")
        if self.max_chunk_chars < 80:
            raise ValueError("Maximum chunk length must be at least 80 characters")
        if not self.preview_text_path.is_file():
            raise ValueError(f"Preview text is missing: {self.preview_text_path}")
        if not self.preview_text_path.read_text(encoding="utf-8").strip():
            raise ValueError("Preview text cannot be empty")
        return self

    def preview_text(self) -> str:
        return self.preview_text_path.read_text(encoding="utf-8").strip()

