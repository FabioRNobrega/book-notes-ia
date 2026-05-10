---
description: Review the current branch against a spec to verify correct implementation and surface leftovers or errors.
---

# review-implementation

Review the current branch against a spec and report on completeness,
correctness, and any deviations.

## Usage

```
/review-implementation
```

Optional: `/review-implementation <spec-folder-name-or-path>`

Examples:
- `/review-implementation` — uses the latest spec under `Specs/`
- `/review-implementation 20260427110015-maf-agent-as-tools-refactor`
- `/review-implementation Specs/20260427110015-maf-agent-as-tools-refactor`

---

## What you must do

### Step 1 — Identify the target spec

If `$ARGUMENTS` is provided, resolve it to a spec folder:
- If it starts with `Specs/`, use it as-is.
- Otherwise, check if `Specs/$ARGUMENTS` exists. If it does, use that.
- If the argument does not resolve to a folder, stop and tell the user:
  "Spec folder not found: `$ARGUMENTS`. Check `ls Specs/` for available specs."

If `$ARGUMENTS` is empty, find the latest spec:

```bash
ls Specs/ | grep -E '^[0-9]{14}-' | sort | tail -1
```

If no spec folder is found, stop and tell the user:
"No spec folders found under `Specs/`. Run `/new-spec` first."

### Step 2 — Read the spec in full

Read all three documents:

1. `Specs/<target>/Requirements.md` — the FRs, user stories, and out-of-scope items.
2. `Specs/<target>/Plan.md` — every file to modify, every file to create, the
   component breakdown, dependencies, and risks.
3. `Specs/<target>/Validation.md` — the acceptance criteria, test cases, manual
   verification steps, and Definition of Done.

Also read `AGENTS.md` for the critical rules that govern implementation in this repo.

Build a checklist from these three documents before touching the codebase.

### Step 3 — Gather the branch diff

Run these commands to understand what changed on the current branch:

```bash
git diff main...HEAD --stat
git log --oneline main..HEAD
```

Then read the full diff for files that appear in Plan.md:

```bash
git diff main...HEAD -- <file>
```

If the branch has no commits ahead of main, stop and tell the user:
"No commits found ahead of main. Nothing to review."

### Step 4 — Verify the file-level implementation

For every **existing file to modify** listed in Plan.md:

- Read the current state of the file.
- Check that the changes described in Plan.md are actually present.
- Flag any described change that is absent, partial, or differs significantly
  from the spec.

For every **new file to create** listed in Plan.md:

- Check whether the file exists (`find` or direct read).
- If it exists, read it and verify it matches the described purpose.
- If it is missing, flag it.

Use `grep` to verify key symbols, types, or identifiers that Plan.md names
explicitly (e.g. a class, interface, method, or config key).

### Step 5 — Check acceptance criteria

Go through every row in the **Acceptance Criteria** table in Validation.md.
For each criterion:

- Determine whether it can be verified statically (file exists, symbol present,
  config set) or requires running the app.
- For static checks: perform the check now and record pass/fail.
- For runtime checks: mark as **manual** and list the verification step from
  Validation.md.

### Step 6 — Check the Definition of Done

Go through every bullet in the **Definition of Done** section. For each:

- Verify it statically where possible (e.g. spec files updated, no skipped tests).
- Flag any bullet that is not satisfied.

### Step 7 — Run tests

Run the project test suite:

```bash
make test
```

Record the outcome. If tests fail, read the error output to determine whether
the failure is caused by the implementation under review or by a pre-existing
issue.

### Step 8 — Report back

Produce a structured report with the following sections. Use checkboxes
(`- [x]` / `- [ ]`) for each item so the user can see status at a glance.

---

#### Spec reviewed
`Specs/<target>/` against branch `<branch-name>`

---

#### Files — Plan.md coverage

List every file from Plan.md with its status:

| File | Expected change | Status |
| ---- | --------------- | ------ |
| `path/to/file` | What Plan.md says | ✅ Done / ⚠️ Partial / ❌ Missing |

---

#### Acceptance Criteria

| Requirement | Criterion | Status |
| ----------- | --------- | ------ |
| FR1 | ... | ✅ Pass / ❌ Fail / 🔲 Manual |
| FR2 | ... | ... |

---

#### Definition of Done

- [x] Item satisfied
- [ ] Item not satisfied — reason

---

#### Test results

Pass / Fail / Error — include failure summary if any tests failed.

---

#### Issues found

Number each issue. For each issue state:
- **What**: what is wrong or missing.
- **Where**: file and line if known.
- **Spec reference**: which FR, Plan.md section, or DoD bullet it violates.
- **Severity**: `blocking` (must fix before merge) or `minor` (deviation but
  feature still works).

If no issues were found, say so explicitly.

---

#### Manual verification steps

List every acceptance criterion or DoD item marked 🔲 Manual, with the
exact steps from Validation.md so the user knows what to verify by hand.
