from __future__ import annotations

from dataclasses import replace
import hashlib
from pathlib import Path
import threading
import time

from fastapi.testclient import TestClient

from app.engine import SynthesisError, SynthesisResult
from app.main import create_app
from app.settings import PINNED_NANO_MODEL_REVISION
from conftest import write_wav


class FakeEngine:
    device = "cpu"
    model_name = "multilingual-v3"
    model_revision = "5bb1f6ee58e50c3b8d408bc82a6d3740c2db6e18"

    def __init__(
        self,
        ready: bool = True,
        synthesis_failure: str | None = None,
        prepare_failure: str | None = None,
        invalid_output: bool = False,
        silent_output: bool = False,
        model_name: str = "multilingual-v3",
        model_revision: str | None = None,
    ) -> None:
        self.model_name = model_name
        self.model_revision = model_revision or type(self).model_revision
        self._ready = ready
        self._synthesis_failure = synthesis_failure
        self._prepare_failure = prepare_failure
        self._invalid_output = invalid_output
        self._silent_output = silent_output
        self.prepare_calls: list[Path] = []
        self.save_calls: list[Path] = []
        self.load_calls: list[Path] = []
        self.synthesis_calls: list[dict] = []
        self.conditioning: bytes | None = None
        self.fail_next_load = False
        self.active = 0
        self.max_active = 0
        self._active_lock = threading.Lock()

    @property
    def ready(self) -> bool:
        return self._ready

    @property
    def load_error(self) -> str | None:
        return "download failed" if not self._ready else None

    def load(self) -> None:
        self._ready = True

    def prepare_conditioning(self, reference_path: Path) -> None:
        if self._prepare_failure:
            raise SynthesisError(self._prepare_failure)
        self.prepare_calls.append(reference_path)
        digest = hashlib.sha256(reference_path.read_bytes()).hexdigest()
        self.conditioning = f"fake-conditioning:{digest}".encode()

    def save_conditioning(self, path: Path) -> None:
        if self.conditioning is None:
            raise SynthesisError("conditioning was not prepared")
        self.save_calls.append(path)
        path.write_bytes(self.conditioning)

    def load_conditioning(self, path: Path) -> None:
        self.load_calls.append(path)
        if self.fail_next_load:
            self.fail_next_load = False
            raise SynthesisError("conditioning could not be loaded")
        content = path.read_bytes()
        if not content.startswith(b"fake-conditioning:"):
            raise SynthesisError("conditioning is invalid")
        self.conditioning = content

    def synthesize(self, **kwargs) -> SynthesisResult:
        with self._active_lock:
            self.active += 1
            self.max_active = max(self.max_active, self.active)
        try:
            time.sleep(0.02)
            if self._synthesis_failure:
                raise SynthesisError(self._synthesis_failure)
            self.synthesis_calls.append(kwargs)
            if self._invalid_output:
                kwargs["output_path"].write_bytes(b"not-wave")
            else:
                write_wav(
                    kwargs["output_path"], sample=0 if self._silent_output else 1000
                )
            callback = kwargs.get("progress_callback")
            if callback:
                for index in range(len(kwargs["chunks"])):
                    callback(index + 1, len(kwargs["chunks"]))
            return SynthesisResult(16_000, 3.1, len(kwargs["chunks"]))
        finally:
            with self._active_lock:
                self.active -= 1


def test_health_reports_both_supported_languages(settings) -> None:
    client = TestClient(create_app(settings, FakeEngine(), auto_load=False))

    response = client.get("/health")

    assert response.status_code == 200
    assert response.json() == {
        "status": "alive",
        "model_ready": True,
        "device": "cpu",
        "model": "multilingual-v3",
        "model_revision": FakeEngine.model_revision,
        "supported_languages": ["en", "pt"],
        "load_error": None,
    }


def test_health_and_preview_report_load_failure(settings) -> None:
    engine = FakeEngine(ready=False)
    client = TestClient(create_app(settings, engine, auto_load=False))

    assert client.get("/health").json()["model_ready"] is False
    response = client.post("/preview")

    assert response.status_code == 503
    assert response.json()["detail"] == "download failed"


