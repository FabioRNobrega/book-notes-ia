from __future__ import annotations

import hashlib
from pathlib import Path

import pytest

from app.engine import SynthesisResult
from app.main import OutputWriteError, PreviewService, _validate_output
from app.settings import PINNED_MODEL_REVISION, Settings
from conftest import write_wav
from test_api import FakeEngine


def test_success_atomically_replaces_output_and_preserves_reference(settings) -> None:
    write_wav(settings.reference_path)
    reference_checksum = hashlib.sha256(settings.reference_path.read_bytes()).hexdigest()
    settings.output_path.write_bytes(b"old-preview")

    PreviewService(settings, FakeEngine()).generate()

    assert _validate_output(settings.output_path) > 0
    assert hashlib.sha256(settings.reference_path.read_bytes()).hexdigest() == reference_checksum
    assert settings.output_path.read_bytes() != b"old-preview"


def test_invalid_engine_output_does_not_replace_previous_preview(settings) -> None:
    class InvalidOutputEngine(FakeEngine):
        def synthesize(self, **kwargs):
            kwargs["output_path"].write_bytes(b"not-wave")
            return SynthesisResult(16_000, 0, 1)

    write_wav(settings.reference_path)
    settings.output_path.write_bytes(b"known-good")

    with pytest.raises(OutputWriteError):
        PreviewService(settings, InvalidOutputEngine()).generate()

    assert settings.output_path.read_bytes() == b"known-good"


def test_settings_reject_audio_path_outside_data_directory(tmp_path: Path) -> None:
    data_dir = tmp_path / "data"
    data_dir.mkdir()
    preview_path = tmp_path / "preview.txt"
    preview_path.write_text("Preview text.", encoding="utf-8")

    with pytest.raises(ValueError, match="direct children"):
        Settings(
            data_dir=data_dir,
            preview_text_path=preview_path,
            reference_path=tmp_path / "reference.wav",
            output_path=data_dir / "synthetic-preview.wav",
            model_revision=PINNED_MODEL_REVISION,
        ).validated()


def test_settings_reject_missing_preview_text(tmp_path: Path) -> None:
    data_dir = tmp_path / "data"
    data_dir.mkdir()

    with pytest.raises(ValueError, match="Preview text is missing"):
        Settings(
            data_dir=data_dir,
            preview_text_path=tmp_path / "missing.txt",
            reference_path=data_dir / "reference.wav",
            output_path=data_dir / "synthetic-preview.wav",
            model_revision=PINNED_MODEL_REVISION,
        ).validated()


@pytest.mark.parametrize(
    ("device", "language_id", "model"),
    [("cuda", "en", "v3"), ("cpu", "pt", "v3"), ("cpu", "en", "v2")],
)
def test_settings_reject_unsupported_runtime_options(
    settings, device: str, language_id: str, model: str
) -> None:
    with pytest.raises(ValueError):
        Settings(
            data_dir=settings.data_dir,
            preview_text_path=settings.preview_text_path,
            reference_path=settings.reference_path,
            output_path=settings.output_path,
            device=device,
            language_id=language_id,
            model=model,
            model_revision=PINNED_MODEL_REVISION,
        ).validated()
