# Validation: Copy Agent Response to Clipboard

## Table of Contents

- [Acceptance Criteria](#acceptance-criteria)
- [Test Cases](#test-cases)
- [Manual Verification](#manual-verification)
- [Definition of Done](#definition-of-done)
- [Rollback Plan](#rollback-plan)

## Acceptance Criteria

| Requirement | Acceptance Criterion |
| --- | --- |
| FR1 | Both `WebApp/Views/Chat/_BotMessage.cshtml` and the assistant branch of `WebApp/Views/Chat/Chat.cshtml` render a `copy-response-btn` `sl-icon-button` wrapped in an `sl-tooltip` with content `"Copy to clipboard"` in the message footer row, next to the `_TtsAudioPlayer.cshtml` controls, with no `hx-*`/`_` attributes. |
| FR2 | Clicking the copy button triggers the delegated `.copy-response-btn` click listener in `WebApp/wwwroot/js/site.js`, which calls `navigator.clipboard.writeText()` with the message's plain text. |
| FR3 | Copying a message when two or more assistant messages are rendered on the same page copies only the clicked message's own text, verified by clicking the copy button on an older message in `Chat.cshtml` history and confirming the clipboard contents match that message, not the most recent one. |
| FR4 | After the clipboard write settles, `handleCopyClick` sends `fetch(POST /chat/copy-notification)` with `success` in the form body; `ChatController.CopyNotification(bool success)` returns the success or danger `_Alert` partial accordingly. |
| FR5 | A rejected or thrown clipboard write always results in `success=false` being sent, never a silent no-op or an incorrect success alert. |
| FR6 | The returned `_Alert` HTML is appended into `#alert` via `insertAdjacentHTML("beforeend", ...)`, and the alert is visible immediately without requiring any additional script processing pass. |
| FR7 | `GET`/anonymous requests to `/chat/copy-notification` are rejected consistently with `[Authorize]` already applied to `ChatController`; the action accepts only the boolean `success` field and no message content. |
| FR8 | The new `sl-icon-button` uses the same `text-white/40` and `--sl-color-primary-*` style overrides as the existing TTS play/download buttons in `WebApp/Views/Chat/_TtsAudioPlayer.cshtml`, confirmed by visual inspection side-by-side in the running app. |

## Test Cases

**Unit tests:**

- `WebApp.Tests/Controllers/ChatControllerTests.cs`: `CopyNotification_WhenSuccessful_ReturnsSuccessAlert` — call `controller.CopyNotification(true)`, assert `Assert.IsType<PartialViewResult>(result)`, assert `partial.ViewName == "~/Views/Shared/Components/_Alert.cshtml"`, and assert the model tuple equals `(true, "Copied to clipboard")`, following the assertion style already used in `NotesControllerTests.cs` (e.g. `Library_ReturnsBooksOrderedByTitle`).
- `WebApp.Tests/Controllers/ChatControllerTests.cs`: `CopyNotification_WhenUnsuccessful_ReturnsDangerAlert` — same shape, calling `controller.CopyNotification(false)` and asserting `(false, "Could not copy to clipboard")`.
- ⚠️ TODO: If the test host does not already cover `[Authorize]` redirect behavior for `ChatController` generically, add (or confirm an existing) authorization test so the new route inherits that coverage automatically rather than needing a bespoke auth test for this one action.

**Integration tests:**

- No PostgreSQL/pgvector/Redis/Ollama-backed integration test is needed — the new action touches no database, cache, or Microsoft Agent Framework state. `WebApp.Tests/Integration/AgentToolsPostgresTests.cs` is unaffected.
- ⚠️ TODO: There is no existing browser-driven (e.g. Playwright/Selenium) test suite in this repo for `site.js` click-through behavior; the clipboard write, `.prose` scoping, and success/failure alert rendering are validated manually per the steps below rather than by an automated end-to-end test.

## Manual Verification

1. Start the stack: `make docker-run` (Linux/SteamOS).
2. Sign in, open the Literary Chat, and send a message that produces a multi-paragraph assistant response.
3. Confirm the copy icon appears in the response's footer row, alongside the TTS play/download controls, and hovering it shows the "Copy to clipboard" tooltip.
4. Click the copy icon; confirm a success notice ("Copied to clipboard") appears in the top-right `#alert` area and shows itself immediately (no delayed/missing render from the `open`-attribute change), then paste (e.g. into a text editor) and confirm the pasted text matches the visible response with formatting stripped (no literal `**`/`#`/HTML tags).
5. Send a second message so two assistant responses are visible; click the copy icon on the first (older) response and confirm the pasted text matches the first response, not the second.
6. Reload the page so both messages render from `Chat.cshtml` history (not the live `_BotMessage.cshtml` OOB swap); repeat step 5 to confirm history-rendered messages behave identically.
7. Simulate a clipboard failure (e.g. in browser devtools, temporarily override `navigator.clipboard.writeText` to return a rejected promise, or test in a deliberately insecure/non-`localhost` context if available) and confirm the danger notice ("Could not copy to clipboard") appears instead of a false success.
8. Click "Clear history" (`/chat/reset`) and save the profile form (`UserProfile/Upsert`) to confirm both still show their alerts correctly after `_Alert.cshtml`'s `me.show()` → `open` attribute change.
9. Confirm no browser console errors appear during any of the above steps.

## Definition of Done

- Requirements, Plan, and Validation docs in this spec folder reflect the implemented behavior.
- All existing tests still pass: `make test`.
- Both `CopyNotification` result paths are covered in `WebApp.Tests/Controllers/ChatControllerTests.cs` and pass.
- `_BotMessage.cshtml` and `Chat.cshtml` render the copy control consistently, with the manual verification steps above completed against the running app (screenshots or a short description of what was observed).
- `_Alert.cshtml` shows itself via the declarative `open` attribute with no Hyperscript; the reset and profile-save alerts (both still HTMX-driven) are confirmed to still work in step 8 above.
- `notice.sass` and the `#alert` container remain unmodified.

## Rollback Plan

- Revert the changes to `WebApp/Views/Chat/_BotMessage.cshtml` and `WebApp/Views/Chat/Chat.cshtml` to remove the copy button and `agent-message-bubble` class.
- Revert the `getMessageBubble`/`handleCopyClick`/`.copy-response-btn` listener additions in `WebApp/wwwroot/js/site.js`.
- Remove the `CopyNotification` action from `WebApp/Controllers/ChatController.cs` (and the corresponding tests from `ChatControllerTests.cs`).
- Revert `_Alert.cshtml`'s `open` attribute back to `_="on load call me.show()"` if any regression is found in the existing HTMX-driven alert call sites.
- No database migration, cache key, environment variable, or feature flag is introduced, so rollback is a pure code revert with no data or infrastructure cleanup required.
