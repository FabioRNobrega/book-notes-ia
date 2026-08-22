MODEL ?= qwen3.5:4b
COMPOSE ?= docker compose

# Auto-detect Docker/Podman socket so make targets work without manual DOCKER_HOST
DOCKER_HOST := $(shell \
  if [ -S /var/run/docker.sock ]; then \
    echo unix:///var/run/docker.sock; \
  elif [ -S /run/user/$$(id -u)/podman/podman.sock ]; then \
    echo unix:///run/user/$$(id -u)/podman/podman.sock; \
  elif [ -S /run/user/$$(id -u)/docker.sock ]; then \
    echo unix:///run/user/$$(id -u)/docker.sock; \
  fi)
export DOCKER_HOST
LINUX_COMPOSE_FILES := -f docker-compose.yml -f docker-compose.linux.yml
MAC_COMPOSE_FILES := -f docker-compose.yml -f docker-compose.mac.yml
WINDOWS_COMPOSE_FILES := -f docker-compose.yml -f docker-compose.windows.yml
TEST_COMPOSE_FILES := -f docker-compose.test.yml
TEST_COMPOSE_PROJECT ?= book-notes-ia-test
CHATTERBOX_COMPOSE_FILES := -f docker-compose.chatterbox.yml
CHATTERBOX_COMPOSE_PROJECT ?= book-notes-ia-chatterbox
CHATTERBOX_LANGUAGE = $(if $(strip $(LANGUAGE)),$(LANGUAGE),en)
CHATTERBOX_MODEL ?= v3
export CHATTERBOX_MODEL
CHATTERBOX_PREVIEW_TIMEOUT_SECONDS ?= 604800
export CHATTERBOX_PREVIEW_TIMEOUT_SECONDS
VOICE_ID ?=
SEED ?= 1234
export CHATTERBOX_SEED := $(SEED)
AUDIOBOOK_ROOT ?= /home/deck/Music
BOOK_NAME ?=
BOOK_INTRO ?=
BOOK_OUTRO ?=
FORCE ?= false
export AUDIOBOOK_ROOT
export AUDIOBOOK_BOOK = $(BOOK)
export AUDIOBOOK_BOOK_NAME = $(BOOK_NAME)
export AUDIOBOOK_LANGUAGE = $(TTS_LANG)
export AUDIOBOOK_VOICE_ID = $(VOICE_ID)
export AUDIOBOOK_INTRO = $(BOOK_INTRO)
export AUDIOBOOK_OUTRO = $(BOOK_OUTRO)
export AUDIOBOOK_FORCE = $(FORCE)
EBOOK_PARSER_COMPOSE_FILES := -f docker-compose.ebook-parser.yml
EBOOK_PARSER_COMPOSE_PROJECT ?= book-notes-ia-ebook-parser
EBOOK_PARSER_PORT ?= 5082
BOOK ?=
TTS ?= false
TTS_LANG ?=

.PHONY: docker-build docker-build-mac docker-build-windows docker-run docker-run-mac docker-run-windows docker-down docker-down-mac docker-down-windows docker-test docker-test-build docker-test-shell test ollama-logs ollama-logs-mac ollama-logs-windows ollama-chat release docker-env debug-tts presentation-bundle chatterbox-preview chatterbox-voices chatterbox-logs chatterbox-test chatterbox-down create-audio-book repair-ebook ebook-parse ebook-parser-test ebook-parser-logs ebook-parser-down

docker-env:
	@echo "export DOCKER_HOST=$(DOCKER_HOST)"


docker-run:
	$(COMPOSE) $(LINUX_COMPOSE_FILES) up

docker-run-mac:
	$(COMPOSE) $(MAC_COMPOSE_FILES) up

docker-run-windows:
	$(COMPOSE) $(WINDOWS_COMPOSE_FILES) up

docker-down:
	$(COMPOSE) $(LINUX_COMPOSE_FILES) down -v

docker-down-mac:
	$(COMPOSE) $(MAC_COMPOSE_FILES) down -v

docker-down-windows:
	$(COMPOSE) $(WINDOWS_COMPOSE_FILES) down -v
	
