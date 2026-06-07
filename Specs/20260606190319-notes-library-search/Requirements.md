# Requirements: Notes Library Search

## Table of Contents

- [Problem Statement](#problem-statement)
- [User Stories](#user-stories)
- [Functional Requirements](#functional-requirements)
- [Non-Functional Requirements](#non-functional-requirements)
- [Out of Scope](#out-of-scope)
- [Open Questions](#open-questions)

## Problem Statement

The notes library partial at `WebApp/Views/Home/_SeeYourNotes.cshtml` renders a user's imported books in updated order, but it does not provide a way to narrow the library by book title or author. Readers with larger imported Kindle libraries need a search control in the existing `#search-container` placeholder that uses the app's current Razor, Shoelace, HTMX, hyperscript, and C# patterns without adding custom JavaScript.

## User Stories

- Given an authenticated reader with imported books, when they type a book title into the notes library search bar, then the library displays matching book cards.
- Given an authenticated reader with imported books, when they type an author name into the notes library search bar, then the library displays books by that author.
- Given a search phrase that does not match title or author through SQL, when standard search returns no results, then the UI automatically shows a librarian-searching message and triggers the closest-book endpoint.
- Given the librarian endpoint finds possible books, when the endpoint returns, then the library displays the possible book cards with a librarian-found message.
- Given the librarian endpoint finds no possible books, when the endpoint returns, then the user sees a final librarian-not-found message instead of an empty grid.

## Functional Requirements

1. FR1 - `WebApp/Views/Home/_SeeYourNotes.cshtml` must render a Shoelace `sl-input` search bar inside the existing `<div id="search-container">` placeholder.
2. FR2 - The search input must use an `sl-icon` search icon in the input prefix slot and preserve the visual conventions already defined for `sl-input` in `WebApp/Styles/Components/input.sass`.
3. FR3 - The search interaction must use C#, HTMX, and optionally hyperscript only; no new custom JavaScript may be added.
4. FR4 - `NotesController` must expose an authenticated endpoint that accepts a search query and returns a Razor partial suitable for replacing the visible notes library results.
5. FR5 - The standard search endpoint must scope every query by the current authenticated user's `UserId`.
6. FR6 - The standard search endpoint must search only the user's books with SQL-backed title and author matching.
7. FR7 - If SQL title/author matching returns no books for a non-empty query, the standard search response must render an intermediate librarian-searching state that says: "No exact match - let our librarian dig through the shelves for you... 📚" and automatically triggers a second endpoint with HTMX.
8. FR8 - A separate authenticated librarian endpoint, similar in shape to `BookContextController` API operations, must try to find the closest matching user-owned books through the existing book embedding/RAG capability.
9. FR9 - The librarian endpoint must return possible books, not generated book context.
10. FR10 - If the librarian endpoint finds possible books, the UI must show "Here's what our librarian found for you!" and render those books.
11. FR11 - If the librarian endpoint finds no possible books, the UI must show: "Hmm, even our librarian couldn't track this one down. Maybe you haven't read that book yet, or maybe a typo slipped in? Make sure you've uploaded your notes before giving it another try!"
12. FR12 - Search and librarian results must render the same book card layout and HTMX book-open behavior currently used by `_SeeYourNotes.cshtml`.
13. FR13 - Clearing the search query must restore the full library ordered by `UpdatedAt` descending.

## Non-Functional Requirements

- The implementation must preserve user-owned data isolation by filtering all book and embedding access by `UserId`.
- The standard search endpoint should remain fast by never generating embeddings; embedding/RAG lookup belongs to the second librarian endpoint only.
- The UI must remain responsive in the existing notes panel layout and must not introduce text or controls that overlap on mobile or desktop widths.
- Provider-specific vector SQL must stay inside a focused service, following the existing `BookLookupService`, `BookContextController`, and pgvector test patterns.
- Generated CSS under `WebApp/wwwroot/css` must not be edited directly.

## Out of Scope

- Searching individual note contents or note embeddings.
- Re-ranking multiple semantic matches.
- Adding a new frontend library or custom JavaScript file.
- Changing the Microsoft Agent Framework chat behavior.
- Generating or returning book context from the librarian search endpoint.
- Modifying Kindle import or embedding generation.

## Open Questions

- None.
