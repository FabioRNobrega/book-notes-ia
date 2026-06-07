# Requirements: Book Title Embedding Sync

## Table of Contents

- [Problem Statement](#problem-statement)
- [User Stories](#user-stories)
- [Functional Requirements](#functional-requirements)
- [Non-Functional Requirements](#non-functional-requirements)
- [Out of Scope](#out-of-scope)
- [Open Questions](#open-questions)

## Problem Statement

`BookTitleService.UpdateTitleAsync` updates `book.Title`, `book.NormalizedTitle`, and `book.UpdatedAt`, then calls `SaveChangesAsync`. It does not touch the `BookEmbedding` row that was created at import time by `KindleClippingsImportService` using `"{book.Title} by {book.Author}"`. After a user renames a book, the embedding in the `book_embedding` table still encodes the old title. When the agent later calls `GenerateBookContext`, `BookLookupService.FindAsync` first performs a cosine-distance vector search against the stale embedding; the distance to the new title phrase typically exceeds `MaxCosineDistance = 0.5`, so vector lookup returns `null`. The string-matching fallback can still resolve the book via the updated `book.NormalizedTitle`, but this relies on an approximate fallback path that may not cover aliased or paraphrased queries — the same problem pgvector lookup was introduced to solve. The fix is to regenerate the `BookEmbedding` whenever `book.Title` changes.

## User Stories

- Given I have imported a Kindle clipping file and a book title contains the author prefix (e.g., "Herbert, Frank - Dune"), when I edit the title to "Dune" via the inline editing UI, then the agent can find the book by asking "Tell me about Dune" using the vector lookup path.
- Given I rename a book in my library, when the agent calls `GenerateBookContext` for that book by its new name, then the vector lookup resolves the book without falling back to string matching.
- Given the Ollama embedding service is temporarily unavailable when I save a title, when the rename is submitted, then the title is saved successfully and a warning is logged — the operation does not fail or roll back the title change.
- Given a book was imported before the embedding feature existed and has no `BookEmbedding` row, when I rename that book, then a new `BookEmbedding` is created with the new title so the agent can find it by vector.

## Functional Requirements

1. **FR1** — `BookTitleService` (`WebApp/Services/BookTitleService.cs`) must inject `IEmbeddingService` as a constructor dependency alongside `AppDbContext`.

2. **FR2** — After a successful title validation and before `SaveChangesAsync`, `UpdateTitleAsync` must attempt to regenerate the `BookEmbedding` for the renamed book using `IEmbeddingService.EmbedAsync($"{trimmedTitle} by {book.Author}", ct)`.

3. **FR3** — `UpdateTitleAsync` must query `AppDbContext.BookEmbeddings` for an existing row matching `BookId == book.Id && UserId == userId`. If a row exists, it must update `embedding.Title`, `embedding.Embedding`, and leave `embedding.Author` and `embedding.UserId` unchanged. If no row exists, it must add a new `BookEmbedding` with `UserId`, `BookId`, `Title = trimmedTitle`, `Author = book.Author`, and the freshly generated `Embedding`.

4. **FR4** — The embedding update and the `book.Title` / `book.NormalizedTitle` / `book.UpdatedAt` changes must be persisted together in a single `SaveChangesAsync` call, so the book row and embedding row are never temporarily out of sync in the database.

5. **FR5** — If `IEmbeddingService.EmbedAsync` throws (e.g., Ollama unavailable), `UpdateTitleAsync` must catch the exception, log a warning via `ILogger<BookTitleService>`, and still persist the title change by calling `SaveChangesAsync` without the embedding update. The method must still return `BookTitleUpdateStatus.Success` — the title rename always succeeds regardless of embedding availability.

6. **FR6** — The `IBookTitleService` interface signature must remain unchanged. No controller, view, or caller is modified by this feature.

7. **FR7** — `BookTitleServiceTests` (`WebApp.Tests/Services/BookTitleServiceTests.cs`) must be updated:
   - The `TestDbContext` that currently ignores `BookEmbedding` via `builder.Ignore<BookEmbedding>()` must be changed to allow the `BookEmbedding` entity so embedding rows can be asserted in tests.
   - A `FakeEmbeddingService` stub must be added and passed to the updated `BookTitleService` constructor in all existing tests.
   - New test cases must be added (see Validation).

## Non-Functional Requirements

- **SOLID — Single Responsibility**: `BookTitleService` coordinates the title update and embedding refresh; `IEmbeddingService` owns the vector generation; `AppDbContext` owns persistence. No new service class is required.
- **SOLID — Dependency Inversion**: `BookTitleService` depends on `IEmbeddingService`, not `EmbeddingService` or Ollama directly.
- **User-data isolation**: all `AppDbContext.BookEmbeddings` queries in `UpdateTitleAsync` must filter by both `BookId` and `UserId`.
- **Resilience**: embedding failure must never block a title save. The graceful-fallback pattern is consistent with `OpenLibraryService` and `BookLookupService`.
- **No new migrations**: `BookEmbedding` already exists; only the data in existing rows changes. No schema change is required.
- **No new packages**: `IEmbeddingService` and `Pgvector.Vector` are already in the project.
- **Testability**: `IEmbeddingService` is already a narrow interface (`EmbedAsync`) and can be faked without Ollama.

## Out of Scope

- Syncing the embedding when `Book.Author` changes (no author-editing UI or service exists).
- Bulk re-embedding of all books whose title was changed before this fix (a one-time backfill is a separate operational concern).
- Updating `BookNoteEmbedding` rows when a title changes (note embeddings encode note content, not the book title).
- Any change to `BookContextAgentTool`, `BookLookupService`, or the vector search threshold.
- Exposing embedding-sync status in the UI.

## Open Questions

None. Design decisions were resolved during discovery:

- Title update always succeeds; embedding failure is non-fatal and logged.
- Upsert semantics: create the `BookEmbedding` if it does not exist, update it if it does.
- Both changes are committed in a single `SaveChangesAsync` when the embedding call succeeds.
