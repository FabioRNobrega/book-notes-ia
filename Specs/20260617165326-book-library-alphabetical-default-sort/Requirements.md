# Requirements: Book Library Alphabetical Default Sort

## Table of Contents

- [Problem Statement](#problem-statement)
- [User Stories](#user-stories)
- [Functional Requirements](#functional-requirements)
- [Non-Functional Requirements](#non-functional-requirements)
- [Out of Scope](#out-of-scope)
- [Open Questions](#open-questions)

## Problem Statement

When a user opens their book library without entering a search query, books are currently returned sorted by `UpdatedAt` descending — the most recently touched book appears first. This ordering is an import/edit artifact, not a browsing aid. For a growing library, finding a specific book by eye requires scanning an arbitrary order rather than navigating a predictable A→Z list. `BookLibrarySearchService.SearchSqlAsync` in `WebApp/Services/BookLibrarySearchService.cs` owns this sort and needs to change its no-query path from `.OrderByDescending(b => b.UpdatedAt)` to `.OrderBy(b => b.Title)`.

## User Stories

- Given I have imported several books, when I open my library with no search query, then books are displayed in ascending Title order (A→Z).
- Given a library of books with varied import dates, when I load the default library view, then the first card in the grid is the book whose title sorts earliest alphabetically.
- Given I type a search query, when results are returned, then the order remains by relevance (UpdatedAt or fuzzy similarity score) unchanged.

## Functional Requirements

1. FR1 — When `SearchSqlAsync` is called with a null or whitespace `query`, the returned `LibrarySearchResult.Books` list is ordered ascending by `Book.Title`.
2. FR2 — The sort change applies only to the no-query path; exact-match and fuzzy-search result ordering is unchanged.
3. FR3 — No new UI controls (sort toggles, dropdowns) are added; the alphabetical order is silent and always active for the default view.

## Non-Functional Requirements

- The single-line EF Core LINQ change (`.OrderBy(b => b.Title)`) must not widen the query or add a new database index; PostgreSQL can satisfy an `ORDER BY "Title"` on `book` without a dedicated index given typical library sizes.
- User data isolation is preserved: the `Where(b => b.UserId == userId)` predicate is unchanged.
- The change must be covered by the existing xUnit test class (`WebApp.Tests/Services/BookLibrarySearchServiceTests.cs`) and the PostgreSQL integration test class (`WebApp.Tests/Integration/BookLibrarySearchPostgresTests.cs`).

## Out of Scope

- A user-facing sort toggle (A→Z vs. recently updated) is not included.
- Sorting search results (non-empty query paths) alphabetically is not included; relevance ordering is preserved.
- Fuzzy search ordering is not changed.
- Any UI changes to `_BookLibraryResults.cshtml` or surrounding Razor views.

## Open Questions

None. All decisions were confirmed during discovery.
