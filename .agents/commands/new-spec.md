---
description: Generate a new SDD spec folder (Requirements, Plan, Validation) under Specs/<timestamp>-<slug>/
---

# new-spec

Generate a new Software Design Document spec folder under `Specs/`.

## Usage

```
/new-spec <short-description>
```

Example: `/new-spec improve-demand-build-cache`

The short description becomes part of the folder name: `<timestamp>-short-description/`.

---

## What you must do

### Step 1 — Gather context

Before writing anything, read the following to ground the spec in reality:

1. Read `Specs/Mission.md` to understand project goals and out-of-scope boundaries.
2. Read `Specs/Roadmap.md` to understand current state and existing gaps.
3. Read `Specs/TechStak.md` to understand the technology constraints, SOLID design guide, EF Core patterns, and service-boundary expectations.
4. Read `AGENTS.md` for critical rules that affect implementation.
5. Explore the codebase areas relevant to `$ARGUMENTS` — find the files that will actually change. Use `find` and `grep` to locate controllers, services, Razor views, Sass files, EF Core models/migrations, Dockerfiles, or templates involved.
6. Identify the existing service boundaries and EF Core access patterns so the spec can preserve SOLID design instead of placing unrelated responsibilities into controllers, agent tools, or broad services.
7. If the feature touches UI, inspect nearby Razor views, partials, layout files, Sass source in `WebApp/Styles`, and any existing HTMX-style interaction patterns before proposing new frontend structure.

If `$ARGUMENTS` is empty, ask the user: "What feature or task should this spec cover?" and wait for the answer before proceeding.

### Step 2 — Run discovery before writing spec files

Before creating `Requirements.md`, `Plan.md`, or `Validation.md`, run a discovery step. Use the user's `$ARGUMENTS` and the codebase context from Step 1 to generate a concise set of questions that would materially improve the quality of the spec and the eventual implementation.

Ask questions that clarify:

- The main user-visible outcome and success criteria.
- The exact workflow, actor, or screen/API/service involved.
- Expected data changes, persistence rules, or ownership boundaries.
- Error handling, edge cases, and backwards compatibility expectations.
- Security, privacy, user isolation, and external dependency constraints.
- Testing expectations and any manual verification constraints.
- Priority, deadline, or phased delivery needs.

Rules for discovery:

- Ask only questions that affect requirements, design, validation, or implementation risk.
- Do not ask questions you can answer by reading the code.
- Prefer one clear grouped list of questions over repeated interruptions.
- If the idea is broad, ask enough questions to make the first implementation slice concrete.
- Stop after asking the discovery questions and wait for the user's answers before creating the spec folder or writing any files.
- After the user answers, incorporate those answers into `Requirements.md`, `Plan.md`, and `Validation.md`.
- If the user's answers leave important unknowns, record them in `Requirements.md` under `Open Questions` rather than blocking forever.

### Step 2.5 — Apply general implementation guidance

Use these rules when shaping the requirements, plan, validation strategy, and future implementation tasks:

- Treat `Specs/TechStak.md` as the source of truth for architecture, SOLID boundaries, EF Core patterns, Microsoft Agent Framework usage, Docker execution, and version constraints.
- Prefer the existing ASP.NET Core MVC structure: controllers coordinate HTTP flow, services own business behavior, Razor views/partials render UI, and EF Core access stays in focused services or `AppDbContext` patterns.
- Keep user-owned data scoped by `UserId` / `ClaimTypes.NameIdentifier`; book lookup, note access, embeddings, generated context, and cache/session behavior must not cross users.
- Preserve the existing Microsoft Agent Framework vocabulary and architecture for AI work. Do not describe it as a generic bot framework or move agent-tool responsibilities into controllers.
- For UI work, follow the existing Razor + Sass + HTMX-style partial update patterns. Edit Sass source under `WebApp/Styles`; do not rely on generated CSS under `WebApp/wwwroot/css` as the source of truth.
- Frontend plans should specify concrete views/partials, expected empty/loading/error states, responsive behavior, accessibility concerns, and how the UI fits the current layout instead of proposing a detached redesign.
- Prefer small, testable interfaces and services for behavior that may be shared, mocked, or changed. Avoid broad utility classes, multi-purpose controllers, and large switch-based orchestration.
- Keep provider-specific SQL, pgvector operations, Ollama calls, Unsplash calls, Redis cache behavior, and external dependencies behind focused services so they can be validated independently.
- Plan Docker-first execution. Any build, test, migration, restore, or scaffolding command should use Make targets or `docker compose exec webapp ...` according to `AGENTS.md`.
- Do not introduce new runtime packages, frontend libraries, background services, or infrastructure unless the spec explains why existing patterns are insufficient and how the dependency will be verified.

### Step 3 — Determine the folder name

**Timestamp rules — read carefully before creating any folder:**

- Format is `YYYYMMDDHHMMSS` (14 digits, no separators).
- The timestamp MUST reflect the real wall-clock time at the moment the spec is created.
  Use the `currentDate` value injected into your context for the date portion (`YYYYMMDD`),
  and the actual current time for the time portion (`HHMMSS`).
- NEVER invent or approximate a timestamp (e.g. `120000`, `130000`). If you are unsure of
  the current time, ask the user before creating any files.
- After computing the timestamp, verify it is strictly greater than every existing timestamp
  prefix already present under `Specs/`. List the existing folders with `ls Specs/` and
  compare. If your timestamp would sort before an existing spec, you have the wrong time —
  stop and correct it before writing any files.

Example for a spec created at 21:27:05 on 2026-04-24:

- Date: `20260424`, time: `212705` → folder prefix: `20260424212705`
- Full path: `Specs/20260424212705-my-feature/`

