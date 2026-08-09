# Requirements: Migrate the local MVP to .NET 10

## Table of Contents

- [Problem Statement](#problem-statement)
- [User Stories](#user-stories)
- [Functional Requirements](#functional-requirements)
- [Non-Functional Requirements](#non-functional-requirements)
- [Out of Scope](#out-of-scope)
- [Open Questions](#open-questions)

## Problem Statement

BOOK-NOTES-IA currently targets .NET 9 across its MVC application, TTS service, test projects, Docker images, and GitHub Actions workflow. .NET 9 reaches end of support on November 10, 2026, so the local-first MVP must move to .NET 10 LTS while preserving its Microsoft Agent Framework, Ollama, PostgreSQL/pgvector, Redis, Identity, and Supertonic TTS workflows. The migration must remain Docker-first: no .NET SDK, NuGet tooling, or runtime installation is required on the developer host.

## User Stories

- Given a developer on Linux, macOS, or Windows, when they start the documented Make target, then the local application stack builds and runs with the .NET 10 images and the appropriate Ollama override.
- Given the repository CI workflow, when a push or pull request runs, then it restores and tests every .NET project using the .NET 10 SDK and PostgreSQL/pgvector service.
- Given the existing application and database, when the migration is applied, then Identity, Kindle import, semantic book lookup, Microsoft Agent Framework chat, Redis sessions, and TTS continue to work without a new database migration.
- Given a clean checkout, when the developer runs the Docker-based test target, then all existing WebApp and TTS tests pass without native tooling installed locally.

## Functional Requirements

1. **FR1 — Target .NET 10:** All four projects (`WebApp`, `WebApp.Tests`, `TtsService.Api`, and `TtsService.Tests`) target `net10.0`.
2. **FR2 — Update Microsoft dependencies:** Direct Microsoft ASP.NET Core, EF Core, Extensions, Npgsql EF Core, Redis, code-generation, and test dependencies are updated to versions compatible with .NET 10; floating package versions are replaced with explicit versions.
3. **FR3 — Update Docker development images:** The WebApp and TTS Dockerfiles and the containerized test Compose file use .NET 10 SDK/runtime images, and the Docker-installed `dotnet-ef` and `dotnet-aspnet-codegenerator` tools use the .NET 10 tool line.
4. **FR4 — Preserve database compatibility:** Existing EF Core migrations remain usable with EF Core 10, PostgreSQL/pgvector remains configured as today, and the migration does not add an application schema migration unless validation proves one is required.
5. **FR5 — Preserve local application behavior:** The migration preserves MVC/Identity flows, Microsoft Agent Framework sessions and tools, Ollama chat and embeddings, Redis cache behavior, Kindle import, pgvector search, and the TTS sidecar contract.
6. **FR6 — Support all local Compose variants:** The base Compose file and Linux, macOS, and Windows override files remain valid and compatible with the upgraded images; the existing Make targets remain the supported entry points.
7. **FR7 — Align GitHub Actions:** `.github/workflows/ci.yml` uses the .NET 10 SDK, restores the solution, runs both test projects, keeps PostgreSQL/pgvector integration coverage, and uploads test results.
8. **FR8 — Validate without native installation:** All restore, build, test, migration inspection, and tool commands documented by the feature use Make targets or Docker commands; the implementation must not require installing .NET, NuGet, EF, or code-generation tools on the host.
9. **FR9 — Document the migration:** The project’s technology inventory, Docker guidance, and roadmap accurately describe .NET 10, the updated package/tool versions, CI behavior, and the local-only scope.

## Non-Functional Requirements

- Preserve the existing Docker-first development model and OS-specific Ollama GPU/platform behavior.
- Keep the application local-only; production and staging deployment work is excluded.
- Keep all user-owned data and Microsoft Agent Framework session/cache behavior scoped by user ID.
- Do not change prompts, agent selection, AI provider behavior, database ownership rules, or TTS API semantics except where required for .NET 10 compatibility.
- Prefer explicit, reproducible package and Docker image versions where the repository currently uses floating versions.
- Validate on Docker-based Linux execution and statically validate the macOS and Windows Compose overrides because GitHub-hosted CI cannot reproduce every local GPU/platform environment.

## Out of Scope

- Production or staging deployment, hosting images, Kubernetes, cloud infrastructure, or release automation.
- Installing the .NET SDK, NuGet, EF tools, or code generator on the developer host.
- Migrating to .NET 11 or changing the project’s AI provider architecture.
- Introducing a new database migration or changing the PostgreSQL/pgvector schema unless EF Core 10 validation demonstrates an unavoidable compatibility requirement.
- Replacing Ollama, Microsoft Agent Framework, Redis, PostgreSQL, pgvector, or the Supertonic TTS service.
- Automatically running native macOS or Windows GPU CI jobs in GitHub Actions.

## Open Questions

- None. Defaults were confirmed: P0 priority, .NET 10-compatible package updates, Linux runtime CI plus static cross-platform Compose checks, no planned schema migration, and local MVP scope only.