def test_english_preview_defaults_and_creates_persistent_voice(settings) -> None:
    profile = settings.profile("en")
    write_wav(profile.reference_path)
    engine = FakeEngine()
    client = TestClient(create_app(settings, engine, auto_load=False))

    response = client.post("/preview")

    assert response.status_code == 200
    body = response.json()
    assert body["language_id"] == "en"
    assert body["conditioning_status"] == "created"
    assert Path(body["conditioning_path"]).is_file()
    assert Path(body["output_path"]).name == "preview-en.wav"
    assert Path(body["output_path"]).parent.name == body["voice_id"]
    assert len(engine.prepare_calls) == 1
    assert engine.prepare_calls[0] == profile.reference_path
    assert engine.synthesis_calls[0]["language_id"] == "en"


def test_restart_reuses_same_voice_without_preparing_reference(settings) -> None:
    write_wav(settings.profile("en").reference_path)
    first = TestClient(create_app(settings, FakeEngine(), auto_load=False))
    created = first.post("/preview").json()
    second_engine = FakeEngine()
    second = TestClient(create_app(settings, second_engine, auto_load=False))

    loaded = second.post("/preview").json()

    assert loaded["voice_id"] == created["voice_id"]
    assert loaded["conditioning_status"] == "loaded"
    assert second_engine.prepare_calls == []
    assert len(second_engine.load_calls) == 1


def test_nano_reuses_voice_id_and_preserves_multilingual_artifacts(settings) -> None:
    write_wav(settings.profile("en").reference_path, duration_seconds=6.1)
    multilingual = TestClient(
        create_app(settings, FakeEngine(), auto_load=False)
    ).post("/preview").json()
    multilingual_conditioning = Path(multilingual["conditioning_path"]).read_bytes()
    multilingual_output = Path(multilingual["output_path"]).read_bytes()
    multilingual_metadata = (
        settings.voices_dir / multilingual["voice_id"] / "metadata.json"
    ).read_bytes()
    nano_settings = replace(
        settings,
        model="nano",
        nano_model_revision=PINNED_NANO_MODEL_REVISION,
    ).validated()
    nano_engine = FakeEngine(
        model_name="nano",
        model_revision=PINNED_NANO_MODEL_REVISION,
    )
    nano_client = TestClient(
        create_app(nano_settings, nano_engine, auto_load=False)
    )

    created = nano_client.post(
        f"/preview?language=en&voice_id={multilingual['voice_id']}"
    )
    loaded_engine = FakeEngine(
        model_name="nano",
        model_revision=PINNED_NANO_MODEL_REVISION,
    )
    loaded = TestClient(
        create_app(nano_settings, loaded_engine, auto_load=False)
    ).post(f"/preview?language=en&voice_id={multilingual['voice_id']}")

    assert created.status_code == 200
    assert loaded.status_code == 200
    created_body = created.json()
    assert created_body["voice_id"] == multilingual["voice_id"]
    assert created_body["model"] == "nano"
    assert created_body["model_conditioning_status"] == "created"
    assert Path(created_body["conditioning_path"]).name == "conditioning-nano.pt"
    assert Path(created_body["output_path"]).name == "preview-en-nano.wav"
    assert created_body["real_time_factor"] > 0
    assert loaded.json()["model_conditioning_status"] == "loaded"
    assert loaded_engine.prepare_calls == []
    assert Path(multilingual["conditioning_path"]).read_bytes() == multilingual_conditioning
    assert Path(multilingual["output_path"]).read_bytes() == multilingual_output
    assert (settings.voices_dir / multilingual["voice_id"] / "metadata.json").read_bytes() == multilingual_metadata


