# Requirements: Upgrade Microsoft.Agents.AI to 1.3.0

## Table of Contents

- [Requirements: Upgrade Microsoft.Agents.AI to 1.3.0](#requirements-upgrade-microsoftagentsai-to-130)
  - [Problem Statement](#problem-statement)
  - [User Stories](#user-stories)
  - [Functional Requirements](#functional-requirements)
  - [Non-Functional Requirements](#non-functional-requirements)
  - [Out of Scope](#out-of-scope)
  - [Open Questions](#open-questions)

## Problem Statement

`WebApp/WebApp.csproj` pins `Microsoft.Agents.AI` at `1.0.0-preview.260212.1`, a pre-release package from February 2026. As of 2026-04-24, the package has reached stable `1.3.0` on NuGet (https://www.nuget.org/packages/Microsoft.Agents.AI). Pinning a preview release in production code means the project misses stability fixes and API refinements shipped across `1.0.0`, `1.1.0`, `1.2.0`, and `1.3.0`. It also signals to contributors that the AI agent integration is experimental when it is not.

The two files that consume the package's public API directly are `WebApp/Services/IChatOrchestratorAgent.cs` and `WebApp/Program.cs`. The types used are `AIAgent`, `AgentSession`, `ChatClientAgent`, and `ChatClientAgentRunOptions`. Any breaking API changes between the preview and `1.3.0` must be identified and adapted.

## User Stories

- Given the app is built with the updated package, when `dotnet build` runs, then the build succeeds with zero errors related to `Microsoft.Agents.AI`.
- Given the app is running, when a user sends a chat message, then the `ChatOrchestratorAgent` still serializes and deserializes the session correctly and returns a non-empty response.
- Given the test suite runs after the upgrade, when `dotnet test WebApp.Tests/WebApp.Tests.csproj` executes, then all existing tests still pass.

## Functional Requirements

1. FR1 — `WebApp/WebApp.csproj` must reference `Microsoft.Agents.AI` at version `1.3.0` (the current stable release as of 2026-04-24).
2. FR2 — `WebApp/Services/IChatOrchestratorAgent.cs` must compile without errors against the `1.3.0` API; any renamed or removed types (`AIAgent`, `AgentSession`, `ChatClientAgent`, `ChatClientAgentRunOptions`) must be updated to their `1.3.0` equivalents.
3. FR3 — `WebApp/Program.cs` must compile without errors; the `ChatClientAgent` construction and `AIAgent` singleton registration must be valid under `1.3.0`.
4. FR4 — `make test` (`docker compose -f docker-compose.test.yml run --rm tests`) must exit with code 0 after the upgrade.
5. FR5 — No other packages in `WebApp.csproj` or `WebApp.Tests.csproj` may be inadvertently upgraded or downgraded as a side effect of this change.

## Non-Functional Requirements

- The upgrade must not change any user-visible behaviour — session serialization, response text, and chat reset must work identically.
- The package version must be an exact pin (`1.3.0`), not a floating range, to match the pinning convention used by all other packages in `WebApp.csproj`.

## Out of Scope

- Adopting new features introduced in `1.1.0`–`1.3.0` beyond what is required to keep the existing code compiling and passing tests.
- Upgrading `Microsoft.Extensions.AI` or `Microsoft.Extensions.AI.Abstractions` (currently `10.3.0`); those are separate packages with their own release cadence.
- Upgrading any other `Microsoft.Agents.*` packages not currently referenced.

## Open Questions

- ⚠️ TODO: Verify whether `AIAgent`, `AgentSession`, `ChatClientAgent`, and `ChatClientAgentRunOptions` exist with identical signatures in `1.3.0`, or whether any have been renamed/moved. This must be checked against the `1.3.0` package contents or release notes before implementation begins.
- ⚠️ TODO: Confirm whether the `1.3.0` stable package is available on the default `nuget.org` feed without any additional NuGet source configuration.
