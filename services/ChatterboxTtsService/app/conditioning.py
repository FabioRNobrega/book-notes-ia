from __future__ import annotations

import logging
from typing import Callable

from .audio_validation import validate_reference
from .engine import SynthesisEngine, SynthesisError
from .settings import Settings
from .voice_store import (
    LocalVoiceStore,
    VoiceCompatibility,
    VoiceDecision,
    VoiceSelectionError,
)


LOGGER = logging.getLogger(__name__)
ProgressCallback = Callable[..., None]


def concise_error(error: Exception) -> str:
    message = " ".join(str(error).split())
    return message[:500] or error.__class__.__name__


class ConditioningService:
    def __init__(
        self,
        settings: Settings,
        engine: SynthesisEngine,
        voice_store: LocalVoiceStore,
        progress_callback: ProgressCallback | None = None,
    ) -> None:
        self.settings = settings
        self.engine = engine
        self.voice_store = voice_store
        self.progress_callback = progress_callback

    def resolve_and_load(
        self, language_id: str, voice_id: str | None = None
    ) -> VoiceDecision:
        decision, compatibility = self.resolve(language_id, voice_id)
        return self.load(decision, compatibility)

    def resolve(
        self, language_id: str, voice_id: str | None = None
    ) -> tuple[VoiceDecision, VoiceCompatibility]:
        profile = self.settings.profile(language_id)
        if self.engine.model_name == "nano" and voice_id is None:
            raise VoiceSelectionError(
                "Chatterbox Nano requires an existing English voice ID"
            )

        compatibility = VoiceCompatibility(
            source_revision=self.settings.source_revision,
            model_revision=self.engine.model_revision,
            model_name=self.engine.model_name,
            format_version=self.settings.conditioning_format_version,
        )
        if voice_id is None:
            self._progress("validating-reference", "Validating current reference", 3)
            validate_reference(
                profile.reference_path,
                self.settings.minimum_reference_seconds,
                self.settings.minimum_pcm_peak,
            )

        self._progress("resolving-voice", "Resolving stored voice", 8)
        decision = self.voice_store.resolve(
            profile.language_id,
            profile.reference_path,
            compatibility,
            voice_id=voice_id,
        )
        self._progress(
            "validating-reference",
            "Validating selected voice reference",
            12,
            voice_id=decision.voice_id,
        )
        validate_reference(
            decision.reference_path,
            self.settings.selected_minimum_reference_seconds,
            self.settings.minimum_pcm_peak,
        )
        return decision, compatibility

    def load(
        self,
        decision: VoiceDecision,
        compatibility: VoiceCompatibility,
    ) -> VoiceDecision:
        if decision.status == "loaded":
            self._progress(
                "loading-conditioning",
                "Loading persisted voice conditioning",
                20,
                voice_id=decision.voice_id,
            )
            try:
                self.engine.load_conditioning(decision.conditioning_path)
                return decision
            except SynthesisError as error:
                LOGGER.warning(
                    "Stored conditioning could not be loaded for voice_id=%s; "
                    "rebuilding: %s",
                    decision.voice_id,
                    concise_error(error),
                )
                decision = self.voice_store.require_regeneration(decision)

        action = "Creating" if decision.status == "created" else "Rebuilding"
        self._progress(
            "preparing-conditioning",
            f"{action} voice conditioning from archived reference",
            15,
            voice_id=decision.voice_id,
        )
        temporary_path = self.voice_store.temporary_conditioning_path(
            decision.voice_id,
            self.engine.model_name,
        )
        try:
            self.engine.prepare_conditioning(decision.reference_path)
            self._progress(
                "saving-conditioning",
                "Saving and validating voice conditioning",
                22,
                voice_id=decision.voice_id,
            )
            self.engine.save_conditioning(temporary_path)
            self.engine.load_conditioning(temporary_path)
            self.voice_store.publish(decision, temporary_path, compatibility)
            self._progress(
                "conditioning-ready",
                "Voice conditioning is ready",
                28,
                voice_id=decision.voice_id,
            )
            return decision
        finally:
            temporary_path.unlink(missing_ok=True)
            try:
                temporary_path.parent.rmdir()
            except OSError:
                pass

    def _progress(
        self,
        stage: str,
        message: str,
        percent: int,
        **context: object,
    ) -> None:
        if self.progress_callback is not None:
            self.progress_callback(stage, message, percent, **context)
