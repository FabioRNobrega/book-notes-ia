# Validation: Context Token Awareness

## Table of Contents

- [Acceptance Criteria](#acceptance-criteria)
- [Test Cases](#test-cases)
- [Manual Verification](#manual-verification)
- [Definition of Done](#definition-of-done)
- [Rollback Plan](#rollback-plan)

## Acceptance Criteria

| Requirement | Acceptance Criterion |
| --- | --- |
| FR1 | A unit test verifies that a `TokenCountingChatClient` wrapping a fake inner client that returns `Usage { InputTokenCount=100, OutputTokenCount=50 }` accumulates `InputTokens=200`, `OutputTokens=100` after two `GetResponseAsync` calls within the same scope. |
| FR2 | `ChatAgentRunResult` has three new fields; `ChatOrchestratorAgent.RunAsync` returns non-zero `InputTokensUsed` when the inner agent makes at least one LLM call; `ElapsedMs` is > 0. |
| FR3 | Migration `AddChatMessage` creates `chat_message` with the correct columns and a composite index on `(UserId, SessionId, DisplayOrder)` verifiable via `dotnet ef migrations list` and a PostgreSQL `\d chat_message` inspection. |
| FR4 | After `Reset`, the current `agentsession:{userId}:{sessionId}` key, `agentcontext:{userId}:{sessionId}` key, `activesessionid:{userId}` pointer, and current session `ChatMessage` rows are deleted. A subsequent `Send` creates a new `SessionId` Guid distinct from the one used before reset. |
| FR5 | After a successful `Send`, two `ChatMessage` rows exist in the DB for the current session: one with `Role="user"` (no token metrics) and one with `Role="assistant"` with non-null `InputTokensUsed`, `OutputTokensUsed`, and `ResponseTimeMs`. |
| FR6 | `Chat()` returns the same messages as `Send()` wrote to the DB, in the correct order, without parsing any session JSON. The `TryGetSessionMessages` method no longer exists in `ChatController`. |
| FR7 | After `Send`, the Redis key `agentcontext:{userId}:{sessionId}` exists and its `usagePct` field is a number in `[0, 100]`. |
| FR8 | The `_BotMessage` partial includes an element with `id="context-ring"` and `hx-swap-oob="outerHTML"` whose `value` attribute matches the `usagePct` from FR7. |
| FR9 | `BuildOrchestratorInstructions` output does not contain any book title strings; no DB query for the book list exists in `ChatController.Send`. |
| FR10 | `BuildProfileInstructions` output is a single line; fields with null/empty values do not appear as "not set" labels; a profile with all fields empty returns `null`. |
| FR11 | `docker compose logs -f webapp` shows a structured log line containing `inputTokens=`, `outputTokens=`, `elapsedMs=`, `usagePct=`, and `promptChars=` after each successful chat turn. |

## Test Cases

**Unit tests** (`WebApp.Tests/Services/` — ⚠️ TODO: create `TokenCountingChatClientTests.cs`):

- `TokenCountingChatClient_AccumulatesAcrossMultipleCalls`: open scope, call `GetResponseAsync` twice with a fake inner client returning Usage 100/50 each time, assert accumulated InputTokens=200, OutputTokens=100.
- `TokenCountingChatClient_ScopesAreIndependent`: open two sequential scopes, verify second scope starts at 0 regardless of first scope's accumulated values.
- `TokenCountingChatClient_HandlesNullUsageGracefully`: fake inner client returns null Usage; assert accumulator stays at 0, no exception thrown.

**Integration tests** (`WebApp.Tests/Integration/AgentToolsPostgresTests.cs`):

- Update `FakeChatOrchestratorAgent` to return `new ChatAgentRunResult("Saved answer", MafSessionJson, 500, 200, 38000)` — verify existing `ChatRefresh_WithPostgresSeededUserAndMafSession_RendersCachedMessages` still passes after the signature change.
- `ChatSend_WritesTwoChatMessageRows`: seed a user and book, call `controller.Send(...)`, query `db.ChatMessages` for the userId, assert exactly two rows with correct Role values and that the assistant row has non-null `InputTokensUsed`.
- `ChatReset_DeletesCurrentSessionData_NewSessionHasNoMessages`: call `Send`, capture the current `SessionId`, call `Reset`, then assert the old `agentsession:{userId}:{sessionId}` key, old `agentcontext:{userId}:{sessionId}` key, current-session pointer, and old session `ChatMessage` rows are gone. Call `Chat` and assert the chat view returns an empty message list.
- `ChatController_Chat_ReadsFromDB_NotSessionJson`: seed two `ChatMessage` rows directly in DB with a known `SessionId` stored in the fake cache, call `controller.Chat()`, assert the returned entries match the seeded rows (not any session JSON).

**Unit tests** (`WebApp.Tests/Controllers/` — ⚠️ TODO: create `ChatControllerProfileTests.cs`):

- `BuildProfileInstructions_WithAllFieldsSet_ReturnsSingleLine`: pass JSON with all fields populated, assert result has no newline and no "not set" substring.
- `BuildProfileInstructions_WithEmptyFields_OmitsThemFromSentence`: pass JSON with only `nickname` set, assert only the nickname appears in the output.
- `BuildProfileInstructions_WithNoFields_ReturnsNull`: pass empty JSON `{}`, assert result is null.
- `BuildOrchestratorInstructions_DoesNotContainBookTitles`: assert output does not include "User's book library" or any book enumeration.

## Manual Verification

1. Start the stack: `make docker-run`.
2. Sign in, navigate to chat.
3. Verify the `#context-ring` element exists near the chat input showing `0%` initially.
4. Send a message. Verify:
   - The ring updates to a non-zero percentage after the response arrives.
   - The response time (seconds) appears next to the ring.
   - `docker compose logs -f webapp` shows a `Turn stats:` log line with all five fields.
5. Send a second message in the same session. Verify the ring percentage increases.
6. Open a PostgreSQL shell: `docker compose exec postgres psql -U postgres booknotes -c "SELECT role, display_order, input_tokens_used, response_time_ms FROM chat_message ORDER BY display_order;"`. Verify two rows per turn with expected values.
7. Click chat reset. Verify the ring returns to 0%, the chat shows the welcome message, the previous session's `chat_message` rows are deleted, and `activesessionid:<userId>` is absent.
8. Send one more message. Verify a new session ID appears in Redis: `docker compose exec redis redis-cli GET activesessionid:<userId>`. Verify the MAF session JSON and context metadata are stored under keys that include both the user ID and the session ID: `agentsession:<userId>:<sessionId>` and `agentcontext:<userId>:<sessionId>`.
9. Check `docker compose logs -f webapp` — verify the book list no longer appears in instructions and the profile is one line.
10. Run `make test` and confirm all tests pass.

## Definition of Done

- `Specs/20260605112315-context-token-awareness/` contains Requirements, Plan, and Validation.
- `TokenCountingChatClient` and `TokenAccumulator` exist and are tested.
- `ChatMessage` model, migration, and `AppDbContext` configuration are in place.
- `ChatOrchestratorAgent.RunAsync` returns token counts and elapsed time.
- `ChatController.Send` writes `ChatMessage` rows and updates `agentcontext:{userId}:{sessionId}`.
- `ChatController.Chat` reads from DB; `TryGetSessionMessages` is deleted.
- `ChatController.Reset` clears the current session keys and deletes current session DB rows.
- `_BotMessage.cshtml` renders the `<sl-progress-ring>` OOB swap.
- `Index.cshtml` has the `#context-ring` placeholder element.
- Book list removed from orchestrator instructions; profile compressed to single sentence.
- All new and existing tests pass under `make test`.

## Rollback Plan

- Remove the `#context-ring` element from `Index.cshtml` and the OOB block from `_BotMessage.cshtml` to stop the ring from appearing — no other changes needed for the UI.
- Revert `BuildOrchestratorInstructions` and `BuildProfileInstructions` to restore the book list and verbose profile — a two-function revert.
- The `chat_message` table can stay, but reset intentionally deletes rows for the current session.
- To stop writing to `chat_message`: remove the two `db.ChatMessages.Add(...)` calls in `ChatController.Send`.
- To restore session-JSON-based display: reintroduce `TryGetSessionMessages` and revert `ChatController.Chat` — the session JSON in Redis is still present and unchanged in format.
- No migration rollback is required unless the table itself needs to be dropped.
