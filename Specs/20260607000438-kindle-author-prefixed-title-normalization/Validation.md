# Validation: Kindle Author-Prefixed Title Normalization

## Acceptance Criteria

| Requirement | Acceptance Criterion |
| --- | --- |
| FR1 | A clipping header title like `Dick, Philip K - Galactic Pot-Healer` is displayed as `Galactic Pot-Healer`. |
| FR2 | The persisted `Book.SourceBookTitle` stores `Dick, Philip K - Galactic Pot-Healer`. |
| FR3 | Import lookup and note dedupe use the normalized source title, so a re-upload after editing `Book.Title` leaves one `Book` row. |
| FR4 | The persisted `Book.Title`, `Book.NormalizedTitle`, and `BookEmbedding.Title` use the cleaned title. |
| FR5 | Editing a book's displayed title and re-uploading the original Kindle clipping does not insert a duplicate book. |
| FR6 | Importing `Galactic Pot-Healer` and later importing `Dick, Philip K - Galactic Pot-Healer` for the same author/user leaves one `Book` row. |
| FR7 | Importing a normal title like `Dune` continues to persist and display `Dune`. |

## Test Cases

- `ImportAsync_WithAuthorPrefixedTitle_PersistsSourceTitleCleanTitleAndEmbeddingTitle`
- `ImportAsync_WhenDisplayTitleWasEdited_ReuploadMatchesSourceTitleAndDoesNotInsertDuplicateBook`
- `ImportAsync_WhenExistingCleanTitleReuploadedWithAuthorPrefix_DoesNotInsertDuplicateBook`
- Existing import tests continue to pass for normal titles.

## Manual Verification

1. Start the app with `make docker-run` on Linux/SteamOS.
2. Upload a clipping file containing `Dick, Philip K - Galactic Pot-Healer (Philip K. Dick)`.
3. Confirm the library displays `Galactic Pot-Healer` and the `book` row stores `SourceBookTitle = 'Dick, Philip K - Galactic Pot-Healer'`.
4. Upload the same file again.
5. Confirm there is still one book row for that user/title.

## Definition of Done

- Spec files exist under `Specs/20260607000438-kindle-author-prefixed-title-normalization/`.
- `Book.SourceBookTitle` and its EF migration are implemented.
- `KindleClippingsImportService` stores the raw source title and uses normalized source-title lookup for import dedupe.
- Tests cover source title persistence, cleaned display persistence, duplicate prevention after title edits, and prefixed re-upload prevention.
- `make test` passes.

## Rollback Plan

- Remove `SourceBookTitle` from `Book`, `AppDbContext`, and the migration.
- Remove the source-title lookup changes from `KindleClippingsImportService`.
- Remove the added importer tests.
- Revert `Specs/Roadmap.md` entries for this work if needed.
