MODEL ?= qwen3.5:4b
COMPOSE ?= docker compose
LINUX_COMPOSE_FILES := -f docker-compose.yml -f docker-compose.linux.yml
MAC_COMPOSE_FILES := -f docker-compose.yml -f docker-compose.mac.yml
WINDOWS_COMPOSE_FILES := -f docker-compose.yml -f docker-compose.windows.yml
TEST_COMPOSE_FILES := -f docker-compose.test.yml

.PHONY: docker-build docker-build-mac docker-build-windows docker-run docker-run-mac docker-run-windows docker-down docker-down-mac docker-down-windows docker-test docker-test-build docker-test-shell test ollama-logs ollama-logs-mac ollama-logs-windows ollama-chat


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
	$(COMPOSE) $(TEST_COMPOSE_FILES) run --rm tests

docker-test-build:
	$(COMPOSE) $(TEST_COMPOSE_FILES) pull tests

docker-test-shell:
	$(COMPOSE) $(TEST_COMPOSE_FILES) run --rm tests sh

ollama-chat:
	docker exec -it ollama ollama run $(MODEL)

ollama-logs: 
	$(COMPOSE) $(LINUX_COMPOSE_FILES) logs -f ollama

ollama-logs-mac:
	$(COMPOSE) $(MAC_COMPOSE_FILES) logs -f ollama

ollama-logs-windows:
	$(COMPOSE) $(WINDOWS_COMPOSE_FILES) logs -f ollama
