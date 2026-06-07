# Requirements: Inline Book Title Editing

## Table of Contents

- [Problem Statement](#problem-statement)
- [User Stories](#user-stories)
- [Functional Requirements](#functional-requirements)
- [Non-Functional Requirements](#non-functional-requirements)
- [Out of Scope](#out-of-scope)
- [Open Questions](#open-questions)

## Problem Statement

`WebApp/Views/Notes/_BookDetails.cshtml` currently renders the selected book title as plain text and contains a TODO beside that title for an edit affordance. Readers who import Kindle clippings sometimes need to correct imported book titles without re-importing their notes. The book detail screen should let an authenticated user toggle only the title area between a view state and an edit state, using the existing Shoelace icon and HTMX-style partial swap patterns, with no custom JavaScript.

## User Stories

- Given I am viewing a book in my notes library, when I click the pencil-square icon beside the title, then only the title area changes into an editable input pre-filled with the current title.
- Given the title is in edit mode, when I click the floppy icon, then the new non-empty title is saved and the title area returns to view mode.
- Given the title is in edit mode, when I press Enter inside the input, then the new non-empty title is saved and the title area returns to view mode.
- Given I submit an empty or whitespace-only title, when the save request is handled, then the title is not updated and the title area stays in edit mode with a visible validation message.
- Given I am signed in as one user, when I try to edit another user's book title, then the request is rejected and no book data is changed.

## Functional Requirements

1. **FR1** - `WebApp/Views/Notes/_BookDetails.cshtml` must replace the inline TODO title markup with a title-region partial that renders only the selected book title controls and can be swapped independently from the rest of the book detail page.

2. **FR2** - The title region view state must display the current title as plain text in the existing `font-title text-3xl leading-tight` visual style and show a Shoelace `pencil-square` icon affordance beside it.

3. **FR3** - Clicking the `pencil-square` affordance must issue an HTMX request that replaces only the title region with an edit-state partial. The rest of `_BookDetails.cshtml`, including cover art, author text, notes, and book context, must not be refreshed.

4. **FR4** - The title region edit state must render a Shoelace-compatible input field pre-filled with the current title and a Shoelace `floppy` icon affordance for saving. The input must use the current layout style and existing Shoelace input theming from `WebApp/Styles/Components/input.sass`.

5. **FR5** - Clicking the `floppy` affordance must submit the edited title via HTMX to a user-scoped endpoint that persists the new title and swaps only the title region back to view state.

6. **FR6** - Pressing Enter while focus is inside the title input must save the edited title. This behavior must be implemented with HTMX and/or hyperscript, not custom JavaScript.

7. **FR7** - The save endpoint must reject null, empty, or whitespace-only titles. On validation failure, it must return the edit-state title partial with a visible inline validation message and must not update the database.

8. **FR8** - A successful title save must trim the submitted title, update `Book.Title`, update `Book.NormalizedTitle` using the same normalization behavior as Kindle import/search code, and update `Book.UpdatedAt`.

9. **FR9** - Title updates must be scoped by both `Book.Id` and the authenticated user's `UserId`. Requests without a valid authenticated user must return `Unauthorized`; requests for a missing or other-user book must return `NotFound`.

10. **FR10** - The implementation must not add custom JavaScript. Toggle and save behavior must use ASP.NET Core MVC actions, Razor partials, HTMX attributes, and/or hyperscript.

11. **FR11** - Existing cover fallback text and cover image alt text may remain unchanged until the full book detail view is next rendered; this feature swaps only the title region after edit/save.

## Non-Functional Requirements

- **User-data isolation**: Every read and write involved in title editing must include the authenticated `UserId` predicate and must never expose another user's book title.
- **SOLID - Single Responsibility**: `NotesController` should coordinate HTTP flow and partial selection only. Title mutation and normalization should live in focused application code rather than being duplicated across views or broad controller logic.
- **SOLID - Testability**: The title update behavior must be covered without a browser by controller/service tests using fake authenticated users and EF Core test patterns already present in `WebApp.Tests`.
- **UI consistency**: The title controls must use `sl-icon`, `sl-icon-button`, `sl-tooltip`, `sl-input`, or `sl-button` patterns already present in Notes/UserProfile views. The UI must fit the current book-detail header on desktop and mobile without overlapping adjacent author or action controls.
- **No new dependencies**: Do not introduce new frontend packages, runtime packages, or infrastructure for this feature.
- **Docker-first validation**: Build and test commands must run through `make test` or `docker compose exec webapp ...` according to `AGENTS.md`.

## Out of Scope

- Editing the author, cover URL, book context, synopsis, notes, imported clipping content, or note metadata.
- Refreshing the entire book details panel after saving the title.
- Re-importing Kindle clippings or changing note deduplication keys after a manual title edit.
- Regenerating Ollama/pgvector book embeddings during title save.
- Adding optimistic concurrency, autosave, undo, or a cancel button unless chosen during implementation.
- Adding custom JavaScript to `WebApp/wwwroot/js/site.js`.

## Open Questions

None. Discovery decisions:

- Saving updates both `Book.Title` and `Book.NormalizedTitle`.
- Empty and whitespace-only titles are rejected.
- HTMX swaps only the title region, not the full `_BookDetails.cshtml` partial.
- Pressing Enter in the input saves the title.
