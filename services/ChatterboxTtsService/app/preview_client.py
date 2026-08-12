from __future__ import annotations

import argparse
import json
import os
import threading
import urllib.error
import urllib.parse
import urllib.request


BASE_URL = "http://localhost:5081"
DEFAULT_PREVIEW_TIMEOUT_SECONDS = 7 * 24 * 60 * 60


def _read_json(
    url: str, *, method: str = "GET", timeout: int | None = 10
) -> dict:
    request = urllib.request.Request(url, method=method)
    with urllib.request.urlopen(request, timeout=timeout) as response:
        return json.load(response)


def configured_preview_timeout() -> int | None:
    raw_value = os.environ.get(
        "CHATTERBOX_PREVIEW_TIMEOUT_SECONDS",
        str(DEFAULT_PREVIEW_TIMEOUT_SECONDS),
    )
    try:
        timeout_seconds = int(raw_value)
    except ValueError as error:
        raise RuntimeError(
            "CHATTERBOX_PREVIEW_TIMEOUT_SECONDS must be a non-negative integer"
        ) from error
    if timeout_seconds < 0:
        raise RuntimeError(
            "CHATTERBOX_PREVIEW_TIMEOUT_SECONDS must be a non-negative integer"
        )
    return None if timeout_seconds == 0 else timeout_seconds


def run_preview(
    language: str,
    voice_id: str | None = None,
    timeout_seconds: int | None = DEFAULT_PREVIEW_TIMEOUT_SECONDS,
) -> dict:
    query = {"language": language}
    if voice_id:
        query["voice_id"] = voice_id
    url = f"{BASE_URL}/preview?{urllib.parse.urlencode(query)}"
    result: dict[str, object] = {}
    failure: list[BaseException] = []

    def request_preview() -> None:
        try:
            result.update(_read_json(url, method="POST", timeout=timeout_seconds))
        except BaseException as error:
            failure.append(error)

    worker = threading.Thread(target=request_preview, daemon=True)
    worker.start()
    last_key: tuple | None = None
    while worker.is_alive():
        try:
            progress = _read_json(f"{BASE_URL}/progress")
            key = (
                progress.get("operation_id"), progress.get("status"),
                progress.get("stage"), progress.get("percent"),
                progress.get("chunk_index"), progress.get("chunk_count"),
                progress.get("message"),
            )
            if progress.get("status") != "idle" and key != last_key:
                chunks = ""
                if progress.get("chunk_count"):
                    chunks = (
                        f" chunks={progress.get('chunk_index')}/"
                        f"{progress.get('chunk_count')}"
                    )
                print(
                    f"[{progress.get('percent', 0):3}%] "
                    f"{progress.get('stage')}: {progress.get('message')}{chunks}",
                    flush=True,
                )
                last_key = key
        except (urllib.error.URLError, TimeoutError, json.JSONDecodeError):
            pass
        worker.join(timeout=1)

    worker.join()
    if failure:
        error = failure[0]
        if isinstance(error, urllib.error.HTTPError):
            try:
                detail = json.loads(error.read().decode("utf-8")).get("detail")
            except (UnicodeDecodeError, json.JSONDecodeError):
                detail = None
            raise RuntimeError(detail or f"HTTP {error.code}") from error
        raise RuntimeError(str(error)) from error
    return result


def main() -> int:
    parser = argparse.ArgumentParser(description="Generate a Chatterbox preview")
    parser.add_argument("--language", choices=("en", "pt"), default="en")
    parser.add_argument("--voice-id")
    args = parser.parse_args()
    try:
        result = run_preview(
            args.language,
            args.voice_id,
            timeout_seconds=configured_preview_timeout(),
        )
    except KeyboardInterrupt:
        print(
            "Preview client interrupted; server-side synthesis may still be running. "
            "Use make chatterbox-logs to follow it.",
            flush=True,
        )
        return 130
    except RuntimeError as error:
        print(f"Preview failed: {error}", flush=True)
        return 1
    print(json.dumps(result, indent=2), flush=True)
    print(f"Voice ID: {result['voice_id']}", flush=True)
    print(f"Preview written to {result['output_path']}", flush=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