- Slug from `$ARGUMENTS`: lowercase, hyphens only, no spaces (e.g. `improve-demand-build-cache`).
- Full path: `Specs/<timestamp>-<slug>/`

### Step 4 — Write Requirements.md

Follow SDD functional requirements style. Derive requirements from the codebase you explored and from what the user described. Every functional requirement gets a numbered FR label (FR1, FR2, …) — these labels are referenced in Plan.md and Validation.md.

```markdown
# Requirements: <Feature Title>

## Table of Contents
...

## Problem Statement

One paragraph. What breaks or is missing today? What does the user need that does not exist?
Ground this in the actual codebase — reference real files or behaviours if relevant.

## User Stories

- Given [context], when [action], then [outcome].
(Write 2–5 stories. Each must be testable.)

## Functional Requirements

1. FR1 — <requirement>
2. FR2 — <requirement>
...

Each FR must be:
- Observable: you can write a test or manual check for it.
- Atomic: one behaviour per FR.
- Grounded: reference the real service, handler, or file it applies to.

## Non-Functional Requirements

- Performance, security, data isolation, testability constraints.
- Only include ones that are real constraints for this feature.
- Include SOLID constraints when the feature touches services, controllers, Microsoft Agent Framework tools, EF Core queries, external APIs, or cross-cutting orchestration.

## Out of Scope

- List things someone might reasonably expect but that are explicitly excluded.

## Open Questions

- ⚠️ TODO: List genuine unknowns that need a decision before or during implementation.
```

### Step 5 — Write Plan.md

This is the technical design. It must reference real files from your codebase exploration. Never invent file names.

```markdown
# Plan: <Feature Title>

## Table of Contents
...

## Summary

One or two sentences: what the implementation does and which existing patterns it follows.

## Technical Approach

Describe the implementation strategy. Reference the real files and patterns already present
in the codebase that this feature extends or modifies. If it follows an existing pattern,
name it explicitly.
Explain how the design follows SOLID:
- Which class owns each responsibility.
- Which interfaces high-level code depends on.
- Where EF Core queries or provider-specific SQL live.
- How the design remains testable with fakes or integration tests.

Also explain how the design follows the general implementation guidance:
- Which existing MVC, Razor partial, Sass, service, EF Core, Microsoft Agent Framework, cache, or Docker pattern it reuses.
- For frontend changes, which existing UI conventions, partial update flow, responsive states, and accessibility expectations apply.
- Why any new abstraction, package, external dependency, or infrastructure is necessary.

## Component Breakdown

**Existing files to modify:**

- `path/to/file.cs` — what changes and why.
- `path/to/view.cshtml` — what changes and why.
- `path/to/styles.scss` — what changes and why.

**New files to create:**

- `path/to/NewService.cs` — purpose.
- Or: "None required."

## Dependencies

- List real runtime, infrastructure, or service dependencies (e.g. a running SQL Server,
  a specific env var, another service being reachable).

## Flow

Include a Mermaid sequence or flowchart diagram showing the main happy-path data flow.
Use real component names from the codebase.

​```mermaid
sequenceDiagram
    ...
​```

## Risk Assessment

| Risk | Evidence | Mitigation |
| --- | --- | --- |
| (identify real risks from the codebase — data isolation, QEMU build time, breaking API, etc.) | | |
```

### Step 6 — Write Validation.md

Map every FR from Requirements.md to an acceptance criterion. Add test cases that match the actual test patterns used in this repo: xUnit tests in `WebApp.Tests`, EF Core in-memory tests where appropriate, and Docker/compose-backed checks when PostgreSQL, pgvector, Redis, Ollama, or full-stack behavior must be verified.

```markdown
# Validation: <Feature Title>

## Table of Contents
...

## Acceptance Criteria

| Requirement | Acceptance Criterion |
| --- | --- |
| FR1 | Describe the exact observable outcome that proves FR1 is satisfied. |
| FR2 | ... |

## Test Cases

**Unit tests:**
- `WebApp.Tests/...Tests.cs`: what to verify.

**Integration tests:**
- What Docker Compose, PostgreSQL/pgvector, Redis, Ollama, or MVC flow would cover the happy path end-to-end.
- Use ⚠️ TODO if a test does not exist yet but should.

## Manual Verification

Numbered steps a developer can follow locally to verify the feature works.
Start from a clean state using the appropriate Make target, e.g. `make docker-run` on Linux/SteamOS, or the OS-specific target documented in `AGENTS.md`.

## Definition of Done

- Requirements, Plan, and Validation docs are updated in this spec folder.
- All existing tests still pass.
- New behaviour has test coverage matching the pattern in `AGENTS.md`.
- If UI changed: Razor views/partials and Sass source are updated consistently, with responsive, empty, loading, error, and accessibility states covered in the plan.
- If AI behavior changed: Microsoft Agent Framework prompts, tools, services, and session/cache effects are documented and validated.
- If Docker, migrations, or infrastructure changed: the Make/Docker workflow is documented and verified.

## Rollback Plan

- How to revert if the feature causes a regression in production.
- Reference the specific config flag, environment variable, migration, service registration, view path, or code path that disables it.
```

### Step 7 — Create the files

Create the three files:

- `Specs/<timestamp>-<slug>/Requirements.md`
- `Specs/<timestamp>-<slug>/Plan.md`
- `Specs/<timestamp>-<slug>/Validation.md`

After writing, update `Specs/Roadmap.md` — add a row to the Phases table for this new spec with the correct priority (ask the user if unknown: P0 = must-do now, P1 = next, P2 = later).

### Step 8 — Report back

Tell the user:

- The folder that was created.
- A one-line summary of each file.
- Any open questions or TODOs that need their input before implementation can begin.
