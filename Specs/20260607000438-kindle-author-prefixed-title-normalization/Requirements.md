# Requirements: Kindle Author-Prefixed Title Normalization

## Problem Statement

Some Kindle clipping exports include the author name inside the title field, such as `Dick, Philip K - Galactic Pot-Healer (Philip K. Dick)`. The importer currently persists that full title and uses the editable display `Book.Title` for normalized lookup keys, so a later export can create duplicates when Kindle restores a source title after the user has edited the displayed title. The upload flow must preserve a stable raw Kindle source title, strip the leading `Author - ` prefix for the user-facing title, and dedupe imports against the source identity instead of the editable display title.

## Functional Requirements

1. **FR1** - `KindleClippingsImportService` must detect a leading author/title separator in the parsed title using a regex and remove the prefix before assigning the user-facing `Book.Title`.
2. **FR2** - `Book` must include a required `SourceBookTitle` column that stores the raw Kindle title before author-prefix stripping is applied.
3. **FR3** - `KindleClippingsImportService` must use `SourceBookTitle` as the stable import identity. Import book lookup keys and note dedupe keys must be based on the normalized source title, not the editable `Book.Title`.
4. **FR4** - The displayed `Book.Title`, `Book.NormalizedTitle`, and `BookEmbedding.Title` must use the cleaned title so the user sees `Galactic Pot-Healer`, not `Dick, Philip K - Galactic Pot-Healer`.
5. **FR5** - Re-uploading a clipping for an existing source title must not insert another `Book` row after the user has edited the displayed title.
6. **FR6** - Re-uploading a clipping for an existing clean title must not insert another `Book` row when the new clipping title contains an author prefix.
7. **FR7** - The parser must preserve displayed titles that do not contain an author prefix.

## Non-Functional Requirements

- User-owned data remains scoped by `UserId`.
- No new runtime packages or infrastructure are required.
- Existing embedding and note import transaction behavior must be preserved.
- `Book.Title` remains user-editable and continues to be the value shown in the library, book details, chat/context generation, and title embeddings for newly imported books.

## Out of Scope

- Changing author parsing.
- Regenerating existing stored titles outside a new upload.
- Adding a migration or background cleanup job for historical prefixed titles.
- Replacing context generation or Microsoft Agent Framework lookup behavior.

## Open Questions

None. The raw Kindle title is stored in `SourceBookTitle`; import matching normalizes that source value and applies the author-prefix cleanup to preserve the previous duplicate prevention behavior when Kindle alternates between clean and prefixed title forms.
