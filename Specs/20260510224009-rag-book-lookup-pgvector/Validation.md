# Validation: RAG Book Lookup with pgvector

## Table of Contents

- [Validation: RAG Book Lookup with pgvector](#validation-rag-book-lookup-with-pgvector)
  - [Acceptance Criteria](#acceptance-criteria)
  - [Test Cases](#test-cases)
  - [Manual Verification](#manual-verification)
  - [Definition of Done](#definition-of-done)
  - [Rollback Plan](#rollback-plan)

## Acceptance Criteria

| Requirement | Acceptance Criterion |
| --- | --- |
| FR1 | `docker-compose.yml` and `docker-compose.test.yml` reference `pgvector/pgvector:0.8.2-pg18-trixie`. `WebApp.csproj` and `WebApp.Tests.csproj` contain a `Pgvector.EntityFrameworkCore` `PackageReference`. `AppDbContext.OnModelCreating` calls `HasPostgresExtension("vector")`. `dotnet build WebApp/WebApp.csproj` succeeds. |
| FR2 | `WebApp/Models/BookEmbedding.cs` exists. `AppDbContext` exposes `DbSet<BookEmbedding> BookEmbeddings`. The EF migration exists under `WebApp/Migrations/` and creates a `book_embedding` table with a `vector(1024)` column, a HNSW index on `Embedding`, and an index on `UserId`. `dotnet ef migrations list` shows the migration as applied in the running stack. |
| FR3 | `WebApp/Services/EmbeddingService.cs` exists. `IEmbeddingService.EmbedAsync` returns a `float[]` of length 1024 when called against a live `mxbai-embed-large` endpoint. `Program.cs` registers `IEmbeddingGenerator<string, Embedding<float>>` as singleton and `IEmbeddingService` as scoped. `docker-compose.yml` `ollama` command includes `ollama pull mxbai-embed-large`. |
| FR4 | After calling `ImportAsync` with a Kindle `.txt` containing `N` new books, `N` rows exist in `book_embedding` scoped to that `UserId`. Re-importing the same file does not add duplicate `book_embedding` rows. `KindleImportSummary` fields are unchanged. |
| FR5 | `BookContextAgentTool` invoked with a query that is semantically similar to a book in the user's library (but not an exact string match) resolves the correct `Book` record via cosine distance and returns its context. Invoked with a query that has no embedding match above the threshold, the tool falls back to string matching. Invoked with a query that matches no book by either method, returns a message containing "not found". `grep` on `BookContextAgentTool.cs` confirms `IEmbeddingService` is a constructor parameter. |
| FR6 | `WebApp.Tests/Services/EmbeddingServiceTests.cs` exists. `WebApp.Tests/Services/BookContextAgentToolTests.cs` seeds `BookEmbedding` rows and verifies cosine lookup. `WebApp.Tests/Integration/AgentToolsPostgresTests.cs` seeds `BookEmbedding` rows for all seeded books. `dotnet test WebApp.Tests/WebApp.Tests.csproj` passes — all tests green. |

## Test Cases

**Unit tests (new — `WebApp.Tests/Services/EmbeddingServiceTests.cs`):**

- `EmbedAsync_CallsGeneratorOnce_ReturnsVector`: create `EmbeddingService` with a `FakeEmbeddingGenerator` that returns a fixed `float[1024]`; call `EmbedAsync("Dune by Frank Herbert")`; assert the returned array equals the fixed vector and the generator was called exactly once.

**Unit tests (updated — `WebApp.Tests/Services/BookContextAgentToolTests.cs`):**

These tests require real `CosineDistance` support and are moved to the Postgres-backed runner. Each test uses `PostgresTestDatabase`, seeds both a `Book` and a `BookEmbedding` row with a deterministic vector from a `FakeEmbeddingService`, and invokes the `AIFunction` via `InvokeAsync`.

- `Create_WhenEmbeddingMatchesUserBook_ReturnsGeneratedContext`: seed `Book` + `BookEmbedding` for "Dune" for `user-1`; `FakeEmbeddingService` returns the same fixed vector for any input; assert the tool returns the generated context.
- `Create_WhenBookBelongsToOtherUser_ReturnsNotFoundOrFallback`: seed `BookEmbedding` for `user-2`; invoke with `userId = "user-1"`; assert "not found".
- `Create_WhenNoEmbeddingExistsAndStringMatchFails_ReturnsNotFound`: seed `Book` with no `BookEmbedding` row; use a non-matching title; assert "not found".
- `Create_WhenNoEmbeddingExistsButStringMatchSucceeds_ReturnsContext` (fallback path): seed `Book` with no `BookEmbedding` row; title matches via `BuildSearchTitles`; assert context is returned via fallback.
- `Create_WhenBookHasExistingContext_ReturnsCachedContextWithoutGenerating`: seed `Book` with `Context` populated + `BookEmbedding`; assert `GenerateAndSaveCalled` is false.

**Unit tests (new — `WebApp.Tests/Services/KindleClippingsImportServiceTests.cs`):**

- `ImportAsync_WhenNewBooksImported_InsertsBookEmbeddingForEachNewBook`: seed Postgres with no existing books; import a `.txt` with 2 books; assert 2 `BookEmbedding` rows exist; assert `FakeEmbeddingService.EmbedCallCount == 2`.
- `ImportAsync_WhenBookAlreadyExists_DoesNotInsertDuplicateEmbedding`: seed Postgres with one existing `Book` + `BookEmbedding`; re-import the same `.txt`; assert `BookEmbedding` count is still 1.

**Integration tests (updated — `WebApp.Tests/Integration/AgentToolsPostgresTests.cs`):**

- Update all three existing tests (`GenerateBookContext_WithPostgresSeededLibrary_*`, `ChatRefresh_*`) to seed a `BookEmbedding` row alongside each `Book` using the `FakeEmbeddingService` pattern, so the cosine lookup path is exercised rather than the fallback.

## Manual Verification

1. Delete existing `pg_data` volume: `docker volume rm book-notes-ia_pg_data` (PG16 → PG18 volume incompatibility).
2. Start the stack: `make docker-run` (Linux/SteamOS).
3. Wait for the `ollama` container to finish pulling both `qwen3.5:4b` and `mxbai-embed-large` (`docker compose logs -f ollama`).
4. Register a user account and navigate to the Notes page.
5. Import a Kindle `.txt` file containing at least three books.
6. Verify embeddings were stored: connect to Postgres and run `SELECT title, author, length(embedding::text) FROM book_embedding WHERE user_id = '<your-user-id>';` — expect one row per imported book with a non-null embedding.
7. Open the Chat page and send: "tell me about [a book title phrased loosely — e.g. just the series name or first word]".
8. Verify the agent replies with correct book context (not "not found" or a wrong book).
9. In the Postgres shell run `SELECT context FROM book WHERE title ILIKE '%<book>%';` and confirm context was either pre-existing or just generated.
10. Re-import the same `.txt` file and verify the `book_embedding` row count did not increase.
11. Send a chat message about a book not in the library; verify the agent returns "not found" (no false positive from cosine similarity).
12. Check `docker compose logs webapp` for any errors during import or tool invocation.

## Definition of Done

- `Specs/20260510224009-rag-book-lookup-pgvector/` contains Requirements.md, Plan.md, and Validation.md.
- `docker-compose.yml` and `docker-compose.test.yml` use `pgvector/pgvector:0.8.2-pg18-trixie`.
- `WebApp.csproj` and `WebApp.Tests.csproj` reference `Pgvector.EntityFrameworkCore`.
- `WebApp/Models/BookEmbedding.cs` exists.
- `AppDbContext` configures `BookEmbedding` with `vector(1024)`, HNSW index, and `UserId` index.
- An EF migration exists and applies cleanly.
- `WebApp/Services/EmbeddingService.cs` exists; `IEmbeddingService` and `EmbeddingService` compile.
- `Program.cs` registers `IEmbeddingGenerator<string, Embedding<float>>` (singleton) and `IEmbeddingService` (scoped); `UseNpgsql` chains `.UseVector()`.
- `docker-compose.yml` `ollama` service pulls `mxbai-embed-large`.
- `KindleClippingsImportService` injects `IEmbeddingService` and inserts `BookEmbedding` for new books only.
- `BookContextAgentTool` injects `IEmbeddingService` and uses cosine distance as primary lookup, with `BuildSearchTitles` as fallback.
- `WebApp.Tests/Services/EmbeddingServiceTests.cs` exists with at least one passing test.
- `WebApp.Tests/Services/BookContextAgentToolTests.cs` covers cosine match, userId isolation, fallback path, and cached context.
- `WebApp.Tests/Services/KindleClippingsImportServiceTests.cs` covers embedding insertion and deduplication.
- `WebApp.Tests/Integration/AgentToolsPostgresTests.cs` seeds `BookEmbedding` rows and passes.
- `dotnet test WebApp.Tests/WebApp.Tests.csproj` passes — all tests green.
- `grep -r "BuildSearchTitles" WebApp/Services/BookContextAgentTool.cs` returns a result only in the fallback branch, not as the primary path.
- `Specs/Roadmap.md` is updated with a row for this spec.

## Rollback Plan

- Revert `docker-compose.yml` and `docker-compose.test.yml` to `postgres:16-alpine`.
- Revert `WebApp.csproj` and `WebApp.Tests.csproj` to remove `Pgvector.EntityFrameworkCore`.
- Revert `WebApp/Models/BookEmbedding.cs`, `WebApp/Services/EmbeddingService.cs`, and the EF migration.
- Revert `AppDbContext.cs`, `Program.cs`, `KindleClippingsImportService.cs`, and `BookContextAgentTool.cs` to their pre-spec state.
- Revert test files.
- Drop the `book_embedding` table if needed: `docker compose exec postgres psql -U postgres booknotes -c "DROP TABLE book_embedding;"`.
- Wipe and recreate the `pg_data` volume if reverting to PG16: `docker volume rm book-notes-ia_pg_data`.

The rollback has no Redis impact. The `agentsession` and `agentcontext` cache keys are unaffected.
