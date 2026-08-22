from __future__ import annotations

import warnings

import torch

from app.chunking import TextChunk
from app.engine import (
    CFG_WEIGHT,
    EXAGGERATION,
    MIN_P,
    REPETITION_PENALTY,
    TEMPERATURE,
    TOP_P,
    ChatterboxEngine,
    suppress_upstream_future_warnings,
)


class FakeMultilingualModel:
    def __init__(self) -> None:
        self.conds = object()
        self.sr = 24_000
        self.calls: list[dict] = []

    def generate(self, text: str, **kwargs):
        waveform = torch.rand((1, 32))
        self.calls.append({"text": text, "waveform": waveform.clone(), **kwargs})
        return waveform


def test_multilingual_synthesis_is_seeded_and_uses_explicit_quality_defaults(
    tmp_path,
) -> None:
    engine = ChatterboxEngine("a" * 40)
    model = FakeMultilingualModel()
    engine._model = model
    chunks = [TextChunk("First sentence.", False), TextChunk("Second sentence.", False)]

    engine.synthesize(chunks, tmp_path / "first.wav", "en", 180, 420, 1234)
    first_waveforms = [call["waveform"] for call in model.calls]
    first_kwargs = [{key: value for key, value in call.items() if key not in ("text", "waveform")} for call in model.calls]
    model.calls.clear()
    engine.synthesize(chunks, tmp_path / "second.wav", "en", 180, 420, 1234)

    assert all(
        torch.equal(expected, actual["waveform"])
        for expected, actual in zip(first_waveforms, model.calls, strict=True)
    )
    assert not torch.equal(first_waveforms[0], first_waveforms[1])
    assert first_kwargs == [
        {
            "language_id": "en",
            "audio_prompt_path": None,
            "exaggeration": EXAGGERATION,
            "cfg_weight": CFG_WEIGHT,
            "temperature": TEMPERATURE,
            "repetition_penalty": REPETITION_PENALTY,
            "min_p": MIN_P,
            "top_p": TOP_P,
        },
        {
            "language_id": "en",
            "audio_prompt_path": None,
            "exaggeration": EXAGGERATION,
            "cfg_weight": CFG_WEIGHT,
            "temperature": TEMPERATURE,
            "repetition_penalty": REPETITION_PENALTY,
            "min_p": MIN_P,
            "top_p": TOP_P,
        },
    ]

    model.calls.clear()
    engine.synthesize(chunks, tmp_path / "third.wav", "en", 180, 420, 42)
    assert not torch.equal(first_waveforms[0], model.calls[0]["waveform"])


def test_only_known_upstream_future_warnings_are_suppressed() -> None:
    with warnings.catch_warnings(record=True) as observed:
        warnings.simplefilter("always")
        with suppress_upstream_future_warnings():
            warnings.warn(
                "`LoRACompatibleLinear` is deprecated and will be removed",
                FutureWarning,
            )
            warnings.warn(
                "`torch.backends.cuda.sdp_kernel()` is deprecated and will be removed",
                FutureWarning,
            )
        warnings.warn("unrelated future change", FutureWarning)

    assert [str(item.message) for item in observed] == ["unrelated future change"]
