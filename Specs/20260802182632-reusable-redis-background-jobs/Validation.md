# Validation: Reusable Redis-Backed Background Jobs for Kindle Import

## Table of Contents

- [Acceptance Criteria](#acceptance-criteria)
- [Test Cases](#test-cases)
- [Manual Verification](#manual-verification)
- [Definition of Done](#definition-of-done)
- [Rollback Plan](#rollback-plan)

## Acceptance Criteria

| Requirement | Acceptance Criterion |
| --- | --- |
| FR1–FR3 | A generic queue and hosted worker dispatch a Kindle job through a dedicated handler, and a worker test proves the generic layer has no Kindle parsing dependency. |
| FR4 | Both Notes upload endpoints return a job ID promptly and do not call the long-running import operation directly inside the request. |
| FR5 | A second upload for the same user receives a conflict response while the first job is queued or running; a different user is not blocked. |
| FR6 | Existing Kindle parsing, deduplication, user isolation, EF transaction, retry strategy, and embedding tests continue to pass. |
| FR7–FR8 | Redis progress keys contain `userId`, optional `sessionId`, and `jobId`; progress contains status, stage, current/total books when known, terminal summary/error data, and a one-day TTL. |
| FR9 | An authenticated user can poll their own job; another user's job ID returns not found or forbidden without progress data. |
| FR10 | Cancelling an active job signals the worker, rolls back all database changes from that import, produces a cancelled terminal state, and releases the user's active-job marker. |
| FR11–FR12 | The upload page and chat flow both show stage/book-count progress, stop polling at a terminal state, and show the correct success/error/cancel result. |
| FR13 | Chat message submission, attachment, and relevant mode controls are disabled while an import is active and restored afterward. |
| FR14 | Queue, worker, handler, progress store, and cancellation dependencies are constructor-injected and replaceable by test doubles. |
| FR15 | `make test` completes successfully in the Docker test environment, and the relevant local Compose workflow is documented or manually verified. |

## Test Cases

**Unit tests:**

- `WebApp.Tests/Jobs/BackgroundJobWorkerTests.cs`: dispatches a registered handler, records handler failures, stops on cancellation, and releases execution state.
- `WebApp.Tests/Jobs/BackgroundJobQueueTests.cs`: enqueues/dequeues envelopes and rejects or handles a bounded queue correctly.
- `WebApp.Tests/Services/RedisImportProgressStoreTests.cs`: verifies user/job key isolation, active-job claim/release, JSON round-trip, TTL configuration, and terminal state storage using the repository's test strategy.
- `WebApp.Tests/Services/KindleClippingsImportServiceTests.cs`: verifies progress stage callbacks, total/completed book reporting, cancellation propagation, existing deduplication, and that cancellation rolls back books, notes, and embeddings without partial persistence.
- `WebApp.Tests/Controllers/NotesControllerTests.cs`: verifies page/chat enqueue responses, invalid-file handling, same-user conflict, progress ownership checks, cancellation authorization, and terminal response behavior.
- A focused client-side test or browser verification should cover polling stop behavior, control disabling, cancellation, and both page/chat render targets. ⚠️ TODO if no JavaScript test harness exists in the repository.

**Integration tests:**

- Run the existing PostgreSQL/pgvector import tests to prove background execution still calls the current transaction-safe import workflow.
- Run a Compose-backed Redis test or manual integration check proving progress survives between the web request and worker and is readable through the authenticated endpoint. ⚠️ TODO if the current test environment does not expose Redis to xUnit.
- Verify the full page and chat flows against the running Docker stack with a representative `My Clippings.txt` containing multiple books and enough notes to exercise embedding progress.

## Manual Verification

1. Start the local stack with `make docker-run` on Linux/SteamOS, `make docker-run-mac` on macOS Apple Silicon, or `make docker-run-windows` on Windows with Docker Desktop/NVIDIA.
2. Sign in and open the Upload Notes mode.
3. Upload a valid multi-book `My Clippings.txt` file.
4. Confirm the request returns the progress UI quickly and shows parsing/saving/embedding stages with book counts.
5. Confirm the chat input, send button, attachment control, and mode controls are disabled during the active job.
6. Confirm progress advances while embeddings run and ends with the existing import summary.
7. Repeat through the chat attachment flow and confirm the progress appears as an agent-style bubble with the same behavior.
8. Start an import and attempt a second upload for the same user; confirm the conflict message.
9. Start another import, click cancel, and confirm the UI reports cancellation, restores chat controls, and does not show the cancelled import's incomplete books or notes in the library.
10. Restart the webapp container during an import and confirm the job does not resume, matching the local MVP requirement.
11. Run `make test` and confirm all existing and new tests pass in Docker.

## Definition of Done

- Requirements, Plan, and Validation documents are complete in this spec folder.
- Generic queue/worker infrastructure and Kindle handler have focused interfaces and tests.
- Redis progress is user-scoped, TTL-bound, and tested.
- Both upload paths provide progress, terminal states, and cancellation.
- Chat is blocked only while the current user's import is active.
- Existing import behavior and all Docker-based tests remain green.
- No database migration, Kubernetes resource, or new runtime package is introduced.

## Rollback Plan

- Revert the job registration and controller/view/JavaScript changes to restore synchronous import behavior temporarily.
- Keep `KindleClippingsImportService`'s transaction/retry correction intact; it is independent of the job system.
- Remove only the new Redis job keys through the existing Redis cleanup workflow if stale progress records remain; no database rollback is required.
