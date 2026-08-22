from __future__ import annotations

from dataclasses import dataclass
import os
from pathlib import Path


PINNED_MODEL_REVISION = "5bb1f6ee58e50c3b8d408bc82a6d3740c2db6e18"
PINNED_NANO_MODEL_REVISION = "71ccd1d0081b430592cea481f4307e764e07bc64"
PINNED_CHATTERBOX_SOURCE_REVISION = "5de7a54aa4e5e2baadb0182dde554908b48b85c2"
CONDITIONING_FORMAT_VERSION = 2
SUPPORTED_LANGUAGES = ("en", "pt")
AUDIOBOOK_LANGUAGE_ID = "en"
AUDIOBOOK_MODEL_NAME = "multilingual-v3"


class UnsupportedLanguageError(ValueError):
    pass


@dataclass(frozen=True)
class VoiceProfile:
    language_id: str
    reference_path: Path
    preview_text_path: Path

    def preview_text(self) -> str:
        return self.preview_text_path.read_text(encoding="utf-8").strip()


@dataclass(frozen=True)
class Settings:
    data_dir: Path
    config_dir: Path
    voices_dir: Path
    outputs_dir: Path
    device: str = "cpu"
    model: str = "v3"
    model_revision: str = PINNED_MODEL_REVISION
    nano_model_revision: str = PINNED_NANO_MODEL_REVISION
    source_revision: str = PINNED_CHATTERBOX_SOURCE_REVISION
    conditioning_format_version: int = CONDITIONING_FORMAT_VERSION
    max_chunk_chars: int = 280
    sentence_silence_ms: int = 180
    paragraph_silence_ms: int = 420
    minimum_reference_seconds: float = 3.0
    minimum_pcm_peak: int = 32
    synthesis_seed: int = 1234

    @classmethod
    def from_environment(cls) -> "Settings":
        data_dir = Path(os.getenv("CHATTERBOX_DATA_DIR", "/data")).resolve()
        config_dir = Path(os.getenv("CHATTERBOX_CONFIG_DIR", "/app/config")).resolve()
        return cls(
            data_dir=data_dir,
            config_dir=config_dir,
            voices_dir=data_dir / "voices",
            outputs_dir=data_dir / "outputs",
            device=os.getenv("CHATTERBOX_DEVICE", "cpu"),
            model=os.getenv("CHATTERBOX_MODEL", "v3"),
            model_revision=os.getenv(
                "CHATTERBOX_MODEL_REVISION", PINNED_MODEL_REVISION
            ),
            nano_model_revision=os.getenv(
                "CHATTERBOX_NANO_MODEL_REVISION", PINNED_NANO_MODEL_REVISION
            ),
            source_revision=os.getenv(
                "CHATTERBOX_SOURCE_REVISION", PINNED_CHATTERBOX_SOURCE_REVISION
            ),
            synthesis_seed=_environment_integer("CHATTERBOX_SEED", 1234),
        ).validated()

    @property
    def supported_languages(self) -> tuple[str, ...]:
        return ("en",) if self.model_name == "nano" else SUPPORTED_LANGUAGES

    @property
    def model_name(self) -> str:
        return "nano" if self.model == "nano" else "multilingual-v3"

    @property
    def selected_model_revision(self) -> str:
        return (
            self.nano_model_revision
            if self.model_name == "nano"
            else self.model_revision
        )

    @property
    def selected_minimum_reference_seconds(self) -> float:
        if self.model_name == "nano":
            return max(self.minimum_reference_seconds, 5.01)
        return self.minimum_reference_seconds

    def profile(self, language_id: str) -> VoiceProfile:
        normalized = language_id.lower()
        if normalized not in self.supported_languages:
            supported = ", ".join(self.supported_languages)
            raise UnsupportedLanguageError(
                f"Unsupported language '{language_id}' for model '{self.model_name}'. "
                f"Supported languages: {supported}"
            )
        if normalized == "en":
            return VoiceProfile(
                language_id="en",
                reference_path=self.data_dir / "reference.wav",
                preview_text_path=self.config_dir / "preview.txt",
            )
        if normalized == "pt":
            return VoiceProfile(
                language_id="pt",
                reference_path=self.data_dir / "pt-reference.wav",
                preview_text_path=self.config_dir / "pt-preview.txt",
            )
        raise UnsupportedLanguageError(f"Unsupported language '{language_id}'")

    def validated(self) -> "Settings":
        data_dir = self.data_dir.resolve()
        config_dir = self.config_dir.resolve()
        if self.voices_dir.resolve().parent != data_dir:
            raise ValueError("Voices directory must be a direct child of the data directory")
        if self.outputs_dir.resolve().parent != data_dir:
            raise ValueError("Outputs directory must be a direct child of the data directory")
        if self.device != "cpu":
            raise ValueError("This proof of concept supports only CPU inference")
        if self.model not in ("v3", "multilingual-v3", "nano"):
            raise ValueError("Supported Chatterbox models are v3 and nano")
        for revision, label in (
            (self.model_revision, "model"),
            (self.nano_model_revision, "Nano model"),
            (self.source_revision, "source"),
        ):
            if len(revision) != 40 or any(
                character not in "0123456789abcdef" for character in revision
            ):
                raise ValueError(
                    f"The Chatterbox {label} revision must be a full lowercase Git commit SHA"
                )
        if self.conditioning_format_version < 1:
            raise ValueError("Conditioning format version must be positive")
        if self.max_chunk_chars < 80:
            raise ValueError("Maximum chunk length must be at least 80 characters")
        if self.minimum_reference_seconds <= 0:
            raise ValueError("Minimum reference duration must be positive")
        if not 0 < self.minimum_pcm_peak <= 32767:
            raise ValueError("Minimum PCM peak must be between 1 and 32767")
        if not 0 < self.synthesis_seed <= 2_147_483_647:
            raise ValueError("Chatterbox synthesis seed must be between 1 and 2147483647")

        for language_id in self.supported_languages:
            profile = self.profile(language_id)
            if profile.reference_path.resolve().parent != data_dir:
                raise ValueError("Reference audio must be a direct child of the data directory")
            if profile.preview_text_path.resolve().parent != config_dir:
                raise ValueError("Preview text must be a direct child of the config directory")
            if not profile.preview_text_path.is_file():
                raise ValueError(f"Preview text is missing: {profile.preview_text_path}")
            if not profile.preview_text():
                raise ValueError("Preview text cannot be empty")
        return self


def _environment_integer(name: str, default: int) -> int:
    raw = os.getenv(name, str(default))
    try:
        return int(raw)
    except ValueError as error:
        raise ValueError(f"{name} must be an integer") from error
