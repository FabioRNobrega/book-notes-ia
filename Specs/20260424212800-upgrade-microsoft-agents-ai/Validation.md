# Validation: Upgrade Microsoft.Agents.AI to 1.3.0

## Table of Contents

- [Validation: Upgrade Microsoft.Agents.AI to 1.3.0](#validation-upgrade-microsoftagentsai-to-130)
  - [Acceptance Criteria](#acceptance-criteria)
  - [Test Cases](#test-cases)
  - [Manual Verification](#manual-verification)
  - [Definition of Done](#definition-of-done)
  - [Rollback Plan](#rollback-plan)

## Acceptance Criteria

| Requirement | Acceptance Criterion |
| --- | --- |
| FR1 | `WebApp/WebApp.csproj` contains `<PackageReference Include="Microsoft.Agents.AI" Version="1.3.0" />` and no other version of the package is referenced. |
| FR2 | `dotnet build WebApp/WebApp.csproj` (inside the test container) exits with code 0 and zero errors; `IChatOrchestratorAgent.cs` compiles cleanly. |
| FR3 | `dotnet build WebApp/WebApp.csproj` exits with code 0; `Program.cs` compiles cleanly with the updated `AIAgent`/`ChatClientAgent` registration. |
| FR4 | `make test` exits with code 0; all tests in `ChatControllerTests`, `BookContextControllerTests`, and `BookContextServiceTests` pass. |
| FR5 | `git diff WebApp.Tests/WebApp.Tests.csproj` shows no package version changes; `git diff WebApp/WebApp.csproj` shows only the `Microsoft.Agents.AI` version line changed. |

## Test Cases

**Existing tests (must remain green — no changes expected to test files):**

- `WebApp.Tests/Controllers/ChatControllerTests.cs` — mock-based; does not touch `Microsoft.Agents.AI` directly.
- `WebApp.Tests/Controllers/BookContextControllerTests.cs` — mock-based; does not touch `Microsoft.Agents.AI` directly.
- `WebApp.Tests/Services/BookContextServiceTests.cs` — uses in-memory EF and `FakeOllamaService`; no agent framework dependency.

**Build verification (not a unit test, but a required gate):**

- ⚠️ TODO: Run `dotnet build WebApp/WebApp.csproj` inside `make docker-test-shell` and confirm zero errors before running the full test suite.

## Manual Verification

1. Run `make docker-test-shell` to open a shell in the .NET 9 SDK container.
2. Inside the container, run `dotnet restore book-notes-ia.sln` and confirm `Microsoft.Agents.AI 1.3.0` appears in the restore output.
3. Run `dotnet build WebApp/WebApp.csproj --no-restore` and confirm zero build errors.
4. Exit the container shell and run `make test` on the host.
5. Confirm the output shows all tests passed and the exit code is 0.
6. Run `git diff WebApp/WebApp.csproj` and confirm only the `Microsoft.Agents.AI` version line changed.

## Definition of Done

- `WebApp/WebApp.csproj` references `Microsoft.Agents.AI 1.3.0`.
- `dotnet build` succeeds with zero errors.
- `make test` passes with zero test failures.
- Requirements, Plan, and Validation docs are present in `Specs/20260424130000-upgrade-microsoft-agents-ai/`.
- `Specs/Roadmap.md` is updated with a row for this spec.
- No unrelated files are modified.

## Rollback Plan

Revert the `WebApp/WebApp.csproj` version string back to `1.0.0-preview.260212.1` and revert any call-site changes in `Program.cs` and `IChatOrchestratorAgent.cs`. Both files are tracked in Git; `git checkout HEAD -- WebApp/WebApp.csproj WebApp/Program.cs WebApp/Services/IChatOrchestratorAgent.cs` restores the original state in one command.
