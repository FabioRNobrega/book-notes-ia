# Validation: Notes Library Search

## Table of Contents

- [Acceptance Criteria](#acceptance-criteria)
- [Test Cases](#test-cases)
- [Manual Verification](#manual-verification)

## Acceptance Criteria

| Requirement | Acceptance Criterion |
| --- | --- |
| FR1 | `_SeeYourNotes.cshtml` renders a search bar inside `#search-container`. |
| FR2 | The search bar is an `sl-input` with a prefix `sl-icon` named `search` and uses the existing Shoelace input styling. |
| FR3 | The implementation adds no new custom JavaScript file. Shoelace `sl-input` event wiring uses `hx-trigger="sl-input delay:400ms"` and `hx-vals="js:{query: document.getElementById('library-search-input').value}"` — HTMX built-in evaluation, not a script block or `.js` file. Hyperscript is not used because it cannot parse hyphenated event names (`sl-input`) in version 0.9.x. |
| FR4 | `NotesController` exposes an authenticated endpoint that returns a Razor partial for library search results. |
| FR5 | Standard SQL search results never include books belonging to another user. |
| FR6 | Title and author SQL matches are returned without invoking embedding or librarian fallback. |
| FR6a | When exact SQL CONTAINS finds no match, `BookLibrarySearchService` retries with pg_trgm `word_similarity >= 0.3` before signalling `NoExactSqlMatch`. Typo queries that pass this step never reach the librarian. |
| FR7 | When both SQL steps return no matches for a non-empty query, the response shows the librarian-searching message and auto-triggers the librarian endpoint. |
| FR8 | The librarian endpoint is authenticated and uses user-scoped book embeddings to find closest matching books. |
| FR8a | The librarian cosine distance threshold is 0.25. Gibberish queries, which produce near-orthogonal embedding vectors, are rejected and cause the librarian-not-found state rather than returning spurious results. |
| FR9 | The librarian endpoint returns possible book results and does not return generated context. |
| FR10 | When the librarian endpoint finds possible books, the UI displays "Here's what our librarian found for you!" above the matching cards. |
| FR11 | When the librarian endpoint finds no possible books, the UI displays the final librarian-not-found message. |
| FR12 | SQL, fuzzy, and librarian results all use the same card layout and `hx-get` book-open behavior as the existing notes library. |
| FR13 | A blank search query returns the full library ordered by `UpdatedAt` descending. |

## Test Cases

**Unit tests (`WebApp.Tests/Services/BookLibrarySearchServiceTests.cs`):**

- Blank query returns all user books ordered by `UpdatedAt` descending.
- Title search matches case-insensitively via exact SQL and does not invoke the embedding service.
- Author search matches case-insensitively via exact SQL and does not invoke the embedding service.
- Exact SQL search excludes books owned by other users.
- Non-matching query on in-memory provider returns `NoExactSqlMatch=true` (fuzzy step is skipped by the `IsRelational()` guard).

**Controller tests (`WebApp.Tests/Controllers/NotesControllerTests.cs`):**

- Unauthenticated standard search returns 401 Unauthorized.
- Authenticated search with SQL matches returns `_BookLibraryResults` partial.
- Standard search with `NoExactSqlMatch=true` returns `_BookLibraryLibrarianSearch` partial, not the final no-results state.
- Unauthenticated librarian search returns 401 Unauthorized.
- Librarian search with found books returns `_BookLibraryResults` partial with "Here's what our librarian found for you!" header.
- Librarian search with no books returns `_BookLibraryResults` partial with `IsLibrarianNotFound=true`.

**Integration tests (`WebApp.Tests/Integration/BookLibrarySearchPostgresTests.cs`):**

- Fuzzy step: a one-character typo in the title ("The Wordl Jones Made") matches the correct book via pg_trgm.
- Fuzzy step: a one-character typo in the author name ("Asimof") matches the correct book via pg_trgm.
- Fuzzy step: user isolation — a typo query on user B finds nothing when the matching book belongs to user A.
- Fuzzy step: a completely unrelated query falls through to `NoExactSqlMatch=true`.
- Librarian step: a seeded `book_embedding` vector returns the closest user-owned book within distance 0.25.
- Librarian step: another user's embeddings are never returned.
- Librarian step: embedding generation failure returns empty results without breaking the page.
- Librarian step: an orthogonal (gibberish) query vector has cosine distance ~1.0 and is rejected by the 0.25 threshold, returning empty results.

## Manual Verification

1. Start the local stack with `make docker-run` on Linux/SteamOS. The `AddPgTrgmExtension` migration runs automatically on startup and enables `pg_trgm` with GIN indexes on `book.Title` and `book.Author`.
2. Sign in and import a Kindle clippings `.txt` file with at least two books and different authors.
3. Open `See Your Notes`.
4. Verify the search bar appears inside the notes panel above the book grid and uses a search icon.
5. Search for part of a book title and confirm only matching book cards appear.
6. Search for part of an author name and confirm matching books by that author appear.
7. Introduce a deliberate typo (e.g. "Tolkein" for "Tolkien", or "wordl" for a title containing "world") and confirm the fuzzy step still returns the correct book — the librarian-searching state should **not** appear.
8. Search for a phrase with no typo or title match that should semantically map to a book embedding (e.g. "desert planet" → Dune). Confirm the standard search first shows "No exact match - let our librarian dig through the shelves for you... 📚" while the librarian endpoint runs.
9. Confirm possible librarian matches render with "Here's what our librarian found for you!" when the vector lookup succeeds.
10. Search with complete gibberish (long random characters). Confirm the librarian-not-found message appears — no spurious book cards.
11. Search for a phrase that has no SQL, fuzzy, or semantic match and confirm the final librarian-not-found message appears.
12. Clear the search input and confirm the full library returns in updated order.
13. Confirm opening a filtered, fuzzy-matched, or librarian-found book card still swaps the selected book into `#chat-container`.