def test_nano_requires_existing_english_voice(settings) -> None:
    nano_settings = replace(settings, model="nano").validated()
    client = TestClient(
        create_app(
            nano_settings,
            FakeEngine(
                model_name="nano",
                model_revision=PINNED_NANO_MODEL_REVISION,
            ),
            auto_load=False,
        )
    )

    missing_voice = client.post("/preview?language=en")
    portuguese = client.post("/preview?language=pt")

    assert missing_voice.status_code == 422
    assert "existing English voice ID" in missing_voice.json()["detail"]
    assert portuguese.status_code == 422
    assert "supports only English" in portuguese.json()["detail"] or "Supported languages: en" in portuguese.json()["detail"]


def test_changed_preview_text_reuses_conditioning(settings) -> None:
    write_wav(settings.profile("en").reference_path)
    first = TestClient(create_app(settings, FakeEngine(), auto_load=False))
    created = first.post("/preview").json()
    conditioning_checksum = hashlib.sha256(
        Path(created["conditioning_path"]).read_bytes()
    ).hexdigest()
    (settings.config_dir / "preview.txt").write_text(
        "This is completely different preview text.", encoding="utf-8"
    )
    engine = FakeEngine()
    second = TestClient(create_app(settings, engine, auto_load=False))

    loaded = second.post("/preview").json()

    assert loaded["voice_id"] == created["voice_id"]
    assert loaded["conditioning_status"] == "loaded"
    assert engine.prepare_calls == []
    assert engine.synthesis_calls[0]["chunks"][0].text.startswith("This is completely")
    assert hashlib.sha256(Path(loaded["conditioning_path"]).read_bytes()).hexdigest() == conditioning_checksum


def test_portuguese_uses_separate_reference_voice_and_output(settings) -> None:
    write_wav(settings.profile("en").reference_path, sample=1000)
    write_wav(settings.profile("pt").reference_path, sample=2000)
    engine = FakeEngine()
    client = TestClient(create_app(settings, engine, auto_load=False))

    english = client.post("/preview").json()
    portuguese = client.post("/preview?language=pt").json()

    assert portuguese["language_id"] == "pt"
    assert portuguese["conditioning_status"] == "created"
    assert portuguese["voice_id"] != english["voice_id"]
    assert Path(portuguese["output_path"]).name == "preview-pt.wav"
    assert engine.prepare_calls == [
        settings.profile("en").reference_path,
        settings.profile("pt").reference_path,
    ]
    assert engine.synthesis_calls[-1]["language_id"] == "pt"


def test_unsupported_languages_are_rejected(settings) -> None:
    client = TestClient(create_app(settings, FakeEngine(), auto_load=False))

    pt_br = client.post("/preview?language=pt-br")
    unknown = client.post("/preview?language=fr")

    assert pt_br.status_code == 422
    assert unknown.status_code == 422
    assert "en, pt" in unknown.json()["detail"]


def test_missing_and_invalid_selected_reference_return_422(settings) -> None:
    client = TestClient(create_app(settings, FakeEngine(), auto_load=False))

    missing = client.post("/preview?language=pt")
    settings.profile("pt").reference_path.write_text("not audio", encoding="utf-8")
    invalid = client.post("/preview?language=pt")

    assert missing.status_code == 422
    assert "missing" in missing.json()["detail"].lower()
    assert invalid.status_code == 422
    assert "readable 16-bit pcm wav" in invalid.json()["detail"].lower()


def test_new_reference_creates_second_voice_and_old_voice_remains_selectable(settings) -> None:
    reference = settings.profile("pt").reference_path
    write_wav(reference, sample=1000)
    client = TestClient(create_app(settings, FakeEngine(), auto_load=False))
    first = client.post("/preview?language=pt").json()
    first_conditioning = Path(first["conditioning_path"]).read_bytes()
    write_wav(reference, sample=2000)

    second = client.post("/preview?language=pt").json()
    selected = client.post(
        f"/preview?language=pt&voice_id={first['voice_id']}"
    ).json()
    voices = client.get("/voices?language=pt").json()

    assert second["voice_id"] != first["voice_id"]
    assert selected["voice_id"] == first["voice_id"]
    assert Path(first["conditioning_path"]).read_bytes() == first_conditioning
    assert {voice["voice_id"] for voice in voices["voices"]} == {
        first["voice_id"], second["voice_id"]
    }
    assert all(voice["reference_archived"] for voice in voices["voices"])


