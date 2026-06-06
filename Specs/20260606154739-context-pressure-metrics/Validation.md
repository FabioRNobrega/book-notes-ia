# Validation: Context Pressure Metrics

## Table of Contents

- [Validation: Context Pressure Metrics](#validation-context-pressure-metrics)
  - [Table of Contents](#table-of-contents)
  - [Acceptance Criteria](#acceptance-criteria)
  - [Test Cases](#test-cases)
  - [Manual Verification](#manual-verification)
  - [Definition of Done](#definition-of-done)
  - [Rollback Plan](#rollback-plan)

## Acceptance Criteria

| Requirement | Acceptance Criterion |
| --- | --- |
| FR1 | A unit test verifies that two fake chat calls with different usage values produce correct total input/output, latest input/output, max input/output, and call count. |
| FR2 | `ChatAgentRunResult` exposes explicit metric fields and no longer exposes `InputTokensUsed` or `OutputTokensUsed`. |
| FR3 | `ChatMessage` and `AppDbContextModelSnapshot` contain the new metric fields and do not contain `InputTokensUsed` or `OutputTokensUsed`. |
| FR4 | The EF migration creates or transitions `chat_message` to the final schema with the new metric columns and without the old ambiguous columns. |
| FR5 | After `ChatController.Send`, the assistant `ChatMessage` row stores total processed tokens, latest prompt tokens, max prompt tokens, model call count, context usage percent, and response time. |
| FR6 | After `ChatController.Send`, `agentcontext:{userId}:{sessionId}` contains the new JSON payload with `contextUsagePct` computed from `maxPromptTokens`. |
| FR7 | The `_BotMessage` OOB `#context-ring` value matches `ContextUsagePct`, not total input tokens processed. |
| FR8 | Refreshing the chat page renders the initial `#context-ring` from the active session's Redis context snapshot. |
| FR9 | Reset deletes the current `agentcontext:{userId}:{sessionId}` key and returns the ring to `0%`. |
| FR10 | The structured turn log contains the new metric names and no longer logs ambiguous `inputTokens` / `outputTokens` as the primary labels. |

## Test Cases

**Unit tests:**

- `WebApp.Tests/Services/TokenCountingChatClientTests.cs`
  - `TokenCountingChatClient_TracksTotalsLatestMaxAndCallCount`: fake first response `InputTokenCount=100`, `OutputTokenCount=50`; fake second response `InputTokenCount=250`, `OutputTokenCount=30`; assert total input `350`, total output `80`, latest prompt `250`, latest output `30`, max prompt `250`, max output `50`, call count `2`.
  - `TokenCountingChatClient_ScopesAreIndependent`: existing scope independence test updated for new fields.
  - `TokenCountingChatClient_HandlesNullUsageGracefully`: assert all numeric metrics remain zero except call-count behavior as chosen by implementation; document whether null-usage calls increment `CallCount`.

- `WebApp.Tests/Controllers/ChatControllerTests.cs`
  - `Send_WritesContextSnapshot_WithContextUsageFromMaxPromptTokens`: fake `ChatAgentRunResult` with total input greater than max prompt; assert Redis `contextUsagePct` uses max prompt.
  - `Send_WritesChatMessageMetrics`: assert the assistant row contains the new metric values and no old property assertions remain.
  - `ChatRefresh_RendersContextRingFromRedisSnapshot`: seed `activesessionid:{userId}` and `agentcontext:{userId}:{sessionId}`, render the page path that owns `Index.cshtml`, and assert the ring starts at the stored value.
  - `Reset_RemovesContextSnapshotAndClearsRing`: assert reset removes `agentcontext:{userId}:{sessionId}`.

**Integration tests:**

- `WebApp.Tests/Integration/AgentToolsPostgresTests.cs`
  - Update `ChatSend_WritesTwoChatMessageRows` to assert the new `ChatMessage` metric columns.
  - Add or update a PostgreSQL schema test to verify `chat_message` has the new columns and no `input_tokens_used` / `output_tokens_used` columns.

## Manual Verification

1. Start the stack: `make docker-run`.
2. Sign in and open the chat page.
3. Send a message that triggers a tool call, such as asking about a saved book.
4. Verify the ring updates from `0%` to the value derived from `maxPromptTokens / Ollama:NumCtx`.
5. Refresh the browser page. Verify the ring still shows the same percentage for the active session.
6. Inspect Redis:
   ```bash
   docker compose exec redis redis-cli GET activesessionid:<userId>
   docker compose exec redis redis-cli HGETALL agentcontext:<userId>:<sessionId>
   ```
   Verify the payload contains `totalInputTokensProcessed`, `totalOutputTokensGenerated`, `latestPromptTokens`, `maxPromptTokens`, `modelCallCount`, `numCtx`, `contextUsagePct`, and `lastResponseMs`.
7. Inspect PostgreSQL:
   ```bash
   docker compose exec postgres psql -U postgres booknotes -c "SELECT display_order, total_input_tokens_processed, total_output_tokens_generated, latest_prompt_tokens, max_prompt_tokens, context_usage_pct, model_call_count, response_time_ms FROM chat_message ORDER BY display_order;"
   ```
   Verify only assistant rows have metric values.
8. Reset chat. Verify the chat clears, the ring returns to `0%`, and the old `agentcontext:<userId>:<sessionId>` key is gone.
9. Run `make test` and confirm all tests pass.

## Definition of Done

- `Specs/20260606154739-context-pressure-metrics/` contains Requirements, Plan, and Validation.
- `TokenAccumulator` records total, latest, max, and call-count metrics.
- `ChatAgentRunResult` uses explicit metric names.
- `ChatMessage`, migration, and model snapshot use the new metric columns and remove old ambiguous columns.
- `ChatController.Send` persists the new metrics and computes ring usage from `MaxPromptTokens`.
- `agentcontext:{userId}:{sessionId}` stores the new refreshable context snapshot.
- Page refresh restores the context ring from Redis for the active session.
- Reset clears the current DB rows and Redis context snapshot.
- Existing and new tests pass under `make test`.

## Rollback Plan

- Revert `TokenCountingChatClient`, `ChatAgentRunResult`, `ChatMessage`, `ChatController`, and Razor/view model changes to the previous context-token-awareness implementation.
- Revert or replace the metric migration. Because there is no production data, local developers may reset the Docker PostgreSQL volume instead of preserving old metric rows.
- If the refresh ring path causes issues, keep the OOB ring update after send and temporarily render `0%` on full page load while retaining DB metric persistence.
