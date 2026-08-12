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
CHATTERBOX_PREVIEW_TIMEOUT_SECONDS ?= 604800
export CHATTERBOX_PREVIEW_TIMEOUT_SECONDS
VOICE_ID ?=
EBOOK_PARSER_COMPOSE_FILES := -f docker-compose.ebook-parser.yml
EBOOK_PARSER_COMPOSE_PROJECT ?= book-notes-ia-ebook-parser
EBOOK_PARSER_PORT ?= 5082
BOOK ?=

.PHONY: docker-build docker-build-mac docker-build-windows docker-run docker-run-mac docker-run-windows docker-down docker-down-mac docker-down-windows docker-test docker-test-build docker-test-shell test ollama-logs ollama-logs-mac ollama-logs-windows ollama-chat release docker-env debug-tts presentation-bundle chatterbox-preview chatterbox-voices chatterbox-logs chatterbox-test chatterbox-down ebook-parse ebook-parser-test ebook-parser-logs ebook-parser-down

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

# Parse one private EPUB from services/EbookParseService.Api/data/input.
# BOOK is intentionally restricted to a plain local filename.
ebook-parse:
	@if [ -z "$(BOOK)" ]; then echo "Usage: make ebook-parse BOOK=book.epub"; exit 1; fi
	@case "$(BOOK)" in *[!A-Za-z0-9._\ -]*|.*) echo "BOOK must be a safe base filename"; exit 1 ;; esac
	@case "$(BOOK)" in *.epub|*.EPUB) ;; *) echo "BOOK must end in .epub"; exit 1 ;; esac
	@test -f "services/EbookParseService.Api/data/input/$(BOOK)" || (echo "Missing services/EbookParseService.Api/data/input/$(BOOK)" && exit 1)
	$(COMPOSE) -p $(EBOOK_PARSER_COMPOSE_PROJECT) $(EBOOK_PARSER_COMPOSE_FILES) up -d --build ebook-parser
	@$(COMPOSE) -p $(EBOOK_PARSER_COMPOSE_PROJECT) $(EBOOK_PARSER_COMPOSE_FILES) exec -T ebook-parser sh -c 'until curl --fail --silent http://localhost:5082/health >/dev/null; do sleep 1; done'
	@$(COMPOSE) -p $(EBOOK_PARSER_COMPOSE_PROJECT) $(EBOOK_PARSER_COMPOSE_FILES) exec -T ebook-parser curl --fail-with-body --silent --show-error -H "Content-Type: application/json" -d '{"fileName":"$(BOOK)"}' http://localhost:5082/api/epubs/parse
	@echo

ebook-parser-test:
	$(COMPOSE) -p $(TEST_COMPOSE_PROJECT) $(TEST_COMPOSE_FILES) run --rm --no-deps tests dotnet test services/EbookParseService.Tests/EbookParseService.Tests.csproj --logger "console;verbosity=minimal"

ebook-parser-logs:
	$(COMPOSE) -p $(EBOOK_PARSER_COMPOSE_PROJECT) $(EBOOK_PARSER_COMPOSE_FILES) logs -f ebook-parser

ebook-parser-down:
	$(COMPOSE) -p $(EBOOK_PARSER_COMPOSE_PROJECT) $(EBOOK_PARSER_COMPOSE_FILES) down --remove-orphans