docker-build:
	$(COMPOSE) $(LINUX_COMPOSE_FILES) build --no-cache

docker-build-mac:
	$(COMPOSE) $(MAC_COMPOSE_FILES) build --no-cache

docker-build-windows:
	$(COMPOSE) $(WINDOWS_COMPOSE_FILES) build --no-cache

test: docker-test

docker-test:
	$(COMPOSE) -p $(TEST_COMPOSE_PROJECT) $(TEST_COMPOSE_FILES) run --rm tests

docker-test-build:
	$(COMPOSE) -p $(TEST_COMPOSE_PROJECT) $(TEST_COMPOSE_FILES) pull tests

docker-test-shell:
	$(COMPOSE) -p $(TEST_COMPOSE_PROJECT) $(TEST_COMPOSE_FILES) run --rm tests sh

release:
	Scripts/release.sh $(VERSION)

ollama-chat:
	docker exec -it ollama ollama run $(MODEL)

ollama-logs: 
	$(COMPOSE) $(LINUX_COMPOSE_FILES) logs -f ollama

ollama-logs-mac:
	$(COMPOSE) $(MAC_COMPOSE_FILES) logs -f ollama

ollama-logs-windows:
	$(COMPOSE) $(WINDOWS_COMPOSE_FILES) logs -f ollama

# Bundle Presentation/index.html + styles.css + charts.js + presentation.js + assets/*.png
# into one self-contained Presentation/dist/presentation.html (CDN libs stay external).
# Runs entirely in a throwaway Node container — no local Node/npm required — and
# everything build-related (build.js, package.json) lives under Presentation/ itself.
# Source files under Presentation/ are left untouched.
presentation-bundle:
	docker run --rm -v "$(CURDIR)/Presentation:/work" -w /work node:22-alpine \
		sh -c "npm install --no-fund --no-audit && node build.js"

# Send a direct HTTP request to the TTS service and save the response as a WAV file.
# The TTS container must be running (make docker-run).
# Override defaults via environment variables (commas in make args break $(or)):
#   TTS_TEXT="Olá, mundo" TTS_LANGUAGE=pt TTS_VOICE=male make debug-tts
debug-tts:
	@TTS_URL=http://localhost:5080 bash Scripts/debug-tts.sh

# Build/start the isolated Chatterbox POC, persist/reuse the selected voice,
# and generate a language-specific preview. LANGUAGE defaults to en; pt is supported.
chatterbox-preview:
	@case "$(CHATTERBOX_MODEL)" in v3|multilingual-v3|nano) ;; *) echo "Unsupported CHATTERBOX_MODEL=$(CHATTERBOX_MODEL). Use v3 or nano"; exit 1 ;; esac
	@if [ "$(CHATTERBOX_MODEL)" = "nano" ] && [ "$(CHATTERBOX_LANGUAGE)" != "en" ]; then echo "Chatterbox Nano supports only LANGUAGE=en"; exit 1; fi
	@if [ "$(CHATTERBOX_MODEL)" = "nano" ] && [ -z "$(VOICE_ID)" ]; then echo "Chatterbox Nano requires an existing English VOICE_ID"; exit 1; fi
	@case "$(CHATTERBOX_LANGUAGE)" in en) reference="reference.wav" ;; pt) reference="pt-reference.wav" ;; *) echo "Unsupported LANGUAGE=$(CHATTERBOX_LANGUAGE). Use en or pt"; exit 1 ;; esac; \
		if [ -z "$(VOICE_ID)" ]; then test -f "services/ChatterboxTtsService/data/$$reference" || (echo "Missing services/ChatterboxTtsService/data/$$reference" && exit 1); fi
	$(COMPOSE) -p $(CHATTERBOX_COMPOSE_PROJECT) $(CHATTERBOX_COMPOSE_FILES) up -d --build chatterbox-tts
	@$(COMPOSE) -p $(CHATTERBOX_COMPOSE_PROJECT) $(CHATTERBOX_COMPOSE_FILES) exec -T chatterbox-tts python -m app.wait_for_ready
	@$(COMPOSE) -p $(CHATTERBOX_COMPOSE_PROJECT) $(CHATTERBOX_COMPOSE_FILES) exec -T chatterbox-tts python -m app.preview_client --language $(CHATTERBOX_LANGUAGE) $(if $(strip $(VOICE_ID)),--voice-id $(VOICE_ID),)

