docker-run:
	docker compose up

docker-down:
	docker compose down -v
	
docker-build:
	docker compose build --no-cache