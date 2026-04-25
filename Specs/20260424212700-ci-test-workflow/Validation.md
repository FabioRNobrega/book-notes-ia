# Validation: CI Test Workflow

## Table of Contents

- [Validation: CI Test Workflow](#validation-ci-test-workflow)
  - [Acceptance Criteria](#acceptance-criteria)
  - [Test Cases](#test-cases)
  - [Manual Verification](#manual-verification)
  - [Definition of Done](#definition-of-done)
  - [Rollback Plan](#rollback-plan)

## Acceptance Criteria

| Requirement | Acceptance Criterion |
| --- | --- |
| FR1 | `.github/workflows/ci.yml` exists in the repository and is valid YAML parseable by GitHub Actions. |
| FR2 | Pushing a commit to `main` triggers the workflow; the Actions tab shows a run for that commit. |
| FR3 | Opening or updating a pull request targeting `main` triggers the workflow; the PR shows a CI status check. |
| FR4 | The workflow step `dotnet restore book-notes-ia.sln` exits with code 0 and all packages are resolved. |
| FR5 | The workflow step `dotnet test` exits with code 0 when all tests pass, and with a non-zero code when any test fails, causing the GitHub Check to be marked as failed. |
| FR6 | The workflow uses `dotnet-version: '9.0.x'` and runs on `ubuntu-latest`; no `services:` block is present in the workflow YAML. |
| FR7 | A workflow artifact named `test-results` is uploaded after the test step and is visible in the Actions run summary. |

## Test Cases

**Unit tests (already passing, must remain green after this change):**

- `WebApp.Tests/Controllers/ChatControllerTests.cs` — verifies chat reset clears both cache keys.
- `WebApp.Tests/Controllers/BookContextControllerTests.cs` — verifies book context API response structure.
- `WebApp.Tests/Services/BookContextServiceTests.cs` — verifies `GenerateToolResponseAsync` appends context and persists the generated summary using in-memory EF Core.

**CI workflow correctness (manual / observable in GitHub Actions):**

- ⚠️ TODO: After merging, push a commit that intentionally breaks a test (e.g., change an `Assert.Equal` to fail), confirm the CI check turns red, then revert and confirm it turns green.

## Manual Verification

1. Ensure the repository is pushed to GitHub with the `.github/workflows/ci.yml` file on the `main` branch.
2. Navigate to the repository on GitHub → **Actions** tab.
3. Confirm a workflow run named (or matching) `ci` appears for the latest commit on `main`.
4. Open the run and verify the job `test` shows three steps completing successfully: `Restore`, `Test`, and `Upload test results`.
5. In the run summary, confirm the `test-results` artifact is listed and downloadable.
6. Open any pull request targeting `main` and confirm the CI check appears in the PR's **Checks** section before merge.
7. To verify failure detection: locally edit `WebApp.Tests/Controllers/ChatControllerTests.cs`, change one assertion to fail, push to a feature branch, open a PR to `main`, and confirm the CI check reports failure. Revert the change.

## Definition of Done

- `.github/workflows/ci.yml` is committed and present on `main`.
- All existing tests (`ChatControllerTests`, `BookContextControllerTests`, `BookContextServiceTests`) still pass in CI.
- The GitHub Actions run completes in under 5 minutes on a warm cache run.
- Requirements, Plan, and Validation docs are present in `Specs/20260424120000-ci-test-workflow/`.
- `Specs/Roadmap.md` is updated with a row for this spec.

## Rollback Plan

Delete `.github/workflows/ci.yml` and push the deletion to `main`. This immediately removes the CI trigger; no application code or configuration is affected. The workflow has no side effects (it does not push, deploy, or mutate any state). Re-enabling is as simple as restoring the file from Git history: `git checkout <original-sha> -- .github/workflows/ci.yml`.