chatterbox-voices:
	@case "$(LANGUAGE)" in ""|en|pt) ;; *) echo "Unsupported LANGUAGE=$(LANGUAGE). Use en or pt"; exit 1 ;; esac
	$(COMPOSE) -p $(CHATTERBOX_COMPOSE_PROJECT) $(CHATTERBOX_COMPOSE_FILES) up -d --build chatterbox-tts
	@$(COMPOSE) -p $(CHATTERBOX_COMPOSE_PROJECT) $(CHATTERBOX_COMPOSE_FILES) exec -T chatterbox-tts python -m app.wait_for_ready
	@$(COMPOSE) -p $(CHATTERBOX_COMPOSE_PROJECT) $(CHATTERBOX_COMPOSE_FILES) exec -T chatterbox-tts python -m app.voice_client $(if $(strip $(LANGUAGE)),--language $(LANGUAGE),)

chatterbox-logs:
	$(COMPOSE) -p $(CHATTERBOX_COMPOSE_PROJECT) $(CHATTERBOX_COMPOSE_FILES) logs -f chatterbox-tts

chatterbox-test:
	$(COMPOSE) -p $(CHATTERBOX_COMPOSE_PROJECT) $(CHATTERBOX_COMPOSE_FILES) build chatterbox-tts
	$(COMPOSE) -p $(CHATTERBOX_COMPOSE_PROJECT) $(CHATTERBOX_COMPOSE_FILES) run --rm --no-deps chatterbox-tts python -m pytest -q

chatterbox-down:
	$(COMPOSE) -p $(CHATTERBOX_COMPOSE_PROJECT) $(CHATTERBOX_COMPOSE_FILES) down --remove-orphans

# Generate one English Multilingual V3 WAV per intro/chapter/outro text file.
# The one-off process persists a checksum manifest and resumes verified tracks.
create-audio-book:
	@test -n "$$AUDIOBOOK_BOOK" || (echo 'Usage: make create-audio-book BOOK=folder BOOK_NAME="Book Name" TTS_LANG=en VOICE_ID=uuid BOOK_INTRO=intro.txt BOOK_OUTRO=outro.txt [SEED=1234 AUDIOBOOK_ROOT=/home/deck/Music FORCE=true|false]' && exit 1)
	@test -n "$$AUDIOBOOK_BOOK_NAME" || (echo "BOOK_NAME is required" && exit 1)
	@test -n "$$AUDIOBOOK_VOICE_ID" || (echo "VOICE_ID is required" && exit 1)
	@test -n "$$AUDIOBOOK_INTRO" || (echo "BOOK_INTRO is required" && exit 1)
	@test -n "$$AUDIOBOOK_OUTRO" || (echo "BOOK_OUTRO is required" && exit 1)
	@case "$$AUDIOBOOK_LANGUAGE" in en) ;; *) echo "TTS_LANG must be en"; exit 1 ;; esac
	@case "$$AUDIOBOOK_FORCE" in true|false) ;; *) echo "FORCE must be true or false"; exit 1 ;; esac
	@mkdir -p "$$AUDIOBOOK_ROOT"
	$(COMPOSE) -p $(CHATTERBOX_COMPOSE_PROJECT) $(CHATTERBOX_COMPOSE_FILES) build chatterbox-tts
	@$(COMPOSE) -p $(CHATTERBOX_COMPOSE_PROJECT) $(CHATTERBOX_COMPOSE_FILES) run --rm --no-deps \
		-e CHATTERBOX_MODEL=v3 \
		-e AUDIOBOOK_BOOK -e AUDIOBOOK_BOOK_NAME -e AUDIOBOOK_LANGUAGE \
		-e AUDIOBOOK_VOICE_ID -e AUDIOBOOK_INTRO -e AUDIOBOOK_OUTRO \
		-e AUDIOBOOK_FORCE chatterbox-tts python -m app.audiobook_client

