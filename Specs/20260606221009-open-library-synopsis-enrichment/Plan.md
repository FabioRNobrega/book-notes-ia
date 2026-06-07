# Plan: Open Library Synopsis Enrichment

## Table of Contents

- [Summary](#summary)
- [Technical Approach](#technical-approach)
- [Component Breakdown](#component-breakdown)
- [Dependencies](#dependencies)
- [Flow](#flow)
- [Risk Assessment](#risk-assessment)

## Summary

A new `IOpenLibraryService` / `OpenLibraryService` fetches a book's synopsis from the Open Library public API and `BookContextService.GenerateAndSaveAsync` persists it to a new `Book.Synopsis` column before enriching the Ollama prompt. This follows the same service-behind-interface pattern already used by `IOllamaService`, `IEmbeddingService`, and `IBookLookupService`.

## Technical Approach

### New service: `IOpenLibraryService` / `OpenLibraryService`

A focused, single-responsibility service that encapsulates the two-step Open Library HTTP interaction described in `open_library_book_synopsis.md`. It depends only on `HttpClient` (injected via `IHttpClientFactory`) and `ILogger<OpenLibraryService>`. All JSON parsing and HTTP error handling lives here — no caller ever sees an exception from this service.

The two API calls are:
1. `GET https://openlibrary.org/search.json?title={Uri.EscapeDataString(title)}&author={Uri.EscapeDataString(author)}`
   → reads `docs[0].key` (the work key, e.g. `/works/OL893415W`)
2. `GET https://openlibrary.org{workKey}.json`
   → reads `description` as either a plain string or `{ "type": "...", "value": "..." }`

The service is registered with `AddHttpClient<IOpenLibraryService, OpenLibraryService>()` which wires `IHttpClientFactory`-managed `HttpClient` lifecycle automatically.

### Modified service: `BookContextService`

`BookContextService` gains `IOpenLibraryService` as an additional constructor dependency alongside `IOllamaService`. The change is isolated to `GenerateAndSaveAsync`:

```
if (book.Synopsis is null or whitespace)
    book.Synopsis = await openLibraryService.GetSynopsisAsync(book.Title, book.Author, ct) ?? book.Synopsis;
```

The synopsis is written to `book.Synopsis` and included in the same `SaveChangesAsync` that already writes `book.Context`. `GenerateContextAsync` receives the synopsis (or null) and conditionally adds a "Synopsis from Open Library:" section to the Ollama prompt before the existing instruction block. The output constraints (≤120 words, plain text, no markdown) are unchanged.

The `BookContextAgentTool` return value is still `book.Context` only — the synopsis is not surfaced there.

### EF Core migration

A new migration adds `"Synopsis" TEXT NULL` to the `book` table. Existing rows default to `NULL` and continue to work without any data backfill.

### SOLID alignment

| Principle | How it applies |
| --- | --- |
| SRP | `OpenLibraryService` owns HTTP; `BookContextService` owns generation; no controller or tool touches `IOpenLibraryService`. |
| OCP | `BookContextService` is extended by adding a new dependency; the existing methods and `IBookContextService` interface are unchanged. |
| LSP | `IOpenLibraryService` can be substituted with a fake in tests without affecting behavior at call sites. |
| ISP | `IOpenLibraryService` has a single method; consumers that only need context generation do not depend on HTTP configuration. |
| DIP | `BookContextService` depends on `IOpenLibraryService`, not `HttpClient` or `OpenLibraryService` directly. |

## Component Breakdown

**Existing files to modify:**

- `WebApp/Models/Book.cs` — add `public string? Synopsis { get; set; }` after `Context`.
- `WebApp/Services/BookContextService.cs` — inject `IOpenLibraryService`; call `GetSynopsisAsync` in `GenerateAndSaveAsync` when `book.Synopsis` is null; pass synopsis to `GenerateContextAsync`; include synopsis in the Ollama prompt when non-null.
- `WebApp/Program.cs` — add `builder.Services.AddHttpClient<IOpenLibraryService, OpenLibraryService>();` after existing service registrations.
- `WebApp.Tests/Services/BookContextServiceTests.cs` — add `FakeOpenLibraryService` stub; update `new BookContextService(db, ollama)` constructor call to also pass the fake; add test cases for the synopsis-enriched path and the fallback (null synopsis) path.

**New files to create:**

- `WebApp/Services/OpenLibraryService.cs` — `IOpenLibraryService` interface and `OpenLibraryService` implementation.
- `WebApp/Migrations/<timestamp>_AddBookSynopsis.cs` — EF Core migration (generated via `dotnet ef migrations add AddBookSynopsis` inside the `webapp` container).

## Dependencies

- `System.Net.Http.Json` — available in the `Microsoft.AspNetCore.App` shared framework (.NET 9); no new `PackageReference` required.
- Open Library public API — free, no API key. Requires outbound HTTP from the `webapp` container at context generation time.
- Running PostgreSQL — needed for the migration; already a Docker Compose dependency.

## Flow

```mermaid
sequenceDiagram
    actor Trigger as User or Agent
    participant BookContextAgentTool
    participant BookContextController
    participant BookContextService
    participant OpenLibraryService
    participant OllamaService
    participant AppDbContext

    alt User-triggered
        Trigger->>BookContextController: POST /api/books/{id}/context/generate
        BookContextController->>BookContextService: GenerateAndSaveAsync(bookId, userId)
    else Agent-triggered
        Trigger->>BookContextAgentTool: GenerateBookContext tool call
        BookContextAgentTool->>BookContextService: GenerateAndSaveAsync(bookId, userId)
    end

    BookContextService->>AppDbContext: Load Book (UserId-scoped)

    alt book.Synopsis is null or empty
        BookContextService->>OpenLibraryService: GetSynopsisAsync(title, author)
        OpenLibraryService->>OpenLibrary API: GET /search.json?title=...&author=...
        OpenLibrary API-->>OpenLibraryService: { docs: [{ key: "/works/OL..." }] }
        OpenLibraryService->>OpenLibrary API: GET /works/OL....json
        OpenLibrary API-->>OpenLibraryService: { description: "..." }
        OpenLibraryService-->>BookContextService: synopsis string or null
        BookContextService->>AppDbContext: book.Synopsis = synopsis (if non-null)
    end

    BookContextService->>OllamaService: CompleteAsync(prompt with synopsis if available)
    OllamaService-->>BookContextService: generated context (≤120 words, plain text)
    BookContextService->>AppDbContext: book.Context = context; SaveChangesAsync
    BookContextService-->>Trigger: context string (synopsis never included)
```

## Risk Assessment

| Risk | Evidence | Mitigation |
| --- | --- | --- |
| Open Library API unavailable at context-generation time | External HTTP dependency; no SLA | `OpenLibraryService` catches all exceptions, logs, and returns `null`; generation falls back to pure-LLM behavior |
| Hallucinated or inaccurate synopsis from Open Library | API data quality varies by book | Synopsis is presented in the prompt as a grounding hint, not as a guaranteed fact; the LLM prompt still instructs the model to produce its own literary context paragraph |
| Increased latency for context generation (two extra HTTP round-trips) | Sequential API calls before Ollama | Synopsis is fetched only once and persisted; subsequent calls reuse `book.Synopsis` without any HTTP |
| EF In-Memory provider does not support pgvector column types | `TestAppDbContext` already ignores `BookEmbedding` and `BookNoteEmbedding` | No pgvector involvement in this feature; the `Synopsis` column is plain `TEXT` and will work with in-memory EF in tests |
| `BookContextService` constructor signature change breaks existing tests | `new BookContextService(db, ollama)` used in `BookContextServiceTests` | Update call site to pass `FakeOpenLibraryService`; the change is isolated to that one test file |
