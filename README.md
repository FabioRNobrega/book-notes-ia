# Book Notes IA

Book Notes IA is a local-first AI reading assistant built with [ASP.NET Core MVC on .NET 9](https://learn.microsoft.com/aspnet/core/mvc/overview?view=aspnetcore-9.0).
It combines local LLM chat through [Ollama](https://ollama.com/), ASP.NET Core Identity, PostgreSQL, Redis caching, [HTMX](https://htmx.org/)-driven UI updates, and [Shoelace](https://shoelace.style/) components.

The project is set up for a Docker-first development flow and is designed to keep the main experience working even when optional integrations, like Unsplash, are not configured.

## Stack

- [.NET 9 MVC](https://learn.microsoft.com/aspnet/core/mvc/overview?view=aspnetcore-9.0)
- [Microsoft Agent Framework](https://learn.microsoft.com/agent-framework/overview/) + [Microsoft.Extensions.AI](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai)
- [Ollama](https://ollama.com/) via [OllamaSharp](https://github.com/awaescher/OllamaSharp)
- [PostgreSQL](https://www.postgresql.org/) with [EF Core 9](https://learn.microsoft.com/ef/core/)
- [Redis distributed cache](https://learn.microsoft.com/aspnet/core/performance/caching/distributed?view=aspnetcore-9.0)
- [ASP.NET Core Identity](https://learn.microsoft.com/aspnet/core/security/authentication/identity?view=aspnetcore-9.0)
- [HTMX](https://htmx.org/) + [Hyperscript](https://hyperscript.org/) + [Shoelace](https://shoelace.style/)
- [AspNetCore.SassCompiler](https://github.com/koenvzeijl/AspNetCore.SassCompiler)
- [Docker Compose](https://docs.docker.com/compose/)

## Current Services

`docker compose` starts these services:

- `webapp` on `http://localhost:8080`
- `ollama` on `http://localhost:11434`
- `postgres` on port `5432`
- `redis` on port `6379`

The Ollama container automatically pulls `qwen2.5:3b`.

## Features

- Local AI chat powered by Ollama
- User login and registration with ASP.NET Core Identity
- Profile-driven assistant behavior
- Redis-backed chat/session caching
- Optional Unsplash home background image
- Graceful fallback to a solid background when Unsplash keys are not configured
- Sass-based styling with generated CSS kept out of git

## Project Structure

```text
book-notes-ia/
├── WebApp/
│   ├── Areas/Identity/       # ASP.NET Core Identity UI
│   ├── Controllers/          # MVC controllers
│   ├── Data/                 # EF Core DbContext
│   ├── Models/               # Domain models
│   ├── Services/             # AI, cache, Unsplash services
│   ├── Styles/               # Sass source files
│   ├── Views/                # MVC views and partials
│   └── wwwroot/              # Static assets
├── docker-compose.yml
├── Makefile
└── README.md
```

## Setup

### 1. Clone the repository

```bash
git clone <repo-url>
cd book-notes-ia
```

### 2. Create your local env file

```bash
cp .env.example .env
```

If you do not want to use Unsplash right now, you can leave the keys empty. The app will use its built-in solid background fallback.

`.env.example`:

```env
UNSPLASH_ACCESS_KEY=your_access_key_here
UNSPLASH_SECRET_KEY=your_secret_key_here
```

### 3. Build and start the stack

```bash
make docker-build
make docker-run
```

Or directly with Docker Compose:

```bash
docker compose build --no-cache
docker compose up
```

## Usage

Open the app at:

```bash
http://localhost:8080/
```

From there you can:

- Sign in or create an account
- Chat with the local AI assistant
- Edit your profile to influence assistant responses
- Switch between chat, notes placeholders, and profile views

Useful commands:

Check Ollama models:

```bash
docker exec -it ollama ollama list
```

Open a shell in the web container:

```bash
docker compose exec webapp bash
```

Open Redis CLI:

```bash
docker exec -it redis redis-cli
```

## Configuration

### Ollama

Configured in `WebApp/appsettings.json` and container env:

- `Ollama:OllamaURL`
- `Ollama:OllamaModel`

The app currently uses `qwen2.5:3b` in Docker.

### Database and Cache

Configured through connection strings:

- `ConnectionStrings:DefaultConnection`
- `ConnectionStrings:Redis`

At startup the app applies EF Core migrations automatically.

### Unsplash

Unsplash is optional.

When `Unsplash:AccessKey` is configured, the app can fetch and cache a home background image.
When it is missing, the app does not call Unsplash and falls back to a solid background color.

Relevant env vars:

- `UNSPLASH_ACCESS_KEY`
- `UNSPLASH_SECRET_KEY`

## Styling Notes

- Sass source files live in `WebApp/Styles/`
- Generated CSS is output to `WebApp/wwwroot/css/`
- Generated CSS is ignored by git and should not be committed

## Troubleshooting

### Ollama is not responding

```bash
docker logs ollama
```

Verify the model exists:

```bash
docker exec -it ollama ollama list
```

### Web app cannot connect to PostgreSQL

```bash
docker logs postgres
```

Verify the connection string matches `docker-compose.yml`.

### Redis issues

```bash
docker logs redis
```

### Rebuild everything clean

```bash
docker compose down -v
docker compose build --no-cache
docker compose up
```

## Git Guidelines

### Branch names

- `feat/branch-name`
- `hotfix/branch-name`
- `poc/branch-name`

### Commit prefixes

- `chore(scope): message`
- `feat(scope): message`
- `fix(scope): message`
- `refactor(scope): message`
- `tests(scope): message`
- `docs(scope): message`
