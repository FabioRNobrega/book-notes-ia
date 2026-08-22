from __future__ import annotations

from dataclasses import replace
from pathlib import Path

import pytest

from app.audiobook_manifest import AudiobookManifestRepository
from app.audiobook_project import build_project
from app.audiobook_service import AudiobookGenerationError, AudiobookService
from app.conditioning import ConditioningService
from app.engine import SynthesisError
from app.voice_store import LocalVoiceStore
from conftest import write_audiobook_book, write_wav
from test_api import FakeEngine


def _project(roots: tuple[Path, Path], chapter_count: int = 2):
    input_root, output_root = roots
    write_audiobook_book(input_root, chapter_count=chapter_count)
    return build_project(
        input_root, output_root, "sample-book", "Sample Book", "intro.txt", "outro.txt"
    )


def _voice(settings) -> tuple[LocalVoiceStore, str]:
    write_wav(settings.profile("en").reference_path)
    store = LocalVoiceStore(settings.voices_dir, settings.outputs_dir)
    decision = ConditioningService(settings, FakeEngine(), store).resolve_and_load("en")
    return store, decision.voice_id


def test_batch_loads_once_generates_in_order_and_resumes(
    settings, audiobook_roots: tuple[Path, Path]
) -> None:
    project = _project(audiobook_roots)
    store, voice_id = _voice(settings)
    output: list[str] = []
    first_engine = FakeEngine()

    first = AudiobookService(settings, first_engine, store, output.append).generate(
        project, "en", voice_id
    )

    assert first["generated_count"] == 4
    assert first_engine.model_load_count == 1
    assert len(first_engine.load_calls) == 1
    assert all(
        call["synthesis_seed"] == settings.synthesis_seed
        for call in first_engine.synthesis_calls
    )
    assert [
        " ".join(chunk.text for chunk in call["chunks"])
        for call in first_engine.synthesis_calls
    ] == [
        "This is the introduction.",
        "Chapter 1. This is chapter 1.",
        "Chapter 2. This is chapter 2.",
        "This is the outro.",
    ]
    assert all(track.output_path.is_file() for track in project.tracks)

    resume_engine = FakeEngine()
    resumed = AudiobookService(
        settings, resume_engine, store, output.append
    ).generate(project, "en", voice_id)

    assert resumed["generated_count"] == 0
    assert resumed["skipped_count"] == 4
    assert resume_engine.model_load_count == 0
    assert resume_engine.load_calls == []


def test_changed_source_regenerates_only_affected_track_and_force_regenerates_all(
    settings, audiobook_roots: tuple[Path, Path]
) -> None:
    project = _project(audiobook_roots)
    store, voice_id = _voice(settings)
    AudiobookService(settings, FakeEngine(), store, lambda _: None).generate(
        project, "en", voice_id
    )
    project.tracks[1].source_path.write_text("Changed chapter one.", encoding="utf-8")

    changed_engine = FakeEngine()
    changed = AudiobookService(
        settings, changed_engine, store, lambda _: None
    ).generate(project, "en", voice_id)

    assert changed["generated_count"] == 1
    assert len(changed_engine.synthesis_calls) == 1

    force_engine = FakeEngine()
    forced = AudiobookService(
        settings, force_engine, store, lambda _: None
    ).generate(project, "en", voice_id, force=True)

    assert forced["generated_count"] == len(project.tracks)
    assert len(force_engine.synthesis_calls) == len(project.tracks)


def test_invalid_forced_output_preserves_previous_track_and_manifest_progress(
    settings, audiobook_roots: tuple[Path, Path]
) -> None:
    project = _project(audiobook_roots)
    store, voice_id = _voice(settings)
    AudiobookService(settings, FakeEngine(), store, lambda _: None).generate(
        project, "en", voice_id
    )
    previous = project.tracks[0].output_path.read_bytes()

    with pytest.raises(Exception):
        AudiobookService(
            settings,
            FakeEngine(invalid_output=True),
            store,
            lambda _: None,
        ).generate(project, "en", voice_id, force=True)

    assert project.tracks[0].output_path.read_bytes() == previous
    assert not list(project.destination_dir.glob(".*.wav"))
    manifest = AudiobookManifestRepository(project.destination_dir).load()
    assert manifest is not None
    assert manifest.status == "in_progress"
    assert manifest.tracks[0].status == "pending"


