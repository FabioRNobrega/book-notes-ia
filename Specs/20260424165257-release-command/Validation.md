# Validation: Release Command

## Table of Contents

- [Acceptance Criteria](#acceptance-criteria)
- [Test Cases](#test-cases)
- [Manual Verification](#manual-verification)
- [Definition of Done](#definition-of-done)
- [Rollback Plan](#rollback-plan)

## Acceptance Criteria

| Requirement | Acceptance Criterion |
| --- | --- |
| FR1 | `make release VERSION=1.0.0` exits 0 and the `release` target is listed in `.PHONY` in `Makefile`. |
| FR2 | `make release` (no VERSION) exits non-zero and prints a usage message containing `VERSION`. |
| FR3 | Running `make release VERSION=1.0.0` with a dirty working tree exits non-zero and prints a message containing `working tree`. |
| FR4 | Running `make release VERSION=1.0.0` a second time (tag already exists) exits non-zero and prints a message containing `already exists`. |
| FR5 | After a successful run, `CHANGELOG.md` contains `## [1.0.0] - <today's date>` with the moved entries beneath it; `## [Unreleased]` sits directly above it and contains exactly the seven Keep a Changelog 1.1.0 subheadings (`### Added`, `### Changed`, `### Deprecated`, `### Removed`, `### Fixed`, `### Security`) with no entries. |
| FR6 | Running `make release VERSION=1.0.0` with no GPG signing key configured exits non-zero before `CHANGELOG.md` is modified; `git status --porcelain` shows no changes after the abort. |
| FR7 | `git log -1 --pretty=%s` outputs exactly `chore(release): v1.0.0`; `git log -1 --show-signature` shows a valid GPG signature. |
| FR8 | `git tag --list v1.0.0` returns `v1.0.0`; `git show v1.0.0` shows the annotation message `Release v1.0.0`. |
| FR9 | `Scripts/release.sh` exists, is executable (`ls -l Scripts/release.sh` shows `-rwxr-xr-x`), `git ls-files --stage Scripts/release.sh` shows mode `100755`, and the `Makefile` `release` target invokes it with `$(VERSION)` as the first argument. |

## Test Cases

**Unit tests:**

No automated unit tests are planned for `Scripts/release.sh`. The script uses only POSIX shell and git commands; the manual verification steps below cover all eight FRs. If a CI pipeline is introduced, a Bats test suite should be added at that point.

**Integration tests:**

No containerised integration test is defined. The existing `docker-compose.test.yml` runs `dotnet test` only and the release script has no application code dependency. A shell-level integration test (clone into a temp directory, run the script, assert git state) is appropriate if CI is introduced.

## Manual Verification

1. Start from a clean working tree on `main`: `git status` must show nothing to commit.
2. Ensure no version tags exist: `git tag --list` should return empty.
3. Add a test entry under `### Added` in `## [Unreleased]` in `CHANGELOG.md` (e.g. `- Test release entry.`) and stage + commit it so the tree is clean again.
4. Run `make release VERSION=0.1.0`.
5. Verify exit code 0.
6. Verify `CHANGELOG.md` now contains `## [0.1.0] - <today>` with the test entry beneath it; `## [Unreleased]` above it contains exactly the seven standard subheadings with no entries.
7. Verify commit: `git log -1 --pretty=%s` → `chore(release): v0.1.0`; `git log -1 --show-signature` shows a valid GPG signature.
8. Verify tag: `git tag --list` → `v0.1.0`; `git show v0.1.0` → shows annotation `Release v0.1.0`.
9. Guard — dirty tree: make a change to any file without committing, then run `make release VERSION=0.2.0`; expect non-zero exit and "working tree" error.
10. Guard — no VERSION: run `make release`; expect non-zero exit and usage message containing `VERSION`.
11. Guard — duplicate tag: run `make release VERSION=0.1.0` again; expect non-zero exit and "already exists" message.
12. Guard — missing `## [Unreleased]`: temporarily remove the `## [Unreleased]` line from `CHANGELOG.md`, run `Scripts/release.sh 0.2.0` directly; expect non-zero exit, an error message, and `CHANGELOG.md` unchanged.
13. Guard — GPG not configured: temporarily unset `user.signingkey` (`git config --local --unset user.signingkey`), run `make release VERSION=0.2.0`; expect non-zero exit before `CHANGELOG.md` is modified (`git status --porcelain` must be empty after abort). Restore the key afterwards.
14. Partial-run recovery: manually mutate `CHANGELOG.md` without staging it (simulate a mid-run failure), then run `make release VERSION=0.2.0`; expect FR3 (dirty tree guard) to fire and `CHANGELOG.md` to be left as-is (the trap only fires from within the script's own run).
15. Clean up test tag and commit before merging: `git tag -d v0.1.0 && git reset --hard HEAD~2`.

## Definition of Done

- `Scripts/release.sh` exists, is executable, committed with mode `100755`, and passes all manual verification steps above.
- `Makefile` has `release` in `.PHONY` and a `release` target that invokes `Scripts/release.sh $(VERSION)`.
- All existing `make test` tests still pass (the script touches no application code).
- `CHANGELOG.md` structure is preserved and conformant with Keep a Changelog 1.1.0 after a release run.
- No `.env`, generated CSS, `bin/`, or `obj/` content is committed.

## Rollback Plan

- The release command only affects local git state (a commit and a tag). No remote is touched.
- To undo: `git tag -d vVERSION` removes the tag; `git reset --hard HEAD~1` removes the release commit and restores `CHANGELOG.md` to its pre-release state.
- If the script aborted mid-run and the `trap` restored `CHANGELOG.md` automatically, no manual file recovery is needed — only verify `git status` is clean.
- No application code, database schema, or Docker configuration is modified by this command.
