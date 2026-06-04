# Requirements: Book Notes Agent Tool

## Table of Contents

- [Problem Statement](#problem-statement)
- [User Stories](#user-stories)
- [Functional Requirements](#functional-requirements)
- [Non-Functional Requirements](#non-functional-requirements)
- [Out of Scope](#out-of-scope)
- [Open Questions](#open-questions)

## Problem Statement

The existing chat experience resolves a book's literary context via `GenerateBookContext`, but the user's personal highlights and annotations (stored as `BookNote` rows) are never surfaced in chat. A reader who asks "What did I highlight in Dune?" or "What themes do my notes from this book point to?" gets no grounded answer — the agent has no tool to fetch or reason over the user's actual notes. `BookNote` records are imported and deduplicated through `KindleClippingsImportService` and stored in the `book_note` table in `AppDbContext`, but no service or Microsoft Agent Framework tool exists to retrieve them and generate a thematic analysis.

## User Stories

- Given I have imported Kindle highlights for a book, when I ask the chat "What notes do I have on Dune?", then the agent fetches my raw notes for grounding and returns a natural thematic analysis without listing the raw notes unless I explicitly ask to see them.
- Given my notes for a book span multiple highlights, when the agent calls `GetBookNotesWithAnalysis`, then the response describes the main relationship between my notes and how their themes connect to the book's literary context.
- Given a book exists in my library but has no imported notes, when I ask about my notes for it, then the agent returns a clear message that no notes or highlights were found.
- Given I ask about a title not in my library, when the agent calls `GetBookNotesWithAnalysis`, then it returns a not-found message without querying notes.
- Given I ask about both the book context and my personal notes in one conversation, then the agent may call both `GenerateBookContext` and `GetBookNotesWithAnalysis` and combine the results.

## Functional Requirements

1. **FR1** — A new `IBookNotesAnalysisService` interface must expose a method that accepts a resolved `Book` and `userId` and returns an LLM-generated analysis grounded in the user's notes. This service owns all `AppDbContext` note queries and `IOllamaService` calls for this feature.

2. **FR2** — `BookNotesAnalysisService` must query `AppDbContext.BookNotes` filtered by `BookId` and `UserId`, ordered by `ClippedAtUtc` ascending, capped at a configurable maximum read from `IConfiguration` key `BookNotes:MaxAnalysisNotes` with a hardcoded fallback of `50`. The cap exists because the raw notes fed into the Ollama prompt are consumed once to produce the analysis paragraph and never surfaced in the agent's conversation context — so completeness beyond ~50 notes does not improve the answer quality but does burn context window and slow response time. The follow-up tool `GetRelevantBookNotes` (spec `20260604140620-book-note-embeddings`) handles focused, exhaustive retrieval via semantic search.

3. **FR3** — Each note must be formatted as `<note>{Content}</note>` in the Ollama analysis prompt, one entry per line. This tag format is the canonical representation shared with the follow-up spec `20260604140620-book-note-embeddings`, which introduces semantic note retrieval using the same format. Raw note lines must not be included in the tool result unless a future explicit exact-notes path is added.

4. **FR4** — `BookNotesAnalysisService` must query `AppDbContext.UserProfiles` for the user's `PreferredLanguage` (defaulting to `"English"` if not set) and include it in the `IOllamaService.CompleteAsync` prompt. The prompt must also include the book's existing `Book.Context` (or "Not available." if absent), the full formatted notes block, and ask for a ≤150-word plain-text analysis answering:
   - What is the main relationship between these notes?
   - What is the overarching theme of these notes, and how does it connect to the book's literary context?

5. **FR5** — When no `BookNote` rows exist for the given book and user, `BookNotesAnalysisService` must return a "no notes found" message without calling `IOllamaService`.

6. **FR6** — A new `IBookNotesAgentTool` interface must match the shape of `IBookContextAgentTool`: `AIFunction Create(string userId)`. The implementation must use `IBookLookupService.FindAsync` to resolve the book and `IBookNotesAnalysisService` to obtain the result. It must not access `AppDbContext` directly.

7. **FR7** — The `AIFunction` returned by `BookNotesAgentTool.Create` must be named `GetBookNotesWithAnalysis` with a description that explains when the agent should call it (user asks about personal notes, highlights, or annotations for a specific book).

8. **FR8** — When `IBookLookupService.FindAsync` returns `null`, the tool must return a "book not found" message without calling `IBookNotesAnalysisService`.

9. **FR9** — `IBookNotesAnalysisService` must be registered as `AddScoped` and `IBookNotesAgentTool` must be registered as `AddScoped` in `Program.cs`.

10. **FR10** — `ChatController` must inject `IBookNotesAgentTool` alongside `IBookContextAgentTool` and include both in the `IReadOnlyList<AITool>` passed to `_agent.RunAsync` when the user has at least one book.

11. **FR11** — The orchestrator instructions built in `ChatController.BuildOrchestratorInstructions` must be extended to tell the agent to call `GetBookNotesWithAnalysis` when the user asks about personal notes, highlights, or annotations, and that both tools may be called together for the same book.

12. **FR12** — The existing `AgentToolsPostgresTests.CreateController` helper must be updated to accept `IBookNotesAgentTool` so all existing controller-level integration tests continue to compile and pass.

## Non-Functional Requirements

- **SOLID — Single Responsibility**: `BookNotesAgentTool` is a thin MAF adapter; `BookNotesAnalysisService` owns all note retrieval and analysis generation. No note DB queries or Ollama calls may live in the tool class.
- **SOLID — Interface Segregation**: `IBookNotesAnalysisService` is narrow. Callers that only need note analysis must not depend on context generation, embedding, or import services.
- **SOLID — Dependency Inversion**: `BookNotesAgentTool` depends on `IBookLookupService` and `IBookNotesAnalysisService` interfaces, not on `AppDbContext` or `IOllamaService` directly.
- **User-data isolation**: all `AppDbContext.BookNotes` queries must filter by `UserId` alongside `BookId`. Cross-user data access must be impossible.
- **Configurable note cap**: `BookNotesAnalysisService` reads `BookNotes:MaxAnalysisNotes` from `IConfiguration` with a fallback of `50`. The cap is justified because highlights fed into the analysis prompt are consumed internally and never placed in the agent's conversation context — the agent retrieves specific highlights via `GetRelevantBookNotes` instead. This follows the same configurable-with-fallback pattern as `BookNotes:TopKRelevantNotes`.
- **Testability**: `IBookNotesAnalysisService` must be fakeable with a simple implementation (no EF or Ollama dependency at the call site) so controller and tool tests can substitute it.
- **No new migrations**: the feature reads from the existing `book_note` table; no schema changes are required.

## Out of Scope

- Caching the generated analysis. Each call re-queries and re-generates; analysis persistence is a future concern.
- Exhaustive note retrieval. A configurable cap applies; focused retrieval is handled by `GetRelevantBookNotes` in spec `20260604140620-book-note-embeddings`.
- Note-level vector search or semantic filtering of notes before analysis — covered by the follow-up spec `20260604140620-book-note-embeddings`.
- Filtering by `EntryType` (Highlight vs. Note). All entry types are included.
- Pagination of notes in the tool response.
- A dedicated UI view for the analysis output beyond what the chat renders.

## Open Questions

None. Both design decisions were resolved before implementation:

- No note cap — all highlights for a book are fetched.
- `PreferredLanguage` from `UserProfile` is included in every Ollama analysis prompt.
