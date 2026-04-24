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
3. Read `Specs/TechStak.md` to understand the technology constraints.
4. Read `AGENTS.md` for critical rules that affect implementation.
5. Explore the codebase areas relevant to `$ARGUMENTS` — find the files that will actually change. Use `find` and `grep` to locate handlers, services, Dockerfiles, pipelines, or templates involved.

If `$ARGUMENTS` is empty, ask the user: "What feature or task should this spec cover?" and wait for the answer before proceeding.

### Step 2 — Ask clarifying questions (if needed)

If the scope is ambiguous after reading the codebase, ask the user up to three targeted questions:

- What is the main user-visible outcome?
- Which service(s) are affected?
- Are there known constraints (deadline, backwards compatibility, external dependency)?

Do not ask questions you can answer by reading the code.

### Step 3 — Determine the folder name

- Timestamp is like `20250224162607` format.
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

## Component Breakdown

**Existing files to modify:**

- `path/to/file.go` — what changes and why.

**New files to create:**

- `path/to/new_file.go` — purpose.
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

Map every FR from Requirements.md to an acceptance criterion. Add test cases that match the actual test patterns used in this repo (testcontainers for Go SQL services, pytest for Python, dotnet test for demand).

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
- `path/to/test_file_test.go`: what to verify.

**Integration tests:**
- What testcontainer or compose-based test would cover the happy path end-to-end.
- Use ⚠️ TODO if a test does not exist yet but should.

## Manual Verification

Numbered steps a developer can follow locally to verify the feature works.
Start from a clean state (e.g. `make docker-run` or `go run ./cmd/server`).

## Definition of Done

- Requirements, Plan, and Validation docs are updated in this spec folder.
- All existing tests still pass.
- New behaviour has test coverage matching the pattern in `AGENTS.md`.
- If UI changed: `sysprompt.go` is updated.
- If a pipeline changed: the change is verified to reduce or not increase build time.

## Rollback Plan

- How to revert if the feature causes a regression in production.
- Reference the specific config flag, environment variable, or code path that disables it.
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
