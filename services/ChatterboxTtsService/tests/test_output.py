from __future__ import annotations

import hashlib
from pathlib import Path

import pytest

from app.audio_validation import OutputWriteError, validate_output
from app.main import PreviewService
from app.settings import Settings
from conftest import write_wav
from test_api import FakeEngine


@pytest.mark.parametrize("language_id", ["en", "pt"])
def test_success_writes_per_voice_output_and_preserves_reference(
    settings: Settings, language_id: str
) -> None:
    profile = settings.profile(language_id)
    write_wav(profile.reference_path)
    reference_checksum = hashlib.sha256(profile.reference_path.read_bytes()).hexdigest()

    response = PreviewService(settings, FakeEngine()).generate(language_id)
    output_path = Path(response["output_path"])

    assert validate_output(output_path) > 0
    assert output_path == (
        settings.outputs_dir / response["voice_id"] / f"preview-{language_id}.wav"
    )
    assert hashlib.sha256(profile.reference_path.read_bytes()).hexdigest() == reference_checksum


def test_invalid_engine_output_does_not_replace_previous_preview(settings) -> None:
    profile = settings.profile("en")
    write_wav(profile.reference_path)
    created = PreviewService(settings, FakeEngine()).generate("en")
    output_path = Path(created["output_path"])
    previous = output_path.read_bytes()

    with pytest.raises(OutputWriteError):
        PreviewService(settings, FakeEngine(invalid_output=True)).generate("en")

    assert output_path.read_bytes() == previous
    assert not list(output_path.parent.glob(".preview-*.wav"))


def test_settings_reject_storage_paths_outside_data_directory(
    settings, tmp_path: Path
) -> None:
    with pytest.raises(ValueError, match="Voices directory"):
        Settings(
            data_dir=settings.data_dir,
            config_dir=settings.config_dir,
            voices_dir=tmp_path / "voices",
            outputs_dir=settings.outputs_dir,
            model_revision=settings.model_revision,
            source_revision=settings.source_revision,
        ).validated()


def test_settings_reject_missing_portuguese_preview(settings) -> None:
    (settings.config_dir / "pt-preview.txt").unlink()

    with pytest.raises(ValueError, match="Preview text is missing"):
        settings.validated()


@pytest.mark.parametrize(
    ("device", "model"),
    [("cuda", "v3"), ("cpu", "v2")],
)
def test_settings_reject_unsupported_runtime_options(
    settings, device: str, model: str
) -> None:
    with pytest.raises(ValueError):
        Settings(
            data_dir=settings.data_dir,
            config_dir=settings.config_dir,
            voices_dir=settings.voices_dir,
            outputs_dir=settings.outputs_dir,
            device=device,
            model=model,
            model_revision=settings.model_revision,
            source_revision=settings.source_revision,
        ).validated()


@pytest.mark.parametrize("seed", [0, -1, 2_147_483_648])
def test_settings_reject_invalid_synthesis_seed(settings, seed: int) -> None:
    from dataclasses import replace

    with pytest.raises(ValueError, match="synthesis seed"):
        replace(settings, synthesis_seed=seed).validated()
