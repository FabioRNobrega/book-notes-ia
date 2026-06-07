# Validation: Book Title Embedding Sync

## Table of Contents

- [Acceptance Criteria](#acceptance-criteria)
- [Test Cases](#test-cases)
- [Manual Verification](#manual-verification)
- [Definition of Done](#definition-of-done)
- [Rollback Plan](#rollback-plan)

## Acceptance Criteria

| Requirement | Acceptance Criterion |
|---|---|
| FR1 | `BookTitleService` constructor accepts `IEmbeddingService` and `ILogger<BookTitleService>` alongside `AppDbContext`. |
| FR2 | After `UpdateTitleAsync` succeeds, `IEmbeddingService.EmbedAsync` was called with the string `"{trimmedTitle} by {book.Author}"`. |
| FR3 | When a `BookEmbedding` row exists for the book, `embedding.Title` equals the new title and `embedding.Embedding` is the newly generated vector after the call. When no row exists, a new `BookEmbedding` row is created with the correct `UserId`, `BookId`, `Title`, `Author`, and `Embedding`. |
| FR4 | A single `SaveChangesAsync` call persists both the `Book` title change and the `BookEmbedding` update when the embedding generation succeeds. |
| FR5 | When `IEmbeddingService.EmbedAsync` throws, `UpdateTitleAsync` returns `BookTitleUpdateStatus.Success`, `book.Title` is updated in the DB, no `BookEmbedding` change is persisted, and a warning is present in the logger output. |
| FR6 | `IBookTitleService` interface and `NotesController` are unchanged; no compilation errors in callers. |
| FR7 | All existing `BookTitleServiceTests` pass after updating the constructor call to include `FakeEmbeddingService`; new test cases are present and green. |

## Test Cases

**Unit tests — `WebApp.Tests/Services/BookTitleServiceTests.cs`:**

Existing tests (must stay green after constructor update):
- `UpdateTitleAsync_WithValidTitle_TrimsAndPersistsTitle`
- `UpdateTitleAsync_WithValidTitle_UpdatesNormalizedTitle`
- `UpdateTitleAsync_WithValidTitle_UpdatesUpdatedAt`
- `UpdateTitleAsync_WithWhitespaceTitle_ReturnsValidationErrorAndDoesNotMutateBook`
- `UpdateTitleAsync_WithOtherUserBook_ReturnsNotFoundAndDoesNotMutateBook`
- `UpdateTitleAsync_WithMissingBook_ReturnsNotFound`

New tests to add:
- `UpdateTitleAsync_WithValidTitle_UpdatesExistingBookEmbeddingTitleAndVector` — seed a `BookEmbedding` row; call `UpdateTitleAsync`; assert `embedding.Title == newTitle` and `embedding.Embedding` equals the vector returned by `FakeEmbeddingService`.
- `UpdateTitleAsync_WithValidTitle_CreatesBookEmbeddingWhenNoneExists` — seed a book with no `BookEmbedding`; call `UpdateTitleAsync`; assert a new `BookEmbedding` row exists with the correct `Title`, `Author`, `UserId`, and `BookId`.
- `UpdateTitleAsync_WhenEmbeddingServiceThrows_StillPersistsTitleAndReturnsSuccess` — `FakeEmbeddingService` throws `InvalidOperationException`; assert `UpdateTitleAsync` returns `Success`, `book.Title` equals the new title in the DB, and no `BookEmbedding` row was added or modified.
- `UpdateTitleAsync_WhenEmbeddingServiceThrows_LogsWarning` — assert the injected `ILogger<BookTitleService>` recorded at least one `LogLevel.Warning` entry.

**Integration tests:**

None required for this spec. The embedding logic is fully tested via the in-memory EF + `FakeEmbeddingService` pattern. A real Ollama-backed test would duplicate coverage already provided by `EmbeddingServiceTests`.

## Manual Verification

1. Start the stack: `make docker-run`.
2. Log in and import a Kindle clipping file containing at least one book.
3. Open **My Notes**, navigate to the book detail page.
4. Rename the book using the inline title editor (e.g., change "Herbert, Frank - Dune" → "Dune").
5. Open a `psql` shell inside the container:
   ```bash
   eval $(make -s docker-env)
   docker compose exec postgres psql -U postgres booknotes
   ```
   Run:
   ```sql
   SELECT b."Title", e."Title" AS "EmbeddingTitle"
   FROM book b
   JOIN book_embedding e ON e."BookId" = b."Id"
   WHERE b."Title" = 'Dune';
   ```
   Confirm `EmbeddingTitle` equals `"Dune"` (not the old title).
6. Open the chat and ask: **"Tell me about Dune"**.
7. Confirm the agent resolves the book and returns its literary context without error. Check `webapp` logs — there should be no string-match fallback warning for this query.
8. To verify the fallback path: temporarily disable Ollama (`docker compose stop ollama`), rename a second book, confirm the title saves successfully and the container logs contain a `Warning` from `BookTitleService`. Re-enable Ollama (`docker compose start ollama`).

## Definition of Done

- `Requirements.md`, `Plan.md`, and `Validation.md` in `Specs/20260607124524-book-title-embedding-sync/` are complete.
- `BookTitleService` constructor updated with `IEmbeddingService` and `ILogger<BookTitleService>`.
- `UpdateTitleAsync` contains embedding upsert logic and exception guard.
- `BookTitleServiceTests` updated: `builder.Ignore<BookEmbedding>()` removed from `TestDbContext`; all existing tests still pass; four new test cases added and green.
- `make test` passes with no regressions.
- Manual verification steps 1–7 completed successfully.

## Rollback Plan

- The change is entirely within `BookTitleService.UpdateTitleAsync`. To revert: remove the `IEmbeddingService` and `ILogger` constructor parameters and the embedding upsert block, restoring the original two-parameter constructor. No migration, no interface change, no view change.
- After rollback, the embedding sync bug re-appears but the app remains functional via the string-matching fallback in `BookLookupService`.
- No data rollback is needed: stale or refreshed `BookEmbedding` rows do not affect correctness of the `book` table.
