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
import wave

from fastapi import FastAPI, HTTPException

from .chunking import chunk_text
from .engine import ChatterboxEngine, SynthesisEngine, SynthesisError
from .settings import Settings


logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(name)s %(message)s")
LOGGER = logging.getLogger(__name__)


class InvalidReferenceError(ValueError):
    pass


class OutputWriteError(RuntimeError):
    pass


class PreviewService:
    def __init__(self, settings: Settings, engine: SynthesisEngine) -> None:
        self.settings = settings
        self.engine = engine
        self._inference_lock = threading.Lock()

    def generate(self) -> dict[str, object]:
        with self._inference_lock:
            if not self.engine.ready:
                raise SynthesisError(
                    self.engine.load_error or "Chatterbox model is still loading"
                )

            _validate_reference(self.settings.reference_path)
            chunks = chunk_text(
                self.settings.preview_text(), self.settings.max_chunk_chars
            )
            self.settings.data_dir.mkdir(parents=True, exist_ok=True)
            descriptor, temporary_name = tempfile.mkstemp(
                prefix=".synthetic-preview-",
                suffix=".wav",
                dir=self.settings.data_dir,
            )
            os.close(descriptor)
            temporary_path = Path(temporary_name)
            temporary_path.unlink()
            started = time.perf_counter()

            try:
                result = self.engine.synthesize(
                    chunks=chunks,
                    reference_path=self.settings.reference_path,
                    output_path=temporary_path,
                    language_id=self.settings.language_id,
                    sentence_silence_ms=self.settings.sentence_silence_ms,
                    paragraph_silence_ms=self.settings.paragraph_silence_ms,
                )
                validated_duration = _validate_output(temporary_path)
                os.replace(temporary_path, self.settings.output_path)
            except (InvalidReferenceError, SynthesisError):
                raise
            except Exception as error:
                raise OutputWriteError(_concise_error(error)) from error
            finally:
                temporary_path.unlink(missing_ok=True)

            elapsed = time.perf_counter() - started
            response = {
                "output_path": str(self.settings.output_path),
                "device": self.engine.device,
                "model": self.engine.model_name,
                "model_revision": self.engine.model_revision,
                "language_id": self.settings.language_id,
                "elapsed_seconds": round(elapsed, 3),
                "output_duration_seconds": round(validated_duration, 3),
                "chunk_count": result.chunk_count,
                "peak_memory_mb": round(_peak_memory_mb(), 1),
            }
            LOGGER.info(
                "Preview generated output=%s device=%s elapsed_seconds=%.3f duration_seconds=%.3f chunks=%d peak_memory_mb=%.1f",
                self.settings.output_path,
                self.engine.device,
                elapsed,
                validated_duration,
                result.chunk_count,
                response["peak_memory_mb"],
            )
            return response


def create_app(
    settings: Settings | None = None,
    engine: SynthesisEngine | None = None,
    auto_load: bool = True,
) -> FastAPI:
    resolved_settings = settings or Settings.from_environment()
    resolved_engine = engine or ChatterboxEngine(resolved_settings.model_revision)
    preview_service = PreviewService(resolved_settings, resolved_engine)

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
        version="0.1.0",
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
            "language_id": resolved_settings.language_id,
            "load_error": resolved_engine.load_error,
        }

    @application.post("/preview")
    async def preview() -> dict[str, object]:
        try:
            return await asyncio.to_thread(preview_service.generate)
        except InvalidReferenceError as error:
            raise HTTPException(status_code=422, detail=str(error)) from error
        except SynthesisError as error:
            status_code = 503 if not resolved_engine.ready else 500
            raise HTTPException(status_code=status_code, detail=str(error)) from error
        except OutputWriteError as error:
            raise HTTPException(status_code=500, detail=str(error)) from error

    return application


def _validate_reference(path: Path) -> None:
    if not path.is_file():
        raise InvalidReferenceError(f"Reference audio is missing: {path}")
    try:
        with wave.open(str(path), "rb") as audio:
            if audio.getnframes() <= 0:
                raise InvalidReferenceError("Reference audio contains no samples")
            if audio.getsampwidth() != 2:
                raise InvalidReferenceError("Reference audio must be 16-bit PCM WAV")
            if audio.getnchannels() not in (1, 2):
                raise InvalidReferenceError("Reference audio must be mono or stereo")
    except InvalidReferenceError:
        raise
    except (wave.Error, EOFError, OSError) as error:
        raise InvalidReferenceError("Reference audio is not a readable PCM WAV") from error


def _validate_output(path: Path) -> float:
    if not path.is_file() or path.stat().st_size <= 44:
        raise OutputWriteError("Synthesis did not produce a non-empty WAV file")
    try:
        with wave.open(str(path), "rb") as audio:
            if audio.getnframes() <= 0 or audio.getframerate() <= 0:
                raise OutputWriteError("Synthesized WAV contains no audio samples")
            return audio.getnframes() / audio.getframerate()
    except OutputWriteError:
        raise
    except (wave.Error, EOFError, OSError) as error:
        raise OutputWriteError("Synthesized output is not a readable WAV file") from error


def _peak_memory_mb() -> float:
    return resource.getrusage(resource.RUSAGE_SELF).ru_maxrss / 1024


def _concise_error(error: Exception) -> str:
    message = " ".join(str(error).split())
    return message[:500] or error.__class__.__name__


app = create_app()
