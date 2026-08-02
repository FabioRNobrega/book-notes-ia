# Validation: Migrate the local MVP to .NET 10

## Table of Contents

- [Acceptance Criteria](#acceptance-criteria)
- [Test Cases](#test-cases)
- [Manual Verification](#manual-verification)
- [Definition of Done](#definition-of-done)
- [Rollback Plan](#rollback-plan)

## Acceptance Criteria

| Requirement | Acceptance Criterion |
| --- | --- |
| FR1 | All four project files target `net10.0`; solution restore succeeds inside the .NET 10 test container. |
| FR2 | Direct Microsoft-family dependencies resolve to compatible .NET 10 versions, and no floating `9.0.*` package reference remains. |
| FR3 | WebApp, TTS, and test Docker definitions use .NET 10 images; Docker-installed EF/code-generator tools use the .NET 10 line. |
| FR4 | Existing migrations can be listed/applied against PostgreSQL/pgvector without an unnecessary new migration or data loss. |
| FR5 | Existing automated tests pass and manual smoke checks confirm Identity, Kindle import, book lookup/context, chat/session cache, and TTS behavior. |
| FR6 | Base, Linux, macOS, and Windows Compose configurations parse successfully and preserve the existing Make targets. |
| FR7 | GitHub Actions uses .NET 10, runs both test projects with PostgreSQL/pgvector, and uploads results. |
| FR8 | The documented verification path uses Docker/Make only and does not require native .NET tooling. |
| FR9 | `AGENTS.md`, `Specs/TechStak.md`, and `Specs/Roadmap.md` no longer describe the active project as .NET 9. |

## Test Cases

**Unit and integration tests:**

- `make test`: restore the solution and run `WebApp.Tests` and `services/TtsService.Tests` in the .NET 10 test container.
- PostgreSQL-backed tests in `WebApp.Tests/Integration`: verify pgvector embedding lookup, user scoping, and context persistence.
- Existing controller/service tests: verify Identity-adjacent flows, Kindle import, Microsoft Agent Framework orchestration, Redis/session abstractions, and TTS behavior.

**Docker checks:**

- `docker compose -f docker-compose.yml -f docker-compose.linux.yml config` succeeds.
- `docker compose -f docker-compose.yml -f docker-compose.mac.yml config` succeeds.
- `docker compose -f docker-compose.yml -f docker-compose.windows.yml config` succeeds.
- The WebApp and TTS images build using the project’s Docker/Make workflow.
- EF tooling inside the WebApp container can list migrations and apply the current database model.

**CI checks:**

- GitHub Actions restores the solution with the .NET 10 SDK and completes both test projects.
- PostgreSQL/pgvector health checks pass and test result artifacts are uploaded even when a test fails.

## Manual Verification

1. Confirm no native .NET SDK is needed on the host.
2. On Linux/SteamOS, run `make docker-run`; on macOS, run `make docker-run-mac`; on Windows with NVIDIA support, run `make docker-run-windows`.
3. Open `http://localhost:8080` and verify registration/login.
4. Import a Kindle `.txt` clipping file and confirm books and notes are visible.
5. Open chat and verify Microsoft Agent Framework responses, book context lookup, and Redis-backed session continuity.
6. Play an assistant response and verify the TTS sidecar returns audio.
7. Stop the stack and run `make test`; confirm both test projects pass.
8. Inspect `docker compose ... config` for all three OS overrides and confirm no .NET 9 image remains.

## Definition of Done

- Requirements, Plan, and Validation documents are present in this spec folder.
- All four projects target .NET 10 and restore/build successfully in Docker.
- Microsoft-family package references and Docker-installed tools are compatible with .NET 10 and reproducibly versioned.
- WebApp, TTS, and test Docker workflows use .NET 10 images.
- Linux, macOS, and Windows Compose configurations validate without losing their existing Ollama platform/GPU settings.
- `make test` passes for both test projects.
- GitHub Actions is aligned with .NET 10 and retains PostgreSQL/pgvector integration coverage and artifacts.
- No new database migration is introduced unless required by validated EF Core 10 model changes.
- The local application smoke checks pass for Identity, Kindle import, Microsoft Agent Framework chat, semantic book lookup, Redis session/cache behavior, and TTS.
- `AGENTS.md`, `Specs/TechStak.md`, and `Specs/Roadmap.md` are updated.

## Rollback Plan

- Revert the migration commit, restoring the four project TFMs, package versions, Docker images/tools, CI SDK version, and documentation to their .NET 9 values.
- If only one OS Compose override fails, revert the related override change independently; the base application code and database schema should remain unchanged.
- No database rollback is expected because the migration must not add schema changes by default.
