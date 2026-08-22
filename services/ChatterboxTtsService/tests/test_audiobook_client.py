from __future__ import annotations

from dataclasses import replace
from types import SimpleNamespace

import pytest

from app import audiobook_client
from app.audiobook_client import AudiobookConfigurationError


@pytest.mark.parametrize(
    ("value", "expected"),
    [(None, False), ("false", False), ("true", True)],
)
def test_force_configuration(monkeypatch, value, expected) -> None:
    if value is None:
        monkeypatch.delenv("AUDIOBOOK_FORCE", raising=False)
    else:
        monkeypatch.setenv("AUDIOBOOK_FORCE", value)

    assert audiobook_client.configured_force() is expected


def test_force_configuration_rejects_other_values(monkeypatch) -> None:
    monkeypatch.setenv("AUDIOBOOK_FORCE", "yes")

    with pytest.raises(AudiobookConfigurationError, match="true or false"):
        audiobook_client.configured_force()


def test_main_reports_missing_configuration_without_traceback(monkeypatch, capsys) -> None:
    for name in (
        "AUDIOBOOK_BOOK",
        "AUDIOBOOK_BOOK_NAME",
        "AUDIOBOOK_LANGUAGE",
        "AUDIOBOOK_VOICE_ID",
        "AUDIOBOOK_INTRO",
        "AUDIOBOOK_OUTRO",
    ):
        monkeypatch.delenv(name, raising=False)

    assert audiobook_client.main() == 1
    output = capsys.readouterr().out
    assert "Missing required environment value" in output
    assert "Traceback" not in output


def test_run_rejects_non_english_before_accessing_files(monkeypatch) -> None:
    values = {
        "AUDIOBOOK_BOOK": "book",
        "AUDIOBOOK_BOOK_NAME": "Book",
        "AUDIOBOOK_LANGUAGE": "pt",
        "AUDIOBOOK_VOICE_ID": "21ecfcec-33c1-40db-8bc8-25d078ce2fa9",
        "AUDIOBOOK_INTRO": "intro.txt",
        "AUDIOBOOK_OUTRO": "outro.txt",
    }
    for name, value in values.items():
        monkeypatch.setenv(name, value)

    with pytest.raises(AudiobookConfigurationError, match="TTS_LANG=en"):
        audiobook_client.run()


def test_run_forces_multilingual_v3_and_delegates_to_service(
    monkeypatch, settings
) -> None:
    values = {
        "AUDIOBOOK_BOOK": "book",
        "AUDIOBOOK_BOOK_NAME": "Book",
        "AUDIOBOOK_LANGUAGE": "en",
        "AUDIOBOOK_VOICE_ID": "21ecfcec-33c1-40db-8bc8-25d078ce2fa9",
        "AUDIOBOOK_INTRO": "intro.txt",
        "AUDIOBOOK_OUTRO": "outro.txt",
    }
    for name, value in values.items():
        monkeypatch.setenv(name, value)
    project = SimpleNamespace(
        book_id="book", tracks=(1, 2, 3, 4), destination_dir="/audiobooks/Book"
    )
    observed: dict[str, object] = {}

    class FakeService:
        def __init__(self, actual_settings, engine) -> None:
            observed["settings"] = actual_settings
            observed["engine"] = engine

        def generate(self, actual_project, language, voice_id, force):
            observed.update(
                project=actual_project,
                language=language,
                voice_id=voice_id,
                force=force,
            )
            return {"track_count": 4}

    monkeypatch.setattr(audiobook_client, "build_project", lambda *args: project)
    monkeypatch.setattr(
        audiobook_client.Settings, "from_environment", lambda: settings
    )
    monkeypatch.setattr(audiobook_client, "AudiobookService", FakeService)
    monkeypatch.setattr(
        audiobook_client,
        "ChatterboxEngine",
        lambda revision, model: (revision, model),
    )

    result = audiobook_client.run()

    assert result == {"track_count": 4}
    assert observed["engine"] == (settings.model_revision, "multilingual-v3")
    assert observed["project"] is project
    assert observed["language"] == "en"
    assert observed["force"] is False


def test_run_rejects_nano_settings(monkeypatch, settings) -> None:
    values = {
        "AUDIOBOOK_BOOK": "book",
        "AUDIOBOOK_BOOK_NAME": "Book",
        "AUDIOBOOK_LANGUAGE": "en",
        "AUDIOBOOK_VOICE_ID": "21ecfcec-33c1-40db-8bc8-25d078ce2fa9",
        "AUDIOBOOK_INTRO": "intro.txt",
        "AUDIOBOOK_OUTRO": "outro.txt",
    }
    for name, value in values.items():
        monkeypatch.setenv(name, value)
    monkeypatch.setattr(
        audiobook_client,
        "build_project",
        lambda *args: SimpleNamespace(
            book_id="book", tracks=(1,), destination_dir="/audiobooks/Book"
        ),
    )
    monkeypatch.setattr(
        audiobook_client.Settings,
        "from_environment",
        lambda: replace(settings, model="nano"),
    )

    with pytest.raises(AudiobookConfigurationError, match="Multilingual V3"):
        audiobook_client.run()
