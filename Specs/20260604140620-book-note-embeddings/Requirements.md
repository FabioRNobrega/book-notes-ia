# Requirements: Book Note Embeddings

## Table of Contents

- [Problem Statement](#problem-statement)
- [Dependency](#dependency)
- [User Stories](#user-stories)
- [Functional Requirements](#functional-requirements)
- [Non-Functional Requirements](#non-functional-requirements)
- [Out of Scope](#out-of-scope)
- [Open Questions](#open-questions)

## Problem Statement

`GetBookNotesWithAnalysis` (spec `20260604133551-book-notes-agent-tool`) returns all highlights for a book, which serves completeness queries well. But when the user asks a focused question — "What did I highlight about power and religion in Dune?" — dumping every highlight into the Ollama prompt is wasteful and dilutes the answer. There is no way today to retrieve only the highlights from a specific book that are semantically relevant to the user's current question. The `book_note` table has no embeddings, so pgvector cannot be used at the note level.

Additionally, the agent currently has no tool to answer a question like "Which of my books has notes about Stoicism?" — cross-book note retrieval is impossible without note-level vectors.

## Dependency

**This spec depends on `20260604133551-book-notes-agent-tool` being implemented first.** That spec:

- Establishes the `<note>Content</note>` tag format used in tool responses.
- Registers the `GetBookNotesWithAnalysis` tool in `ChatController` and proves the two-tool registration pattern.
- Confirms the `IBookNotesAgentTool` + service two-layer pattern that this spec replicates.

## User Stories

- Given I have imported highlights for Dune, when I ask "What did I capture about Paul's transformation?", then the agent retrieves only the highlights semantically closest to that question and returns them as `<note>` blocks.
- Given I have highlights across multiple books, when I ask "Which of my books has notes about leadership?", then the agent can find relevant notes regardless of which book they belong to.
- Given new highlights are imported from a Kindle clipping file, then embeddings for those notes are generated and stored automatically without any manual action.
- Given a highlight has no semantically close embedding match for a query, then only genuinely relevant notes are returned and no hallucinated content is introduced.

## Functional Requirements

1. **FR1** — A new `BookNoteEmbedding` model must be added with fields: `Id` (Guid), `UserId` (string), `BookId` (Guid FK to `Book`), `BookNoteId` (Guid FK to `BookNote`), `Embedding` (Vector 1024), `CreatedAt` (DateTime UTC).

2. **FR2** — A new EF Core migration must create a `book_note_embedding` table, a `UserId` index, a `(UserId, BookId)` composite index for book-scoped searches, and an HNSW index on `Embedding` with `vector_cosine_ops` — matching the pattern from `20260510230000_AddBookEmbedding`.

3. **FR3** — `AppDbContext` must expose `DbSet<BookNoteEmbedding>` and configure the HNSW index using the same `HasMethod("hnsw").HasOperators("vector_cosine_ops")` pattern used for `BookEmbedding`.

4. **FR4** — `KindleClippingsImportService` must embed each new `BookNote.Content` using `IEmbeddingService` after the initial `SaveChangesAsync` (when `BookNote.Id` values are available), and persist one `BookNoteEmbedding` row per note — mirroring the existing book-level embedding step in that service.

5. **FR5** — A new `IBookNoteSearchService` interface must expose a method that accepts a resolved `Book`, a `searchQuery` string, a `userId`, and a `CancellationToken`, and returns a list of the most semantically relevant `BookNote` records for that book and user. The `topK` limit must be read from configuration key `BookNotes:TopKRelevantNotes` (injected via `IConfiguration`) with a hardcoded fallback of `20` — following the same configurable-with-fallback pattern as `NotesImport:MaxFileSizeBytes` in `Program.cs`.

6. **FR6** — `BookNoteSearchService` must embed the `searchQuery` using `IEmbeddingService`, then execute a parameterized pgvector cosine distance query against `book_note_embedding` filtered by `UserId` and `BookId`, ordered by distance ascending, limited to the configured `topK` value — following the raw SQL pattern in `BookLookupService.FindClosestEmbeddingAsync`.

7. **FR7** — A new `IBookNoteSearchAgentTool` interface and `BookNoteSearchAgentTool` implementation must expose `AIFunction Create(string userId)`. The `AIFunction` must be named `GetRelevantBookNotes`, accept parameters `bookTitle` (string) and `searchQuery` (string), use `IBookLookupService.FindAsync` to resolve the book, delegate to `IBookNoteSearchService`, and return results formatted as `<note loc="{LocationText}">{Content}</note>` — one per line. The `loc` attribute lets the agent cite the exact Kindle location and reason about highlights that cluster in the same passage.

8. **FR8** — When `IBookLookupService.FindAsync` returns `null`, the tool must return a "book not found" message. When `IBookNoteSearchService` returns no results, the tool must return a "no relevant notes found" message.

9. **FR9** — `IBookNoteSearchService` and `IBookNoteSearchAgentTool` must be registered as `AddScoped` in `Program.cs`.

10. **FR10** — `ChatController` must inject `IBookNoteSearchAgentTool` and include it in the `IReadOnlyList<AITool>` alongside `GenerateBookContext` and `GetBookNotesWithAnalysis` when the user has at least one book.

11. **FR11** — The orchestrator instructions in `ChatController.BuildOrchestratorInstructions` must be extended to tell the agent to call `GetRelevantBookNotes` when the user asks a focused question about a specific topic within a book's highlights, passing the user's question as `searchQuery`.

12. **FR12** — `KindleClippingsImportService` must skip embedding for notes that already have a `BookNoteEmbedding` row (using `BookNoteId` as the deduplication key), so re-importing the same clipping file does not produce duplicate embeddings.

## Non-Functional Requirements

- **SOLID — Single Responsibility**: `BookNoteSearchService` owns the embedding call and the pgvector query. `BookNoteSearchAgentTool` is a thin adapter. Note-level SQL must not appear in the controller or the tool class.
- **SOLID — Dependency Inversion**: `BookNoteSearchAgentTool` depends on `IBookLookupService` and `IBookNoteSearchService` interfaces only.
- **User-data isolation**: all `book_note_embedding` queries must filter by both `UserId` and `BookId`. Cross-user access must be impossible.
- **Testability**: `IBookNoteSearchService` must be fakeable without EF Core or Ollama at the controller test call site.
- **Import resilience**: embedding failures during Kindle import must be caught and logged (not re-thrown), so an Ollama outage does not block note import — matching the existing fallback pattern in `BookLookupService`.
- **No cap on import**: every imported note gets an embedding. `topK` only limits retrieval at search time, not storage.

## Out of Scope

- Cross-book semantic note search (e.g. "Which of my books has notes about X?") — this spec focuses on book-scoped retrieval. Cross-book search is a future spec.
- Backfilling embeddings for notes imported before this feature ships — a separate backfill migration or admin command is needed and not included here.
- Re-embedding notes when `Content` is edited — notes are currently immutable after import.
- Configurable `topK` per user. The default is fixed at implementation time.

## Open Questions

None. Both decisions were resolved before implementation:

- `topK` is read from `appsettings.json` under `BookNotes:TopKRelevantNotes` with a hardcoded fallback (see FR5 and FR6 updates). This follows the same configurable-with-fallback pattern used for `NotesImport:MaxFileSizeBytes` in `Program.cs`, so the value can be tuned without a redeploy.
- Notes include `LocationText` as a tag attribute: `<note loc="Location 42">Content</note>`. This lets the agent cite where in the book a highlight came from (useful for navigating back to the passage in Kindle) and enables proximity reasoning — highlights at nearby locations are likely from the same passage, which the agent can use to identify concentrated reading focus.
