# Validation: Book Library Alphabetical Default Sort

## Table of Contents

- [Acceptance Criteria](#acceptance-criteria)
- [Test Cases](#test-cases)
- [Manual Verification](#manual-verification)
- [Definition of Done](#definition-of-done)
- [Rollback Plan](#rollback-plan)

## Acceptance Criteria

| Requirement | Acceptance Criterion |
| --- | --- |
| FR1 | Calling `SearchSqlAsync(query: null, userId)` returns books with `Title` values in ascending alphabetical order (i.e. `result.Books[0].Title` ≤ `result.Books[1].Title` ≤ … for all consecutive pairs). |
| FR2 | Calling `SearchSqlAsync(query: "some title", userId)` returns books ordered by `UpdatedAt` descending (exact match path) or by similarity score descending (fuzzy path), unchanged from the current behavior. |
| FR3 | The library grid rendered by `_BookLibraryResults.cshtml` contains no sort controls, dropdowns, or toggle UI. |

## Test Cases

**Unit tests (`WebApp.Tests/Services/BookLibrarySearchServiceTests.cs`):**

- Seed three `Book` rows with titles `"Zen and the Art of Motorcycle Maintenance"`, `"Anna Karenina"`, `"Brave New World"` with different `UpdatedAt` values that do not match alphabetical order. Assert that `SearchSqlAsync(null, userId)` returns them in the order: `Anna Karenina`, `Brave New World`, `Zen…`.
- Assert that an empty string query (`""`) also returns the same alphabetical order.
- Assert that passing a non-empty query (`"Anna"`) does not change the existing UpdatedAt-descending ordering.

**Integration tests (`WebApp.Tests/Integration/BookLibrarySearchPostgresTests.cs`):**

- ⚠️ TODO: Add or update a no-query test case against a real PostgreSQL instance that seeds books with mismatched `UpdatedAt` vs. alphabetical order and asserts `Title` A→Z in the result.

## Manual Verification

1. Start the stack: `make docker-run` (Linux/SteamOS) or the appropriate OS-specific target.
2. Log in and navigate to your library (home page, open the library panel).
3. Without typing a search query, confirm that the book grid shows books in ascending alphabetical Title order (A→Z from top-left to bottom-right, row by row).
4. Type a partial title in the search box and confirm results still appear in the existing relevance/UpdatedAt order and are not re-sorted alphabetically.
5. Clear the search and confirm the grid returns to alphabetical order.

## Definition of Done

- `BookLibrarySearchService.SearchSqlAsync` no-query branch uses `.OrderBy(b => b.Title)`.
- `BookLibrarySearchServiceTests` updated to assert alphabetical ordering on the no-query path.
- `BookLibrarySearchPostgresTests` has a passing no-query alphabetical sort integration test.
- All existing tests pass (`make test`).
- No UI changes were made to `_BookLibraryResults.cshtml` or any other Razor view.
- No new packages, migrations, or infrastructure were introduced.

## Rollback Plan

Revert the single-line change in `WebApp/Services/BookLibrarySearchService.cs`: restore `.OrderByDescending(b => b.UpdatedAt)` in the no-query branch and revert the corresponding test assertions. No migration, config flag, or infrastructure change is involved.
