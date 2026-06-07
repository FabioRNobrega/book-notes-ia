# Validation: Inline Book Title Editing

## Table of Contents

- [Acceptance Criteria](#acceptance-criteria)
- [Test Cases](#test-cases)
- [Manual Verification](#manual-verification)
- [Definition of Done](#definition-of-done)
- [Rollback Plan](#rollback-plan)

## Acceptance Criteria

| Requirement | Acceptance Criterion |
| --- | --- |
| FR1 | `_BookDetails.cshtml` no longer contains the title TODO and renders a stable, title-only partial region for the selected book. |
| FR2 | View mode displays the current title as plain text with the existing title typography and a visible `pencil-square` Shoelace icon affordance. |
| FR3 | Clicking `pencil-square` swaps only `#book-title-{bookId}` into edit mode; author, cover, context, notes, and Back to Library remain unchanged in the DOM. |
| FR4 | Edit mode displays a pre-filled Shoelace input and a `floppy` save affordance styled consistently with existing inputs/buttons. |
| FR5 | Clicking `floppy` saves a valid title and swaps only the title region back to view mode with the updated title. |
| FR6 | Pressing Enter while focused in the title input saves the valid title without custom JavaScript. |
| FR7 | Submitting an empty or whitespace-only title does not update the database and returns edit mode with an inline validation message. |
| FR8 | A successful save trims the title, updates `Book.Title`, updates `Book.NormalizedTitle`, and updates `Book.UpdatedAt`. |
| FR9 | Unauthorized requests return `Unauthorized`; requests for missing or other-user books return `NotFound`; no cross-user mutation occurs. |
| FR10 | No custom JavaScript is added or modified for this feature; toggle/save behavior is expressed with MVC, Razor, HTMX, and/or hyperscript. |
| FR11 | After save, only the title region changes; cover fallback text and cover image alt text are allowed to refresh only on the next full book detail render. |

## Test Cases

**Unit tests - `WebApp.Tests/Services/BookTitleServiceTests.cs`:**

- `UpdateTitleAsync_WithValidTitle_TrimsAndPersistsTitle` - seed a user-owned book, save `"  New Title  "`, assert `Book.Title == "New Title"`.
- `UpdateTitleAsync_WithValidTitle_UpdatesNormalizedTitle` - save `"The Left Hand of Darkness!"`, assert normalized title is `thelefthandofdarkness`.
- `UpdateTitleAsync_WithValidTitle_UpdatesUpdatedAt` - assert `UpdatedAt` is later than the seeded value.
- `UpdateTitleAsync_WithWhitespaceTitle_ReturnsValidationErrorAndDoesNotMutateBook` - assert the original title and normalized title remain unchanged.
- `UpdateTitleAsync_WithOtherUserBook_ReturnsNotFoundAndDoesNotMutateBook` - seed book for another user and assert no update.
- `UpdateTitleAsync_WithMissingBook_ReturnsNotFound`.

**Controller tests - `WebApp.Tests/Controllers/NotesControllerTests.cs`:**

- `EditTitle_WithAuthenticatedOwner_ReturnsBookTitlePartialInEditMode` - assert `PartialViewResult` uses `~/Views/Notes/_BookTitle.cshtml` and model has `IsEditing = true`.
- `EditTitle_WithoutUser_ReturnsUnauthorized`.
- `EditTitle_WithMissingOrOtherUserBook_ReturnsNotFound`.
- `UpdateTitle_WithValidTitle_ReturnsBookTitlePartialInViewMode` - assert returned model contains the updated title and `IsEditing = false`.
- `UpdateTitle_WithWhitespaceTitle_ReturnsBookTitlePartialInEditModeWithError`.

**Razor/manual UI checks:**

- Confirm `_BookTitle.cshtml` uses `sl-icon-button` or equivalent Shoelace controls with `pencil-square` and `floppy` names.
- Confirm the edit input uses `sl-input` or existing input styling and includes a label or accessible name.
- Confirm no changes are made to `WebApp/wwwroot/js/site.js` for this feature.

**Integration tests:**

None required for the first implementation slice. EF Core in-memory service/controller tests are sufficient because this feature uses ordinary `Book` row updates and does not require PostgreSQL-specific SQL, pgvector, Redis, Ollama, Open Library, or Microsoft Agent Framework behavior.

## Manual Verification

1. Start the app with the OS-appropriate Make target from `AGENTS.md`, for example `make docker-run` on Linux/SteamOS.
2. Log in and import a Kindle clippings `.txt` file with at least one book.
3. Open the Notes library and select a book.
4. In the book detail header, click the `pencil-square` icon beside the title.
5. Verify only the title area changes into an input and `floppy` icon; the author, cover, notes, context, and Back to Library button remain in place.
6. Change the title and click the `floppy` icon.
7. Verify only the title area returns to view mode and displays the new title.
8. Repeat the edit flow and press Enter in the input instead of clicking `floppy`; verify the title saves.
9. Try saving a blank title; verify the input stays visible, an inline error appears, and the old title is still shown after leaving edit mode or reloading the book.
10. Inside the `webapp` container, inspect the database row for the edited book and verify `Title`, `NormalizedTitle`, and `UpdatedAt` changed.
11. Run the regression suite with `make test`.

## Definition of Done

- `Requirements.md`, `Plan.md`, and `Validation.md` in `Specs/20260606233114-inline-book-title-editing/` are complete.
- `_BookDetails.cshtml` renders a title-only partial region and the TODO comment is removed.
- `_BookTitle.cshtml` supports view and edit states with `pencil-square`, `floppy`, HTMX swaps, and Enter-to-save without custom JavaScript.
- `NotesController` exposes user-scoped title edit and save actions.
- `BookTitleService` persists valid title changes, updates `NormalizedTitle`, updates `UpdatedAt`, and rejects empty titles.
- `Program.cs` registers the new service.
- Service and controller tests cover success, validation failure, missing book, unauthorized user, and other-user isolation.
- Existing tests pass through `make test`.
- No generated CSS, `.env`, `bin/`, or `obj/` artifacts are committed.

## Rollback Plan

- Remove the title edit/save actions from `NotesController`.
- Remove `IBookTitleService` / `BookTitleService` registration from `Program.cs`.
- Delete `WebApp/Services/BookTitleService.cs`, `WebApp/Models/BookTitleEditViewModel.cs`, and `WebApp/Views/Notes/_BookTitle.cshtml`.
- Restore `_BookDetails.cshtml` to render `@Model.Title` directly as plain text.
- Remove the related service/controller tests.
- No database migration rollback is needed because the feature updates existing `Book` columns only.
