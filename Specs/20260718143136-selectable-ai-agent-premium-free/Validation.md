# Validation: Selectable AI Agent (Premium ChatGPT / Free Qwen 3.5)

## Table of Contents

- [Validation: Selectable AI Agent (Premium ChatGPT / Free Qwen 3.5)](#validation-selectable-ai-agent-premium-chatgpt--free-qwen-35)
  - [Acceptance Criteria](#acceptance-criteria)
  - [Test Cases](#test-cases)
  - [Manual Verification](#manual-verification)
  - [Definition of Done](#definition-of-done)
  - [Rollback Plan](#rollback-plan)

## Acceptance Criteria

| Requirement | Acceptance Criterion |
| --- | --- |
| FR1 | `GET /` renders an `sl-select` with exactly `sl-option value="premium"` ("Premium — ChatGPT") and `sl-option value="free"` ("Free — Qwen 3.5"); any authenticated user can select either without a permission check. |
| FR2 | `POST /chat/agent` with a valid `agentKey` writes `activeagent:{userId}` in Redis and returns the `_AgentIndicator` partial; `#chat`, `activesessionid:{userId}`, and `agentsession:{userId}:{sessionId}` are unchanged by the call. |
| FR3 | `ChatController.Send` resolves `IChatAgentProvider.GetAgent(agentKey)` and the returned `ChatAgentRunResult.ResponseText` originates from the correct provider (verified via a distinguishing fake response per provider in tests). |
| FR4 | A chat turn on either agent that asks about a book in the user's library successfully triggers `GenerateBookContext`, `GetBookNotesWithAnalysis`, or `GetRelevantBookNotes` and returns tool output in the response — same behavior for both `agentKey` values. |
| FR5 | `BookContextService.GenerateAndSaveAsync` called with `agentKey = "premium"` resolves the Azure-keyed `IChatClient` via `IChatClientProvider`; called with `"free"` resolves the Ollama-keyed one — verified with fakes, not real endpoints. |
| FR6 | A two-turn test — turn 1 on `"free"`, agent switched to `"premium"` via `POST /chat/agent`, turn 2 sent — round-trips the serialized session so turn 2's `AgentSession` still contains turn 1's message history. |
| FR7 | `agentcontext:{userId}:{sessionId}` and the `ContextUsagePct` returned in `_BotMessage`/history are populated (non-null, in `[0,100]`) for a turn run on either agent. |
| FR8 | After `Send` completes, the persisted assistant `ChatMessage` row has `AgentType` equal to the `agentKey` used for that turn; `Chat.cshtml` and `_BotMessage.cshtml` render a label matching it. |
| FR9 | A user with no `activeagent:{userId}` key present is routed through the Ollama agent by `Send`, and `Index.cshtml` pre-selects `sl-option value="free"` on load. |
| FR10 | A test where the Azure-keyed `IChatClient` fake throws (auth/timeout/etc.) causes `Send` to return `_BotMessage` with `<p>Error: ...</p>` and does not call the Ollama-keyed client for that turn. |
| FR11 | `HomeController.Index` sets `ViewData["ActiveAgent"]` from cache before rendering, matching the value later confirmed by `GET /chat/agent`'s state (no reliance on a prior change event in the same page load). |

## Test Cases

**Unit tests:**

- `WebApp.Tests/Controllers/ChatControllerTests.cs`: extend with cases for `SetAgent` (persists cache key, returns indicator partial), `Send` routing to the correct fake `AIAgent` per `activeagent:{userId}` value, default-to-`"free"` when the cache key is absent or holds an invalid value, and the error-partial-on-Azure-failure path (FR2, FR3, FR9, FR10).
- `WebApp.Tests/Services/BookContextServiceTests.cs`: update existing tests for the new `agentKey` parameter on `GenerateAndSaveAsync`/`GenerateContextAsync`, asserting the fake `IChatClientProvider` is queried with the passed-through key (FR5).
- `WebApp.Tests/Services/BookContextAgentToolTests.cs`: update for `Create(userId, agentKey)`, asserting the tool forwards `agentKey` into `IBookContextService.GenerateAndSaveAsync` (FR4, FR5).
- New `WebApp.Tests/Services/ChatCompletionServiceTests.cs` (renamed/replacing any existing Ollama-service-specific test if one exists): verify `CompleteAsync(prompt, agentKey, ct)` resolves the client via `IChatClientProvider.GetChatClient(agentKey)` for both keys.
- New `WebApp.Tests/Services/ChatAgentProviderTests.cs` / `ChatClientProviderTests.cs`: verify `GetAgent`/`GetChatClient` resolve the correct keyed DI registration and throw a clear exception for an unknown key.
- New `WebApp.Tests/Services/ChatOrchestratorAgentTests.cs` (or extend existing coverage if present): verify `RunAsync(agent, ...)` works with an injected fake `AIAgent` rather than a constructor-bound one, and that session serialization round-trips across two different fake `AIAgent` instances sharing the same underlying message store shape (FR6).
- `WebApp.Tests/Controllers/HomeControllerTests.cs` (create if it does not exist, mirroring the existing `ContextUsagePct` test pattern): verify `ViewData["ActiveAgent"]` reflects cache state, defaulting to `"free"` (FR11).

**Integration tests:**

- `WebApp.Tests/Integration/AgentToolsPostgresTests.cs`: update call sites for the new `agentKey` parameter on `BookContextAgentTool.Create` / `IBookContextService`, keeping the existing Docker-backed Postgres/pgvector coverage for book lookup working under both agent keys (FR4, FR5).
- ⚠️ TODO: A full HTMX/browser-level test that `POST /chat/agent` followed by `POST /chat/send` actually reaches the real Ollama container in the Docker Compose stack does not exist yet and would need to be added as a manual or Playwright-style check if deeper end-to-end coverage is desired later; Azure-side end-to-end coverage should stay faked, not run against the real endpoint.

## Manual Verification

1. Start the stack: `make docker-run` (Linux/SteamOS).
2. Sign in, confirm `.env` has real values for `AZURE_OPENAI_ENDPOINT`, `AZURE_LLM_DEPLOYMENT_NAME`, and `AZURE_OPENAI_API_KEY` (copy from `.env.example` and fill in the key).
3. On the home page, confirm the `sl-select` shows "Free — Qwen 3.5" selected by default with no prior interaction.
4. Send a message with Free active; confirm the response, context ring, and TTS controls behave exactly as before this change, and the message bubble shows a "Free" label.
5. Switch the selector to "Premium — ChatGPT"; confirm the indicator updates immediately and the chat history is not cleared.
6. Send a follow-up message referencing something from the earlier Free-agent turn (e.g. "what did I just ask you?"); confirm the Azure-backed response demonstrates awareness of that prior turn (validates FR6 in the real stack, not just the fake-based unit test).
7. Ask about a book in the user's library while on Premium; confirm `GenerateBookContext` still returns grounded context (validates FR4/FR5 against the real Azure deployment).
8. Temporarily set an invalid `AZURE_OPENAI_API_KEY`, restart the stack, send a message on Premium; confirm the chat surfaces the existing error bubble and the Free agent is not silently used instead (FR10). Revert the key afterward.
9. Reload the page after switching to Premium; confirm the selector and indicator still show "Premium — ChatGPT" (FR11), and past assistant messages in history show the correct per-message agent label (FR8).
10. Run `make test` and confirm all `WebApp.Tests` (including the new/updated ones above) and `TtsService.Tests` pass.

## Definition of Done

- Requirements, Plan, and Validation docs in this folder are complete and internally consistent.
- All existing `WebApp.Tests` and `TtsService.Tests` pass via `make test`; new/updated tests listed above are green.
- `Index.cshtml` selector and indicator work as described, verified per the Manual Verification steps above.
- The `ChatMessage.AgentType` migration applies cleanly via `dotnet ef migrations add`/`dotnet ef database update` inside the `webapp` container, and existing rows do not error on the new nullable column.
- No secret (`AZURE_OPENAI_API_KEY`) appears in any log statement, committed file, or test fixture.
- `docker-compose.yml` passes the three Azure env vars through to `webapp`, matching the existing `UNSPLASH_ACCESS_KEY` pass-through pattern.

## Rollback Plan

- The `AgentType` column on `ChatMessage` is additive and nullable — reverting the application code without reverting the migration is safe; unused columns have no runtime effect.
- Because the default is `"free"` (FR9) and there is no automatic fallback out of Premium (FR10), the Azure integration is only exercised when a user explicitly selects it — if `AZURE_OPENAI_ENDPOINT`/`AZURE_LLM_DEPLOYMENT_NAME`/`AZURE_OPENAI_API_KEY` are unset or invalid, only the Premium path is affected; the Free/Ollama path (today's only path) keeps working unchanged.
- To fully roll back, revert the commits touching `WebApp/Program.cs`, `ChatController.cs`, `BookContextService.cs`/`BookContextAgentTool.cs`, `IOllamaService.cs`/`OllamaService.cs`, `Index.cshtml`, `Chat.cshtml`, `_BotMessage.cshtml`, and drop the `AgentType` migration with `dotnet ef migrations remove` if it has not yet been applied to a shared environment, or a follow-up migration dropping the column if it has.
