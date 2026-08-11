from __future__ import annotations

import threading

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

    def fake_read_json(url: str, *, method: str = "GET", timeout: int = 10) -> dict:
        if method == "POST":
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
    assert "[ 45%] synthesizing" in capsys.readouterr().out
