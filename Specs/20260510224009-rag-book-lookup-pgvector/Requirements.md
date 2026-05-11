# Requirements: RAG Book Lookup with pgvector

## Table of Contents

- [Requirements: RAG Book Lookup with pgvector](#requirements-rag-book-lookup-with-pgvector)
  - [Problem Statement](#problem-statement)
  - [User Stories](#user-stories)
  - [Functional Requirements](#functional-requirements)
  - [Non-Functional Requirements](#non-functional-requirements)
  - [Out of Scope](#out-of-scope)
  - [Open Questions](#open-questions)

## Problem Statement

`BookContextAgentTool` resolves the book a user is asking about through normalized string matching against `Book.NormalizedTitle` and `Book.NormalizedAuthor`. The match logic in `BuildSearchTitles` handles a handful of known title formats (`"Title by Author"`, `"Author - Title"`) but fails whenever qwen3 phrases the query differently — a short alias, a series name, a subtitle, or a language variant. Because the chat agent passes whatever title string it extracts from the user's message, any mismatch causes the tool to return "not found" and the agent answers without book context.

The correct fix is to replace string matching with semantic vector similarity: embed every book's `"Title by Author"` string at import time using `mxbai-embed-large`, store the result in a new `BookEmbedding` table with a HNSW index, and at tool invocation time embed the incoming query string and run a cosine-distance lookup against that table scoped to the authenticated user's library. Because `mxbai-embed-large` is an encoder model (not generative), embedding a title string takes milliseconds, making inline generation at import time feasible.

## User Stories

- Given a user who imported Kindle clippings for "Leviathan Wakes (James S. A. Corey)", when they ask the chat "tell me about the first Expanse novel", then qwen3 calls `GenerateBookContext` with "the first Expanse novel" and the tool finds the correct `Book` record via vector similarity and returns its context.
- Given a user who imported a book with a Kindle-style header like "Dick, Philip K - Gather Yourselves Together", when they ask "what do you know about Gather Yourselves Together", then the tool resolves the right book and returns or generates its context.
- Given a user importing a Kindle `.txt` file with 10 books, when the import completes, then `BookEmbedding` rows exist for every newly created book and the import summary is unchanged.
- Given a user with books in their library that were imported before this feature was deployed (no `BookEmbedding` rows exist), when they ask about one of those books, then the tool degrades gracefully by falling back to the existing string-matching logic rather than returning "not found" blindly.
- Given two users who both imported "Dune" by Frank Herbert, when either user asks about Dune, then the tool only searches embeddings scoped to the requesting user's `UserId` and cannot surface the other user's book.

## Functional Requirements

1. FR1 — Upgrade the PostgreSQL service image from `postgres:16-alpine` to `pgvector/pgvector:0.8.2-pg18-trixie` in `docker-compose.yml` and `docker-compose.test.yml`. Add `Pgvector.EntityFrameworkCore` as a `PackageReference` in `WebApp/WebApp.csproj` and `WebApp.Tests/WebApp.Tests.csproj`. Enable the `vector` extension in `AppDbContext.OnModelCreating` via `HasPostgresExtension("vector")`. Add `Pgvector.EntityFrameworkCore` to `UseNpgsql` options via `.UseVector()`.

2. FR2 — Create `WebApp/Models/BookEmbedding.cs` defining a `BookEmbedding` entity: `Id` (Guid, PK), `UserId` (string, FK to `IdentityUser`), `BookId` (Guid, FK to `Book`, cascade delete), `Title` (string, denormalized), `Author` (string, denormalized), `Embedding` (`Vector`, `vector(1024)`), `CreatedAt` (DateTime UTC). Configure it in `AppDbContext` under the table `book_embedding`. Add a HNSW index on `Embedding` using `vector_cosine_ops`. Add a plain index on `UserId` for filter performance. Add `DbSet<BookEmbedding> BookEmbeddings` to `AppDbContext`. Generate and apply an EF migration.

3. FR3 — Create `WebApp/Services/EmbeddingService.cs` defining `IEmbeddingService` and `EmbeddingService`. The interface exposes one method: `Task<float[]> EmbedAsync(string text, CancellationToken ct = default)`. The implementation wraps `IEmbeddingGenerator<string, Embedding<float>>` from `Microsoft.Extensions.AI` and returns the first result's vector as `float[]`. Register a singleton `IEmbeddingGenerator<string, Embedding<float>>` in `Program.cs` by casting a dedicated `OllamaApiClient` instance pointed at `mxbai-embed-large`. Register `IEmbeddingService` / `EmbeddingService` as scoped. Add `mxbai-embed-large` to the `ollama pull` command in `docker-compose.yml`.

4. FR4 — Update `KindleClippingsImportService` to accept `IEmbeddingService` as a constructor dependency. After the first `SaveChangesAsync` (when new `Book` records have been assigned their database IDs), for each newly created book generate an embedding of `"{book.Title} by {book.Author}"` and insert a `BookEmbedding` row. Books that already existed before this import (identified by the existing `bookMap` lookup) must not produce a duplicate `BookEmbedding` row. The `KindleImportSummary` return value is unchanged.

5. FR5 — Update `BookContextAgentTool` to replace the `BuildSearchTitles` string-matching path with a vector lookup: inject `IEmbeddingService` alongside `AppDbContext`; embed the incoming `bookTitle` argument; query `AppDbContext.BookEmbeddings` ordered by `CosineDistance` scoped to `userId`, take the top result; if the distance exceeds a threshold of `0.5` (or no row exists), fall back to the existing `BuildSearchTitles` in-memory match against `AppDbContext.Books` so books imported before this feature was deployed still resolve. When the vector match succeeds, load the matched `Book` directly by `BookId` (including its `Context` field). The rest of the tool — cached context check and `GenerateAndSaveAsync` call — is unchanged.

6. FR6 — Update `WebApp.Tests` to cover the new code paths:
   - Update `WebApp.Tests/Services/BookContextAgentToolTests.cs`: replace in-memory DB tests that exercised the string-matching path with integration-style Postgres tests (reusing `PostgresTestDatabase` from `AgentToolsPostgresTests.cs`) that seed both `Book` and `BookEmbedding` rows with real vectors and verify cosine lookup resolves the correct book. Keep the "not found" and "userId isolation" tests.
   - Add `WebApp.Tests/Services/EmbeddingServiceTests.cs`: unit test using a `FakeEmbeddingGenerator` that verifies `EmbedAsync` calls `GenerateAsync` once and returns the expected float array.
   - Update `WebApp.Tests/Services/KindleClippingsImportServiceTests.cs` (if it exists) or add it: verify that `ImportAsync` calls `IEmbeddingService.EmbedAsync` once per new book and inserts a `BookEmbedding` row, and does not insert a duplicate for an already-existing book.
   - Update `WebApp.Tests/Integration/AgentToolsPostgresTests.cs`: seed `BookEmbedding` rows alongside `Book` rows so the existing integration tests continue to pass under the new lookup path.

## Non-Functional Requirements

- Embedding generation at import time is synchronous and inline; no background queue or separate process is introduced in this spec.
- `IEmbeddingService` must be registered as scoped so `AppDbContext` (also scoped) can be injected into services that use both.
- `IEmbeddingGenerator<string, Embedding<float>>` registration must be singleton (stateless HTTP client; analogous to the existing `IChatClient` singleton).
- `UserId` isolation: every `BookEmbedding` query must include a `.Where(e => e.UserId == userId)` filter. No cross-user data must be accessible.
- The HNSW index must specify `vector_cosine_ops` to match the `CosineDistance` query operator; a mismatch would silently degrade to a sequential scan.
- All new and modified code must use file-scoped namespaces and constructor injection as used throughout the project.

## Out of Scope

- Background job or `IHostedService` for embedding generation; embeddings are generated inline at import time only.
- Backfill of embeddings for books imported before this feature is deployed; the fallback string-matching path in FR5 handles those books.
- Embedding `Book.Context` or `BookNote` content; only `"Title by Author"` is embedded in this spec.
- Semantic search across notes or context text.
- Tuning of HNSW index parameters (`m`, `ef_construction`); defaults are used.
- Changing the chat controller, agent orchestrator, or session serialization.
- Alpine variant of the pgvector Docker image; the official `pgvector/pgvector` image is Debian-based.

## Open Questions

- ✅ **Resolved** — `OllamaApiClient` implements `IEmbeddingGenerator<string, Embedding<float>>` natively (alongside `IOllamaApiClient` and `IChatClient`). The cast pattern is confirmed in OllamaSharp docs and 5.4.25 release notes. No smoke test needed before implementation.
- ✅ **Resolved** — `Pgvector.EntityFrameworkCore` supports the HNSW index fluent API: `.HasMethod("hnsw").HasOperators("vector_cosine_ops").HasStorageParameter("m", 16).HasStorageParameter("ef_construction", 64)`. No raw SQL migration fallback needed.
- ⚠️ TODO: Validate the cosine distance threshold. Cosine distance ranges from 0 (identical) to 2 (diametrically opposed), so `0.5` is a conservative starting point. Test against at least 5–10 real book titles with loose query phrasing before hardcoding. Expose the threshold as a named constant or app setting so it can be tuned without a code change.