# Repair one private EPUB's ZIP media-type marker without changing the source.
# The repaired publication is written beside it as <name>-fixed.epub.
repair-ebook:
	@if [ -z "$(BOOK)" ]; then echo "Usage: make repair-ebook BOOK=book.epub"; exit 1; fi
	@case "$(BOOK)" in *[!A-Za-z0-9._\ -]*|.*) echo "BOOK must be a safe base filename"; exit 1 ;; esac
	@case "$(BOOK)" in *.epub|*.EPUB) ;; *) echo "BOOK must end in .epub"; exit 1 ;; esac
	@test -f "services/EbookParseService.Api/data/input/$(BOOK)" || (echo "Missing services/EbookParseService.Api/data/input/$(BOOK)" && exit 1)
	$(COMPOSE) -p $(EBOOK_PARSER_COMPOSE_PROJECT) $(EBOOK_PARSER_COMPOSE_FILES) build ebook-parser
	@$(COMPOSE) -p $(EBOOK_PARSER_COMPOSE_PROJECT) $(EBOOK_PARSER_COMPOSE_FILES) run --rm --no-deps --entrypoint /app/repair-ebook.sh ebook-parser "$(BOOK)"

# Parse one private EPUB from services/EbookParseService.Api/data/input.
# BOOK is intentionally restricted to a plain local filename.
ebook-parse:
	@if [ -z "$(BOOK)" ]; then echo "Usage: make ebook-parse BOOK=book.epub [TTS=true TTS_LANG=en|pt]"; exit 1; fi
	@case "$(BOOK)" in *[!A-Za-z0-9._\ -]*|.*) echo "BOOK must be a safe base filename"; exit 1 ;; esac
	@case "$(BOOK)" in *.epub|*.EPUB) ;; *) echo "BOOK must end in .epub"; exit 1 ;; esac
	@case "$(TTS)" in ""|false|true) ;; *) echo "TTS must be true or false"; exit 1 ;; esac
	@if [ "$(TTS)" = "true" ]; then case "$(TTS_LANG)" in en|pt) ;; *) echo "TTS=true requires TTS_LANG=en or TTS_LANG=pt"; exit 1 ;; esac; fi
	@test -f "services/EbookParseService.Api/data/input/$(BOOK)" || (echo "Missing services/EbookParseService.Api/data/input/$(BOOK)" && exit 1)
	$(COMPOSE) -p $(EBOOK_PARSER_COMPOSE_PROJECT) $(EBOOK_PARSER_COMPOSE_FILES) up -d --build ebook-parser
	@$(COMPOSE) -p $(EBOOK_PARSER_COMPOSE_PROJECT) $(EBOOK_PARSER_COMPOSE_FILES) exec -T ebook-parser sh -c 'until curl --fail --silent http://localhost:5082/health >/dev/null; do sleep 1; done'
	@$(COMPOSE) -p $(EBOOK_PARSER_COMPOSE_PROJECT) $(EBOOK_PARSER_COMPOSE_FILES) exec -T ebook-parser curl --fail-with-body --silent --show-error -H "Content-Type: application/json" -d '{"fileName":"$(BOOK)","tts":$(if $(filter true,$(TTS)),true,false),"ttsLanguage":$(if $(filter true,$(TTS)),"$(TTS_LANG)",null)}' http://localhost:5082/api/epubs/parse
	@echo

ebook-parser-test:
	$(COMPOSE) -p $(TEST_COMPOSE_PROJECT) $(TEST_COMPOSE_FILES) run --rm --no-deps tests dotnet test services/EbookParseService.Tests/EbookParseService.Tests.csproj --logger "console;verbosity=minimal"

ebook-parser-logs:
	$(COMPOSE) -p $(EBOOK_PARSER_COMPOSE_PROJECT) $(EBOOK_PARSER_COMPOSE_FILES) logs -f ebook-parser

ebook-parser-down:
	$(COMPOSE) -p $(EBOOK_PARSER_COMPOSE_PROJECT) $(EBOOK_PARSER_COMPOSE_FILES) down --remove-orphans
