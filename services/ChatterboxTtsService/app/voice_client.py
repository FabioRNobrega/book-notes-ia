from __future__ import annotations

import argparse
import json
import urllib.parse
import urllib.request


def main() -> int:
    parser = argparse.ArgumentParser(description="List stored Chatterbox voices")
    parser.add_argument("--language", choices=("en", "pt"))
    args = parser.parse_args()
    query = ""
    if args.language:
        query = "?" + urllib.parse.urlencode({"language": args.language})
    with urllib.request.urlopen(
        f"http://localhost:5081/voices{query}", timeout=30
    ) as response:
        result = json.load(response)
    print(json.dumps(result, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
