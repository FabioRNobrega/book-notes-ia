from __future__ import annotations

import argparse
import json
import threading
import urllib.error
import urllib.parse
import urllib.request


BASE_URL = "http://localhost:5081"


def _read_json(url: str, *, method: str = "GET", timeout: int = 10) -> dict:
    request = urllib.request.Request(url, method=method)
    with urllib.request.urlopen(request, timeout=timeout) as response:
        return json.load(response)


def run_preview(language: str, voice_id: str | None = None) -> dict:
    query = {"language": language}
    if voice_id:
        query["voice_id"] = voice_id
    url = f"{BASE_URL}/preview?{urllib.parse.urlencode(query)}"
    result: dict[str, object] = {}
    failure: list[BaseException] = []

    def request_preview() -> None:
        try:
            result.update(_read_json(url, method="POST", timeout=7_200))
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
        result = run_preview(args.language, args.voice_id)
    except RuntimeError as error:
        print(f"Preview failed: {error}", flush=True)
        return 1
    print(json.dumps(result, indent=2), flush=True)
    print(f"Voice ID: {result['voice_id']}", flush=True)
    print(f"Preview written to {result['output_path']}", flush=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
