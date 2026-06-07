# Validation: Open Library Synopsis Enrichment

## Table of Contents

- [Acceptance Criteria](#acceptance-criteria)
- [Test Cases](#test-cases)
- [Manual Verification](#manual-verification)
- [Definition of Done](#definition-of-done)
- [Rollback Plan](#rollback-plan)

## Acceptance Criteria

| Requirement | Acceptance Criterion |
| --- | --- |
| FR1 | `Book` has a `string? Synopsis` property; `dotnet ef migrations list` inside the container shows `AddBookSynopsis`; the migration applies cleanly to a fresh database. |
| FR2 | `IOpenLibraryService` exists in `WebApp/Services/OpenLibraryService.cs` with exactly one method `GetSynopsisAsync(string, string, CancellationToken)`. |
| FR3 | `OpenLibraryService` correctly extracts the synopsis from a plain-string `description` and from an object `description.value`. Returns `null` when `docs` is empty or the description is absent. |
| FR4 | When the simulated HTTP call throws or returns invalid JSON, `OpenLibraryService.GetSynopsisAsync` returns `null` without throwing; a warning is written to the logger. |
| FR5 | After `GenerateAndSaveAsync` completes for a book with no stored synopsis and Open Library returns a value, `book.Synopsis` in the DB equals that value and is persisted in the same `SaveChangesAsync` as `book.Context`. |
| FR6 | When `book.Synopsis` is non-null, the Ollama prompt passed to `IOllamaService.CompleteAsync` contains the synopsis text. |
| FR7 | When `GetSynopsisAsync` returns `null` (Open Library has no synopsis), `CompleteAsync` is still called and the generated context is persisted; no exception is thrown. |
| FR8 | `BookContextAgentTool.Create` returns only `book.Context`; the synopsis string does not appear in the tool result. |
| FR9 | `Program.cs` registers `IOpenLibraryService` via `AddHttpClient<IOpenLibraryService, OpenLibraryService>()`. |

## Test Cases

**Unit tests — `WebApp.Tests/Services/BookContextServiceTests.cs`:**

- `GenerateAndSaveAsync_PersistsGeneratedContextToBook` — existing test; update constructor call to pass a `FakeOpenLibraryService` that returns `null`. Verifies that when no synopsis is available the behavior is unchanged.
- `GenerateAndSaveAsync_WithSynopsis_IncludesSynopsisInOllamaPrompt` — new test; `FakeOpenLibraryService` returns a non-empty synopsis string. Assert that `ollama.LastPrompt` contains that synopsis string and that `savedBook.Synopsis` equals the returned synopsis.
- `GenerateAndSaveAsync_WithExistingSynopsis_DoesNotCallOpenLibrary` — new test; seed `book.Synopsis` with a pre-existing value. Assert `FakeOpenLibraryService.CallCount == 0` and that the saved synopsis is unchanged.
- `GenerateAndSaveAsync_WhenOpenLibraryReturnsNull_FallsBackToLlmGeneration` — new test; `FakeOpenLibraryService` returns `null`. Assert that `CompleteAsync` is still called and `book.Context` is persisted.

**Unit tests — `WebApp.Tests/Services/OpenLibraryServiceTests.cs` (new file):**

- ⚠️ TODO: `GetSynopsisAsync_ReturnsPlainStringDescription` — mock `HttpMessageHandler` returning a search result and a work detail with `"description": "plain string"`. Assert return value equals that string.
- ⚠️ TODO: `GetSynopsisAsync_ReturnsObjectDescription` — mock returns `"description": { "type": "/type/text", "value": "..." }`. Assert return value equals the `value` field.
- ⚠️ TODO: `GetSynopsisAsync_ReturnsNullWhenDocsEmpty` — mock search returns `{ "docs": [] }`. Assert `null`.
- ⚠️ TODO: `GetSynopsisAsync_ReturnsNullOnHttpError` — mock throws `HttpRequestException`. Assert `null` and that the logger recorded a warning.
- ⚠️ TODO: `GetSynopsisAsync_ReturnsNullWhenDescriptionAbsent` — work detail JSON has no `description` field. Assert `null`.

**Integration tests:**

None required for this spec. `IOpenLibraryService` is tested via mock; no Docker-backed HTTP tests are in scope.

## Manual Verification

1. Start the stack with `make docker-run` (Linux/SteamOS).
2. Log in and import a Kindle clippings file that contains at least one well-known book (e.g. *Dune* by Frank Herbert).
3. Open the Notes library, navigate to the book detail, and confirm `Synopsis` is initially empty/absent.
4. Trigger context generation via the UI or call:
   ```
   curl -X POST http://localhost:8080/api/books/{bookId}/context/generate \
        -H "Cookie: <auth-cookie>"
   ```
5. Verify the response body contains a `context` string.
6. Open a `psql` shell inside the container and run:
   ```sql
   SELECT "Title", "Synopsis", "Context" FROM book WHERE "Title" ILIKE '%dune%';
   ```
   Confirm `Synopsis` is now populated with a real Open Library description and `Context` is non-null.
7. Trigger context generation again for the same book and confirm the `webapp` container logs do not show any Open Library HTTP request (synopsis is reused from DB).
8. Ask the chat agent "Tell me about the book Dune" and confirm the generated context response does not literally quote the stored synopsis verbatim (it is used as a grounding hint, not returned as-is).
9. Test fallback: remove the internet route from the `webapp` container temporarily (`docker network disconnect` or equivalent) and trigger context generation for a new book. Verify context is still generated (fallback to pure-LLM), and the `webapp` logs contain a warning from `OpenLibraryService`.

## Definition of Done

- `Requirements.md`, `Plan.md`, and `Validation.md` in `Specs/20260606221009-open-library-synopsis-enrichment/` are complete.
- `Book.Synopsis` property added to `WebApp/Models/Book.cs`.
- EF Core migration `AddBookSynopsis` applied successfully inside the `webapp` container.
- `IOpenLibraryService` and `OpenLibraryService` created in `WebApp/Services/OpenLibraryService.cs`.
- `BookContextService` updated to inject `IOpenLibraryService`, fetch and persist the synopsis, and enrich the Ollama prompt.
- `Program.cs` registers `IOpenLibraryService` via `AddHttpClient`.
- All existing tests pass (`make test`).
- `BookContextServiceTests.cs` updated: existing test constructor call fixed; new test cases added for synopsis-enriched path, pre-existing synopsis skip, and null-synopsis fallback.
- `OpenLibraryServiceTests.cs` created with the five unit test cases listed above.
- `BookContextAgentTool` is unchanged; manual verification confirms the synopsis does not appear in tool responses.

## Rollback Plan

- The `Book.Synopsis` column is nullable — removing the feature does not require a down-migration to keep the app running; existing rows with a populated `Synopsis` are silently ignored if the property is removed.
- To fully revert: remove `IOpenLibraryService` injection from `BookContextService`, delete `WebApp/Services/OpenLibraryService.cs`, remove the `AddHttpClient` registration from `Program.cs`, remove `Book.Synopsis` from the model, and run `dotnet ef migrations remove` inside the `webapp` container to drop the `AddBookSynopsis` migration before it is applied to production.
- No agent tool, orchestrator instruction, or Razor view change is required to disable the feature; all synopsis logic is contained within `BookContextService.GenerateAndSaveAsync` and `GenerateContextAsync`.
