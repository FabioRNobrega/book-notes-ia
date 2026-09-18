# Validation: EPUB 2 Numbered NCX Titles

## Acceptance Criteria

- `Chapter N` and plain `N` NCX fixtures retain their current behavior.
- `N. Title` labels with target headings beginning with the same number produce ordered parsed chapters.
- A different target-heading number fails as unsupported structure.
- Existing NCX path, duplicate, language, and no-chapter tests remain green.
- `TheLeanStartup-fixed.epub` parses through the existing Make command after container build.

## Verification

1. Run `make ebook-parser-test`.
2. Run `make ebook-parse BOOK=TheLeanStartup-fixed.epub`.
3. Confirm the response reports numbered chapters and the output folder contains `chapter-001.txt` onward.

## Rollback

Revert this spec, the parser-rule/test changes, and README wording together. Private EPUB input and generated output remain ignored and untouched.
