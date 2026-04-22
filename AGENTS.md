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
- Spec folder names follow `DD-MM-YYYY-feature-name`, for example `21-04-2026-example-task`.
- Always describe the AI integration as Microsoft Agent Framework.
- Do not commit `.env`, generated CSS, `bin/`, or `obj/` content.

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

## Spec-Kit Workflow

The repository has a `Specs/` directory and the current documentation follows the spec-driven sequence: specify requirements, plan implementation, validate with tests, then implement. Slash commands referenced by the project context are `/speckit.specify`, `/speckit.plan`, and `/speckit.tasks`. ⚠️ TODO: No checked-in command implementation is present in the current tree; a stash entry named `spec kit implementation` exists but is not applied.

## Key Documentation

- [Specs/Mission.md](Specs/Mission.md)
- [Specs/TechStak.md](Specs/TechStak.md)
- [Specs/Roadmap.md](Specs/Roadmap.md)
- [CHANGELOG.md](CHANGELOG.md)
- [GENERATE.md](GENERATE.md)
- [README.md](README.md)
