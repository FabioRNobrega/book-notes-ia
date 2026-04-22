# Validation: Chat-Triggered Book Context Generation

## Table of Contents

- [Validation: Chat-Triggered Book Context Generation](#validation-chat-triggered-book-context-generation)
  - [Acceptance Criteria](#acceptance-criteria)
  - [Test Cases](#test-cases)
  - [Manual Verification](#manual-verification)
  - [Definition of Done](#definition-of-done)
  - [Rollback Plan](#rollback-plan)

## Acceptance Criteria

| Requirement | Acceptance Criteria |
| --- | --- |
| FR1 | Blank messages return an empty response and unauthenticated sends return unauthorized. |
| FR2 | A clear request like "Generate context for Dune" routes to `GenerateBookContext` when the user owns a matching book. |
| FR3 | Users with no books, ambiguous requests, or non-context requests receive a `none` route decision. |
| FR4 | A book owned by another user is not loaded or updated. |
| FR5 | `IOllamaService.CompleteAsync` is called with a prompt containing title, author, and preferred language when profile data exists. |
| FR6 | `Book.Context` equals the generated text and `UpdatedAt` changes after generation. |
| FR7 | The working context contains `[GenerateBookContext]`, `Book:`, `Author:`, and `Summary:`. |
| FR8 | Redis receives updated `agentcontext:{userId}` and `agentsession:{userId}` values. |
| FR9 | Reset removes both chat session and working context cache keys. |

## Test Cases

Unit tests:

- `WebApp.Tests/Services/BookContextServiceTests.cs`: verify `GenerateToolResponseAsync` saves generated context, appends existing context, includes `[GenerateBookContext]`, and uses the profile language in the prompt.
- ⚠️ TODO: Add `IChatToolRouter` tests for clear title match, author match, no books, non-context message, and ambiguous message.

Controller tests:

- `WebApp.Tests/Controllers/ChatControllerTests.cs`: verify selected tool appends working context, persists session, renders `_BotMessage`, and reset clears both cache keys.
- `WebApp.Tests/Controllers/BookContextControllerTests.cs`: verify API generation response payload and not-found behavior.

Integration tests:

- ⚠️ TODO: Add a container-backed test that imports a small Kindle clipping file, creates a book, generates context, and verifies persisted `Book.Context` through the API.

## Manual Verification

1. Start the stack with the platform-appropriate Make target, for example `make docker-run`.
2. Open `http://localhost:8080/`, register or sign in, and import a valid Kindle clippings `.txt` file.
3. Open the notes library and confirm the imported book appears.
4. Ask chat to generate context/background for that exact book title.
5. Confirm the assistant answers and that reopening the book details shows generated context.
6. Use chat reset and confirm the UI reports that session history was deleted.

## Definition of Done

- Requirements, plan, and validation docs are updated in this spec folder.
- `make test` passes through `docker-compose.test.yml`.
- No source changes bypass user-owned data filters.
- New behavior has controller or service coverage matching the existing `WebApp.Tests` style.
- README or `AGENTS.md` is updated only if commands, architecture, or workflow changed.

## Rollback Plan

- If routing causes regressions, disable the chat tool path by returning `new ChatToolRouteDecision("none", null)` from `IChatToolRouter` while preserving direct notes-page generation.
- If generated context corrupts book data, clear affected rows through `BookContextService.ClearAsync` or the `DELETE api/books/{bookId}/context` endpoint.
- If Redis session data causes bad chat behavior, use the existing chat reset path to remove `agentsession:{userId}` and `agentcontext:{userId}`.
