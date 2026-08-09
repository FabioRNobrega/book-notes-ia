from __future__ import annotations

from pathlib import Path
import threading
import time

from fastapi.testclient import TestClient

from app.engine import SynthesisError, SynthesisResult
from app.main import create_app
from conftest import write_wav


class FakeEngine:
    device = "cpu"
    model_name = "multilingual-v3"
    model_revision = "5bb1f6ee58e50c3b8d408bc82a6d3740c2db6e18"

    def __init__(self, ready: bool = True, failure: str | None = None) -> None:
        self._ready = ready
        self._failure = failure
        self.calls = []
        self.active = 0
        self.max_active = 0
        self._active_lock = threading.Lock()

    @property
    def ready(self) -> bool:
        return self._ready

    @property
    def load_error(self) -> str | None:
        return "download failed" if not self._ready and self._failure else None

    def load(self) -> None:
        self._ready = True

    def synthesize(self, **kwargs) -> SynthesisResult:
        with self._active_lock:
            self.active += 1
            self.max_active = max(self.max_active, self.active)
        try:
            time.sleep(0.02)
            if self._failure:
                raise SynthesisError(self._failure)
            self.calls.append(kwargs)
            write_wav(kwargs["output_path"])
            return SynthesisResult(16_000, 0.05, len(kwargs["chunks"]))
        finally:
            with self._active_lock:
                self.active -= 1


def test_health_reports_ready_model(settings) -> None:
    client = TestClient(create_app(settings, FakeEngine(), auto_load=False))

    response = client.get("/health")

    assert response.status_code == 200
    assert response.json() == {
        "status": "alive",
        "model_ready": True,
        "device": "cpu",
        "model": "multilingual-v3",
        "model_revision": FakeEngine.model_revision,
        "language_id": "en",
        "load_error": None,
    }


def test_health_and_preview_report_load_failure(settings) -> None:
    engine = FakeEngine(ready=False, failure="network unavailable")
    client = TestClient(create_app(settings, engine, auto_load=False))

    assert client.get("/health").json()["model_ready"] is False
    response = client.post("/preview")

    assert response.status_code == 503
    assert response.json()["detail"] == "download failed"


def test_preview_uses_fixed_reference_and_returns_metadata(settings) -> None:
    write_wav(settings.reference_path)
    engine = FakeEngine()
    client = TestClient(create_app(settings, engine, auto_load=False))

    response = client.post("/preview")

    assert response.status_code == 200
    body = response.json()
    assert body["output_path"] == str(settings.output_path)
    assert body["device"] == "cpu"
    assert body["model"] == "multilingual-v3"
    assert body["language_id"] == "en"
    assert body["chunk_count"] == len(engine.calls[0]["chunks"])
    assert body["elapsed_seconds"] >= 0
    assert body["output_duration_seconds"] > 0
    assert body["peak_memory_mb"] > 0
    assert engine.calls[0]["reference_path"] == settings.reference_path


def test_missing_and_invalid_reference_return_422(settings) -> None:
    client = TestClient(create_app(settings, FakeEngine(), auto_load=False))

    missing = client.post("/preview")
    settings.reference_path.write_text("not audio", encoding="utf-8")
    invalid = client.post("/preview")

    assert missing.status_code == 422
    assert "missing" in missing.json()["detail"].lower()
    assert invalid.status_code == 422
    assert "readable pcm wav" in invalid.json()["detail"].lower()


def test_synthesis_failure_preserves_previous_output(settings) -> None:
    write_wav(settings.reference_path)
    settings.output_path.write_bytes(b"previous-preview")
    engine = FakeEngine(failure="inference failed")
    client = TestClient(create_app(settings, engine, auto_load=False))

    response = client.post("/preview")

    assert response.status_code == 500
    assert response.json()["detail"] == "inference failed"
    assert settings.output_path.read_bytes() == b"previous-preview"
    assert not list(settings.data_dir.glob(".synthetic-preview-*.wav"))


def test_invalid_synthesized_output_maps_to_500(settings) -> None:
    class InvalidOutputEngine(FakeEngine):
        def synthesize(self, **kwargs) -> SynthesisResult:
            kwargs["output_path"].write_bytes(b"not-wave")
            return SynthesisResult(16_000, 0, 1)

    write_wav(settings.reference_path)
    client = TestClient(
        create_app(settings, InvalidOutputEngine(), auto_load=False)
    )

    response = client.post("/preview")

    assert response.status_code == 500
    assert "wav file" in response.json()["detail"].lower()


def test_preview_requests_are_serialized(settings) -> None:
    write_wav(settings.reference_path)
    engine = FakeEngine()
    client = TestClient(create_app(settings, engine, auto_load=False))
    threads = [threading.Thread(target=lambda: client.post("/preview")) for _ in range(2)]

    for thread in threads:
        thread.start()
    for thread in threads:
        thread.join()

    assert engine.max_active == 1
