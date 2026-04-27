# Validation: MAF Agent-as-Tools Refactor

## Table of Contents

- [Validation: MAF Agent-as-Tools Refactor](#validation-maf-agent-as-tools-refactor)
  - [Acceptance Criteria](#acceptance-criteria)
  - [Test Cases](#test-cases)
  - [Manual Verification](#manual-verification)
  - [Definition of Done](#definition-of-done)
  - [Rollback Plan](#rollback-plan)

## Acceptance Criteria

| Requirement | Acceptance Criterion |
| --- | --- |
| FR1 | `IChatToolRouter.cs`, `ChatToolRouteDecision.cs`, and any reference to `ChatToolRouter` are absent from the compiled solution (`dotnet build` succeeds and `grep -r "ChatToolRouter" WebApp/ WebApp.Tests/` returns no results). |
| FR2 | `BookContextAgentTool.Create("user-id")` returns a non-null `AIFunction` with `Name == "GenerateBookContext"`. When invoked with a title matching a book owned by that user, it returns a non-empty context string. When invoked with an unmatched title, it returns a message containing "not found". |
| FR3 | `ChatOrchestratorAgent.RunAsync` passes the provided `tools` list to `ChatClientAgentRunOptions.ChatOptions.Tools` before calling `agent.RunAsync`. A unit test on `FakeChatOrchestratorAgent` confirms the tools are forwarded. |
| FR4 | `ChatController.Send()` contains no reference to `IChatToolRouter`, `RouteAsync`, `agentcontext:`, or `workingContext`. `grep` on `ChatController.cs` for these strings returns no matches. |
| FR5 | The system instructions string passed to `_agent.RunAsync` contains the user's book library list (title + author) when the user has at least one book; it omits the library section when the user has no books. |
| FR6 | `IBookContextService` has no `GenerateToolResponseAsync` method. `BookContextService` has no `AppendContext` method. `GenerateBookContextToolResult.cs` does not exist. `NotesController.GenerateContext` is unchanged (verified by `grep` — it still calls `GenerateAndSaveAsync`). `BookContextController.Generate` calls `GenerateAndSaveAsync` and returns `Ok(new { context })`; `GenerateBookContextToolRequest` and `GenerateBookContextToolResponse` records do not exist. |
| FR7 | `ChatControllerTests.cs` has no `FakeChatToolRouter` class. The `Send_WhenToolIsSelected_*` test asserts that the tools list passed to `FakeChatOrchestratorAgent` contains one `AIFunction` named `"GenerateBookContext"`. |
| FR8 | `ChatToolRouterTests.cs` does not exist. `BookContextAgentToolTests.cs` exists with at minimum three `[Fact]` tests covering: match found, no match, and userId isolation. |
| FR9 | `Program.cs` contains no registration for `IChatToolRouter`. It contains a scoped registration for `IBookContextAgentTool`. |

## Test Cases

**Unit tests (new — `WebApp.Tests/Services/BookContextAgentToolTests.cs`):**

- `Create_WhenBookTitleMatchesUserBook_ReturnsGeneratedContext`: seed one book for `user-1`, invoke `AIFunction` with the exact title, assert return value equals `FakeBookContextService.GenerateResult`.
- `Create_WhenBookTitleDoesNotMatch_ReturnsNotFoundMessage`: invoke `AIFunction` with a title not in the DB, assert return string contains "not found".
- `Create_WhenBookBelongsToOtherUser_ReturnsNotFoundMessage`: seed one book for `user-2`, invoke `AIFunction` with `userId = "user-1"`, assert "not found" (userId isolation).

**Unit tests (updated — `WebApp.Tests/Controllers/BookContextControllerTests.cs`):**

- `Generate_ReturnsContextWhenBookExists`: call `Generate(bookId, ct)`; assert `OkObjectResult` with an anonymous `{ context }` value matching `FakeBookContextService.GenerateAndSaveResult`.
- `Generate_ReturnsNotFoundWhenBookDoesNotExist`: unchanged behavior, still asserts `NotFoundObjectResult` when service throws `KeyNotFoundException`.

**Unit tests (updated — `WebApp.Tests/Controllers/ChatControllerTests.cs`):**

- `Send_WhenBookContextToolIsRegistered_PassesToolToAgent`: verify `FakeChatOrchestratorAgent.LastTools` is non-empty and the first tool is named `"GenerateBookContext"`.
- `Send_WhenNoToolRouted_CallsAgentAndSavesSession`: this test remains but without `FakeChatToolRouter`; assert agent receives no tools list or an empty list when the user has no books.
- `Reset_RemovesSessionAndWorkingContext`: unchanged behavior; assert `agentsession:{userId}` is cleared.
- `Send_WhenMessageIsEmpty_ReturnsEmptyContent`: unchanged.
- `Send_WhenAgentThrows_ReturnsErrorBotMessage`: unchanged.

**Unit tests (updated — `WebApp.Tests/Services/BookContextServiceTests.cs`):**

- Remove: `GenerateToolResponseAsync_AppendsContextAndPersists` (method deleted).
- Retain: `GenerateAndSaveAsync_PersistsContextToBook` and any other existing tests that do not reference the deleted method.

**⚠️ TODO: Integration test (manual):** After the stack is running (`make docker-run-mac` or equivalent), log in, import a Kindle clipping for a known book, open the chat, and send "Tell me about [Book Title]". Verify the agent calls `GenerateBookContext`, the chat response includes literary context, and the book's context field is updated in the database (`SELECT context FROM "Books" WHERE "Title" = '...'`).

## Manual Verification

1. Start the stack: `make docker-run-mac` (or Linux/Windows equivalent).
2. Log in and navigate to the Notes library. Import a Kindle `.txt` clipping for at least one book.
3. Open the Chat page.
4. Send: "Tell me about [exact book title from your library]."
5. Observe: the assistant replies with a literary context paragraph for the book. No routing error or generic response.
6. In the database (via `docker compose exec postgres psql -U postgres booknotes -c "SELECT title, context FROM \"Books\" WHERE context IS NOT NULL;"`), confirm the `context` column is populated for that book.
7. Send a follow-up: "What are the main themes?" — the assistant should answer using the already-embedded context without calling the tool again.
8. Send a completely unrelated message: "What should I have for lunch?" — the assistant should answer without mentioning book context.
9. Open another chat session (Reset chat) and verify the session is cleared.
10. Check `docker compose logs webapp` for any errors during tool execution.

## Definition of Done

- `Specs/20260427110015-maf-agent-as-tools-refactor/` contains Requirements.md, Plan.md, and Validation.md.
- `WebApp/Services/BookContextAgentTool.cs` exists and compiles.
- `IChatToolRouter.cs`, `ChatToolRouteDecision.cs`, `GenerateBookContextToolResult.cs` are deleted.
- `WebApp.Tests/Services/ChatToolRouterTests.cs` is deleted.
- `WebApp.Tests/Services/BookContextAgentToolTests.cs` exists with ≥ 3 passing tests.
- `dotnet test WebApp.Tests/WebApp.Tests.csproj` passes (all tests green).
- `ChatController.cs` contains no reference to `IChatToolRouter`, `agentcontext:`, or `workingContext`.
- `IBookContextService` has no `GenerateToolResponseAsync` method.
- `BookContextController.Generate` returns `{ context }` (not `GenerateBookContextToolResponse`). `GenerateBookContextToolRequest` and `GenerateBookContextToolResponse` records do not exist.
- `NotesController.GenerateContext` is unmodified and still calls `GenerateAndSaveAsync`.
- `Program.cs` registers `IBookContextAgentTool` and does not register `IChatToolRouter`.
- `Specs/Roadmap.md` is updated with a row for this spec.

## Rollback Plan

Revert all changed files to their state on `main` before this spec's branch was merged. Specifically:

- Restore `WebApp/Services/IChatToolRouter.cs`, `ChatToolRouteDecision.cs`, `GenerateBookContextToolResult.cs` from Git history.
- Restore `WebApp/Services/IBookContextService.cs` and `BookContextService.cs` (re-add `GenerateToolResponseAsync`).
- Delete `WebApp/Services/BookContextAgentTool.cs`.
- Restore `ChatController.cs` and `Program.cs`.
- Restore `ChatControllerTests.cs` and `ChatToolRouterTests.cs`.

The rollback has no database schema impact (no migrations). Redis session keys are forward-compatible: a session started after the rollback will resume with the old routing layer on the next request.
