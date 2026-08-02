# Requirements: Reusable Redis-Backed Background Jobs for Kindle Import

## Table of Contents

- [Problem Statement](#problem-statement)
- [User Stories](#user-stories)
- [Functional Requirements](#functional-requirements)
- [Non-Functional Requirements](#non-functional-requirements)
- [Out of Scope](#out-of-scope)
- [Decisions](#decisions)

## Problem Statement

Kindle imports currently run inside the request handled by `NotesController`, through the same `IKindleClippingsImportService` used by the page and chat upload flows. Embedding books and notes is the longest part of the operation, so the user receives no progress feedback and cannot distinguish an active import from a stalled request. The application already has Redis-backed caching through `ICacheHandler`, and the local Docker Compose MVP does not need jobs to survive an application restart. The import workflow needs a reusable in-process job queue and worker, Redis progress state, cancellation, and a shared progress experience for both upload entry points.

## User Stories

- Given an authenticated user uploads a valid Kindle clippings file from the upload screen, when the upload is accepted, then the screen shows a progress bar with the current stage and book count while the background job runs.
- Given an authenticated user attaches a Kindle file from chat, when the import starts, then a progress message appears as an agent-style chat bubble and the user cannot submit chat questions until the import finishes, fails, or is cancelled.
- Given an import is embedding several books, when each book completes, then Redis-backed progress reports the completed and total book counts and the current stage.
- Given a user cancels an active import, when cancellation is observed by the worker, then the job reports a cancelled state, the UI stops polling, and the user can use chat again.
- Given another processor is added later, when it defines a job payload and handler, then it can reuse the queue and worker without Kindle-specific code in the generic job infrastructure.

## Functional Requirements

1. FR1 — The application shall expose a reusable background job queue and worker that accept job envelopes independently of Kindle import details.
2. FR2 — The queue shall execute jobs in-process and may lose queued or active jobs when the webapp container restarts; no job database table or Kubernetes Job shall be introduced.
3. FR3 — The worker shall resolve a dedicated handler for each supported job type, beginning with a Kindle import handler, and shall keep processor-specific behavior outside the generic worker.
4. FR4 — The Kindle upload endpoints shall validate the file and enqueue an import job instead of performing parsing, persistence, or embedding synchronously in the HTTP request.
5. FR5 — The system shall allow at most one active or queued Kindle import per authenticated user and shall return a clear conflict response when another import is already active.
6. FR6 — The Kindle import job shall preserve the existing user-scoped parsing, deduplication, EF Core transaction, retry strategy, and embedding behavior of `KindleClippingsImportService`.
7. FR7 — Import progress shall be stored through the existing Redis-backed cache abstraction using keys containing the authenticated `userId`, the optional chat `sessionId` when the import originates from chat, and the `jobId`; the record shall include status, stage, completed books, total books when known, notes/book summary values when available, error information, and timestamps as appropriate.
8. FR8 — The import workflow shall publish meaningful stages, including parsing/reading highlights, saving notes, embedding books, and completed, failed, or cancelled states. During embedding it shall report completed books and total books rather than presenting only an estimated percentage.
9. FR9 — The application shall provide an authenticated, user-scoped progress endpoint that returns only the requesting user's job state and supports terminal-state polling.
10. FR10 — The application shall provide a cancellation endpoint for the user's active import; cancellation shall signal the in-process worker through a cancellation mechanism, and the import transaction shall roll back all database insertions and updates from that job so an incomplete import is not persisted.
11. FR11 — The upload page shall render a Shoelace progress bar and stage/book-count text, poll the progress endpoint at approximately 500–1000 ms intervals, and show loading, completed, failed, cancelled, and conflict states.
12. FR12 — The chat upload flow shall render the same progress information inside an agent-style response bubble, poll the same progress endpoint, and replace the progress message with the final import summary or error.
13. FR13 — While a user's import is active, the chat message input, send control, file attachment control, and relevant mode controls shall be disabled; they shall be re-enabled for completed, failed, cancelled, or conflict states.
14. FR14 — The generic queue/worker and progress abstractions shall be registered in `Program.cs` with constructor-injected interfaces and shall be testable without requiring Redis for unit tests.
15. FR15 — The implementation shall document the Docker-first local workflow and verify the behavior with the existing containerized test command.

## Non-Functional Requirements

- All job and progress reads/writes must remain scoped to the authenticated `UserId`; a job ID alone must not grant access to another user's state.
- Generic job infrastructure must follow SOLID boundaries: controllers coordinate HTTP, the queue manages dispatch, the worker manages lifecycle, handlers own processor workflows, and the progress store owns Redis serialization.
- Redis progress records must have a one-day TTL so the final result remains available for review, while the active-job marker is removed immediately when the job reaches a terminal state. Normal active progress is expected to finish within a few minutes; stale active markers must not block the user indefinitely.
- Cancellation must be cooperative, explicitly requested by the user, and must roll back the complete Kindle import transaction. A client disconnect alone must not cancel or roll back the job.
- The design must not add a frontend package, a new message broker, a database migration, or Kubernetes infrastructure.
- Existing page and chat import behavior, deduplication, EF retry handling, and Microsoft Agent Framework behavior must remain compatible.

## Out of Scope

- Durable jobs that resume after a webapp/container restart.
- Multiple simultaneous imports for one user.
- Kubernetes Jobs, Hangfire, Quartz, RabbitMQ, or another external job broker.
- Cross-container worker scaling or distributed job locking beyond the single local webapp process.
- Cancellation of individual embedding operations beyond cooperative cancellation through the existing service calls.
- A general-purpose job administration dashboard.

## Decisions

- Redis job state is scoped by `userId`, optional `sessionId`, and `jobId`; the exact prefix can follow existing cache naming conventions during implementation.