def test_later_failure_persists_completed_track_and_next_run_resumes(
    settings, audiobook_roots: tuple[Path, Path]
) -> None:
    project = _project(audiobook_roots)
    store, voice_id = _voice(settings)

    class FailsOnSecondTrack(FakeEngine):
        def __init__(self) -> None:
            super().__init__()
            self.track_calls = 0

        def synthesize(self, **kwargs):
            self.track_calls += 1
            if self.track_calls == 2:
                raise SynthesisError("second track failed")
            return super().synthesize(**kwargs)

    with pytest.raises(SynthesisError, match="second track failed"):
        AudiobookService(
            settings, FailsOnSecondTrack(), store, lambda _: None
        ).generate(project, "en", voice_id)

    interrupted = AudiobookManifestRepository(project.destination_dir).load()
    assert interrupted is not None
    assert interrupted.status == "in_progress"
    assert interrupted.tracks[0].status == "completed"
    assert interrupted.tracks[1].status == "pending"

    resumed = AudiobookService(
        settings, FakeEngine(), store, lambda _: None
    ).generate(project, "en", voice_id)

    assert resumed["skipped_count"] == 1
    assert resumed["generated_count"] == len(project.tracks) - 1


def test_incompatible_manifest_requires_force_and_removes_only_owned_stale_track(
    settings, audiobook_roots: tuple[Path, Path]
) -> None:
    project = _project(audiobook_roots, chapter_count=2)
    store, voice_id = _voice(settings)
    AudiobookService(settings, FakeEngine(), store, lambda _: None).generate(
        project, "en", voice_id
    )
    stale = project.tracks[-1].output_path
    unrelated = project.destination_dir / "keep.wav"
    unrelated.write_bytes(b"unrelated")
    (project.source_dir / "chapter-002.txt").unlink()
    changed = build_project(
        audiobook_roots[0],
        audiobook_roots[1],
        "sample-book",
        "Sample Book",
        "intro.txt",
        "outro.txt",
    )

    with pytest.raises(AudiobookGenerationError, match="FORCE=true"):
        AudiobookService(settings, FakeEngine(), store, lambda _: None).generate(
            changed, "en", voice_id
        )

    AudiobookService(settings, FakeEngine(), store, lambda _: None).generate(
        changed, "en", voice_id, force=True
    )

    assert not stale.exists()
    assert unrelated.read_bytes() == b"unrelated"


def test_unmanifested_wav_and_unsupported_settings_are_rejected(
    settings, audiobook_roots: tuple[Path, Path]
) -> None:
    project = _project(audiobook_roots)
    store, voice_id = _voice(settings)
    (project.destination_dir / "unknown.wav").write_bytes(b"unknown")

    with pytest.raises(AudiobookGenerationError, match="unmanifested"):
        AudiobookService(settings, FakeEngine(), store, lambda _: None).generate(
            project, "en", voice_id
        )
    with pytest.raises(AudiobookGenerationError, match="TTS_LANG=en"):
        AudiobookService(settings, FakeEngine(), store, lambda _: None).generate(
            project, "pt", voice_id, force=True
        )
    with pytest.raises(AudiobookGenerationError, match="Multilingual V3"):
        AudiobookService(
            settings,
            FakeEngine(model_name="nano"),
            store,
            lambda _: None,
        ).generate(project, "en", voice_id, force=True)


def test_changed_seed_requires_force(
    settings, audiobook_roots: tuple[Path, Path]
) -> None:
    project = _project(audiobook_roots)
    store, voice_id = _voice(settings)
    AudiobookService(settings, FakeEngine(), store, lambda _: None).generate(
        project, "en", voice_id
    )
    changed_settings = replace(settings, synthesis_seed=42).validated()

    with pytest.raises(AudiobookGenerationError, match="FORCE=true"):
        AudiobookService(
            changed_settings, FakeEngine(), store, lambda _: None
        ).generate(project, "en", voice_id)

    forced = AudiobookService(
        changed_settings, FakeEngine(), store, lambda _: None
    ).generate(project, "en", voice_id, force=True)

    assert forced["synthesis_seed"] == 42


def test_console_progress_does_not_include_source_prose(
    settings, audiobook_roots: tuple[Path, Path]
) -> None:
    project = _project(audiobook_roots)
    secret = "private prose must not be logged"
    project.tracks[0].source_path.write_text(secret, encoding="utf-8")
    store, voice_id = _voice(settings)
    output: list[str] = []

    AudiobookService(settings, FakeEngine(), store, output.append).generate(
        project, "en", voice_id
    )

    assert secret not in "\n".join(output)
    assert any("chunk" in line for line in output)
