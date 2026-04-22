# Requirements: Chat-Triggered Book Context Generation

## Table of Contents

- [Requirements: Chat-Triggered Book Context Generation](#requirements-chat-triggered-book-context-generation)
  - [Problem Statement](#problem-statement)
  - [User Stories](#user-stories)
  - [Functional Requirements](#functional-requirements)
  - [Non-Functional Requirements](#non-functional-requirements)
  - [Out of Scope](#out-of-scope)
  - [Open Questions](#open-questions)

## Problem Statement

Readers can import Kindle notes into books, but a chat answer needs grounded literary background when the user asks for context about a specific saved book. The system needs to detect that intent, generate concise context with the local Ollama model, persist it on the book, append it to the current chat working context, and answer through the existing Microsoft Agent Framework session.

## User Stories

- Given I am signed in and have imported a book, when I ask chat to generate context for that book title, then the system generates context for that user-owned book and uses it in the assistant answer.
- Given my profile has a preferred language, when context is generated for one of my books, then the context prompt asks Ollama to respond in that language.
- Given I reset chat, when the reset completes, then both the serialized agent session and working book context are removed from cache.

## Functional Requirements

1. The chat send endpoint must ignore blank messages and require an authenticated user.
2. The system must route explicit context/background requests for a single matching user-owned book to `GenerateBookContext`.
3. The system must return `none` when the user has no books or the request does not clearly target a single book.
4. The context generation service must load the book by both `bookId` and `userId`.
5. The service must generate context through `IOllamaService.CompleteAsync`.
6. The generated context must be saved to `Book.Context` and update `Book.UpdatedAt`.
7. The generated context must be appended to the chat working context with a `[GenerateBookContext]` marker, book title, author, and summary.
8. The chat controller must persist updated working context to `agentcontext:{userId}` and serialized agent session to `agentsession:{userId}` with the existing seven-day TTL.
9. Chat reset must remove both `agentsession:{userId}` and `agentcontext:{userId}`.

## Non-Functional Requirements

- Generated book context should stay concise; the existing prompt asks for under 120 words.
- Responses must use the local Ollama path already configured through `OllamaSharp`, not a cloud LLM provider.
- The feature must preserve per-user data isolation by filtering on `UserId` in database queries.
- The feature must remain testable without Ollama by using `IOllamaService`, `IChatToolRouter`, and `IChatOrchestratorAgent` abstractions in tests.
- Error handling should return an assistant error partial instead of crashing the MVC request.

## Out of Scope

- Importing non-Kindle or non-`.txt` note sources.
- Generating long-form essays, citations, or bibliographies.
- Sharing generated context across users.
- Replacing the direct notes-page context generation button/API.
- Adding a new model provider beyond Ollama.

## Open Questions

- ⚠️ TODO: Should users be able to review generated context before it is saved to `Book.Context`?
- ⚠️ TODO: Should repeated context generations overwrite `Book.Context`, create history, or keep only the latest generated text?
- ⚠️ TODO: Should routing support translated intent words beyond the current English-oriented heuristic terms?
