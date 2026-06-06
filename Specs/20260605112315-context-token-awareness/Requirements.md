# Requirements: Context Token Awareness

## Table of Contents

- [Problem Statement](#problem-statement)
- [Dependency](#dependency)
- [User Stories](#user-stories)
- [Functional Requirements](#functional-requirements)
- [Non-Functional Requirements](#non-functional-requirements)
- [Out of Scope](#out-of-scope)
- [Open Questions](#open-questions)

## Problem Statement

Two problems are costing context window tokens unnecessarily, and there is no visibility into how full the context window is until Ollama crashes with a 500.

**Visibility gap:** `ChatController.Send` calls `agent.RunAsync` which internally makes multiple `IChatClient.GetResponseAsync` calls (initial reasoning + tool-call loop + final response). None of these calls surface `prompt_eval_count` back to the application. The timer shown in the UI counts client-side seconds and is lost when the response arrives. There is no way to know whether you are at 10% or 90% of the configured `num_ctx` budget.

**Waste gap:** `BuildOrchestratorInstructions` in `ChatController` injects the full list of every book in the user's library on every request. `BuildProfileInstructions` emits all ten profile fields verbatim even when they are "not set". Both sections consume tokens that could be used for conversation history or tool results. The book list is redundant because `BookContextAgentTool` and `GetBookNotesWithAnalysis` resolve books by semantic embedding lookup — they do not need the list to find a book.

**Display fragility:** The chat history displayed to the user is reconstructed from the MAF session JSON in Redis on every `Chat` page load via `TryGetSessionMessages`, which parses an internal MAF serialization format through multiple fallback paths. When the session expires or is reset, display history is gone. Response time and token metrics have no durable home. To support the context ring UI and persist timing data, chat messages must be written to the database so the display layer is decoupled from the LLM context layer.

## Dependency

This spec has no dependency on previous specs. It modifies `ChatController`, `ChatOrchestratorAgent`, and the `IChatClient` pipeline established in earlier phases, but does not require any of them to be refactored first.

## User Stories

- Given I have had several chat exchanges, when I look at the chat input area, then I see a progress ring showing approximately what percentage of the context window is used so I know when to reset the conversation.
- Given the agent finishes a response, when the thinking indicator disappears, then the final response time is displayed alongside the context ring instead of being lost.
- Given my chat session approaches the context window limit, when I look at the ring, then I can make an informed decision to reset rather than hitting an unexpected Ollama error.
- Given my profile has several fields set and several left blank, when the agent receives its instructions, then only the non-empty fields appear — not ten lines of "not set" labels.
- Given I reset the chat, when I start a new conversation, then the ring resets to 0%, the current session's DB messages are deleted, and the next request starts with a fresh session ID.

## Functional Requirements

1. **FR1** — A new `TokenCountingChatClient` class implementing `DelegatingChatClient` (from `Microsoft.Extensions.AI`) is added to the `IChatClient` middleware pipeline in `Program.cs`. It uses `AsyncLocal<TokenAccumulator?>` to accumulate `InputTokenCount` and `OutputTokenCount` from every `GetResponseAsync` call that occurs within a single agent turn. The accumulator is thread-safe and reset per-scope.

2. **FR2** — `ChatAgentRunResult` (`IChatOrchestratorAgent.cs`) is extended with `int InputTokensUsed`, `int OutputTokensUsed`, and `long ElapsedMs`. `ChatOrchestratorAgent.RunAsync` opens a `TokenCountingChatClient` accumulation scope before calling `agent.RunAsync`, reads the accumulated counts immediately after, and measures elapsed wall-clock time with `Stopwatch`. These values are returned as part of `ChatAgentRunResult`.

3. **FR3** — A new `ChatMessage` EF Core model is created in `WebApp/Models/ChatMessage.cs` with fields: `Id` (Guid), `UserId` (string), `SessionId` (Guid), `Role` (string — `"user"` or `"assistant"`), `Content` (string — raw plain text), `DisplayOrder` (long), `CreatedAt` (DateTime UTC), `InputTokensUsed` (int? — assistant only), `OutputTokensUsed` (int? — assistant only), `ResponseTimeMs` (long? — assistant only). A migration creates the `chat_message` table with a composite index on `(UserId, SessionId, DisplayOrder)`.

4. **FR4** — Redis uses a user-scoped current-session pointer plus session-scoped data keys. The key `activesessionid:{userId}` stores the current session Guid as a string. The MAF session JSON is stored at `agentsession:{userId}:{sessionId}`. `ChatController.Send` reads the current session ID, creates a new Guid if it is absent, and reads/writes the MAF session JSON using the combined `userId` + `sessionId` key. `ChatController.Reset` deletes the current `agentsession:{userId}:{sessionId}` key, `agentcontext:{userId}:{sessionId}` key, `activesessionid:{userId}`, and the current session's `ChatMessage` rows so the next request starts a fresh session.

5. **FR5** — After every successful `agent.RunAsync`, `ChatController.Send` writes two `ChatMessage` rows to `AppDbContext`: one for the user message (`Role="user"`, `DisplayOrder=N`, no metrics) and one for the assistant message (`Role="assistant"`, `DisplayOrder=N+1`, with `InputTokensUsed`, `OutputTokensUsed`, `ResponseTimeMs`). `DisplayOrder` is computed as `(current max DisplayOrder for this session) + 1` and `+ 2` for the pair. `AppDbContext.SaveChangesAsync` is called once for both rows.

6. **FR6** — `ChatController.Chat` queries `AppDbContext.ChatMessages` filtered by `UserId` and the current `SessionId` (read from Redis `activesessionid:{userId}`), ordered by `DisplayOrder` ascending, and maps rows to `List<ChatEntry>`. The existing `TryGetSessionMessages` JSON parse and its two fallback paths are removed.

7. **FR7** — After each successful turn, `ChatController.Send` writes a Redis key `agentcontext:{userId}:{sessionId}` (same 7-day TTL as `agentsession:{userId}:{sessionId}`) containing a compact JSON payload: `{ "inputTokens": N, "outputTokens": N, "numCtx": N, "lastResponseMs": N, "usagePct": N }`. `usagePct` is `inputTokens * 100 / numCtx`, clamped to `[0, 100]`.

8. **FR8** — The `_BotMessage.cshtml` partial view model changes from `string` to a new `BotMessageViewModel` record containing `string HtmlContent` and `int UsagePct`. The partial renders the existing bot message HTML plus an HTMX out-of-band swap (`hx-swap-oob="outerHTML"`) targeting `#context-ring` — a `<sl-progress-ring>` element added to `Index.cshtml` near the chat input. The ring's `value` attribute is set to `UsagePct`. A `<span>` inside the ring displays `UsagePct%`.

9. **FR9** — `BuildOrchestratorInstructions` removes the book list section entirely. Tool descriptions replace "one of these books" with "any book the user mentions". `ChatController.Send` no longer queries the books table for the instruction list (though it may still query for other purposes if needed elsewhere; if not, the DB query is also removed).

10. **FR10** — `BuildProfileInstructions` is rewritten to produce a single compact sentence. Only fields with non-null, non-whitespace values are included. The sentence template is a `private const string` in `ChatController` so it can be changed in one place. Example output: `"Reader: Fabio, reads in Portuguese, prefers concise tone, goals: understand philosophy, likes science fiction."` Fields absent from the profile simply do not appear in the sentence.

11. **FR11** — After each turn, `ChatController.Send` logs a structured `Information`-level line: `"Turn stats: inputTokens={InputTokens} outputTokens={OutputTokens} elapsedMs={ElapsedMs} usagePct={UsagePct:F1}% promptChars={PromptChars}"` where `promptChars` is the character length of the full orchestrator instructions string plus the user message. This provides the raw data to manually calibrate chars-per-token per model without requiring a schema change.

## Non-Functional Requirements

- **SOLID — Single Responsibility**: `TokenCountingChatClient` owns only token accumulation; it does not read sessions, write to Redis, or know about the chat controller. `ChatController` reads the result from `ChatAgentRunResult` and handles persistence.
- **SOLID — Dependency Inversion**: `IChatOrchestratorAgent` callers depend on the extended `ChatAgentRunResult` record; they do not depend on `TokenCountingChatClient` or `AsyncLocal` internals.
- **User-data isolation**: `ChatMessage` rows are always queried and written filtered by `UserId`. Redis session data keys include both the authenticated `userId` and the `sessionId` (`agentsession:{userId}:{sessionId}`), and context metadata follows the same shape (`agentcontext:{userId}:{sessionId}`). The application never loads a session by bare `sessionId`; if a future UI accepts a session ID, it must verify ownership by filtering on `UserId` before constructing Redis keys.
- **Testability**: `TokenAccumulator` is a plain class with no DI dependencies. `TokenCountingChatClient` wraps a fakeable inner `IChatClient`. `ChatController` tests that previously passed a fake `IChatOrchestratorAgent` continue to work by returning the extended `ChatAgentRunResult` from the fake.
- **No breaking change to session payload format**: the serialized Microsoft Agent Framework session JSON remains unchanged in format and TTL. Only the Redis key shape changes from user-only to user-and-session scoped.
- **No new runtime packages**: `DelegatingChatClient` is already available in `Microsoft.Extensions.AI`. Shoelace is already loaded in `_Layout.cshtml`. No new CDN dependencies.

## Out of Scope

- **Rolling summarisation**: Compressing old messages into a summary when the context nears its limit is a separate phase. This spec only adds visibility and removes waste.
- **Dynamic model-aware calibration**: FR11 logs the raw data needed; a future spec will use it to build a per-model calibration service that adapts automatically when the configured model changes.
- **Session reconstruction from DB into Redis**: If the Redis session expires mid-conversation, the display will show the DB messages but the LLM context is lost. Rebuilding the LLM context from DB messages is a future concern.
- **Backfilling existing sessions**: Chat messages sent before this feature ships are not back-populated into the DB.
- **Analytics or reporting UI**: The per-turn log (FR11) is the only aggregate output. No dashboard or export is included.

## Open Questions

None. All design decisions were resolved before implementation:

- Token scope: cumulative across all calls in a turn (FR1–FR2).
- Persistence: DB for messages, Redis for lightweight context metadata (FR3–FR7).
- Profile format: single configurable sentence, non-empty fields only (FR10).
- Book list: removed from instructions; tools handle discovery (FR9).
- Calibration: log-first MVP (FR11); dynamic calibration is a follow-up spec.
