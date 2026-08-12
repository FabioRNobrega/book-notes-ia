from __future__ import annotations

import threading

import pytest

from app import preview_client
from app.progress import ProgressTracker


def test_progress_is_monotonic_and_preserves_operation_context() -> None:
    tracker = ProgressTracker()
    started = tracker.start("pt")
    first = tracker.update("preparing", "Preparing", 25, voice_id="voice")
    second = tracker.update("synthesizing", "Chunk", 10, chunk_index=1, chunk_count=3)
    completed = tracker.complete()

    assert started.operation_id
    assert first.operation_id == started.operation_id
    assert second.percent == 25
    assert second.voice_id == "voice"
    assert completed.percent == 100
    assert completed.status == "completed"
    assert completed.chunk_index == 3


def test_failed_progress_keeps_last_percentage_and_error() -> None:
    tracker = ProgressTracker()
    tracker.start("en")
    tracker.update("synthesizing", "Chunk", 70, chunk_index=2, chunk_count=4)

    failed = tracker.fail("concise failure")

    assert failed.status == "failed"
    assert failed.percent == 70
    assert failed.error == "concise failure"


def test_preview_client_prints_progress_before_returning_result(
    monkeypatch, capsys
) -> None:
    allow_post_to_finish = threading.Event()

    observed_timeouts: list[int | None] = []

    def fake_read_json(
        url: str, *, method: str = "GET", timeout: int | None = 10
    ) -> dict:
        if method == "POST":
            observed_timeouts.append(timeout)
            assert allow_post_to_finish.wait(timeout=2)
            return {"voice_id": "voice-id", "output_path": "/data/output.wav"}
        allow_post_to_finish.set()
        return {
            "operation_id": "operation-id",
            "status": "running",
            "stage": "synthesizing",
            "message": "Completed audio chunk 1/4",
            "percent": 45,
            "chunk_index": 1,
            "chunk_count": 4,
        }

    monkeypatch.setattr(preview_client, "_read_json", fake_read_json)

    result = preview_client.run_preview("pt")

    assert result["voice_id"] == "voice-id"
    assert observed_timeouts == [preview_client.DEFAULT_PREVIEW_TIMEOUT_SECONDS]
    assert "[ 45%] synthesizing" in capsys.readouterr().out


@pytest.mark.parametrize(
    ("configured", "expected"),
    [
        (None, 604_800),
        ("86400", 86_400),
        ("0", None),
    ],
)
def test_preview_timeout_configuration(
    monkeypatch, configured: str | None, expected: int | None
) -> None:
    if configured is None:
        monkeypatch.delenv("CHATTERBOX_PREVIEW_TIMEOUT_SECONDS", raising=False)
    else:
        monkeypatch.setenv("CHATTERBOX_PREVIEW_TIMEOUT_SECONDS", configured)

    assert preview_client.configured_preview_timeout() == expected


@pytest.mark.parametrize("configured", ["-1", "not-a-number", "1.5"])
def test_preview_timeout_rejects_invalid_configuration(
    monkeypatch, configured: str
) -> None:
    monkeypatch.setenv("CHATTERBOX_PREVIEW_TIMEOUT_SECONDS", configured)

    with pytest.raises(RuntimeError, match="non-negative integer"):
        preview_client.configured_preview_timeout()


def test_preview_client_supports_unlimited_post_timeout(monkeypatch) -> None:
    observed_timeouts: list[int | None] = []

    def fake_read_json(
        url: str, *, method: str = "GET", timeout: int | None = 10
    ) -> dict:
        if method == "POST":
            observed_timeouts.append(timeout)
            return {"voice_id": "voice-id", "output_path": "/data/output.wav"}
        return {"status": "idle"}

    monkeypatch.setattr(preview_client, "_read_json", fake_read_json)

    preview_client.run_preview("en", timeout_seconds=None)

    assert observed_timeouts == [None]
