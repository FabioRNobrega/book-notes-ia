from __future__ import annotations

import json
import os
from pathlib import Path

from .audiobook_manifest import AudiobookManifestError
from .audiobook_project import AudiobookProjectError, build_project
from .audiobook_service import AudiobookGenerationError, AudiobookService
from .audio_validation import InvalidReferenceError, OutputWriteError
from .conditioning import concise_error
from .engine import ChatterboxEngine, SynthesisError
from .settings import (
    AUDIOBOOK_LANGUAGE_ID,
    AUDIOBOOK_MODEL_NAME,
    Settings,
    UnsupportedLanguageError,
)
from .voice_store import VoiceSelectionError, VoiceStoreError


class AudiobookConfigurationError(ValueError):
    pass


def configured_value(name: str) -> str:
    value = os.environ.get(name, "")
    if not value:
        raise AudiobookConfigurationError(f"Missing required environment value: {name}")
    return value


def configured_force() -> bool:
    value = os.environ.get("AUDIOBOOK_FORCE", "false")
    if value not in ("true", "false"):
        raise AudiobookConfigurationError("AUDIOBOOK_FORCE must be true or false")
    return value == "true"


def run() -> dict[str, object]:
    book_id = configured_value("AUDIOBOOK_BOOK")
    book_name = configured_value("AUDIOBOOK_BOOK_NAME")
    language_id = configured_value("AUDIOBOOK_LANGUAGE")
    voice_id = configured_value("AUDIOBOOK_VOICE_ID")
    intro_name = configured_value("AUDIOBOOK_INTRO")
    outro_name = configured_value("AUDIOBOOK_OUTRO")
    input_root = Path(os.environ.get("AUDIOBOOK_INPUT_ROOT", "/books"))
    output_root = Path(os.environ.get("AUDIOBOOK_OUTPUT_ROOT", "/audiobooks"))
    force = configured_force()
    if language_id != AUDIOBOOK_LANGUAGE_ID:
        raise AudiobookConfigurationError("Audiobook POC supports only TTS_LANG=en")

    project = build_project(
        input_root,
        output_root,
        book_id,
        book_name,
        intro_name,
        outro_name,
    )
    print(
        f"Preflight complete: book={project.book_id} tracks={len(project.tracks)} "
        f"destination={project.destination_dir}",
        flush=True,
    )
    settings = Settings.from_environment()
    if settings.model_name != AUDIOBOOK_MODEL_NAME:
        raise AudiobookConfigurationError(
            "Audiobook POC requires Chatterbox Multilingual V3"
        )
    engine = ChatterboxEngine(
        settings.selected_model_revision,
        settings.model_name,
    )
    return AudiobookService(settings, engine).generate(
        project,
        language_id,
        voice_id,
        force,
    )


def main() -> int:
    try:
        result = run()
    except KeyboardInterrupt:
        print(
            "Audiobook generation interrupted. Completed tracks remain resumable.",
            flush=True,
        )
        return 130
    except (
        AudiobookConfigurationError,
        AudiobookGenerationError,
        AudiobookManifestError,
        AudiobookProjectError,
        InvalidReferenceError,
        OutputWriteError,
        SynthesisError,
        UnsupportedLanguageError,
        VoiceSelectionError,
        VoiceStoreError,
        OSError,
    ) as error:
        print(f"Audiobook generation failed: {concise_error(error)}", flush=True)
        return 1
    print(json.dumps(result, indent=2), flush=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
