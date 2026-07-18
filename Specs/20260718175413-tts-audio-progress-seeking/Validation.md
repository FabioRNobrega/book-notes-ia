# Validation: TTS Audio Progress Seeking

## Table of Contents

- [Validation: TTS Audio Progress Seeking](#validation-tts-audio-progress-seeking)
  - [Table of Contents](#table-of-contents)
  - [Acceptance Criteria](#acceptance-criteria)
  - [Test Cases](#test-cases)
  - [Manual Verification](#manual-verification)
  - [Definition of Done](#definition-of-done)
  - [Rollback Plan](#rollback-plan)

## Acceptance Criteria

| Requirement | Acceptance Criterion |
| --- | --- |
| FR1 | Assistant messages with `MessageId` render a play/pause button, progress control, and timestamp area in both initial chat history and HTMX-added bot messages. |
| FR2 | Before audio is fetched or metadata is available, the progress control is visible but disabled and the loading state does not cause layout shift. |
| FR3 | Loaded audio shows elapsed and total duration in a readable timestamp format. |
| FR4 | During playback, the progress value and elapsed timestamp move forward in sync with the audio. |
| FR5 | Clicking the progress track and dragging the thumb both seek playback to the selected position. |
| FR6 | Switching away from a partially played message and returning to it during the same page load resumes from the remembered position. |
| FR7 | When audio reaches the end, the widget returns to play state and its remembered position resets to the beginning. |
| FR8 | Fetch, playback, or decode errors still show `Audio unavailable` and leave the seek control non-interactive. |
| FR9 | The implementation exposes reusable widget-oriented helpers or a shared partial so equivalent audio widgets do not duplicate full playback logic. |
| FR10 | Existing controller/service tests for `GetMessageAudio` and `ChatMessageAudioService` continue to pass without server-side behavior changes. |

## Test Cases

**Unit tests:**

- `WebApp.Tests/Controllers/ChatControllerTests.cs` - existing `GetMessageAudio` tests should continue to pass, proving endpoint behavior did not regress.
- `WebApp.Tests/Services/ChatMessageAudioServiceTests.cs` - existing cache, synthesis, language, and voice tests should continue to pass.
- ⚠️ TODO: If a JavaScript test harness is introduced later, cover duration formatting, seek percentage conversion, state transitions, and per-message position memory.

**Integration tests:**

- Existing Docker-backed tests are sufficient for server-side audio retrieval because this feature should not change the endpoint, database model, or TTS service contract.
- ⚠️ TODO: Add browser automation only if the project adopts Playwright or another frontend test runner; cover click-to-seek, drag-to-seek, pause/resume, and switching between two message widgets.

## Manual Verification

1. Start the app with `make docker-run` on Linux/SteamOS, or the OS-specific Make target documented in `AGENTS.md`.
2. Sign in and open the home chat screen.
3. Send a message that produces an assistant response with a `MessageId`.
4. Verify the TTS widget displays a play button, disabled progress bar, and timestamp area before audio is loaded.
5. Click play and verify the loading state appears while audio is generated or fetched.
6. Once playback begins, verify the play icon becomes pause, the progress bar advances, and elapsed/total timestamps are visible.
7. Click midway through the progress track and verify playback jumps to that time.
8. Drag the progress thumb backward and verify playback rewinds.
9. Pause the message, play a second assistant message, then return to the first and verify it resumes near the previous position.
10. Let audio finish and verify the first message returns to the play state and starts from the beginning when played again.
11. Reload the page and verify playback positions are not persisted.
12. Run `make test` and confirm the existing WebApp and TTS service test suites pass.

## Definition of Done

- Requirements, Plan, and Validation docs are updated in this spec folder.
- All existing tests still pass.
- New behavior has test coverage matching the pattern in `AGENTS.md`, with manual frontend verification documented where no JS test harness exists.
- Razor views/partials and Sass source are updated consistently.
- Responsive, loading, error, keyboard, click, drag, and accessibility states are covered by the implementation and manual verification.
- No generated CSS, `.env`, `bin/`, or `obj/` content is committed.

## Rollback Plan

- Revert the TTS player markup changes in `WebApp/Views/Chat/Chat.cshtml` and `WebApp/Views/Chat/_BotMessage.cshtml` or the optional `_TtsAudioPlayer.cshtml` partial.
- Revert the TTS playback changes in `WebApp/wwwroot/js/site.js` to restore the existing play/pause-only behavior.
- Revert any Sass import/component file added under `WebApp/Styles`.
- No database migration, environment variable, Microsoft Agent Framework, TTS service, or Docker change should be required to roll back this feature.
