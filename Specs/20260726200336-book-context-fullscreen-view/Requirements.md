# Requirements: Book Context Fullscreen View

## Table of Contents

- [Requirements: Book Context Fullscreen View](#requirements-book-context-fullscreen-view)
  - [Problem Statement](#problem-statement)
  - [User Stories](#user-stories)
  - [Functional Requirements](#functional-requirements)
  - [Non-Functional Requirements](#non-functional-requirements)
  - [Out of Scope](#out-of-scope)
  - [Open Questions](#open-questions)

## Problem Statement

`WebApp/Views/Notes/_BookContext.cshtml` used to render generated book context inline, inside a `.book-context-scroll` div capped at `max-height: 100px` with `overflow-y: auto`, next to an `<sl-details summary="Book Context">`. Longer AI-generated context was only readable through a cramped 100px scrollbox, with no way to read the full context in a larger, distraction-free view. The codebase already had a paper-stack visual style (`WebApp/Styles/Components/paper_style.sass`, used full-page in `_BookDetails.cshtml`) to draw on for an immersive reading surface, but no `<sl-dialog>` usage existed anywhere in the app yet, and `BookContextViewModel` carried only `BookId` and `Context` — no title/author.

**As implemented**, the fix goes further than adding a fullscreen *option* alongside the inline preview: the inline `sl-details`/scrollbox preview is removed entirely and replaced by a single "See Book Context" trigger button that opens a paper-stack styled `sl-dialog` containing the full context, a title/author header, and the Regenerate/info controls that previously lived inline.

## User Stories

- Given a book with generated context, when the reader views the Notes detail header, then they see a "See Book Context" button (with a `book` icon) next to "Back to Library" instead of an inline context preview.
- Given the reader clicks "See Book Context", then a dialog opens with a custom header showing the book's title and author plus Regenerate/info controls, and a body showing the full, unclipped book context rendered as markdown inside a paper-stack styled surface.
- Given the dialog is open, when the reader clicks the close (×) button, presses Escape, or clicks the overlay, then the dialog closes and returns focus to the page without altering `Book.Context`.
- Given the dialog is open, when the reader reads a long context, then the paper-stack panel scrolls internally (`.book-context-modal-body`) so the dialog never exceeds the viewport.
- Given the reader clicks "Regenerate context with AI" from inside the dialog, then the whole `_BookContext.cshtml` partial is swapped via HTMX and the dialog closes (since the freshly rendered dialog is not marked `open`); the reader clicks "See Book Context" again to view the regenerated text.

## Functional Requirements

1. FR1 — In the non-empty-context branch of `_BookContext.cshtml`, replace the inline `sl-details`/scrollbox preview with an `sl-button` (class `book-context-fullscreen-btn`, icon `book`, text "See Book Context", `data-dialog-id="book-context-dialog-@Model.BookId"`) that opens the paired dialog.
2. FR2 — Render an `sl-dialog` (id `book-context-dialog-@Model.BookId`, class `book-context-dialog`, `label="@Model.Title — @Model.Author"` for assistive tech, and the `no-header` attribute to suppress Shoelace's default header/close button in favor of a custom one) whose body renders the Markdig HTML for `Model.Context`, with no `max-height` clamp.
3. FR3 — Inside the dialog, render a custom `<header class="book-context-modal-header">` containing an `<h2>` with `Model.Title` and a `<p>` with `Model.Author`, sourced from new `Title`/`Author` properties on `BookContextViewModel`.
4. FR4 — `BookContextViewModel` (`WebApp/Models/BookContextViewModel.cs`) gains `Title` and `Author` string properties. Every place that constructs a `BookContextViewModel` populates them:
   - `WebApp/Views/Notes/_BookDetails.cshtml` — passes `Model.Title` and `Model.Author` (already present on `BookDetailsViewModel`) when partial-rendering `_BookContext.cshtml`.
   - `NotesController.GenerateContext` (`WebApp/Controllers/NotesController.cs`) — on both the success path and the generic-`Exception` catch path (not the `KeyNotFoundException` path, which returns `NotFound()` directly with no view model), projects `Title`/`Author` from `_db.Books` (`AsNoTracking`, filtered by `Id == id` and `UserId == userId`, matching the read pattern already used elsewhere in the controller) with `?? string.Empty` fallbacks if the book row is somehow missing.
5. FR5 — The dialog's paper visual uses a new `.paper-style--modal` modifier in `WebApp/Styles/Components/paper_style.sass` (not the raw `.paper-style` class, hardcoded to `height: 100vh` for full-page layout), sized to `height: min(70vh, 640px)` with `inset: 0` pseudo-element "stacked pages" and the existing `sunrise*` keyframes; the `sl-dialog` host itself is restyled via `::part(panel)`/`::part(body)` (transparent background, no default box-shadow, `overflow: visible`) so Shoelace's own panel chrome doesn't visually stack with the paper-stack surface.
6. FR6 — The Regenerate icon-button (`arrow-counterclockwise`, wrapped in its existing `sl-tooltip`) and the "may contain inaccuracies" info icon move into the dialog's custom header — they are no longer duplicated inline because there is no more inline preview. A `<sl-spinner id="context-spinner-@Model.BookId">` used as the `hx-indicator` target for Regenerate also lives in the dialog header.
7. FR7 — Opening the dialog is pure client-side: `site.js` adds a delegated `click` listener on `.book-context-fullscreen-btn` that reads `data-dialog-id` and sets `dialog.open = true`. Because `no-header` removes Shoelace's built-in close button, a second delegated `click` listener on `.book-context-dialog-close-btn` (an `sl-icon-button` with the `x-lg` icon inside the dialog header) sets `dialog.open = false`. Escape-key and overlay-click dismissal are left to `sl-dialog`'s native light-dismiss behavior (not overridden).
8. FR8 — The trigger button and dialog only render when `Model.Context` is non-empty (the existing `else` branch of `_BookContext.cshtml`); the "Generate Context" empty-state branch is unchanged aside from a minor layout tweak (spinner now precedes the button).
9. FR9 — The dialog body renders the Markdig HTML from a single hoisted `contextHtml` local variable (`Markdown.ToHtml(Model.Context!, new MarkdownPipelineBuilder().UseAdvancedExtensions().Build())`), computed once per partial render — there is no second render site to keep in sync since the inline scrollbox was removed.

## Non-Functional Requirements

- Accessibility: the dialog carries a `label` attribute for assistive tech even though its visual header is custom (`no-header`); the close `sl-icon-button` sets `label="Close book context"`; `sl-dialog` provides its own focus trap and Escape-to-close, which is not suppressed by the custom header.
- Responsive: a `@media (max-width: 640px)` block in `paper_style.sass` narrows `.book-context-dialog::part(panel)` to `calc(100vw - 2rem)`, shortens `.paper-style--modal` to `height: 65vh`, and tightens header/body padding so the dialog stays usable on narrow viewports without horizontal overflow.
- No new runtime dependency: Shoelace's `sl-dialog`, `sl-icon` (`book`, `x-lg`, `arrow-counterclockwise`, `question-circle`), and `no-header` are already available through the existing Shoelace autoloader `<script>` in `WebApp/Views/Shared/_Layout.cshtml`; no new package or CDN entry is introduced.
- Styling source of truth: all new styling lives in `WebApp/Styles/Components/paper_style.sass`, compiled by `AspNetCore.SassCompiler` into gitignored `wwwroot/css` output — never hand-edited there.
- Data isolation: the `_db.Books` lookup added to `NotesController.GenerateContext` for FR4 filters by both `Id` and the authenticated `UserId` (`ClaimTypes.NameIdentifier`), consistent with every other user-owned-data query in the controller.
- No persistence change beyond the view model: `IBookContextService`, `Book.Context`, and the `api/books/{bookId}/context` API are untouched — this feature only reads existing `Title`/`Author` columns already on `Book`.

## Out of Scope

- Editing context from within the fullscreen dialog (Regenerate is present in the dialog per FR6, but it works exactly as before — no new editing capability).
- Any change to context generation, persistence, or the `api/books/{bookId}/context` API.
- A generic/reusable "fullscreen dialog" component for other partials — this spec only covers the Book Context panel.
- Print or export of the fullscreen context view.
- Preserving the dialog's open state across an HTMX-triggered Regenerate swap (regenerating from inside the dialog currently closes it — see the last User Story — and re-opening it is a manual click; keeping it open across the swap is not implemented).

## Open Questions

None — the implementation reflects the final, tested behavior described above.
