# Requirements: CI Test Workflow

## Table of Contents

- [Requirements: CI Test Workflow](#requirements-ci-test-workflow)
  - [Problem Statement](#problem-statement)
  - [User Stories](#user-stories)
  - [Functional Requirements](#functional-requirements)
  - [Non-Functional Requirements](#non-functional-requirements)
  - [Out of Scope](#out-of-scope)
  - [Open Questions](#open-questions)

## Problem Statement

There is no automated gate that verifies the test suite passes before or after code lands on `main`. A developer pushing directly to `main`, or merging a pull request, has no machine-enforced check that `dotnet test WebApp.Tests/WebApp.Tests.csproj` is green. Failures are discovered only after the fact, manually, by running `make test` locally. The repository has no `.github/workflows/` directory today.

## User Stories

- Given a push to any branch, when the push completes, then a GitHub Actions job runs `dotnet test` and reports pass or fail on the commit.
- Given a push to a feature branch, when the push completes, then the developer sees a green or red check on their branch without waiting for a PR.
- Given a pull request targeting `main`, when the PR is opened or updated, then the CI check must pass before the branch can be considered mergeable.
- Given a failing test, when CI runs, then the workflow exits with a non-zero code and the GitHub check is marked as failed.
- Given a green test run, when CI completes, then the workflow exits with code 0 and the GitHub check is marked as passed.

## Functional Requirements

1. FR1 — A workflow file must exist at `.github/workflows/ci.yml` in the repository root.
2. FR2 — The workflow must trigger on `push` events targeting any branch, so feature-branch commits get a CI check without needing an open PR.
3. FR3 — The workflow must trigger on `pull_request` events targeting the `main` branch.
4. FR4 — The workflow must restore NuGet packages for `book-notes-ia.sln` before running tests.
5. FR5 — The workflow must run `dotnet test WebApp.Tests/WebApp.Tests.csproj --no-restore` and propagate the exit code to GitHub Checks.
6. FR6 — The workflow must use the .NET 9 SDK on an `ubuntu-latest` runner; no external services (PostgreSQL, Redis, Ollama) are required because all tests use in-memory fakes and `Microsoft.EntityFrameworkCore.InMemory`.
7. FR7 — The workflow must upload test results as a workflow artifact so failures can be inspected without re-running.

## Non-Functional Requirements

- The job must complete in under 5 minutes on a cold runner (NuGet restore is the main cost; caching the packages cache reduces subsequent runs to under 2 minutes).
- The workflow must not require any repository secrets for a green run; Ollama and PostgreSQL are not contacted during tests.

## Out of Scope

- Deployment or release steps; those are handled by `scripts/release.sh` and `make release`.
- Code coverage upload to an external service (Coveralls, Codecov); `coverlet.collector` is present but reporting is not wired up.
- Container-based test execution matching `docker-compose.test.yml` exactly; the GitHub Actions runner can run `dotnet` natively, which is simpler and faster.
- Branch protection rule configuration; that is a GitHub repository settings change, not a code change.

## Open Questions

- ⚠️ TODO: Is there a desired NuGet package cache strategy (e.g., cache by `book-notes-ia.sln` hash) to reduce restore time in CI?
