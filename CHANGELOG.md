# Changelog

## Table of Contents

- [Changelog](#changelog)
  - [Unreleased](#unreleased)
  - [Stashed / Unapplied History](#stashed--unapplied-history)
  - [Initial Test and Agent Tooling Milestone - 2026-04-20](#initial-test-and-agent-tooling-milestone---2026-04-20)
  - [Multi-OS Docker and Book Context Milestone - 2026-04-04 to 2026-04-15](#multi-os-docker-and-book-context-milestone---2026-04-04-to-2026-04-15)
  - [Kindle Notes and Chat UX Milestone - 2026-04-03](#kindle-notes-and-chat-ux-milestone---2026-04-03)
  - [Profile, Cache, and Agent Framework Milestone - 2026-02-14 to 2026-03-01](#profile-cache-and-agent-framework-milestone---2026-02-14-to-2026-03-01)
  - [Identity, PostgreSQL, and Styling Milestone - 2025-10-26 to 2025-11-21](#identity-postgresql-and-styling-milestone---2025-10-26-to-2025-11-21)
  - [Initial Setup - 2025-10-14](#initial-setup---2025-10-14)
  - [Notes](#notes)

## Unreleased

No version tags exist in the repository (`git tag --list` returned no tags). Releases below are grouped by chronological milestones from `git log --no-merges` and `git show --stat`.

## Stashed / Unapplied History

### Added

- Stash `stash@{0}` (`56fb2b8` / index commit `66284b1`, 2026-04-21, FabioRNobrega) contains local, unapplied changes for `.devcontainer/`, `docker-compose.speckit.yml`, `Makefile`, and `README.md`.

### Changed

- ⚠️ TODO: Decide whether the stashed spec-kit/devcontainer work should be applied, documented as planned work, or dropped. These files are visible in Git history under `refs/stash` but are not present in the checked-out tree.

## Initial Test and Agent Tooling Milestone - 2026-04-20

### Added

- Added `WebApp.Tests` to the solution with xUnit tests for `ChatController`, `BookContextController`, and `BookContextService` in commit `e750b28`.
- Added `docker-compose.test.yml` and Makefile targets for containerized test execution in commit `e750b28`.
- Added `IChatOrchestratorAgent`, `IChatToolRouter`, and `ChatToolRouteDecision` so chat orchestration and tool routing can be tested outside the MVC controller in commit `e750b28`.

### Changed

- Refactored chat flow so the controller calls a tool router and orchestrator abstraction instead of holding all agent/session behavior directly in commit `e750b28`.
- Refactored book context generation into a tool-style path that can append generated context into the chat working context in commit `e8806ca`.

## Multi-OS Docker and Book Context Milestone - 2026-04-04 to 2026-04-15

### Added

- Added OS-specific Docker Compose overrides for Linux, macOS, and Windows Ollama runtime configuration in commit `d16cb46`.
- Added the `Book.Context` migration and model property for persisted generated context in commit `9629009`.
- Added book context generation through `BookContextController`, `BookContextService`, `IOllamaService`, notes views, and related registration in commit `349e29e`.
- Added Vulkan/GPU-related Ollama configuration and updated the Docker model to `qwen3.5:4b` in commit `ad12f2f`.

### Changed

- Moved Ollama platform/GPU details out of the base compose file and into OS-specific overrides in commit `d16cb46`.

### Fixed

- Fixed chat thinker focus behavior in commit `d2651c6`.

## Kindle Notes and Chat UX Milestone - 2026-04-03

### Added

- Added Kindle clippings upload/import, `Book` and `BookNote` models, EF Core migration `20260403204829_AddBooksAndBookNotes`, and notes library/detail partial views in commit `36f9026`.
- Added optional Unsplash background integration and `.env.example` keys in commit `fc6d6f6`.
- Added chat auto-scroll, reload-persistent chat history, and assistant thinking placeholder behavior in commits `e6ec8fa`, `1899b75`, and `a1e52fd`.
- Added paper-style page visuals and sunrise animation in commits `521ef06` and `3a73fb8`.

### Changed

- Updated README documentation in commits `92b4dd8` and `36f9026`.
- Reworked chat and page layouts across home, notes, profile, and shared views in commits `42d3393`, `d450698`, `51668c8`, `a60d8cc`, and `5e775d7`.

### Fixed

- Removed the requirement for Unsplash API keys by allowing a fallback when keys are not configured in commit `2ddeca4`.
- Fixed scroll container behavior in commit `62279f5`.

## Profile, Cache, and Agent Framework Milestone - 2026-02-14 to 2026-03-01

### Added

- Added user profile persistence, migrations, CRUD scaffolding, and generated MVC views in commit `044732b`.
- Added an upsert profile flow and alert component in commit `2035c1c`.
- Added Redis and cache handling for session/history data in commit `508d3f9`.
- Added Redis-backed chat history/session handling in commit `0dd4f28`.
- Added user profile instructions into per-session chat behavior in commit `0e844bc`.

### Changed

- Replaced Semantic Kernel usage with Microsoft Agent Framework in commit `3c3ba7a`.
- Changed `AgentProfileCompact` storage to JSONB through migration and model updates in commits `2ea47b9`, `4708965`, and `543a271`.
- Switched the Ollama model from Gemma to `qwen2.5:3b` before later model changes in commit `44e7274`.

### Fixed

- Fixed Ollama model syntax in configuration in commit `221ed83`.
- Fixed user profile JSONB model formatting in commit `543a271`.

### Removed

- Removed the earlier `RadisCacheService.cs` implementation when replacing it with `CacheHandler` in commit `0dd4f28`.

## Identity, PostgreSQL, and Styling Milestone - 2025-10-26 to 2025-11-21

### Added

- Added Ollama-powered chat UI and controller behavior in commit `7e6c4a4`.
- Added Sass compilation, Shoelace-related UI setup, and initial chat/home styling in commit `73418ef`.
- Added PostgreSQL connection setup, EF Core configuration, and Docker Compose database service in commit `59de2aa`.
- Added ASP.NET Core Identity UI, migrations, login partial, and Identity registration in commit `9c63d2b`.
- Added Tailwind-oriented Identity view styling and `ShoelaceInputTagHelper` in commit `79a1693`.

### Changed

- Removed custom color emphasis from Sass/CSS in commit `5ab00ff`.

## Initial Setup - 2025-10-14

### Added

- Created the ASP.NET Core MVC web project, solution file, Dockerfile, Docker Compose file, Makefile, and initial README in commit `2d39f99`.
- Added the initial `.gitignore` and `LICENSE` in commit `ed31218`.

## Notes

- Git commit authors observed: `FabioRNobrega` and `Fabio Rodrigues Nóbrega`.
- Changelog structure follows [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/).
- Versioning guidance follows [Semantic Versioning](https://semver.org/), but no semantic version tags are currently present in Git.
