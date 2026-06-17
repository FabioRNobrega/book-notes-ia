#!/usr/bin/env bash
set -euo pipefail

TTS_URL="${TTS_URL:-http://localhost:5080}"
ENDPOINT="$TTS_URL/tts/synthesize"
TEXT="${TTS_TEXT:-Hello, this is a text to speech test.}"
LANGUAGE="${TTS_LANGUAGE:-en}"
VOICE_GENDER="${TTS_VOICE:-female}"
OUT_DIR="debug-output"
OUT_FILE="$OUT_DIR/tts-test.wav"
HEADERS_FILE="$OUT_DIR/tts-response-headers.txt"
ERROR_FILE="$OUT_DIR/tts-error-body.txt"

mkdir -p "$OUT_DIR"

PAYLOAD=$(printf '{"text":"%s","language":"%s","voiceGender":"%s"}' "$TEXT" "$LANGUAGE" "$VOICE_GENDER")

echo "============================================================"
echo " TTS Debug Request"
echo "============================================================"
echo "  URL     : $ENDPOINT"
echo "  Payload : $PAYLOAD"
echo ""

HTTP_STATUS=$(curl \
    --silent \
    --show-error \
    --fail-with-body \
    --write-out "%{http_code}" \
    --output "$OUT_FILE" \
    --dump-header "$HEADERS_FILE" \
    --header "Content-Type: application/json" \
    --header "Accept: audio/wav" \
    --data "$PAYLOAD" \
    "$ENDPOINT" 2>"$ERROR_FILE" || true)

echo "------------------------------------------------------------"
echo " Response"
echo "------------------------------------------------------------"
echo "  HTTP status : $HTTP_STATUS"

CONTENT_TYPE=$(grep -i "^content-type:" "$HEADERS_FILE" 2>/dev/null | head -1 | tr -d '\r' | sed 's/^[Cc]ontent-[Tt]ype: //')
echo "  Content-Type: ${CONTENT_TYPE:-(not found in headers)}"

if [ -f "$OUT_FILE" ]; then
    FILE_SIZE=$(wc -c < "$OUT_FILE" | tr -d ' ')
    echo "  Output file : $OUT_FILE"
    echo "  File exists : yes"
    echo "  File size   : $FILE_SIZE bytes"

    if [ "$FILE_SIZE" -eq 0 ]; then
        echo ""
        echo "  ⚠️  File is EMPTY — the server returned no body."
    else
        # Read the first 12 bytes as hex to check the RIFF/WAVE header
        HEADER_HEX=$(od -A n -t x1 -N 12 "$OUT_FILE" | tr -d ' \n')
        echo "  First 12 B  : $HEADER_HEX"

        # RIFF....WAVE = 52494646 xxxxxxxx 57415645
        if echo "$HEADER_HEX" | grep -qi "^52494646" && echo "$HEADER_HEX" | grep -qi "57415645$"; then
            echo ""
            echo "  ✅ Valid WAV file (RIFF/WAVE header detected)."
            echo "  You can play it with:  vlc $OUT_FILE"
            echo "                    or:  aplay $OUT_FILE"
        else
            echo ""
            echo "  ❌ File does NOT look like a WAV (no RIFF/WAVE header)."
            echo "  First 256 bytes as text:"
            head -c 256 "$OUT_FILE" | cat -v
        fi
    fi
else
    echo "  File exists : NO — output file was not created."
fi

if [ -f "$ERROR_FILE" ] && [ -s "$ERROR_FILE" ]; then
    echo ""
    echo "------------------------------------------------------------"
    echo " curl stderr / error body"
    echo "------------------------------------------------------------"
    cat "$ERROR_FILE"
fi

# If the response was not 2xx, also show what the body says (it may be JSON error)
if [ "$HTTP_STATUS" != "200" ] && [ -f "$OUT_FILE" ] && [ -s "$OUT_FILE" ]; then
    echo ""
    echo "------------------------------------------------------------"
    echo " Error response body"
    echo "------------------------------------------------------------"
    cat "$OUT_FILE"
fi

echo ""
echo "============================================================"
echo " Full response headers saved to: $HEADERS_FILE"
echo "============================================================"