def test_progress_finishes_with_chunk_counts(settings) -> None:
    write_wav(settings.profile("en").reference_path)
    client = TestClient(create_app(settings, FakeEngine(), auto_load=False))

    response = client.post("/preview")
    progress = client.get("/progress").json()

    assert response.status_code == 200
    assert progress["status"] == "completed"
    assert progress["percent"] == 100
    assert progress["chunk_index"] == progress["chunk_count"]
    assert progress["chunk_count"] == response.json()["chunk_count"]


def test_short_and_silent_references_are_rejected(settings) -> None:
    reference = settings.profile("en").reference_path
    client = TestClient(create_app(settings, FakeEngine(), auto_load=False))
    write_wav(reference, duration_seconds=1, sample=1000)
    short = client.post("/preview")
    write_wav(reference, sample=0)
    silent = client.post("/preview")

    assert short.status_code == 422
    assert "at least 3 seconds" in short.json()["detail"]
    assert silent.status_code == 422
    assert "silent or too quiet" in silent.json()["detail"]


def test_failed_conditioning_load_regenerates_with_same_voice_id(settings) -> None:
    write_wav(settings.profile("en").reference_path)
    created = TestClient(
        create_app(settings, FakeEngine(), auto_load=False)
    ).post("/preview").json()
    engine = FakeEngine()
    engine.fail_next_load = True
    client = TestClient(create_app(settings, engine, auto_load=False))

    regenerated = client.post("/preview").json()

    assert regenerated["voice_id"] == created["voice_id"]
    assert regenerated["conditioning_status"] == "regenerated"
    assert len(engine.prepare_calls) == 1
    assert len(engine.load_calls) == 2


def test_synthesis_failure_preserves_previous_output(settings) -> None:
    write_wav(settings.profile("en").reference_path)
    created = TestClient(
        create_app(settings, FakeEngine(), auto_load=False)
    ).post("/preview").json()
    output_path = Path(created["output_path"])
    previous = output_path.read_bytes()
    engine = FakeEngine(synthesis_failure="inference failed")
    client = TestClient(create_app(settings, engine, auto_load=False))

    response = client.post("/preview")

    assert response.status_code == 500
    assert response.json()["detail"] == "inference failed"
    assert output_path.read_bytes() == previous
    assert not list(output_path.parent.glob(".preview-*.wav"))


def test_invalid_synthesized_output_maps_to_500(settings) -> None:
    write_wav(settings.profile("en").reference_path)
    client = TestClient(
        create_app(settings, FakeEngine(invalid_output=True), auto_load=False)
    )

    response = client.post("/preview")

    assert response.status_code == 500
    assert "wav file" in response.json()["detail"].lower()


def test_silent_synthesized_output_preserves_previous_preview(settings) -> None:
    write_wav(settings.profile("en").reference_path)
    created = TestClient(
        create_app(settings, FakeEngine(), auto_load=False)
    ).post("/preview").json()
    output_path = Path(created["output_path"])
    previous = output_path.read_bytes()
    client = TestClient(
        create_app(settings, FakeEngine(silent_output=True), auto_load=False)
    )

    response = client.post("/preview")

    assert response.status_code == 500
    assert "silent or too quiet" in response.json()["detail"]
    assert output_path.read_bytes() == previous


def test_preview_requests_are_serialized(settings) -> None:
    write_wav(settings.profile("en").reference_path)
    engine = FakeEngine()
    client = TestClient(create_app(settings, engine, auto_load=False))
    threads = [threading.Thread(target=lambda: client.post("/preview")) for _ in range(2)]

    for thread in threads:
        thread.start()
    for thread in threads:
        thread.join()

    assert engine.max_active == 1
