# Book Notes IA | Fábio R. Nóbrega  

This project is a **local AI web app playground** built with [.NET 9.0 MVC](https://learn.microsoft.com/en-us/aspnet/core/tutorials/first-mvc-app/start-mvc?view=aspnetcore-9.0&tabs=visual-studio) using the new [Microsoft Agent Framework and Microsoft.Extensions.AI](https://learn.microsoft.com/en-us/agent-framework/overview/?pivots=programming-language-csharp) abstraction layer.

It integrates with **Ollama** to run a **local LLM (Gemma 3 270M)** fully offline, free, and ready for experiments with **HTMX**, **Hyperscript**, and **Shoelace** UI components. The goal is to build a clean, modern, Docker-first AI web architecture without external cloud dependencies.

We use:

-   .NET SDK 9.0\
-   Microsoft Agent Framework (preview)\
-   Microsoft.Extensions.AI (provider abstraction)\
-   OllamaSharp (Ollama .NET bridge)\
-   Ollama running Gemma 3 (270M parameters)\
-   PostgreSQL (EF Core 9)\
-   Docker + Docker Compose\
-   HTMX + Hyperscript + Shoelace (no heavy JS frontend)



## Table of Contents

-   [Install](#install)\
-   [Usage](#usage)\
-   [Architecture](#architecture)\
-   [Troubleshooting](#troubleshooting)\
-   [Git Guideline](#git-guideline)



## Install

Clone the repo and enter the project folder:

``` bash
git clone <repo-url>
cd book-notes-ia
```

Make sure **Docker Desktop** (or Docker Engine) is installed and running.

Build and run the full stack:

``` bash
make docker-build 
```

then

```bash
make docker-run
```

This will start **3 services**:

-   **webapp** → .NET MVC + Agent Framework (port 8080)
-   **ollama** → local LLM server (port 11434)
-   **postgres** → PostgreSQL database (port 5432)



## Usage

Once running, open your browser at:  

```bash
http://localhost:8080/
```

From there you can:

-   Interact with a fully local AI model (Gemma 3 270M)
-   Chat using HTMX-driven partial updates
-   Modify `.cs`, `.cshtml`, `.css` files and rebuild via Docker
-   Use Identity authentication (ASP.NET Core Identity + PostgreSQL)

The .NET app communicates with Ollama internally via:

http://ollama:11434

To test Ollama directly:

``` bash
docker exec -it ollama ollama run gemma3:270m
```

You also can access .NET from the docker with:

```bash
docker compose exec webapp bash
```



## Architecture

### Service Overview

``` mermaid
flowchart LR
    user([🌐 Browser])
    web([🧩 WebApp<br/>.NET MVC + Agent Framework])
    db[(🐘 PostgreSQL)]
    ollama([🦙 Ollama Server<br/>Gemma3:270M])

    user --> web
    web --> db
    web --> ollama
```

## Troubleshooting

### Ollama not responding

Check logs:

``` bash
docker logs ollama
```

Ensure model is installed:

``` bash
docker exec -it ollama ollama list
```

### Webapp fails to connect to database

Ensure PostgreSQL container is healthy:

``` bash
docker logs postgres
```

Check connection string matches docker-compose configuration.

### Rebuild everything clean

``` bash
docker compose down -v
docker compose build --no-cache
docker compose up
```

## Git Guideline

Follow clear naming and commit conventions.

### Branches

- Feature:  `feat/branch-name`  
- Hotfix: `hotfix/branch-name`  
- POC: `poc/branch-name`  

### Commit prefixes

- Chore: `chore(context): message`  
- Feat: `feat(context): message`  
- Fix: `fix(context): message`  
- Refactor: `refactor(context): message`  
- Tests: `tests(context): message`  
- Docs: `docs(context): message`  
