from __future__ import annotations

from dataclasses import replace
import json
from pathlib import Path

import pytest

from app.audiobook_manifest import (
    AudiobookManifestError,
    AudiobookManifestRepository,
    complete_track,
    create_manifest,
    manifests_are_compatible,
    sha256_file,
    track_can_be_skipped,
)
from app.audiobook_project import build_project
from app.voice_store import VoiceCompatibility
from conftest import write_audiobook_book, write_wav


VOICE_ID = "21ecfcec-33c1-40db-8bc8-25d078ce2fa9"
COMPATIBILITY = VoiceCompatibility(
    source_revision="5de7a54aa4e5e2baadb0182dde554908b48b85c2",
    model_revision="5bb1f6ee58e50c3b8d408bc82a6d3740c2db6e18",
    model_name="multilingual-v3",
    format_version=2,
)


def _project(roots: tuple[Path, Path]):
    input_root, output_root = roots
    write_audiobook_book(input_root)
    return build_project(
        input_root, output_root, "sample-book", "Sample Book", "intro.txt", "outro.txt"
    )


def test_manifest_round_trips_and_detects_compatibility(
    audiobook_roots: tuple[Path, Path],
) -> None:
    project = _project(audiobook_roots)
    manifest = create_manifest(project, "en", VOICE_ID, COMPATIBILITY)
    repository = AudiobookManifestRepository(project.destination_dir)

    repository.save(manifest)
    loaded = repository.load()

    assert loaded == manifest
    assert manifests_are_compatible(
        loaded, project, "en", VOICE_ID, COMPATIBILITY
    )
    assert not manifests_are_compatible(
        loaded, project, "en", "730ef8a7-dbf7-4478-8776-225ca692de87", COMPATIBILITY
    )
    assert not manifests_are_compatible(
        loaded, project, "en", VOICE_ID, COMPATIBILITY, synthesis_seed=42
    )


def test_legacy_seedless_manifest_is_readable_but_incompatible(
    audiobook_roots: tuple[Path, Path],
) -> None:
    project = _project(audiobook_roots)
    repository = AudiobookManifestRepository(project.destination_dir)
    manifest = create_manifest(project, "en", VOICE_ID, COMPATIBILITY)
    repository.save(manifest)
    raw = json.loads(repository.path.read_text(encoding="utf-8"))
    raw["schema_version"] = 1
    raw.pop("synthesis_seed")
    repository.path.write_text(json.dumps(raw), encoding="utf-8")

    loaded = repository.load()

    assert loaded is not None
    assert loaded.schema_version == 1
    assert loaded.synthesis_seed is None
    assert not manifests_are_compatible(
        loaded, project, "en", VOICE_ID, COMPATIBILITY
    )


def test_manifest_rejects_corrupt_or_unexpected_state(
    audiobook_roots: tuple[Path, Path],
) -> None:
    project = _project(audiobook_roots)
    repository = AudiobookManifestRepository(project.destination_dir)
    repository.path.write_text(json.dumps({"schema_version": 1}), encoding="utf-8")

    with pytest.raises(AudiobookManifestError, match="invalid"):
        repository.load()


def test_completed_track_skip_requires_matching_source_output_and_valid_wav(
    audiobook_roots: tuple[Path, Path],
) -> None:
    project = _project(audiobook_roots)
    manifest = create_manifest(project, "en", VOICE_ID, COMPATIBILITY)
    plan = project.tracks[0]
    write_wav(plan.output_path)
    completed = complete_track(
        manifest, plan.number, sha256_file(plan.output_path), 3.1
    )

    assert track_can_be_skipped(completed.tracks[0], plan, 32)

    plan.output_path.write_bytes(b"changed output")
    assert not track_can_be_skipped(completed.tracks[0], plan, 32)

    write_wav(plan.output_path)
    plan.source_path.write_text("Changed introduction.", encoding="utf-8")
    assert not track_can_be_skipped(completed.tracks[0], plan, 32)


def test_manifest_save_failure_preserves_previous_file(
    audiobook_roots: tuple[Path, Path], monkeypatch
) -> None:
    project = _project(audiobook_roots)
    repository = AudiobookManifestRepository(project.destination_dir)
    manifest = create_manifest(project, "en", VOICE_ID, COMPATIBILITY)
    repository.save(manifest)
    previous = repository.path.read_bytes()

    def fail_replace(_source, _destination):
        raise OSError("replace failed")

    monkeypatch.setattr("app.audiobook_manifest.os.replace", fail_replace)
    with pytest.raises(OSError, match="replace failed"):
        repository.save(replace(manifest, status="completed"))

    assert repository.path.read_bytes() == previous
    assert not list(project.destination_dir.glob(".audiobook-manifest-*.json"))
