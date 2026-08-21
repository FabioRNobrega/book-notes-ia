# Validation: EPUB 2 NCX Chapter Support

## Acceptance Criteria

- Existing EPUB 3 fixtures retain their current results.
- A synthetic spine-referenced NCX with `Chapter 1` and `Chapter 2` whole-document targets produces chapters numbered 1 and 2 with isolated prose.
- Non-chapter NCX entries are ignored and do not need to be manifest XHTML resources.
- NCX fragment targets remain supported when their target heading matches the declared number.
- Missing/invalid spine NCX declarations, missing `navMap`, incomplete chapter entries, undeclared targets, unsafe paths, missing fragments, heading mismatches, empty prose, duplicate numbers, and duplicate targets return sanitized unsupported/invalid EPUB errors without output.
- The local repaired *The Room* publication parses to exactly 65 files, from `chapter-001.txt` through `chapter-065.txt`.

## Verification

1. Run `make ebook-parser-test`.
2. Run `make test` for the full Dockerized regression suite.
3. Run `make ebook-parse BOOK=TheRoom-fixed.epub`.
4. Confirm the response reports title `The Room`, language `en`, and 65 chapters.
5. Confirm the ignored output directory contains exactly `chapter-001.txt` through `chapter-065.txt` and no front-matter files.
6. Confirm no staging/backup directory remains.

## Rollback

Revert the parser, synthetic fixture, tests, and documentation changes. Leave private EPUB inputs and generated output untouched.
