# Plan: Book Context Fullscreen View

## Table of Contents

- [Plan: Book Context Fullscreen View](#plan-book-context-fullscreen-view)
  - [Summary](#summary)
  - [Technical Approach](#technical-approach)
  - [Component Breakdown](#component-breakdown)
  - [Dependencies](#dependencies)
  - [Flow](#flow)
  - [Risk Assessment](#risk-assessment)

## Summary

Replace `_BookContext.cshtml`'s inline `sl-details`/100px-scrollbox preview with a "See Book Context" trigger button and a paired `sl-dialog` that shows the full context in a paper-stack styled surface, with a custom title/author header, Regenerate/info controls relocated into the dialog, and two small `site.js` click handlers (open/close) instead of any HTMX round-trip for the dialog itself.

## Technical Approach

This is a presentation-only extension of an existing Razor partial plus one small view-model/controller change to carry `Title`/`Author` through to the dialog header — no new service, controller action, or persistence path was introduced.

- **Inline preview removed, not duplicated**: the original 100px `.book-context-scroll` clamp and its `<style>` block are deleted outright rather than kept alongside a new fullscreen option. The single Markdig render (`var contextHtml = Markdown.ToHtml(...)`) is computed once per partial render and used only in the dialog body, so there is exactly one render site instead of two that could drift (FR9).
- **Dialog is pure client-side**: opening/closing the dialog is not a data fetch — the content is already in the DOM — so no `hx-*` attribute drives it. `site.js` gets two delegated `click` listeners grouped with the existing ones near `.copy-response-btn`/`.tts-play-btn`: one on `.book-context-fullscreen-btn` (reads `data-dialog-id`, sets `dialog.open = true`) and one on `.book-context-dialog-close-btn` (walks up via `.closest("sl-dialog")`, sets `dialog.open = false`). The second listener exists because the dialog uses Shoelace's `no-header` attribute to fully own its header markup (title/author + Regenerate + info icon + spinner + close button), which suppresses Shoelace's own default header and built-in close button. Escape and overlay-click dismissal are unaffected by `no-header` — they are `sl-dialog`'s native light-dismiss behavior and are not overridden.
- **Paper-stack modifier, not the raw class, and dialog chrome overridden**: `.paper-style` is hardcoded to `height: 100vh` and page-level margins, tuned for full-page layout (`_BookDetails.cshtml`). The implementation adds `.paper-style--modal` in the same file: `height: min(70vh, 640px)`, `inset: 0` on the `::before`/`::after` "stacked page" pseudo-elements (simpler than the page-layout class's discrete `top`/`left`/`right` offsets, since the modal doesn't need the pages to peek out from a full-page sheet), reduced rotation (`-1deg`/`0.7deg` vs `-2.5deg`/`1.4deg`) so the stack reads at a smaller scale, and an inner `> div` with a solid `#2C2B2B` background carrying the actual page content above the pseudo-element stack (`z-index: 1`). Because `sl-dialog`'s own `::part(panel)` ships a default background/box-shadow that would double up with the paper visual, `.book-context-dialog::part(panel)` is set to `background: transparent`, `box-shadow: none`, `overflow: visible`, and a `width: min(90vw, 850px)` / `max-height: 90vh` cap; `::part(body)` drops its default padding so the paper-stack fills the panel edge-to-edge.
- **SOLID / MVC boundaries preserved**: `NotesController.GenerateContext` already queried `_db.Books` directly for other reads in this controller, so adding one more `AsNoTracking` projection for `Title`/`Author` (on the success branch and the generic-`Exception` catch branch — not the `KeyNotFoundException` branch, which returns `NotFound()` before any view model is built) follows the controller's existing read pattern rather than introducing a new service just to carry two strings.
- **View model growth stays minimal**: `BookContextViewModel` gains two `string` properties (`Title`, `Author`, both `= default!`). Every constructor call site is updated in the same change (`_BookDetails.cshtml` and both applicable branches of `NotesController.GenerateContext`), keeping the view model's shape consistent everywhere it's built.
- **Layout consequence in `_BookDetails.cshtml`**: since `_BookContext.cshtml` no longer renders a full-width inline block, its header section was restructured from a single `flex flex-wrap items-start justify-between` row into a `flex-col` stack: the book cover/title/author row stays on top, and a new bottom row (`flex w-full flex-wrap items-center justify-between`) holds "Back to Library" and the "See Book Context" trigger side by side, instead of the context preview occupying its own bordered block below.
- **Test coverage added at the controller boundary**: `WebApp.Tests/Controllers/NotesControllerTests.cs` gained `GenerateContext_ReturnsBookTitleAndAuthorOnSuccess` and `GenerateContext_ReturnsBookTitleAndAuthorWhenGenerationFails`, using the existing `AddBook` test helper and an extended `FakeBookContextService` that now accepts a canned context string or an exception to throw, matching the repo's existing xUnit fake-based testing pattern (no new mocking library introduced).

## Component Breakdown

**Existing files modified:**

- `WebApp/Models/BookContextViewModel.cs` — added `public string Title { get; set; } = default!;` and `public string Author { get; set; } = default!;`.
- `WebApp/Controllers/NotesController.cs` (`GenerateContext`) — on the success path and the generic-`Exception` catch path, queries `_db.Books.AsNoTracking().Where(x => x.Id == id && x.UserId == userId).Select(x => new { x.Title, x.Author }).FirstOrDefaultAsync(ct)` and populates `Title`/`Author` (with `?? string.Empty` fallback) on the returned `BookContextViewModel`.
- `WebApp/Views/Notes/_BookDetails.cshtml` — restructured the header into a `flex-col` layout; passes `Title = Model.Title, Author = Model.Author` into the `BookContextViewModel` construction; moved "Back to Library" and the `_BookContext.cshtml` partial into a shared bottom row.
- `WebApp/Views/Notes/_BookContext.cshtml` — removed the inline `<style>` block and `.book-context-scroll`/`sl-details` preview; added the "See Book Context" `sl-button` trigger and the `sl-dialog` (custom header with title/author/Regenerate/info/spinner/close, and a body rendering the hoisted `contextHtml`).
- `WebApp/Styles/Components/paper_style.sass` — added `.book-context-dialog` (`::part(panel)`/`::part(body)` overrides), `.paper-style--modal`, `.book-context-modal-content`, `.book-context-modal-header`, `.book-context-modal-body`, and a `@media (max-width: 640px)` responsive block.
- `WebApp/wwwroot/js/site.js` — added two delegated `click` listeners (`.book-context-fullscreen-btn` to open, `.book-context-dialog-close-btn` to close).
- `WebApp.Tests/Controllers/NotesControllerTests.cs` — added two tests covering `Title`/`Author` population on success and failure; extended `FakeBookContextService` to accept a canned context or an exception.

**New files created:**

- None. All styling was added to the existing `paper_style.sass`; no new Sass partial, controller, service, or view model file was needed.

## Dependencies

- Shoelace `sl-dialog` (including its `no-header` attribute and `::part(panel)`/`::part(body)` CSS shadow parts) and `sl-icon` (`book`, `x-lg`, `arrow-counterclockwise`, `question-circle`), already loaded via the CDN autoloader `<script>` in `WebApp/Views/Shared/_Layout.cshtml` — no version bump needed.
- No database migration — `Book.Title` and `Book.Author` already exist as columns; this only reads them into an existing view model.
- Docker-first dev/test loop: `make test` (confirmed green: 190/190 `WebApp.Tests`, 44/44 `TtsService.Tests`) and `make docker-run` with the Sass compiler watching `WebApp/Styles` for manual/visual verification.

## Flow

```mermaid
sequenceDiagram
    participant Reader
    participant BookContextPartial as _BookContext.cshtml
    participant SiteJs as site.js (delegated click)
    participant SlDialog as sl-dialog (no-header)
    participant NotesController

    Reader->>BookContextPartial: Clicks "See Book Context" (.book-context-fullscreen-btn)
    BookContextPartial->>SiteJs: click bubbles, data-dialog-id read
    SiteJs->>SlDialog: dialog.open = true
    SlDialog-->>Reader: Renders custom header (Title/Author + Regenerate + info) and paper-stack--modal body

    alt Reader clicks close / Escape / overlay
        Reader->>SlDialog: dismiss
        SlDialog-->>Reader: dialog.open = false (custom close handler, or native light-dismiss)
    else Reader clicks Regenerate inside the dialog
        Reader->>NotesController: hx-post /notes/book/{id}/context/generate
        NotesController-->>BookContextPartial: outerHTML swap of #book-context-{id} (button + fresh, closed dialog)
        BookContextPartial-->>Reader: "See Book Context" button shown again; dialog no longer open
    end
```

## Risk Assessment

| Risk | Evidence | Mitigation |
| --- | --- | --- |
| Regenerating from inside the open dialog closes it (the `hx-swap="outerHTML"` on `#book-context-{id}` replaces the whole button+dialog block, and the freshly rendered dialog has no `open` attribute), which could read as a bug rather than a deliberate trade-off. | `_BookContext.cshtml`'s Regenerate icon-button still targets `hx-target="#book-context-@Model.BookId"` / `hx-swap="outerHTML"`, the same wrapper that now contains the dialog. | Documented explicitly in Requirements.md's last User Story and Out of Scope; call out in manual verification so it's confirmed as accepted behavior, not silently missed. |
| `no-header` plus a custom close button means Escape/overlay-click rely entirely on `sl-dialog`'s untouched native behavior — if a future change accidentally listens for `sl-request-close` and calls `preventDefault()`, Escape/overlay dismissal would silently break. | No `sl-request-close` handler exists today in `site.js`; behavior depends on Shoelace defaults remaining unmodified. | Manual verification step confirms Escape and overlay-click both close the dialog today; any future PR touching `sl-dialog` events should re-check this. |
| `.book-context-dialog::part(panel)` sets `background: transparent`/`box-shadow: none`, so if `.paper-style--modal`'s inner `> div` background is ever removed, the dialog would render with no visible surface at all. | `paper_style.sass`'s `.paper-style--modal > div` carries the only opaque background (`#2C2B2B`) in the whole dialog chrome chain. | Covered by manual visual verification; the coupling is called out here so a future Sass edit doesn't drop the inner background without noticing the dialog goes transparent. |
| Sass not recompiled before manual verification, making the dialog look unstyled. | `AspNetCore.SassCompiler` compiles `WebApp/Styles` to `wwwroot/css` on build/watch; generated CSS is gitignored. | Confirm the dev stack (`make docker-run`) is running with Sass watch active, or rebuild, before visually verifying the dialog. |
