# Mission

## Table of Contents

- [Mission](#mission)
  - [Project](#project)
  - [Problem Statement](#problem-statement)
  - [Vision](#vision)
  - [Target Users](#target-users)
  - [Success Metrics](#success-metrics)
  - [Out of Scope](#out-of-scope)
  - [Evidence](#evidence)

## Project

BOOK-NOTES-IA is a local-first AI reading assistant for importing Kindle clippings, organizing books and notes, and chatting with a Microsoft Agent Framework agent backed by Ollama.

## Problem Statement

The repository implements a workflow for readers who have Kindle clippings in text files and want those highlights and notes to become searchable, per-user reading context. The current app imports `My Clippings.txt`-style files, deduplicates notes, stores books and notes in PostgreSQL, and lets an authenticated user ask a local AI assistant questions that can include generated book context.

## Vision

The intended end state visible in the code is a Docker-first ASP.NET Core MVC application where a signed-in reader can maintain a personal reading profile, import Kindle notes, view a book library, generate concise literary context for a saved book, and use chat sessions that stay grounded in profile and book-note context.

## Target Users

- Readers who export Kindle highlights and notes and want them organized by book and author.
- Readers who want a local AI assistant to answer in a preferred language and tone configured in their profile.
- Developers running a local .NET/Docker/Ollama stack for an AI reading assistant.

## Success Metrics

- A valid `.txt` Kindle clipping import reports `BooksTouched`, `NotesImported`, `DuplicatesSkipped`, and `InvalidEntriesSkipped` through `KindleImportSummary`.
- Imported notes are deduplicated through the unique `{ UserId, DedupeKey }` index configured in `WebApp/Data/AppDbContext.cs`.
- Generated book context is persisted to `Book.Context` and returned through both MVC partials and the `api/books/{bookId}/context` API.
- Chat reset removes both `agentsession:{userId}` and `agentcontext:{userId}` cache keys, as covered by `WebApp.Tests/Controllers/ChatControllerTests.cs`.
- The containerized test target runs `dotnet test WebApp.Tests/WebApp.Tests.csproj` through `docker-compose.test.yml`.

## Out of Scope

- Cloud-hosted LLM providers are not configured; the checked-in implementation uses Ollama through `OllamaSharp` and `Microsoft.Extensions.AI`.
- Non-Kindle import formats are not implemented; `NotesController` only permits `.txt` files and returns "Only Kindle clippings .txt files are supported in v1."
- Public or shared libraries are not implemented; `Book`, `BookNote`, profile, chat session, and context paths are scoped by authenticated user id.
- Production deployment manifests are not present; infrastructure is Docker Compose for local development and tests.
- Unsplash is optional visual enhancement, not a required application dependency.

## Evidence

- [../README.md](../README.md)
- [../WebApp/Program.cs](../WebApp/Program.cs)
- [../WebApp/Services/KindleClippingsImportService.cs](../WebApp/Services/KindleClippingsImportService.cs)
- [../WebApp/Services/BookContextService.cs](../WebApp/Services/BookContextService.cs)
- [../WebApp/Controllers/ChatController.cs](../WebApp/Controllers/ChatController.cs)
- [../WebApp.Tests/Controllers/ChatControllerTests.cs](../WebApp.Tests/Controllers/ChatControllerTests.cs)
