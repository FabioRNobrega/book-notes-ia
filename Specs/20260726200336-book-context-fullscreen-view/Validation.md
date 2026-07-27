# Validation: Book Context Fullscreen View

## Table of Contents

- [Validation: Book Context Fullscreen View](#validation-book-context-fullscreen-view)
  - [Acceptance Criteria](#acceptance-criteria)
  - [Test Cases](#test-cases)
  - [Manual Verification](#manual-verification)
  - [Definition of Done](#definition-of-done)
  - [Rollback Plan](#rollback-plan)

## Acceptance Criteria

| Requirement | Acceptance Criterion |
| --- | --- |
| FR1 | The non-empty-context branch of `_BookContext.cshtml` shows an `sl-button.book-context-fullscreen-btn` labeled "See Book Context" with a `book` prefix icon and `data-dialog-id="book-context-dialog-{BookId}"`; no inline `sl-details`/scrollbox preview remains. |
| FR2 | Clicking the button sets `open` on `sl-dialog#book-context-dialog-{BookId}` (`no-header`, `label` set to "Title — Author"); its body shows the full context markdown with no `max-height` clamp. |
| FR3 | The dialog's custom `<header>` shows an `<h2>` with the book title and a `<p>` with the author. |
| FR4 | `BookContextViewModel.Title`/`Author` are populated in `_BookDetails.cshtml`'s partial call and in `NotesController.GenerateContext`'s success and generic-`Exception` branches (verified by the two new controller tests). |
| FR5 | The dialog's paper visual uses `.paper-style--modal` (not `.paper-style`), and `.book-context-dialog::part(panel)`/`::part(body)` remove Shoelace's default panel background/shadow/padding so only the paper-stack surface is visible. |
| FR6 | The Regenerate icon-button and the "may contain inaccuracies" info icon appear only inside the dialog header — not duplicated anywhere else in `_BookContext.cshtml`. |
| FR7 | Opening the dialog fires no network request; `.book-context-fullscreen-btn` sets `open = true`, and `.book-context-dialog-close-btn` sets `open = false`; Escape and overlay-click still close the dialog via Shoelace's untouched native behavior. |
| FR8 | The trigger button and dialog are absent from the DOM when `Model.Context` is null/empty (the "Generate Context" button branch renders instead, spinner preceding the button). |
| FR9 | The dialog body's HTML comes from the single `contextHtml` local variable; there is no second `Markdown.ToHtml` call or second render site in the file. |

## Test Cases

**Unit tests:**
- `WebApp.Tests/Controllers/NotesControllerTests.cs::GenerateContext_ReturnsBookTitleAndAuthorOnSuccess` — asserts `Title`/`Author`/`Context` on the success path. **Implemented and passing.**
- `WebApp.Tests/Controllers/NotesControllerTests.cs::GenerateContext_ReturnsBookTitleAndAuthorWhenGenerationFails` — asserts `Title`/`Author` are still populated (and `Context` is `null`) when `IBookContextService.GenerateAndSaveAsync` throws. **Implemented and passing.**
- Full suite run via `make test`: **190/190 `WebApp.Tests` passed, 44/44 `TtsService.Tests` passed** (confirmed during this review).
- ⚠️ Gap: no test covers `GenerateContext` when the book row itself can't be found by the new `_db.Books` lookup (i.e. `book` is `null` and `Title`/`Author` fall back to `string.Empty`) while `GenerateAndSaveAsync` still succeeds or throws — this is a narrow edge case (the book existed moments earlier for `GenerateAndSaveAsync`/the `KeyNotFoundException` path to have been reached at all) but is currently unverified by any test. Recommend adding it if this path is considered reachable in practice, otherwise document it as accepted dead code.
- Razor view markup itself (`_BookContext.cshtml`, `_BookDetails.cshtml`) is not unit tested, consistent with this repo's existing convention (Razor-only features rely on manual verification, e.g. Phase 18 `_Alert.cshtml`, Phase 23 copy-to-clipboard).

**Integration tests:**
- No Docker/PostgreSQL/pgvector/Ollama integration test is needed — this feature does not touch embeddings, chat, or context generation logic, only view assembly of already-persisted `Title`/`Author`/`Context` columns.

## Manual Verification

1. Start the stack: `make docker-run` (Linux/SteamOS) and wait for `webapp`, `postgres`, and `redis` to become healthy.
2. Sign in, open a book's Notes detail page. If it has no context yet, click "Generate Context" and wait for it to populate — confirm the spinner appears before the button (not after) while loading.
3. Confirm the header row now shows "Back to Library" and a "See Book Context" button (book icon) side by side, with no inline context text visible below.
4. Click "See Book Context" and confirm the dialog opens showing: a custom header with the book's title and author, a Regenerate icon-button with its "Regenerate context with AI" tooltip, the info icon with its "may contain inaccuracies" tooltip, and a close (×) button — all inside the dialog, none duplicated outside it.
5. Confirm the body shows the full, unclamped context text inside a paper-stack styled panel (stacked-page edges visible via the `::before`/`::after` layers, sunrise entrance animation plays once, solid page background — not transparent/see-through).
6. Scroll within the dialog if the context is long; confirm the dialog itself does not grow past the viewport (`max-height: 90vh` on the panel) and the page behind it does not scroll.
7. Click the dialog's own × close button; confirm it closes. Reopen it and press `Escape`; confirm it closes. Reopen it and click the overlay (outside the panel); confirm it closes. All three must work since none are wired through custom JS except the × button.
8. Reopen the dialog, click "Regenerate context with AI" from inside it, and confirm — as expected, not as a bug — that the dialog closes once regeneration completes (the whole partial is swapped and the fresh dialog renders closed); click "See Book Context" again and confirm the newly regenerated text and correct title/author appear.
9. Tab to the "See Book Context" button with the keyboard only (no mouse) and press Enter/Space; confirm the dialog opens the same way as a mouse click, and that focus lands somewhere sensible inside the dialog (Shoelace's built-in focus trap). Close it via keyboard (`Escape`) and confirm focus returns to the trigger button.
10. Resize the browser to a narrow/mobile width (< 640px) and confirm the dialog panel narrows to `calc(100vw - 2rem)`, the paper-stack shortens to `65vh`, and header/body padding tighten — no horizontal overflow, internal scroll still works.
11. Run `make test` and confirm all existing and new tests pass (already confirmed: 190/190 + 44/44 green during this review — re-run after any further changes).

## Definition of Done

- Requirements, Plan, and Validation docs in this spec folder reflect the implemented behavior (updated in this pass).
- All existing tests still pass; new `NotesControllerTests` cases for `Title`/`Author` population pass. **Confirmed via `make test`.**
- `_BookContext.cshtml`, `_BookDetails.cshtml`, `paper_style.sass`, and `site.js` are updated consistently; responsive, empty (`Model.Context` null), and open/close/regenerate interaction states are covered per the Manual Verification steps above.
- No AI/Microsoft Agent Framework behavior changed — `BookContextService`, `BookContextAgentTool`, and prompts are untouched.
- No Docker, migration, or infrastructure change was required; Sass source under `WebApp/Styles` was edited, not generated CSS under `wwwroot/css`.
- ⚠️ Outstanding before calling this fully done: manual verification steps 4-10 above (dialog open/close, Regenerate-inside-dialog behavior, keyboard access, responsive check) should be walked through in a running browser at least once — they were not executed as part of this spec-sync pass, only the automated test suite was.

## Rollback Plan

- This feature is additive Razor/Sass/JS markup with no feature flag, migration, or config toggle. To roll back:
  - Revert the commit(s) touching `WebApp/Views/Notes/_BookContext.cshtml`, `WebApp/Views/Notes/_BookDetails.cshtml`, `WebApp/Models/BookContextViewModel.cs`, `WebApp/Controllers/NotesController.cs` (`GenerateContext`), `WebApp/Styles/Components/paper_style.sass`, `WebApp/wwwroot/js/site.js`, and `WebApp.Tests/Controllers/NotesControllerTests.cs`.
  - Because `BookContextViewModel.Title`/`Author` are purely additive properties read from existing `Book` columns (no new persisted state), reverting the code fully removes the feature with no data cleanup required.
