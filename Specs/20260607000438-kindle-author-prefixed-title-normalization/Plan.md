# Plan: Kindle Author-Prefixed Title Normalization

## Summary

Clean author-prefixed Kindle titles inside `KindleClippingsImportService.TryParseBlock` for display, while preserving the raw Kindle title in `Book.SourceBookTitle` as the stable import identity. This lets users edit `Book.Title` without breaking future Kindle re-upload dedupe.

## Technical Approach

Add a compiled regex to `WebApp/Services/KindleClippingsImportService.cs` that matches a leading prefix ending in ` - ` and captures the remaining title. `TryParseBlock` should keep both values:

- `SourceBookTitle` - raw title from the Kindle header before cleanup.
- `Title` - cleaned display title after author-prefix stripping.

Change `ImportAsync` so the book map and note dedupe keys use the normalized source title, not the editable display title:

```csharp
BuildBookLookupKey(parsedBook.NormalizedSourceBookTitle, parsedBook.NormalizedAuthor)
```

The source lookup normalization should apply the author-prefix cleanup before `NormalizeKey(...)`. That means `SourceBookTitle` still stores the raw Kindle value, while the lookup treats `Galactic Pot-Healer` and `Dick, Philip K - Galactic Pot-Healer` as the same import source title.

Add an EF Core migration that adds required `SourceBookTitle` to `book`, backfills existing rows from `Title`, and indexes `{ UserId, SourceBookTitle, NormalizedAuthor }` for source-aware import lookup. The index must not be unique because historical duplicate rows may already exist before this migration. `Book.Title` stays editable and remains the value used by UI, context generation, and newly created `BookEmbedding.Title`.

## Component Breakdown

**Existing files to modify:**

- `WebApp/Models/Book.cs` - add required `SourceBookTitle`.
- `WebApp/Data/AppDbContext.cs` - configure `SourceBookTitle` with the same max length as `Title` and add the source-title index.
- `WebApp/Services/KindleClippingsImportService.cs` - add the regex and title cleanup helper; preserve raw source title; use source-title lookup for import dedupe and note dedupe.
- `WebApp.Tests/Services/KindleClippingsImportServiceTests.cs` - add tests for persisting source/display titles, preventing duplicate books after display-title edits, and preventing duplicate books when re-uploading a prefixed title.
- `Specs/Roadmap.md` - list this spec and the inline title editing spec created in this session.

**New files to create:**

- `WebApp/Migrations/<timestamp>_AddBookSourceTitle.cs` - EF Core migration for `SourceBookTitle`.

## Validation

- Run `make test`.
- Confirm service tests cover both prefixed and non-prefixed source title paths.
- Confirm re-upload after editing `Book.Title` does not create a duplicate.

## Risk Assessment

| Risk | Mitigation |
| --- | --- |
| A legitimate title containing ` - ` could be over-trimmed | Only strip a leading prefix when there is non-empty text on both sides of the first spaced dash separator. The raw value is still preserved in `SourceBookTitle`. |
| Existing historical prefixed rows remain in the database | Backfill `SourceBookTitle` from current `Title`; broader historical cleanup remains out of scope. |
| Exact raw source title index may not catch all source variants | Import code normalizes the source title with prefix cleanup before lookup, preserving duplicate prevention even when Kindle alternates source formats. |
