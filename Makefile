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

.PHONY: docker-build docker-build-mac docker-build-windows docker-run docker-run-mac docker-run-windows docker-down docker-down-mac docker-down-windows docker-test docker-test-build docker-test-shell test ollama-logs ollama-logs-mac ollama-logs-windows ollama-chat release docker-env debug-tts presentation-bundle

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
