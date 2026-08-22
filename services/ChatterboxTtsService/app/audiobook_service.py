from __future__ import annotations

from dataclasses import replace
import os
from pathlib import Path
import tempfile
from typing import Callable

from .audiobook_manifest import (
    AudiobookManifest,
    AudiobookManifestRepository,
    complete_track,
    create_manifest,
    ManifestTrack,
    manifests_are_compatible,
    sha256_file,
    track_can_be_skipped,
)
from .audiobook_project import AudiobookProject
from .audio_validation import validate_output
from .chunking import chunk_text
from .conditioning import ConditioningService
from .engine import SynthesisEngine, SynthesisError
from .settings import AUDIOBOOK_LANGUAGE_ID, AUDIOBOOK_MODEL_NAME, Settings
from .voice_store import LocalVoiceStore


class AudiobookGenerationError(RuntimeError):
    pass


OutputCallback = Callable[[str], None]


class AudiobookService:
    def __init__(
        self,
        settings: Settings,
        engine: SynthesisEngine,
        voice_store: LocalVoiceStore | None = None,
        output: OutputCallback = print,
    ) -> None:
        self.settings = settings
        self.engine = engine
        self.voice_store = voice_store or LocalVoiceStore(
            settings.voices_dir, settings.outputs_dir
        )
        self.output = output

    def generate(
        self,
        project: AudiobookProject,
        language_id: str,
        voice_id: str,
        force: bool = False,
    ) -> dict[str, object]:
        if language_id != AUDIOBOOK_LANGUAGE_ID:
            raise AudiobookGenerationError("Audiobook POC supports only TTS_LANG=en")
        if self.engine.model_name != AUDIOBOOK_MODEL_NAME:
            raise AudiobookGenerationError(
                "Audiobook POC requires Chatterbox Multilingual V3"
            )

        conditioning = ConditioningService(
            self.settings,
            self.engine,
            self.voice_store,
            self._conditioning_progress,
        )
        decision, compatibility = conditioning.resolve(language_id, voice_id)
        repository = AudiobookManifestRepository(project.destination_dir)
        previous = repository.load()
        if previous is not None and not manifests_are_compatible(
            previous,
            project,
            language_id,
            decision.voice_id,
            compatibility,
            self.settings.synthesis_seed,
        ) and not force:
            raise AudiobookGenerationError(
                "Existing audiobook manifest is incompatible; rerun with FORCE=true"
            )
        if previous is None and not force and any(project.destination_dir.glob("*.wav")):
            raise AudiobookGenerationError(
                "Destination contains unmanifested WAV files; rerun with FORCE=true"
            )

        manifest = create_manifest(
            project,
            language_id,
            decision.voice_id,
            compatibility,
            synthesis_seed=self.settings.synthesis_seed,
            previous=previous,
            force=force,
        )
        pending = []
        verified_tracks: list[ManifestTrack] = []
        for plan, track in zip(project.tracks, manifest.tracks, strict=True):
            if not force and track_can_be_skipped(
                track, plan, self.settings.minimum_pcm_peak
            ):
                verified_tracks.append(track)
            else:
                pending.append(plan)
                verified_tracks.append(
                    replace(
                        track,
                        status="pending",
                        output_sha256=None,
                        output_duration_seconds=None,
                    )
                )
        manifest = replace(manifest, tracks=tuple(verified_tracks))
        repository.save(manifest)
        skipped = len(project.tracks) - len(pending)
        pending_numbers = {plan.number for plan in pending}
        for plan, track in zip(project.tracks, manifest.tracks, strict=True):
            if plan.number not in pending_numbers:
                self.output(
                    f"Track {plan.number:03d}/{len(project.tracks):03d} skipped: "
                    f"{track.output_name}"
                )

        if pending:
            self.output("Loading Chatterbox Multilingual V3")
            self.engine.load()
            if not self.engine.ready:
                raise SynthesisError(
                    self.engine.load_error or "Chatterbox model failed to load"
                )
            conditioning.load(decision, compatibility)
            self.output(f"Voice ready: {decision.voice_id}")

        generated = 0
        for plan in pending:
            self.output(
                f"Track {plan.number:03d}/{len(project.tracks):03d} generating: "
                f"{plan.source_name}"
            )
            manifest = self._generate_track(
                project, plan.number, manifest, repository, language_id
            )
            generated += 1

        manifest = replace(manifest, status="completed")
        repository.save(manifest)
        if force and previous is not None:
            self._remove_obsolete_owned_outputs(previous, manifest, project.destination_dir)
        self.output(
            f"Audiobook complete: generated={generated} skipped={skipped} "
            f"tracks={len(project.tracks)} seed={self.settings.synthesis_seed} "
            f"destination={project.destination_dir}"
        )
        return {
            "destination": str(project.destination_dir),
            "manifest": str(repository.path),
            "track_count": len(project.tracks),
            "generated_count": generated,
            "skipped_count": skipped,
            "voice_id": decision.voice_id,
            "synthesis_seed": self.settings.synthesis_seed,
        }

    def _generate_track(
        self,
        project: AudiobookProject,
        number: int,
        manifest: AudiobookManifest,
        repository: AudiobookManifestRepository,
        language_id: str,
    ) -> AudiobookManifest:
        plan = project.tracks[number - 1]
        expected_source_sha256 = manifest.tracks[number - 1].source_sha256
        try:
            if sha256_file(plan.source_path) != expected_source_sha256:
                raise AudiobookGenerationError(
                    f"Source changed during generation: {plan.source_name}; rerun to resume"
                )
            text = plan.source_path.read_text(encoding="utf-8").strip()
        except (OSError, UnicodeError) as error:
            raise AudiobookGenerationError(
                f"Source could not be read during generation: {plan.source_name}"
            ) from error
        if not text:
            raise AudiobookGenerationError(
                f"Source became empty during generation: {plan.source_name}"
            )
        chunks = chunk_text(text, self.settings.max_chunk_chars)
        descriptor, name = tempfile.mkstemp(
            prefix=f".{project.book_name}-{number:03d}-",
            suffix=".wav",
            dir=project.destination_dir,
        )
        os.close(descriptor)
        temporary = Path(name)
        temporary.unlink()

        def report_chunk(index: int, count: int) -> None:
            self.output(
                f"Track {number:03d}/{len(project.tracks):03d} "
                f"chunk {index}/{count}"
            )

        try:
            self.engine.synthesize(
                chunks=chunks,
                output_path=temporary,
                language_id=language_id,
                sentence_silence_ms=self.settings.sentence_silence_ms,
                paragraph_silence_ms=self.settings.paragraph_silence_ms,
                synthesis_seed=self.settings.synthesis_seed,
                progress_callback=report_chunk,
            )
            duration = validate_output(temporary, self.settings.minimum_pcm_peak)
            output_sha256 = sha256_file(temporary)
            if sha256_file(plan.source_path) != expected_source_sha256:
                raise AudiobookGenerationError(
                    f"Source changed during synthesis: {plan.source_name}; rerun to resume"
                )
            os.replace(temporary, plan.output_path)
            updated = complete_track(
                manifest, number, output_sha256, duration
            )
            repository.save(updated)
            self.output(f"Track {number:03d} completed: {plan.output_path}")
            return updated
        finally:
            temporary.unlink(missing_ok=True)

    def _conditioning_progress(
        self,
        stage: str,
        message: str,
        percent: int,
        **_: object,
    ) -> None:
        self.output(f"Voice {percent:02d}% {stage}: {message}")

    @staticmethod
    def _remove_obsolete_owned_outputs(
        previous: AudiobookManifest,
        current: AudiobookManifest,
        destination: Path,
    ) -> None:
        current_names = {track.output_name for track in current.tracks}
        for track in previous.tracks:
            if track.output_name in current_names:
                continue
            candidate = (destination / track.output_name).resolve()
            if candidate.parent == destination and candidate.is_file():
                candidate.unlink()
