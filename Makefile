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
CHATTERBOX_OUTPUT := services/ChatterboxTtsService/data/synthetic-preview.wav

.PHONY: docker-build docker-build-mac docker-build-windows docker-run docker-run-mac docker-run-windows docker-down docker-down-mac docker-down-windows docker-test docker-test-build docker-test-shell test ollama-logs ollama-logs-mac ollama-logs-windows ollama-chat release docker-env debug-tts presentation-bundle chatterbox-preview chatterbox-logs chatterbox-test chatterbox-down

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

# Build/start the isolated Chatterbox POC, wait for its pinned model to load,
# and generate data/synthetic-preview.wav from data/reference.wav.
chatterbox-preview:
	@test -f services/ChatterboxTtsService/data/reference.wav || (echo "Missing services/ChatterboxTtsService/data/reference.wav" && exit 1)
	$(COMPOSE) -p $(CHATTERBOX_COMPOSE_PROJECT) $(CHATTERBOX_COMPOSE_FILES) up -d --build chatterbox-tts
	@$(COMPOSE) -p $(CHATTERBOX_COMPOSE_PROJECT) $(CHATTERBOX_COMPOSE_FILES) exec -T chatterbox-tts python -m app.wait_for_ready
	@$(COMPOSE) -p $(CHATTERBOX_COMPOSE_PROJECT) $(CHATTERBOX_COMPOSE_FILES) exec -T chatterbox-tts python -c "import json,urllib.request; request=urllib.request.Request('http://localhost:5081/preview', method='POST'); print(json.dumps(json.load(urllib.request.urlopen(request, timeout=7200)), indent=2))"
	@echo "Preview written to $(CHATTERBOX_OUTPUT)"

chatterbox-logs:
	$(COMPOSE) -p $(CHATTERBOX_COMPOSE_PROJECT) $(CHATTERBOX_COMPOSE_FILES) logs -f chatterbox-tts

chatterbox-test:
	$(COMPOSE) -p $(CHATTERBOX_COMPOSE_PROJECT) $(CHATTERBOX_COMPOSE_FILES) build chatterbox-tts
	$(COMPOSE) -p $(CHATTERBOX_COMPOSE_PROJECT) $(CHATTERBOX_COMPOSE_FILES) run --rm --no-deps chatterbox-tts python -m pytest -q

chatterbox-down:
	$(COMPOSE) -p $(CHATTERBOX_COMPOSE_PROJECT) $(CHATTERBOX_COMPOSE_FILES) down --remove-orphans
