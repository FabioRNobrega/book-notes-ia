# Agent Context

## Table of Contents

- [Agent Context](#agent-context)
  - [Project Overview](#project-overview)
  - [Repository Map](#repository-map)
  - [Architecture Summary](#architecture-summary)
  - [Docker Strategy](#docker-strategy)
  - [Coding Conventions](#coding-conventions)
  - [Constraints](#constraints)
  - [Make Commands](#make-commands)
  - [Spec-Kit Workflow](#spec-kit-workflow)
  - [Key Documentation](#key-documentation)

## Project Overview

BOOK-NOTES-IA is a local-first AI reading assistant built as an ASP.NET Core MVC app on .NET 9. It imports Kindle clipping `.txt` files into per-user books and notes, stores data in PostgreSQL, and uses Redis for chat/session cache. Chat uses Microsoft Agent Framework over `Microsoft.Extensions.AI` with Ollama as the local model runtime. The app also supports profile-driven assistant behavior and generated literary context for saved books.

## Repository Map

- `.env` - local environment values; ignored by Git.
- `.env.example` - documents `UNSPLASH_ACCESS_KEY` and `UNSPLASH_SECRET_KEY`.
- `.gitignore` - excludes .NET build outputs, `.env`, generated CSS, and test artifacts.
- `.vscode/` - workspace settings currently track the `ollama` spell-check word.
- `AGENTS.md` - agent-facing project context.
- `CHANGELOG.md` - milestone changelog derived from Git history.
- `GENERATE.md` - Docker-first guide for EF migrations, MVC scaffolding, and Identity scaffolding.
- `LICENSE` - project license file.
- `Makefile` - Docker, test, and Ollama helper commands.
- `README.md` - human setup, stack, usage, and troubleshooting guide.
- `Specs/` - mission, tech stack, roadmap, and feature spec folders.
- `WebApp/` - ASP.NET Core MVC application.
- `WebApp.Tests/` - xUnit test project for controllers and services.
- `book-notes-ia.sln` - Visual Studio solution containing `WebApp` and `WebApp.Tests`.
- `docker-compose.yml` - base local stack for web app, Ollama, PostgreSQL, and Redis.
- `docker-compose.linux.yml` - Linux Ollama override with Vulkan/device mappings.
- `docker-compose.mac.yml` - macOS Ollama override using `linux/arm64`.
- `docker-compose.windows.yml` - Windows/NVIDIA Ollama override.
- `docker-compose.test.yml` - containerized test runner.

## Architecture Summary

`WebApp` registers MVC, Identity, EF Core/Npgsql, Redis distributed cache, `IChatClient`, `AIAgent`, chat orchestration services, Kindle import, Unsplash, Ollama, and book context services in `Program.cs`. Authenticated controllers coordinate user flows: `NotesController` imports Kindle clippings and renders the notes library, `ChatController` runs the Microsoft Agent Framework session and optional tool routing, `BookContextController` exposes context API operations, and `UserProfileController` manages profile data. `AppDbContext` stores Identity records, user profiles, books, and notes in PostgreSQL. Redis stores per-user chat session/context keys, while Ollama serves the local `qwen3.5:4b` model.

## Docker Strategy

Use `docker-compose.yml` as the base configuration. Add exactly one OS-specific override for normal app runs:

- Linux: `docker-compose.yml` + `docker-compose.linux.yml`, or `make docker-run`.
- macOS Apple Silicon: `docker-compose.yml` + `docker-compose.mac.yml`, or `make docker-run-mac`.
- Windows with Docker Desktop/WSL2 and NVIDIA GPU: `docker-compose.yml` + `docker-compose.windows.yml`, or `make docker-run-windows`.
- Tests: `docker-compose.test.yml`, or `make test`.

## Coding Conventions

- C# uses file-scoped namespaces in most application and test files.
- Dependency injection is constructor-based, including primary constructors where already used.
- MVC controllers return Razor partials for HTMX-style updates and JSON/API responses only where an API route exists.
- Services are registered behind interfaces for behavior that is tested or shared (`IChatOrchestratorAgent`, `IChatToolRouter`, `IBookContextService`, `IOllamaService`).
- User-owned data must be filtered by `ClaimTypes.NameIdentifier` / `UserId`.
- Sass source lives in `WebApp/Styles`; generated CSS under `WebApp/wwwroot/css` is ignored by Git.

## Constraints

- Do not modify existing source code without being asked.
- Follow the SDD workflow: spec -> plan -> tasks -> implement.
- All new features must have a spec file in `Specs/` before implementation begins.
- Match the existing code style and naming conventions.
- Spec folder names follow `YYYYMMDDHHMMSS-feature-name`, for example `20260421162607-example-task`.
- Always describe the AI integration as Microsoft Agent Framework.
- Do not commit `.env`, generated CSS, `bin/`, or `obj/` content.

## Execution Environment

All code execution happens inside Docker. Never run `dotnet`, `nuget`, or any other native CLI tool directly on the host — these are not installed there. Use `docker compose exec` or Make targets instead.

The `webapp` container runs the full `.NET SDK 9.0` image (not a slim runtime). When the stack is running you can open a shell inside it and use any `dotnet` command:

```bash
docker compose exec webapp bash
# then inside the container:
dotnet build WebApp/WebApp.csproj
dotnet test WebApp.Tests/WebApp.Tests.csproj
dotnet ef migrations list
```

| Intent | Command to use |
| --- | --- |
| Open a dev shell with full .NET SDK | `docker compose exec webapp bash` |
| Build the application | `docker compose exec webapp bash` → `dotnet build WebApp/WebApp.csproj` |
| Run tests (isolated container) | `make test` |
| Open a shell in the isolated test container | `make docker-test-shell` |
| Run a migration | `docker compose exec webapp bash` → `dotnet ef migrations add <Name>` |
| Restore packages | `docker compose exec webapp bash` → `dotnet restore` |
| Debug or inspect app output | `docker compose logs -f webapp` |

> **Note:** `make test` spins up a separate, ephemeral `mcr.microsoft.com/dotnet/sdk:9.0` container via `docker-compose.test.yml`. Use it for clean CI-style test runs. Use `docker compose exec webapp bash` for interactive development against the already-running stack.

### Pre-installed tools in the `webapp` container

The following global .NET tools are installed in `WebApp/Dockerfile` and available inside the container without any extra setup:

| Tool | Command |
| --- | --- |
| Entity Framework Core CLI | `dotnet ef` |
| ASP.NET Core Code Generator | `dotnet-aspnet-codegenerator` |

If a task requires a tool that is not in this list, **add it to `WebApp/Dockerfile`** with a `RUN dotnet tool install --global <tool>` line — do not install it at runtime inside the container. After editing the Dockerfile, rebuild with `make docker-build` (or the platform-specific equivalent) before using the new tool.

If a task requires a command not listed here, use `docker compose exec <service> <command>` rather than running the command on the host.

## Make Commands

Infrastructure:

- `docker-run` - start the Linux compose stack.
- `docker-run-mac` - start the macOS compose stack.
- `docker-run-windows` - start the Windows/NVIDIA compose stack.
- `docker-down` - stop Linux stack and remove volumes.
- `docker-down-mac` - stop macOS stack and remove volumes.
- `docker-down-windows` - stop Windows stack and remove volumes.
- `docker-build` - rebuild Linux stack without cache.
- `docker-build-mac` - rebuild macOS stack without cache.
- `docker-build-windows` - rebuild Windows stack without cache.

Testing:

- `test` - alias for `docker-test`.
- `docker-test` - run `dotnet restore` and `dotnet test` in the test container.
- `docker-test-build` - pull the test image.
- `docker-test-shell` - open a shell in the test container.

Ollama:

- `ollama-chat` - run `ollama run $(MODEL)` inside the `ollama` container; default `MODEL` is `qwen3.5:4b`.
- `ollama-logs` - follow Linux Ollama logs.
- `ollama-logs-mac` - follow macOS Ollama logs.
- `ollama-logs-windows` - follow Windows Ollama logs.

## Agent-Agnostic Commands

Slash commands are stored in `.agents/commands/` — the canonical, agent-neutral location. Any agent or tool that supports slash commands should read command definitions from there.

| Command | File |
| --- | --- |
| `/implement-spec` | `.agents/commands/implement-spec.md` |
| `/new-spec` | `.agents/commands/new-spec.md` |
| `/pr-description` | `.agents/commands/pr-description.md` |
| `/prompt-generator` | `.agents/commands/prompt-generator.md` |

**Claude Code** resolves these via a symlink: `.claude/commands → .agents/commands`. No extra configuration needed.

**Codex** resolves these via the `claude-commands` skill in `.claude/codex/skills/claude-commands/`, which reads from `.agents/commands/` directly.

To add a new command that works in all agents: create a single `.md` file in `.agents/commands/`.

## Spec-Kit Workflow

The repository follows a spec-driven sequence: specify requirements → plan implementation → validate with tests → implement. Use the `/new-spec` command to create a spec folder under `Specs/`, then `/implement-spec` to carry out the implementation.

## Key Documentation

- [Specs/Mission.md](Specs/Mission.md)
- [Specs/TechStak.md](Specs/TechStak.md)
- [Specs/Roadmap.md](Specs/Roadmap.md)
- [CHANGELOG.md](CHANGELOG.md)
- [GENERATE.md](GENERATE.md)
- [README.md](README.md)
