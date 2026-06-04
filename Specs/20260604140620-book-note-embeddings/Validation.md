# Validation: Book Note Embeddings

## Table of Contents

- [Acceptance Criteria](#acceptance-criteria)
- [Test Cases](#test-cases)
- [Manual Verification](#manual-verification)
- [Definition of Done](#definition-of-done)
- [Rollback Plan](#rollback-plan)

## Acceptance Criteria

| Requirement | Acceptance Criterion |
| --- | --- |
| FR1 | `WebApp/Models/BookNoteEmbedding.cs` exists with `Id`, `UserId`, `BookId`, `BookNoteId`, `Embedding` (Vector 1024), and `CreatedAt` fields. |
| FR2 | The migration creates `book_note_embedding` with a `UserId` index, a `(UserId, BookId)` composite index, and an HNSW index using `vector_cosine_ops`. |
| FR3 | `AppDbContext` has `DbSet<BookNoteEmbedding>` and the HNSW index is configured in `OnModelCreating`. |
| FR4 | After a Kindle import, one `BookNoteEmbedding` row exists per imported `BookNote`. |
| FR5 | `IBookNoteSearchService` exists with a single search method accepting `Book`, `searchQuery`, `userId`, `topK`, and `CancellationToken`. |
| FR6 | An integration test confirms that cosine distance retrieval returns the note whose embedding is closest to the query vector, not the furthest one. |
| FR7 | The `AIFunction` is named `GetRelevantBookNotes` and accepts both `bookTitle` and `searchQuery` string parameters. Results are formatted as `<note loc="{LocationText}">{Content}</note>` lines, one per matched note. |
| FR8 | When no book is found, the tool returns a "not found" message. When no notes match, the tool returns a "no relevant notes found" message. |
| FR9 | `Program.cs` contains `AddScoped<IBookNoteSearchService, BookNoteSearchService>()` and `AddScoped<IBookNoteSearchAgentTool, BookNoteSearchAgentTool>()`. |
| FR10 | The tools list in `ChatController.Send` contains `GetBookNotesWithAnalysis` and `GetRelevantBookNotes` alongside `GenerateBookContext`. |
| FR11 | `BuildOrchestratorInstructions` output contains "GetRelevantBookNotes" and describes using `searchQuery` to pass the user's focused question. |
| FR12 | Re-importing the same clipping file does not create duplicate `BookNoteEmbedding` rows — the count before and after a second import is identical. |

## Test Cases

**Integration tests** (`WebApp.Tests/Integration/AgentToolsPostgresTests.cs`):

- `GetRelevantBookNotes_WithSeededEmbeddings_ReturnsClosestNoteAsNoteTag`:
  Seed a user, book, and two `BookNote` rows with distinct `LocationText` values. Create two `BookNoteEmbedding` rows: note A with `VectorWithFirstValue(1)`, note B with `VectorWithFirstValue(-1)`. Use `FakeEmbeddingService(VectorWithFirstValue(1))` as the query embedder. Invoke the tool. Assert the result contains note A's content in a `<note loc="...">` tag and does not contain note B's content.

- `GetRelevantBookNotes_WithUnknownTitle_ReturnsNotFoundMessage`:
  Invoke the tool with a title not in the library. Assert result contains "was not found in your library".

- `GetRelevantBookNotes_WithNoMatchingNotes_ReturnsNoRelevantNotesMessage`:
  Seed a book with no `BookNoteEmbedding` rows. Invoke the tool. Assert result contains "no relevant notes found".

- `GetRelevantBookNotes_IsolatesEmbeddingsByUserId_DoesNotReturnOtherUserNotes`:
  Seed user A's note with a vector matching the query. Scope the tool to user B. Assert result contains "no relevant notes found".

- `KindleImport_CreatesOneEmbeddingPerNote`:
  Run `KindleClippingsImportService` with a clipping file containing two distinct notes. Assert two `BookNoteEmbedding` rows are created.

- `KindleImport_SkipsEmbeddingOnReImport`:
  Import the same clipping file twice. Assert the `BookNoteEmbedding` count after the second import equals the count after the first.

- `KindleImport_ContinuesWhenEmbeddingServiceFails`:
  Configure `FakeEmbeddingService` to throw on the note embedding call. Assert the import completes successfully (notes are saved) and no `BookNoteEmbedding` rows are created — no exception propagates.

**Unit tests** (`WebApp.Tests/Services/` — ⚠️ TODO: create `BookNoteSearchServiceTests.cs`):

- `SearchAsync_BuildsCorrectSqlWithUserIdAndBookIdScope`: use a PostgreSQL test database, seed embeddings for two books under the same user, assert only embeddings for the target book are queried.

**Update to existing test:**

- `ChatRefresh_WithPostgresSeededUserAndMafSession_RendersCachedMessages` — add `FakeBookNoteSearchAgentTool` and pass it to the updated `CreateController` (same pattern used when adding `FakeBookNotesAgentTool` in the prior spec).

## Manual Verification

1. Start the stack: `make docker-run`.
2. Import a Kindle clippings file with at least 5 highlights for one book.
3. Confirm `BookNoteEmbedding` rows were created: `docker compose exec postgres psql -U postgres booknotes -c "SELECT COUNT(*) FROM book_note_embedding;"`.
4. Re-import the same file. Confirm the count is unchanged.
5. In chat, ask: "What did I highlight about [specific topic] in [book title]?"
6. Confirm the agent calls `GetRelevantBookNotes` (visible in `docker compose logs -f webapp`) with a meaningful `searchQuery`.
7. Confirm the response contains only relevant highlights wrapped in `<note>` tags, not all highlights.
8. Ask a completeness query: "List all my highlights for [book title]." Confirm the agent still calls `GetBookNotesWithAnalysis` (not `GetRelevantBookNotes`) for this case.
9. Run `make test` and confirm all tests pass.

## Definition of Done

- `Specs/20260604140620-book-note-embeddings/` contains Requirements, Plan, and Validation.
- `20260604133551-book-notes-agent-tool` is fully implemented (prerequisite).
- `BookNoteEmbedding` model, migration, and `AppDbContext` configuration are in place.
- `KindleClippingsImportService` embeds notes at import time with deduplication and error resilience.
- `IBookNoteSearchService`, `BookNoteSearchService`, `IBookNoteSearchAgentTool`, and `BookNoteSearchAgentTool` exist and follow the two-layer SOLID pattern.
- `Program.cs` registers both new scoped services.
- `ChatController` includes `GetRelevantBookNotes` in the tools list and updates orchestrator instructions.
- `AgentToolsPostgresTests.cs` compiles and all new + existing tests pass.
- `make test` is green.

## Rollback Plan

- Remove `_bookNoteSearchTool.Create(userId)` from the tools list in `ChatController.Send` to stop the agent from calling `GetRelevantBookNotes`.
- Remove the two `AddScoped` registrations from `Program.cs`.
- The `book_note_embedding` table and all existing data are unaffected — the migration can stay; the table is simply unused.
- Note import continues to work without embeddings because the embedding step is wrapped in try/catch.
- To fully remove the table: run a down-migration or manual `DROP TABLE book_note_embedding`.
