from __future__ import annotations

import json
import sys
import time
import urllib.error
import urllib.request


HEALTH_URL = "http://localhost:5081/health"


def main(timeout_seconds: int = 3_600) -> int:
    deadline = time.monotonic() + timeout_seconds
    started = time.monotonic()
    last_reported = -1
    print("Waiting for Chatterbox model readiness...", flush=True)
    while time.monotonic() < deadline:
        try:
            with urllib.request.urlopen(HEALTH_URL, timeout=5) as response:
                health = json.load(response)
            if health.get("model_ready"):
                print("Chatterbox model is ready.", flush=True)
                return 0
            if health.get("load_error"):
                print(f"Chatterbox model load failed: {health['load_error']}", file=sys.stderr)
                return 2
        except (urllib.error.URLError, TimeoutError, json.JSONDecodeError):
            pass
        elapsed = int(time.monotonic() - started)
        if elapsed // 15 != last_reported:
            last_reported = elapsed // 15
            print(f"Still loading model ({elapsed}s elapsed)...", flush=True)
        time.sleep(5)

    print(
        "Chatterbox did not become ready before the one-hour timeout; "
        "run make chatterbox-logs",
        file=sys.stderr,
    )
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
