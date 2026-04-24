# Requirements: Release Command

## Table of Contents

- [Problem Statement](#problem-statement)
- [User Stories](#user-stories)
- [Functional Requirements](#functional-requirements)
- [Non-Functional Requirements](#non-functional-requirements)
- [Out of Scope](#out-of-scope)
- [Open Questions](#open-questions)

## Problem Statement

The repository has no release workflow. `CHANGELOG.md` records milestones grouped by hand, `git tag --list` returns nothing, and the `Makefile` has no release target. Creating a release today requires a developer to manually move changelog entries, craft a commit message that follows the Git Guidelines in `README.md`, and push an annotated tag — steps that are easy to forget or get wrong. A single `make release VERSION=x.y.z` command should replace this manual sequence with a repeatable, convention-compliant operation.

## User Stories

- Given a clean working tree, when a developer runs `make release VERSION=1.0.0`, then `CHANGELOG.md` is updated, a GPG-signed commit following the `chore(release): v1.0.0` convention is created, and an annotated tag `v1.0.0` exists in the local repo.
- Given that `CHANGELOG.md` has entries under `## [Unreleased]`, when the release command runs, then those entries are moved under a new `## [1.0.0] - 2026-04-24` section and `## [Unreleased]` is reset to the seven standard Keep a Changelog 1.1.0 subheadings with no entries.
- Given a dirty working tree (uncommitted changes), when the release command runs, then it aborts with a clear error message before modifying any files.
- Given that `VERSION` is not set, when a developer runs `make release`, then the command aborts with a usage error explaining the required argument.
- Given a version tag that already exists in the repo, when the release command runs with that version, then it aborts without creating a duplicate tag.

## Functional Requirements

1. **FR1** — The `Makefile` must expose a `release` target that accepts a `VERSION` variable (e.g. `make release VERSION=1.0.0`).
2. **FR2** — The release target must abort with a non-zero exit code and a descriptive message when `VERSION` is not provided.
3. **FR3** — The release target must abort with a non-zero exit code when the working tree is not clean (`git status --porcelain` is non-empty).
4. **FR4** — The release target must abort with a non-zero exit code when the tag `v$(VERSION)` already exists in the local repository.
5. **FR5** — The release target must update `CHANGELOG.md`: move all content under `## [Unreleased]` into a new section `## [VERSION] - YYYY-MM-DD` inserted directly below `## [Unreleased]`, and reset `## [Unreleased]` to the seven empty Keep a Changelog 1.1.0 subheadings (`### Added`, `### Changed`, `### Deprecated`, `### Removed`, `### Fixed`, `### Security`) with no entries beneath them.
6. **FR6** — Before mutating any file, the script must verify that a GPG signing key is configured (`git config user.signingkey` is non-empty or `gpg --list-secret-keys` returns at least one key) and abort with a descriptive error if signing is not available.
7. **FR7** — The release target must stage `CHANGELOG.md` and create a GPG-signed git commit whose message follows the project Git Guidelines: `chore(release): vVERSION`.
8. **FR8** — The release target must create an annotated git tag `vVERSION` on the release commit with the message `Release vVERSION`.
9. **FR9** — A shell script `Scripts/release.sh` must contain the full release logic so that the `Makefile` target remains a thin wrapper and the script can be read and tested independently. The script receives the version as its first positional argument (`$1`) and must be committed to the repository with the executable bit set.

## Non-Functional Requirements

- The script must run with standard POSIX shell tools (`git`, `sed`, `date`) available in macOS and Linux — no external dependencies beyond what is already on the developer host.
- Guard checks are idempotent: if any guard fires before `CHANGELOG.md` is modified, running the script again for the same version produces the same error without corrupting the file or git history. If a failure occurs after `CHANGELOG.md` is modified but before the commit succeeds, the script automatically restores the file to its pre-run state via a shell `trap` before exiting.

## Out of Scope

- Pushing the commit or tag to a remote (`git push` / `git push --tags`) — the developer does this manually after reviewing the result.
- Creating a GitHub Release or any CI/CD pipeline artifact.
- Bumping version numbers in `WebApp/*.csproj` or any source file — this project does not embed a version string in application code.
- Validating that `VERSION` follows semver format (e.g. rejecting `1.0` or `abc`).

## Open Questions

None. All design questions resolved:

- Release commit is GPG-signed (`git commit -S`).
- `## [Unreleased]` uses the bracket notation required by Keep a Changelog 1.1.0.
- `## [Unreleased]` is reset to the seven standard subheadings with no entries after each release.
- `CHANGELOG.md` has been rewritten from scratch to conform to Keep a Changelog 1.1.0.
- The script receives the version as positional argument `$1` and is committed with the executable bit set via `git add --chmod=+x`.
- A shell `trap` restores `CHANGELOG.md` automatically if the script fails after mutating it.
