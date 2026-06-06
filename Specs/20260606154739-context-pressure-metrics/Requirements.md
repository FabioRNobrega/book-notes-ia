# Requirements: Context Pressure Metrics

## Table of Contents

- [Requirements: Context Pressure Metrics](#requirements-context-pressure-metrics)
  - [Table of Contents](#table-of-contents)
  - [Problem Statement](#problem-statement)
  - [User Stories](#user-stories)
  - [Functional Requirements](#functional-requirements)
  - [Non-Functional Requirements](#non-functional-requirements)
  - [Out of Scope](#out-of-scope)
  - [Open Questions](#open-questions)

## Problem Statement

The context token awareness implementation persists `InputTokensUsed` and `OutputTokensUsed` in `ChatMessage` and stores the same input count in `agentcontext:{userId}:{sessionId}`. That count is accumulated across every internal Microsoft Agent Framework `IChatClient.GetResponseAsync` call in a turn. It is useful for understanding total model work, but it is not the same as context window pressure. A tool turn may make multiple model calls, so summing prompt tokens can overstate how full the current `Ollama:NumCtx` window actually is. The app needs separate durable metrics for total tokens processed and for the largest/latest prompt seen during the turn, and the context ring should use the context-pressure metric rather than the accumulated work metric.

## User Stories

- Given I send a message that uses tools, when the response finishes, then the context ring shows the highest prompt pressure observed during that turn rather than the sum of multiple internal calls.
- Given I refresh the page during an active chat session, when the chat UI loads again, then the context ring still shows the last known context usage percentage for the active session.
- Given I inspect `chat_message` rows, when I compare turns, then I can distinguish total input tokens processed from current context pressure.
- Given I reset the chat, when the session is cleared, then both the chat messages and context-pressure Redis snapshot reset to zero for the next session.

## Functional Requirements

1. **FR1** — `TokenAccumulator` is refactored to track per-turn metrics separately: `TotalInputTokensProcessed`, `TotalOutputTokensGenerated`, `LatestPromptTokens`, `LatestOutputTokens`, `MaxPromptTokens`, `MaxOutputTokens`, and `CallCount`. Each `TokenCountingChatClient.GetResponseAsync` call updates totals, latest values, max values, and call count from `response.Usage`.

2. **FR2** — `ChatAgentRunResult` is refactored to expose the new token metrics plus `ElapsedMs`. `InputTokensUsed` and `OutputTokensUsed` are removed because their names conflate model work with context pressure.

3. **FR3** — `ChatMessage` token columns are replaced with clearer assistant-only metrics: `TotalInputTokensProcessed` (int?), `TotalOutputTokensGenerated` (int?), `LatestPromptTokens` (int?), `MaxPromptTokens` (int?), `ContextUsagePct` (int?), `ModelCallCount` (int?), and `ResponseTimeMs` (long?). The existing `InputTokensUsed` and `OutputTokensUsed` columns are removed. Because there is no production data, backward compatibility and data backfill are not required.

4. **FR4** — The EF migration strategy creates the final desired `chat_message` schema with the new metric columns. If `AddChatMessage` remains uncommitted at implementation time, the migration may be regenerated or edited before shipping. If it has already been applied locally, a new migration may drop/recreate `chat_message` or drop old columns and add the new columns. No production-preserving migration path is required.

5. **FR5** — `ChatController.Send` persists the new assistant metrics to `ChatMessage`. `ContextUsagePct` is computed from `MaxPromptTokens * 100 / Ollama:NumCtx`, clamped to `[0, 100]`.

6. **FR6** — `agentcontext:{userId}:{sessionId}` stores a compact refreshable context snapshot using the new shape: `{ "totalInputTokensProcessed": N, "totalOutputTokensGenerated": N, "latestPromptTokens": N, "maxPromptTokens": N, "modelCallCount": N, "numCtx": N, "contextUsagePct": N, "lastResponseMs": N }`.

7. **FR7** — The context ring uses `ContextUsagePct` from `MaxPromptTokens`, not total input tokens processed. `_BotMessage.cshtml` continues to update the ring through the existing HTMX out-of-band swap after a response arrives.

8. **FR8** — Page refresh preserves the ring state for the active session. When the chat page is rendered or refreshed, the server reads `agentcontext:{userId}:{sessionId}` and renders the existing `#context-ring` with the stored `contextUsagePct`. If there is no active session or no context snapshot, the ring shows `0%`.

9. **FR9** — `ChatController.Reset` continues to delete current session DB rows and removes `agentcontext:{userId}:{sessionId}`. After reset, the ring returns to `0%`.

10. **FR10** — Structured logs are renamed to reflect the split metrics, e.g. `totalInputTokensProcessed`, `totalOutputTokensGenerated`, `latestPromptTokens`, `maxPromptTokens`, `modelCallCount`, `contextUsagePct`, `elapsedMs`, and `promptChars`.

## Non-Functional Requirements

- **Accuracy**: The ring must represent context pressure, not total token work. `MaxPromptTokens` is the source of truth for the ring because a Microsoft Agent Framework turn can contain multiple internal model calls.
- **User-data isolation**: DB rows remain filtered by authenticated `UserId` and active `SessionId`. Redis keys remain scoped as `agentcontext:{userId}:{sessionId}` and must never be loaded by bare `sessionId`.
- **SOLID — Single Responsibility**: `TokenCountingChatClient` owns only token observation. `ChatController` coordinates persistence and UI model creation. EF schema mapping stays in `AppDbContext`.
- **Testability**: `TokenAccumulator` remains a simple class with no DI dependencies. Controller tests can fake `ChatAgentRunResult` values. Token-counting tests must verify totals, latest values, max values, and call counts independently.
- **No backward compatibility requirement**: Existing local chat metric data may be discarded. There is no production deployment and no required data migration/backfill.
- **No new runtime packages**: The feature uses existing `Microsoft.Extensions.AI`, EF Core, Redis cache, Razor, and HTMX/Shoelace patterns.

## Out of Scope

- Rolling summarisation or trimming of Microsoft Agent Framework session history.
- Exact tokenization of the serialized Redis session without making a model call.
- A token analytics dashboard or visible per-message diagnostics beyond the existing context ring.
- Preserving old local `InputTokensUsed` / `OutputTokensUsed` values.
- Changing Ollama model settings or `Ollama:NumCtx`.

## Open Questions

None. The user explicitly chose no backward compatibility, `MaxPromptTokens` for the ring, persisted Redis refresh state, and DB schema changes for the clearer metrics.
