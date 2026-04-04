MODEL ?= qwen3.5:4b

.PHONY: docker-build docker-run docker-down ollama-logs ollama-chat


docker-run:
	docker compose up

docker-down:
	docker compose down -v
	
docker-build:
	docker compose build --no-cache

ollama-chat:
	docker exec -it ollama ollama run $(MODEL)

ollama-logs: 
	docker compose logs -f ollama