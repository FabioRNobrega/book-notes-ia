# Validation: Book Notes Agent Tool

## Table of Contents

- [Acceptance Criteria](#acceptance-criteria)
- [Test Cases](#test-cases)
- [Manual Verification](#manual-verification)
- [Definition of Done](#definition-of-done)
- [Rollback Plan](#rollback-plan)

## Acceptance Criteria

| Requirement | Acceptance Criterion |
| --- | --- |
| FR1 | `IBookNotesAnalysisService` exists in `WebApp/Services/BookNotesAnalysisService.cs` with a single method; `BookNotesAnalysisService` implements it and does not access `IOllamaService` from `BookNotesAgentTool`. |
| FR2 | An integration test confirms that when a book has seeded `BookNote` rows, all of them are returned ordered by `ClippedAtUtc` with no truncation; querying with a different `userId` returns the no-notes message. |
| FR3 | The prompt sent to `FakeOllamaService` contains lines matching `<note>{Content}</note>` for each note, while the returned tool string does not list raw `<note>` lines by default. |
| FR4 | The Ollama prompt passed to `FakeOllamaService` in tests contains the substring `Book Context:`, the formatted notes block, and the user's `PreferredLanguage` value (e.g. `"English"`). |
| FR5 | When no `BookNote` rows exist for the book + user, the tool returns a string containing "No notes or highlights found" without calling `IOllamaService`. |
| FR6 | `BookNotesAgentTool` constructor parameters are `IBookLookupService` and `IBookNotesAnalysisService` only; no `AppDbContext` or `IOllamaService` injection. |
| FR7 | `AIFunctionFactory.Create` is called with `name: "GetBookNotesWithAnalysis"`. |
| FR8 | When `IBookLookupService.FindAsync` returns `null`, the tool returns a string containing "was not found in your library". |
| FR9 | `Program.cs` contains `AddScoped<IBookNotesAnalysisService, BookNotesAnalysisService>()` and `AddScoped<IBookNotesAgentTool, BookNotesAgentTool>()`. |
| FR10 | In `ChatController.Send`, the tools list contains both `_bookContextTool.Create(userId)` and `_bookNotesTool.Create(userId)` when `books.Count > 0`. |
| FR11 | `BuildOrchestratorInstructions` output contains "GetBookNotesWithAnalysis" and references calling it for personal notes, highlights, or annotations. |
| FR12 | All existing tests in `AgentToolsPostgresTests.cs` compile and pass after `CreateController` is updated to accept `IBookNotesAgentTool`. |

## Test Cases

**Integration tests** (`WebApp.Tests/Integration/AgentToolsPostgresTests.cs`):

- `GetBookNotesWithAnalysis_WithSeededNotes_ReturnsAnalysisWithoutRawNotes`:
  Seed a user, book, and two `BookNote` rows with distinct `LocationText` and `Content`. Create a `BookNotesAnalysisService` with a `FakeOllamaService`. Invoke the `AIFunction` with the book title. Assert the result string contains the analysis and does not contain raw `<note>` lines. Assert the captured prompt contains both formatted note lines. Assert `FakeOllamaService` was called exactly once.

- `GetBookNotesWithAnalysis_WithNoNotes_ReturnsNoNotesMessage`:
  Seed a user and book but no `BookNote` rows. Invoke the tool. Assert the result contains "No notes or highlights found". Assert `FakeOllamaService` was never called.

- `GetBookNotesWithAnalysis_WithUnknownTitle_ReturnsNotFoundMessage`:
  Do not seed a matching book. Invoke the tool with an unrecognised title. Assert the result contains "was not found in your library".

- `GetBookNotesWithAnalysis_IsolatesNotesByUserId_DoesNotReturnOtherUserNotes`:
  Seed user A with a book and notes; seed user B with the same book (same title). Invoke the tool scoped to user B with no notes. Assert the result contains "No notes or highlights found", proving user A's notes are never surfaced.

- `GetBookNotesWithAnalysis_WithPreferredLanguage_IncludesLanguageInPrompt`: seed a user profile with `PreferredLanguage = "Portuguese"`, seed notes, invoke the tool, assert the captured prompt contains `"Portuguese"`.

**Update to existing test:**

- `ChatRefresh_WithPostgresSeededUserAndMafSession_RendersCachedMessages` — add a `FakeBookNotesAgentTool` (returns a stub `AIFunction` for `GetBookNotesWithAnalysis`) and pass it to the updated `CreateController` signature. Test behaviour is unchanged.

**Unit tests** (`WebApp.Tests/Services/` — ⚠️ TODO: create file `BookNotesAnalysisServiceTests.cs`):

- `GetNotesWithAnalysisAsync_BuildsPromptWithBookContextAndNotes`: use EF Core InMemory provider, seed a `Book` with a non-null `Context` and two notes, assert the captured prompt passed to `FakeOllamaService` contains the context text and both note lines.
- `GetNotesWithAnalysisAsync_WhenContextIsNull_UsesNotAvailablePlaceholder`: seed a `Book` with `Context = null`, assert the prompt contains "Not available.".

## Manual Verification

1. Start the stack: `make docker-run` (Linux) or the appropriate platform target.
2. Sign in and import a Kindle clippings file that has highlights for at least one book.
3. Navigate to chat and ask: "What highlights do I have from [book title]?"
4. Verify the agent calls `GetBookNotesWithAnalysis` (visible in `docker compose logs -f webapp` as the tool invocation) and returns a natural thematic analysis paragraph without listing raw note lines unless explicitly asked.
5. Ask about a book in your library with no imported notes. Verify the agent returns a clear "no notes found" message.
6. Ask about a title not in your library. Verify the agent returns a "not found" message.
7. Ask a combined question: "Tell me about Dune and what I highlighted." Verify the agent calls both `GenerateBookContext` and `GetBookNotesWithAnalysis` and incorporates both results.
8. Run `make test` and confirm all tests pass.

## Definition of Done

- `Specs/20260604133551-book-notes-agent-tool/` contains Requirements, Plan, and Validation.
- `WebApp/Services/BookNotesAnalysisService.cs` exists with `IBookNotesAnalysisService` and `BookNotesAnalysisService`.
- `WebApp/Services/BookNotesAgentTool.cs` exists and does not inject `AppDbContext` or `IOllamaService` directly.
- `Program.cs` registers both new scoped services.
- `ChatController.cs` injects `IBookNotesAgentTool`, includes it in the tools list, and updates orchestrator instructions.
- `AgentToolsPostgresTests.cs` compiles: `CreateController` accepts `IBookNotesAgentTool`; `FakeBookNotesAgentTool` is present.
- All new integration test cases pass under `make test`.
- No existing tests regress.

## Rollback Plan

- Remove `_bookNotesTool.Create(userId)` from the tools list in `ChatController.Send` to stop the agent from calling the tool without deleting any code.
- Remove the two `AddScoped` lines from `Program.cs` to unregister the services.
- The `book_note` table and all existing data are unaffected; no migration rollback is needed.
