# Validation: Multiple Free Ollama Agents

## Table of Contents

- [Validation: Multiple Free Ollama Agents](#validation-multiple-free-ollama-agents)
  - [Acceptance Criteria](#acceptance-criteria)
  - [Test Cases](#test-cases)
  - [Manual Verification](#manual-verification)
  - [Definition of Done](#definition-of-done)
  - [Rollback Plan](#rollback-plan)

## Acceptance Criteria

| Requirement | Acceptance Criterion |
| --- | --- |
| FR1 | The supported key list contains `premium`, `free-qwen`, `free-llama3`, `free-phi4`, and `free-granite4`; the old single `"free"` key is not rendered as a selectable menu value. |
| FR2 | Passing `null`, `""`, unknown text, or legacy `"free"` to normalization returns `free-qwen`. |
| FR3 | DI can resolve keyed `IChatClient` and `AIAgent` services for `free-qwen`, `free-llama3`, `free-phi4`, `free-granite4`, and `premium`. |
| FR4 | Each free local `IChatClient` registration applies `Temperature = 0`, `think = false`, and `num_ctx = Ollama:NumCtx`. |
| FR5 | Server and browser normalization accept the five supported keys and default invalid values to `free-qwen`. |
| FR6 | `_AgentIndicator.cshtml` displays Premium, Free - Qwen 3.5, Free - Llama 3.2, Free - Phi-4 Mini, and Free - Granite 4; no visible menu text contains "Ollama". |
| FR7 | Sending with each free key stores that exact key in Redis `activeagent:{userId}` and in the assistant `ChatMessage.AgentType`. |
| FR8 | A book-context tool call made during a `free-llama3`, `free-phi4`, or `free-granite4` turn passes the same key through to `BookContextService` and `IChatClientProvider`. |
| FR9 | The `ollama` service startup command pulls all four chat models and `mxbai-embed-large`. |
| FR10 | Adding a fifth free model can be described as updating the catalog/model list and Docker pull list, without changing controller normalization branches or view label switches. |

## Test Cases

**Unit tests:**

- `WebApp.Tests/Services/ChatAgentCatalogTests.cs`: verify supported entries, default key, legacy `"free"` normalization, invalid-key fallback, friendly labels, and local model names for Qwen, Llama 3.2, Phi-4 Mini, and Granite 4.
- `WebApp.Tests/Controllers/ChatControllerTests.cs`: extend `SetAgent` and `Send` tests for `free-qwen`, `free-llama3`, `free-phi4`, and `free-granite4`; assert exact cache value, exact `ChatMessage.AgentType`, selected fake `AIAgent`, and `BookContextAgentTool` key.
- `WebApp.Tests/Controllers/HomeControllerTests.cs`: assert missing or legacy cached values normalize to `free-qwen`, and supported free keys flow into `ViewData["ActiveAgent"]`.
- `WebApp.Tests/Services/ChatAgentProviderTests.cs` and `ChatClientProviderTests.cs`: register fakes under the new keys and verify each resolves correctly.
- `WebApp.Tests/Services/ChatCompletionServiceTests.cs`: verify `CompleteAsync(prompt, agentKey, ct)` requests the selected free key and premium key through `IChatClientProvider`.
- `WebApp.Tests/Services/BookContextServiceTests.cs` and `BookContextAgentToolTests.cs`: update existing cases to use `free-qwen` as the default free key and add at least one non-Qwen free model pass-through case.
- Existing label tests or new focused tests for `BotMessageViewModel.AgentLabel` / `ChatController.GetAgentLabel`: verify friendly labels for all new keys plus legacy `"free"`.

**Integration tests:**

- `WebApp.Tests/Integration/AgentToolsPostgresTests.cs`: update existing default-free expectations from `"free"` to `free-qwen`; add one provider-pass-through case for a non-Qwen free key (e.g. `free-llama3`) using fakes so the test does not require real local model inference.
- ⚠️ TODO: A full Docker-backed chat turn against each real Ollama chat model can remain manual unless the test harness already has a reliable way to wait for model pulls and bound inference time.

## Manual Verification

1. Start the local stack with the platform-appropriate Make target, for example `make docker-run` on Linux/SteamOS.
2. Watch the `ollama` container logs and confirm it pulls `qwen3.5:4b`, `llama3.2:3b`, `phi4-mini:3.8b`, `granite4:3b`, and `mxbai-embed-large`.
3. Sign in and open the home chat dock.
4. Open the agent selector and confirm it shows Premium plus four free local choices, with no visible "Ollama" wording.
5. Select Free - Qwen 3.5, send a message, and confirm the assistant response renders with the Qwen/free label.
6. Select Free - Llama 3.2, send a message, and confirm the new assistant response renders with the Llama/free label, does not error with a "does not support tools" message, and prior chat history remains visible.
7. Select Free - Phi-4 Mini or Free - Granite 4, ask about a saved book, and confirm the response can still use generated book context through the Microsoft Agent Framework tool path without a tools-unsupported error.
8. Refresh the page and confirm the last selected agent remains selected via `activeagent:{userId}`.
9. Temporarily set a stale Redis active-agent value of `"free"` if convenient, reload chat, and confirm it behaves as Qwen.
10. Run `make test` to verify the containerized test suite.

## Definition of Done

- Requirements, Plan, and Validation docs exist in this spec folder.
- `premium`, `free-qwen`, `free-llama3`, `free-phi4`, and `free-granite4` are the only selectable agent keys.
- Every free local model is confirmed tool-calling capable on Ollama (checked against `ollama.com/library`) before being added to the catalog.
- Legacy `"free"` remains accepted and maps to `free-qwen`.
- All free agents use the current Qwen-style Ollama options.
- Book context generation uses the same selected key as the main chat turn.
- The agent selector UI is updated, accessible, compact, and free of visible "Ollama" wording.
- Docker Compose auto-pulls all required local models.
- Unit tests and relevant integration tests are updated.
- `make test` passes.
- README/model documentation is updated.

## Rollback Plan

- Revert the catalog, controller, view, JavaScript, Docker Compose, README, and test changes from this feature.
- Restore the prior keyed `"free"` Ollama registration in `WebApp/Program.cs` and the prior `"premium"`/`"free"` normalization in `ChatController` and `site.js`.
- Existing `ChatMessage.AgentType` values of `free-qwen`, `free-llama3`, `free-phi4`, or `free-granite4` can remain in the nullable string column; after rollback they will render as unlabeled or default to Free depending on the restored code, but they do not block chat operation.
- No database migration rollback is required because this feature does not add schema.
