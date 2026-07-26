# Requirements: Copy Agent Response to Clipboard

## Table of Contents

- [Problem Statement](#problem-statement)
- [User Stories](#user-stories)
- [Functional Requirements](#functional-requirements)
- [Non-Functional Requirements](#non-functional-requirements)
- [Out of Scope](#out-of-scope)
- [Open Questions](#open-questions)

## Problem Statement

Assistant responses in the Literary Chat (`WebApp/Views/Chat/Chat.cshtml` for history, `WebApp/Views/Chat/_BotMessage.cshtml` for the live HTMX out-of-band swap) render Markdown-to-HTML content inside a `.prose` block, with a footer row that already hosts the `_TtsAudioPlayer.cshtml` play/download controls, the agent label, and the response time. There is no way for a user to copy an assistant's answer to the clipboard — they must manually select the rendered text, which drags formatting artifacts and is error-prone on long, multi-paragraph responses. The app already has a reusable notice component, `WebApp/Views/Shared/Components/_Alert.cshtml`, and a standard ASP.NET Core MVC action-returns-partial pattern used elsewhere for client-driven updates (e.g. `ChatController`'s `/chat/agent` endpoint, called from plain `fetch()` in `persistAgentSelection` in `WebApp/wwwroot/js/site.js`, which fetches rendered HTML and swaps it into the DOM manually). This feature follows that existing plain-JS-plus-MVC-partial convention instead of HTMX/Hyperscript, keeping the implementation to one small controller action and one click handler in `site.js`.

## User Stories

- Given an assistant response is visible in the chat, when I click the copy icon in its footer row, then the response's plain text is written to my clipboard and a success notice appears confirming the copy.
- Given I hover over the copy icon before clicking it, when the tooltip appears, then it tells me what the button does (e.g. "Copy to clipboard").
- Given the browser denies or fails the clipboard write (e.g. insecure context, revoked permission), when I click the copy icon, then I see a danger-variant notice telling me the copy failed, instead of a false success message.
- Given I am viewing chat history after a page reload, when I click the copy icon on an older assistant message, then it copies that message's text exactly like it would for a just-generated response.

## Functional Requirements

1. **FR1** — Every assistant/bot message footer row renders a copy control: the markup next to `_TtsAudioPlayer.cshtml` in both `WebApp/Views/Chat/_BotMessage.cshtml` and the assistant branch of the `@foreach` loop in `WebApp/Views/Chat/Chat.cshtml` gains a `sl-icon-button` using the Shoelace `copy` icon (consistent with the existing `play-circle`/`download` icons), with class `copy-response-btn`, wrapped in an `sl-tooltip` with content `"Copy to clipboard"`. No `hx-*` or Hyperscript `_` attributes are added to this button.
2. **FR2** — A single delegated click listener in `WebApp/wwwroot/js/site.js` (following the existing `document.body.addEventListener("click", ...)` pattern used for `.tts-play-btn`/`.tts-download-btn`) handles `.copy-response-btn` clicks and copies the plain, human-readable text of that specific message to the clipboard via `navigator.clipboard.writeText()`.
3. **FR3** — The plain text must be resolved by reading the `innerText` (not `innerHTML`/`textContent`) of that message's own `.prose` element, scoped by walking up from the clicked button to its containing message bubble so a click always copies the text of the message it belongs to, never a different message when several are rendered on the page at once.
4. **FR4** — After the clipboard write settles (success or failure), the click handler calls a standard MVC controller action with a plain `fetch()` POST, following the same request/response shape as `persistAgentSelection` in `site.js` (`fetch` with a `URLSearchParams` body, `await response.text()`, then insert the returned HTML into the page). The action is a single `ChatController` method, `CopyNotification(bool success)`, that returns `PartialView("~/Views/Shared/Components/_Alert.cshtml", (true, "Copied to clipboard"))` when `success` is `true` and `(false, "Could not copy to clipboard")` when `false`.
5. **FR5** — The click handler determines the `success` value itself from the `navigator.clipboard.writeText()` outcome (resolved vs. rejected, including a missing `navigator.clipboard`) via a `try`/`catch`, and always calls `CopyNotification` exactly once per click with that value.
6. **FR6** — The returned `_Alert` HTML is inserted into the `#alert` container (`WebApp/Views/Shared/_Layout.cshtml`) with `insertAdjacentHTML("beforeend", html)`, matching the existing append behavior of the HTMX-driven alerts elsewhere in the app (`hx-swap="beforeend"` on `/chat/reset` and `UserProfile/Upsert`).
7. **FR7** — `CopyNotification` accepts only the boolean clipboard result and no message content; it is a stateless notice renderer reachable only by an authenticated user, consistent with `[Authorize]` already applied to `ChatController`.
8. **FR8** — The copy control is visually consistent with the existing footer row: same icon sizing/opacity convention as the TTS play/download `sl-icon-button` elements in `WebApp/Views/Chat/_TtsAudioPlayer.cshtml` (e.g. `text-white/40`, `font-size` in the same range), so it does not visually dominate the row.

## Non-Functional Requirements

- **No new runtime dependencies**: implemented with plain JavaScript already loaded via `WebApp/wwwroot/js/site.js` and Shoelace (`sl-icon-button`, `sl-tooltip`), already loaded in `_Layout.cshtml`; no new npm/NuGet packages, no HTMX or Hyperscript attributes for this feature.
- **No inline scripting**: all client behavior lives in the existing `site.js` file as a named function plus one delegated event listener, matching how `.tts-play-btn`/`.tts-download-btn` and `persistAgentSelection` are already implemented — no `onclick=` attributes or inline `<script>` blocks in the Razor views.
- **Single Responsibility**: `CopyNotification` only renders the existing `_Alert` partial for the supplied boolean; it must not take on session/message lookup, persistence, or any responsibility beyond notice rendering, per the SOLID guide in `Specs/TechStak.md`.
- **Consistency**: reuses the exact `(bool ok, string message)` model contract of `_Alert.cshtml`. Because the alert HTML is now inserted with plain `insertAdjacentHTML` rather than through HTMX's swap pipeline (which calls `htmx.process()`/triggers Hyperscript's `on load`), `_Alert.cshtml` shows itself via Shoelace's declarative `open` attribute instead of a `_="on load call me.show()"` Hyperscript handler, so it does not depend on any script processing pass over the inserted node.
- **Accessibility**: the `sl-icon-button` must carry a `label` attribute (e.g. `label="Copy to clipboard"`) in addition to the `sl-tooltip`, matching the existing TTS controls' accessibility pattern.

## Out of Scope

- Copying anything other than the assistant's own response text (e.g. copying the user's own message bubble, or copying the full conversation transcript).
- Adding a "copied" inline state change on the icon itself (e.g. icon morphing to a checkmark); the confirmation is solely the `_Alert` notice per the reused component.
- Changing `notice.sass` or the `#alert` container's position/behavior.
- Removing or altering HTMX/Hyperscript usage anywhere else in the app (`/chat/reset`, `UserProfile/Upsert`, etc.) — this spec only avoids introducing new HTMX/Hyperscript wiring for the copy feature itself.
- Server-side logging, analytics, or auditing of copy actions.
- Copying content for messages rendered by any view other than `WebApp/Views/Chat/Chat.cshtml` and `WebApp/Views/Chat/_BotMessage.cshtml`.

## Open Questions

None — plain JS in `site.js`, a single `CopyNotification(bool success)` MVC action, and the Shoelace `copy` icon were confirmed during discovery.
