from __future__ import annotations

import asyncio
from contextlib import asynccontextmanager
import logging
import os
from pathlib import Path
import resource
import tempfile
import threading
import time

from fastapi import FastAPI, HTTPException, Query

from .audio_validation import (
    InvalidReferenceError,
    OutputWriteError,
    validate_output as _validate_output,
    validate_reference as _validate_reference,
)
from .chunking import chunk_text
from .conditioning import ConditioningService, concise_error
from .engine import ChatterboxEngine, SynthesisEngine, SynthesisError
from .progress import ProgressTracker
from .settings import Settings, UnsupportedLanguageError
from .voice_store import (
    LocalVoiceStore,
    VoiceSelectionError,
    VoiceStoreError,
)


logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(name)s %(message)s")
LOGGER = logging.getLogger(__name__)


class PreviewService:
    def __init__(
        self,
        settings: Settings,
        engine: SynthesisEngine,
        voice_store: LocalVoiceStore | None = None,
        progress_tracker: ProgressTracker | None = None,
    ) -> None:
        self.settings = settings
        self.engine = engine
        self.voice_store = voice_store or LocalVoiceStore(
            settings.voices_dir, settings.outputs_dir
        )
        self.progress_tracker = progress_tracker or ProgressTracker()
        self._inference_lock = threading.Lock()

    def generate(
        self, language_id: str = "en", voice_id: str | None = None
    ) -> dict[str, object]:
        with self._inference_lock:
            self.progress_tracker.start(language_id)
            try:
                return self._generate(language_id, voice_id)
            except Exception as error:
                concise = _concise_error(error)
                self.progress_tracker.fail(concise)
                LOGGER.error("Preview failed language=%s error=%s", language_id, concise)
                raise

    def _generate(
        self, language_id: str, voice_id: str | None
    ) -> dict[str, object]:
        if not self.engine.ready:
            raise SynthesisError(
                self.engine.load_error or "Chatterbox model is still loading"
            )

        profile = self.settings.profile(language_id)
        chunks = chunk_text(profile.preview_text(), self.settings.max_chunk_chars)
        started = time.perf_counter()
        decision = ConditioningService(
            self.settings,
            self.engine,
            self.voice_store,
            self._progress,
        ).resolve_and_load(profile.language_id, voice_id)

        output_path = self.voice_store.output_path(
            decision.voice_id,
            profile.language_id,
            self.engine.model_name,
        )
        output_path.parent.mkdir(parents=True, exist_ok=True)
        descriptor, temporary_name = tempfile.mkstemp(
            prefix=f".preview-{profile.language_id}-{self.engine.model_name}-",
            suffix=".wav",
            dir=output_path.parent,
        )
        os.close(descriptor)
        temporary_path = Path(temporary_name)
        temporary_path.unlink()

        self._progress(
            "synthesizing",
            f"Synthesizing {len(chunks)} text chunks",
            30,
            voice_id=decision.voice_id,
            chunk_index=0,
            chunk_count=len(chunks),
        )

        def report_chunk(index: int, count: int) -> None:
            percent = 30 + int(index / count * 60)
            self._progress(
                "synthesizing",
                f"Completed audio chunk {index}/{count}",
                percent,
                voice_id=decision.voice_id,
                chunk_index=index,
                chunk_count=count,
            )

        try:
            result = self.engine.synthesize(
                chunks=chunks,
                output_path=temporary_path,
                language_id=profile.language_id,
                sentence_silence_ms=self.settings.sentence_silence_ms,
                paragraph_silence_ms=self.settings.paragraph_silence_ms,
                synthesis_seed=self.settings.synthesis_seed,
                progress_callback=report_chunk,
            )
            self._progress("validating-output", "Validating generated audio", 95)
            validated_duration = _validate_output(
                temporary_path, self.settings.minimum_pcm_peak
            )
            self._progress("publishing-output", "Publishing preview audio", 98)
            os.replace(temporary_path, output_path)
        except SynthesisError:
            raise
        except Exception as error:
            raise OutputWriteError(_concise_error(error)) from error
        finally:
            temporary_path.unlink(missing_ok=True)

        elapsed = time.perf_counter() - started
        real_time_factor = elapsed / validated_duration
        response = {
            "voice_id": decision.voice_id,
            "conditioning_status": decision.status,
            "model_conditioning_status": decision.status,
            "conditioning_path": str(decision.conditioning_path),
            "output_path": str(output_path),
            "device": self.engine.device,
            "model": self.engine.model_name,
            "model_revision": self.engine.model_revision,
            "synthesis_seed": self.settings.synthesis_seed,
            "language_id": profile.language_id,
            "elapsed_seconds": round(elapsed, 3),
            "output_duration_seconds": round(validated_duration, 3),
            "real_time_factor": round(real_time_factor, 3),
            "chunk_count": result.chunk_count,
            "peak_memory_mb": round(_peak_memory_mb(), 1),
        }
        self.progress_tracker.complete()
        LOGGER.info(
            "Preview generated voice_id=%s language=%s model=%s conditioning=%s elapsed_seconds=%.3f duration_seconds=%.3f rtf=%.3f chunks=%d peak_memory_mb=%.1f",
            decision.voice_id,
            profile.language_id,
            self.engine.model_name,
            decision.status,
            elapsed,
            validated_duration,
            real_time_factor,
            result.chunk_count,
            response["peak_memory_mb"],
        )
        return response

    def _progress(
        self,
        stage: str,
        message: str,
        percent: int,
        *,
        voice_id: str | None = None,
        chunk_index: int | None = None,
        chunk_count: int | None = None,
    ) -> None:
        snapshot = self.progress_tracker.update(
            stage,
            message,
            percent,
            voice_id=voice_id,
            chunk_index=chunk_index,
            chunk_count=chunk_count,
        )
        LOGGER.info(
            "Preview progress percent=%d stage=%s language=%s voice_id=%s chunks=%d/%d message=%s",
            snapshot.percent,
            snapshot.stage,
            snapshot.language_id,
            snapshot.voice_id or "pending",
            snapshot.chunk_index,
            snapshot.chunk_count,
            snapshot.message,
        )


