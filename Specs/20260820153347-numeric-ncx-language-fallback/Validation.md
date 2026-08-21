# Validation: Numeric NCX and Chapter Language Fallback

## Acceptance Criteria

- Existing EPUB 3 and `Chapter N` NCX fixtures retain their behavior.
- A numeric-label NCX with no package language and consistent chapter `xml:lang="pt-br"` produces chapters 1 and 2 with language `pt-br`.
- Missing language on all or only some accepted chapters fails.
- Conflicting explicit chapter languages fail.
- Nonnumeric parent/peer entries remain excluded.
- The real `Neuromancer-pt-fixed.epub` reports 24 chapters and produces exactly `chapter-001.txt` through `chapter-024.txt`.

## Verification

1. Run `make ebook-parser-test`.
2. Run `make ebook-parse BOOK=Neuromancer-pt-fixed.epub`.
3. Verify response title, inferred language, count, complete file sequence, and absence of staging/backup directories.
4. Run `make test`.

## Rollback

Revert the parser, tests, builder, documentation, and this spec while leaving all ignored private EPUBs and generated chapter output untouched.
