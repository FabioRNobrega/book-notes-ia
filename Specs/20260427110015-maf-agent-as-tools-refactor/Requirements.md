# Requirements: MAF Agent-as-Tools Refactor

## Table of Contents

- [Requirements: MAF Agent-as-Tools Refactor](#requirements-maf-agent-as-tools-refactor)
  - [Problem Statement](#problem-statement)
  - [User Stories](#user-stories)
  - [Functional Requirements](#functional-requirements)
  - [Non-Functional Requirements](#non-functional-requirements)
  - [Out of Scope](#out-of-scope)
  - [Open Questions](#open-questions)

## Problem Statement

The current chat implementation does not use Microsoft Agent Framework (MAF) tool-calling correctly. `ChatController.Send()` manually invokes `IChatToolRouter.RouteAsync()` before the agent runs, using a second LLM call to decide which tool to execute. The tool result is then string-concatenated into a `workingContext` blob and passed back to the agent as raw instructions. This bypasses MAF's native function-calling loop entirely:

- `ChatToolRouter` reimplements routing that the model's built-in function-calling already handles.
- `ChatOrchestratorAgent` receives no registered `AITool` objects; it only receives pre-computed text as instructions.
- Two LLM calls are made per user message: one for routing, one for answering.
- Adding future tools (e.g. notes search, recommendation) requires duplicating the routing logic in the controller.

The correct MAF pattern is: register tools on the `ChatClientAgent` before the run; let the model decide autonomously whether and when to call them; MAF executes the function and feeds the result back into the conversation transparently.

## User Stories

- Given a user on the Notes library page, when they press the **Generate Context** button on a book card, then the app calls `IBookContextService.GenerateAndSaveAsync`, saves the literary context to `Book.Context` in the database, and replaces the button with the rendered context panel — with no chat interaction required.
- Given a user who has already generated context via the button, when they open the chat and ask about that book, then the context already stored in `Book.Context` is available to the agent through the book list in system instructions and the agent can reference it without calling any tool.
- Given a user message that references a book in their library (e.g. "tell me about Dune"), when the agent processes the message, then the agent autonomously calls `GenerateBookContext` for that book, incorporates the result into its reply, and the generated context is persisted to `Book.Context` — without the controller making a separate routing decision.
- Given a user message that does not reference any book (e.g. "recommend something for my weekend"), when the agent processes the message, then no tool is invoked and the agent answers directly using profile instructions.
- Given a user whose library has no books, when the agent processes any message, then the `GenerateBookContext` tool is registered but the model does not call it because no books are listed in the system instructions.
- Given a conversation where `GenerateBookContext` was already called for a book, when the user asks a follow-up question about the same book, then the tool result is already in the conversation history and the model answers without calling the tool a second time.
- Given a developer adding a new tool (e.g. `SearchNotes`), when they create a new `IBookContextAgentTool`-style service, then wiring it into `ChatController` requires no changes to routing logic or a tool-dispatch switch.

## Functional Requirements

1. FR1 — Delete `IChatToolRouter`, `ChatToolRouter`, and `ChatToolRouteDecision`. These classes are replaced by native MAF tool registration and must not exist in the codebase after the refactor.

2. FR2 — Create `IBookContextAgentTool` (interface) and `BookContextAgentTool` (implementation) in `WebApp/Services/BookContextAgentTool.cs`. The service exposes a `Create(string userId)` method that returns an `AIFunction` named `GenerateBookContext`. The function accepts a `bookTitle` string parameter (not a GUID), resolves it to a `Book` record owned by that user via a case-insensitive title match, calls `IBookContextService.GenerateAndSaveAsync`, and returns the generated context string.

3. FR3 — Update `IChatOrchestratorAgent.RunAsync` to accept an optional `IReadOnlyList<AITool>? tools` parameter. `ChatOrchestratorAgent` must pass those tools to `ChatClientAgentRunOptions.ChatOptions.Tools` so MAF's function-calling loop can invoke them.

4. FR4 — Update `ChatController.Send()` to remove all references to `IChatToolRouter` and the `agentcontext:{userId}` cache key. Instead, `Send()` must build the `AIFunction` from `IBookContextAgentTool.Create(userId)` and pass it to `IChatOrchestratorAgent.RunAsync`.

5. FR5 — Update `ChatController.BuildOrchestratorInstructions` to include the user's book library list (title + author, up to 25 books) in the system instructions so the model can identify when a user message is about a known book and call the tool. The book list must be fetched from `AppDbContext` scoped to the authenticated user.

6. FR6 — Remove `GenerateToolResponseAsync` from `IBookContextService` and `BookContextService`. The `AppendedContext` pattern it implemented is superseded by the native MAF conversation history. Remove `GenerateBookContextToolResult` once unused. `NotesController.GenerateContext` (the "Generate Context" button handler at `POST /notes/book/{id}/context/generate`) already calls `GenerateAndSaveAsync` and must not be modified. `BookContextController.Generate` (the JSON API at `POST /api/books/{bookId}/context/generate`) currently calls `GenerateToolResponseAsync`; update it to call `GenerateAndSaveAsync` and simplify its response to `{ context }`. Remove the `GenerateBookContextToolRequest` and `GenerateBookContextToolResponse` records.

7. FR7 — Update `WebApp.Tests/Controllers/ChatControllerTests.cs` to remove `FakeChatToolRouter` and any assertions on the `agentcontext:{userId}` cache key. Update `FakeChatOrchestratorAgent` to capture the registered tools list so tests can assert that `BookContextAgentTool` is wired in.

8. FR8 — Delete `WebApp.Tests/Services/ChatToolRouterTests.cs`. Add `WebApp.Tests/Services/BookContextAgentToolTests.cs` with unit tests that verify the `GenerateBookContext` `AIFunction`: resolves a matching book title for the correct user, calls `IBookContextService.GenerateAndSaveAsync`, and returns a non-empty context string; returns a "not found" message when no matching book exists.

9. FR9 — Update `WebApp/Program.cs` to remove the `IChatToolRouter` / `ChatToolRouter` registration and add the `IBookContextAgentTool` / `BookContextAgentTool` scoped registration.

## Non-Functional Requirements

- The number of LLM calls per user message must drop from two (routing call + answer call) to one (answer call with tools registered). The routing call is eliminated.
- All new and modified code must follow file-scoped namespaces and constructor injection as used throughout the project.
- `AppDbContext` must not be injected into a singleton service. `BookContextAgentTool` must be registered as scoped.
- The refactored agent must remain testable without Ollama or PostgreSQL: `FakeBookContextService` and `FakeChatOrchestratorAgent` fakes suffice for controller tests; `BookContextAgentToolTests` uses in-memory EF Core.

## Out of Scope

- Adding additional tools beyond `GenerateBookContext` (e.g. `SearchNotes`, `RecommendBook`); the architecture must make them easy to add, but they are not implemented in this spec.
- Streaming responses; the current non-streaming `RunAsync` pattern is unchanged.
- Changes to how session JSON is serialized or stored in Redis.
- UI changes to the chat view, the Notes library view, or any HTMX partials.
- Changes to `NotesController.GenerateContext` or `_BookContext.cshtml`: the "Generate Context" button already calls `GenerateAndSaveAsync` correctly and is untouched by this refactor.
- Routing the "Generate Context" button through the agent orchestrator (Option 2): the button is an explicit user action with a fixed output format; it goes directly to `IBookContextService`, not through the agent framework.
- Displaying generated context inside the chat panel when triggered from the Notes page.

## Open Questions

- ~~⚠️ TODO: Does `Microsoft.Agents.AI 1.3.0`'s `ChatClientAgent` correctly handle the function-calling loop for models served by `OllamaSharp`?~~ **Resolved:** tested and confirmed working. No mitigation needed.
- ~~⚠️ TODO: Should the book list injected into system instructions be capped at 25 or expanded?~~ **Resolved:** cap stays at 25, matching the current router limit.
- ~~⚠️ TODO: Should the tool skip re-generation when `Book.Context` already exists?~~ **Resolved:** the `GenerateBookContext` `AIFunction` must check `Book.Context` first; if it is already populated, return it directly from the database without calling `IBookContextService.GenerateAndSaveAsync`. Only generate when `Book.Context` is null or empty. This is now FR2 behavior — see also the impact on `BookContextAgentTool` in Plan.md.
