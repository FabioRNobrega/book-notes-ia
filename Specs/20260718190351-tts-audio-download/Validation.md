# Validation: TTS Audio Download

## Table of Contents

- [Validation: TTS Audio Download](#validation-tts-audio-download)
  - [Table of Contents](#table-of-contents)
  - [Acceptance Criteria](#acceptance-criteria)
  - [Test Cases](#test-cases)
  - [Manual Verification](#manual-verification)
  - [Definition of Done](#definition-of-done)
  - [Rollback Plan](#rollback-plan)

## Acceptance Criteria

| Requirement | Acceptance Criterion |
| --- | --- |
| FR1 | `_TtsAudioPlayer.cshtml` renders a `.tts-download-btn` `sl-icon-button` inside `.tts-widget`, visible alongside `.tts-play-btn` in both `Chat.cshtml` and `_BotMessage.cshtml` render paths. |
| FR2 | The download button is present for every widget rendered with a non-null `MessageId`/`Guid` model, whether or not that message's audio has been played this session. |
| FR3 | Clicking download on a widget that is `currentWidget` with a live `currentObjectUrl` triggers zero network requests (verified via browser dev tools Network tab showing no new request to `/chat/messages/{id}/audio`); clicking download on any other widget issues exactly one `GET` request to that endpoint. |
| FR4 | The saved file opens in a local media player as valid WAV audio matching what `GET /chat/messages/{messageId}/audio` returns, saved with `audio/wav` as the blob type. |
| FR5 | The browser's save dialog / downloads list shows a filename matching `book-response-{messageId}.wav` with no illegal filename characters. |
| FR6 | Clicking download while a different (or the same) widget is actively playing does not pause, stop, or restart playback — the `Audio` element's `paused`/`currentTime` state is unaffected. |
| FR7 | The download button shows a distinct loading indicator only on itself while its own fetch is pending, and a distinct error indicator only on itself on failure (e.g. simulate by taking down `/chat/messages/{id}/audio`), while `.tts-play-btn`, `.tts-loading`, and `.tts-error` remain in their prior state. |
| FR8 | `git diff` for this feature touches only `WebApp/Views/Chat/_TtsAudioPlayer.cshtml` and `WebApp/wwwroot/js/site.js` — no changes to `ChatController.cs`, `ChatMessageAudioService.cs`, `IAudioStorage.cs`, `ChatMessageAudio.cs`, or any migration. |

## Test Cases

**Unit tests:**
- ⚠️ TODO: This repo has no JS test harness (`WebApp.Tests` is xUnit/.NET only, per `Specs/TechStak.md`); the download handler's filename-sanitization and state-toggle logic should be kept in small, isolated functions (mirroring `formatDuration`/`updateProgressUI`) so they are ready to unit-test if a JS test harness (e.g. Vitest) is introduced later. No existing `WebApp.Tests` file needs to change for this feature since no C#/server code changes.

**Integration tests:**
- ⚠️ TODO (manual, since there is no browser/E2E harness in this repo today): verify the full click → fetch-or-reuse → save flow in a real browser against the Docker Compose stack, covering both the "already played" and "never played" paths from Manual Verification below.

## Manual Verification

Starting from a clean state with `make docker-run` (Linux/SteamOS; substitute `make docker-run-mac` / `make docker-run-windows` as appropriate):

1. Sign in, open a chat session, and send a message that produces an assistant response with TTS available.
2. Without clicking play, click the new download icon on that message. Confirm: a network request fires to `/chat/messages/{id}/audio`, the button shows its own loading indicator briefly, and the browser saves a file named `book-response-{messageId}.wav`.
3. Play the saved file in a local media player and confirm it is audible, valid WAV audio matching the spoken response.
4. On a different message, click play first, let audio start, then click that same message's download button. Confirm in dev tools that no second network request is made (the already-fetched blob is reused) and the file downloads immediately.
5. While message A is actively playing, click download on message B. Confirm message A's playback continues uninterrupted and message B downloads independently.
6. Temporarily block or fail the `/chat/messages/{id}/audio` route (e.g. stop the `webapp` container mid-request, or use browser dev tools request blocking) and click download on a never-played message. Confirm the download button shows its own error indicator, the shared `.tts-error` banner does NOT appear, and the play button still works normally afterward once the endpoint is restored.
7. Confirm keyboard navigation (Tab to the download button, Enter/Space to activate) triggers the same download behavior, and a screen reader announces a label distinct from "Listen to this response" (e.g. "Download audio").

## Definition of Done

- Requirements, Plan, and Validation docs in this spec folder are complete and internally consistent.
- All existing tests still pass (`make test`); no test changes are expected since no C#/server code changes per FR8.
- Manual verification steps above pass against the Docker Compose stack.
- UI: `_TtsAudioPlayer.cshtml` and `site.js` updated consistently; loading, error, and idle states for the new download control are covered and visually distinct from the shared play/pause states; keyboard and screen-reader accessibility confirmed per step 7 above.
- No AI/Microsoft Agent Framework behavior changed; no Docker, migration, or infrastructure changed — confirmed via `git diff` scope check (FR8's acceptance criterion).

## Rollback Plan

- This feature is additive and isolated to two files (`WebApp/Views/Chat/_TtsAudioPlayer.cshtml`, `WebApp/wwwroot/js/site.js`) with no database, config, or Docker changes.
- To roll back: revert the diff to those two files (e.g. `git revert <commit>` for the commit that introduced this feature). No migration rollback, environment variable, or feature flag is needed since the download button is a static addition to markup and client-side JS with no server-side state to unwind.
