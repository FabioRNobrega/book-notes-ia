---
description: Implement the most recent spec under Specs/ (identified by the highest timestamp prefix)
---

# implement-spec

Implement the most recently created spec under `Specs/`.

## Usage

```
/implement-spec
```

No arguments required. The command identifies the target spec by sorting all folders under `Specs/` and selecting the one with the highest 14-digit timestamp prefix.

---

## What you must do

### Step 1 — Identify the latest spec

Run:

```bash
ls Specs/ | grep -E '^[0-9]{14}-' | sort | tail -1
```

That folder is the target spec. All subsequent steps operate on `Specs/<target>/`.

If no spec folder is found, stop and tell the user: "No spec folders found under `Specs/`. Run `/new-spec` first."

### Step 2 — Read the spec in full

Read all three documents in order:

1. `Specs/<target>/Requirements.md` — understand what must be built (FR labels, user stories, constraints, out-of-scope).
2. `Specs/<target>/Plan.md` — understand the technical approach, every file to modify, every file to create, dependencies, and risks.
3. `Specs/<target>/Validation.md` — understand the acceptance criteria and Definition of Done. This is your checklist.

Also read `AGENTS.md` to apply the critical rules that govern implementation in this repo.

Do not start writing code until you have read all three documents.

### Step 3 — Explore the codebase

For every file listed in Plan.md's "Existing files to modify" and "New files to create" sections:

- Read the existing file if it already exists.
- Use `find` and `grep` to locate any related files not explicitly named (e.g. callers, test files, sibling patterns).

Understand the current state before touching anything.

### Step 4 — Implement

Carry out the changes described in Plan.md exactly. For each item:

- Modify existing files as specified.
- Create new files as specified.
- Do not add features, abstractions, or refactors beyond what Plan.md requires.
- Apply all Critical Rules from `AGENTS.md`.

### Step 5 — Verify against the Definition of Done

Go through every bullet in the **Definition of Done** section of `Validation.md`. For each item, confirm it is satisfied. If a bullet refers to a file existing, read it. If it refers to a package being present, check the relevant project file.

Do not mark the task complete until every DoD item is checked.

### Step 6 — Run tests

Run the project test suite:

```bash
make test
```

Fix any test failures before reporting done. Do not skip or disable failing tests.

### Step 7 — Report back

Tell the user:

- Which spec was implemented (folder name).
- A summary of every file created or modified.
- The test results (pass/fail counts).
- Any DoD items that could not be verified automatically (e.g. manual verification steps) — list them explicitly so the user knows what to check by hand.
- Any `⚠️ TODO` items from Validation.md that were intentionally deferred — list them so the user can decide when to address them.