def create_app(
    settings: Settings | None = None,
    engine: SynthesisEngine | None = None,
    auto_load: bool = True,
) -> FastAPI:
    resolved_settings = settings or Settings.from_environment()
    resolved_engine = engine or ChatterboxEngine(
        resolved_settings.selected_model_revision,
        resolved_settings.model_name,
    )
    preview_service = PreviewService(resolved_settings, resolved_engine)

    for language_id in resolved_settings.supported_languages:
        profile = resolved_settings.profile(language_id)
        try:
            migrated_voice_id = preview_service.voice_store.backfill_legacy_reference(
                language_id, profile.reference_path
            )
            if migrated_voice_id:
                LOGGER.info(
                    "Archived legacy reference language=%s voice_id=%s",
                    language_id,
                    migrated_voice_id,
                )
        except VoiceStoreError as error:
            LOGGER.error(
                "Legacy reference backfill failed language=%s error=%s",
                language_id,
                _concise_error(error),
            )

    @asynccontextmanager
    async def lifespan(_: FastAPI):
        if auto_load:
            threading.Thread(
                target=resolved_engine.load,
                name="chatterbox-model-loader",
                daemon=True,
            ).start()
        yield

    application = FastAPI(
        title="Chatterbox Custom Voice POC",
        version="0.3.0",
        lifespan=lifespan,
    )

    @application.get("/health")
    async def health() -> dict[str, object]:
        return {
            "status": "alive",
            "model_ready": resolved_engine.ready,
            "device": resolved_engine.device,
            "model": resolved_engine.model_name,
            "model_revision": resolved_engine.model_revision,
            "synthesis_seed": resolved_settings.synthesis_seed,
            "supported_languages": list(resolved_settings.supported_languages),
            "load_error": resolved_engine.load_error,
        }

    @application.get("/progress")
    async def progress() -> dict[str, object]:
        return preview_service.progress_tracker.snapshot().to_dict()

    @application.get("/voices")
    async def voices(language: str | None = None) -> dict[str, object]:
        try:
            normalized = None
            if language is not None:
                normalized = resolved_settings.profile(language).language_id
            items = preview_service.voice_store.voice_summaries(normalized)
            return {"count": len(items), "voices": items}
        except (UnsupportedLanguageError, VoiceSelectionError) as error:
            raise HTTPException(status_code=422, detail=str(error)) from error
        except VoiceStoreError as error:
            raise HTTPException(status_code=500, detail=str(error)) from error

    @application.post("/preview")
    async def preview(
        language: str = Query(default="en", min_length=2, max_length=5),
        voice_id: str | None = Query(default=None, min_length=36, max_length=36),
    ) -> dict[str, object]:
        try:
            return await asyncio.to_thread(
                preview_service.generate, language, voice_id
            )
        except (
            UnsupportedLanguageError,
            InvalidReferenceError,
            VoiceSelectionError,
        ) as error:
            raise HTTPException(status_code=422, detail=str(error)) from error
        except SynthesisError as error:
            status_code = 503 if not resolved_engine.ready else 500
            raise HTTPException(status_code=status_code, detail=str(error)) from error
        except (VoiceStoreError, OutputWriteError, OSError) as error:
            raise HTTPException(status_code=500, detail=_concise_error(error)) from error

    return application


def _peak_memory_mb() -> float:
    return resource.getrusage(resource.RUSAGE_SELF).ru_maxrss / 1024


def _concise_error(error: Exception) -> str:
    return concise_error(error)


app = create_app()
