# Validation: TTS Spelled-Out Chapter Numbers POC

## Table of Contents

- [Acceptance Criteria](#acceptance-criteria)
- [Test Cases](#test-cases)
- [Manual Verification](#manual-verification)
- [Definition of Done](#definition-of-done)
- [Rollback Plan](#rollback-plan)

## Acceptance Criteria

| Requirement | Acceptance Criterion |
| --- | --- |
| FR1 | Publishing chapter 1 with explicit English TTS metadata begins `chapter-001.txt` with `Chapter One.`. |
| FR2 | Publishing chapter 1 with explicit Portuguese TTS metadata begins `chapter-001.txt` with `Capítulo Um.`. |
| FR3 | Converter theory data proves successful conversion for 0, 999, and representative boundaries between them in both languages. |
| FR4 | English cases assert `Zero`, `Eleven`, `Twenty`, `Twenty-four`, `One hundred`, `One hundred one`, and `Nine hundred ninety-nine` exactly. |
| FR5 | Portuguese cases assert `Zero`, `Onze`, `Vinte`, `Vinte e quatro`, `Cem`, `Cento e um`, and `Novecentos e noventa e nove` exactly. |
| FR6 | TTS writer tests confirm the localized label, one separating space, converted number, period, and blank-line paragraph boundary. |
| FR7 | For a converted chapter such as 24, the output file and response retain `chapter-024.txt`, numeric value 24, deterministic ordering, UTF-8 content, and the final rendered character count. |
| FR8 | A book with no explicit heading-number language still begins with `Chapter 24.` even if its EPUB metadata language is `en`, `pt`, or another value. |
| FR9 | A normalized book uses `TtsTextOptions.Language`; a deliberately conflicting `ParsedEpubBook.Language` and localized label do not change conversion selection. |
| FR10 | `TryConvert` returns `false` for -1, 1000, `pt-BR`, `eng`, empty, and null language inputs without producing a word. |
| FR11 | A writer fixture carrying TTS heading language and chapter 1000 publishes `Chapter 1000.` or `Capítulo 1000.` successfully instead of throwing. |
| FR12 | Normalizer tests prove the returned record carries `en` or `pt` heading-number language and the input record remains unchanged. |
| FR13 | Conversion logic exists behind `INumberToWordsConverter`; no number tables or language composition rules appear in the parser, controller, normalizer, or writer. |
| FR14 | Project/package and Compose diffs contain no new runtime dependency or service, and conversion tests run without external infrastructure. |
| FR15 | Existing writer rollback, staging replacement, stale-file cleanup, safe summary, and character-count tests continue to pass with the injected converter. |
| FR16 | The focused test suite covers converter rules and writer/normalizer integration for both languages, fallback, raw output, filenames, and counts. |
| FR17 | The parser README documents the 0–999 word behavior with English and Portuguese examples, numeric fallback, and numeric filename preservation. |

## Test Cases

**Unit tests:**

- `services/EbookParseService.Tests/NumberToWordsConverterTests.cs`
  - Use xUnit theories for English 0–19, round tens, compound tens, exact hundreds, hundreds with remainders, and 999.
  - Use xUnit theories for Portuguese 0–19, round tens, `e` compounds, `Cem` versus `Cento e ...`, all irregular hundred stems, and 999.
  - Assert sentence casing, English hyphens, absence of English `and`, and Portuguese conjunction spacing.
  - Assert `false` for negative, above-range, missing, and unsupported-language inputs.
- `services/EbookParseService.Tests/TtsTextNormalizerTests.cs`
  - Confirm `en` selects `Chapter` and heading-number language `en` despite conflicting EPUB metadata.
  - Confirm `pt` selects `Capítulo` and heading-number language `pt` despite conflicting EPUB metadata.
  - Confirm immutable normalization does not modify the source book.
- `services/EbookParseService.Tests/ChapterOutputWriterTests.cs`
  - Render exact English and Portuguese word headings for representative chapters including 1, 24, 100, 101, and 999.
  - Preserve raw/non-TTS `Chapter N.` when heading-number language is null.
  - Fall back to invariant digits for chapter 1000 with TTS heading language present.
  - Verify filenames and response chapter numbers remain numeric and zero-padded.
  - Verify response character count reflects the complete final word-based heading and prose.
  - Keep all existing atomic replacement, rollback, cleanup, UTF-8, validation, and safe-response cases passing.

**Integration tests:**

- Run `make ebook-parser-test` in the existing isolated Docker test container; no PostgreSQL, Redis, Ollama, Chatterbox, WebApp, or external network is needed.
- Run the full `make test` regression target so the existing WebApp, TTS service, and EPUB parser suites remain compatible.

## Manual Verification

1. Confirm a legally obtained English EPUB is present under `services/EbookParseService.Api/data/input/` and run:

   ```bash
   make ebook-parse BOOK=book.epub TTS=true TTS_LANG=en
   ```

2. Inspect representative generated files and confirm headings such as `Chapter One.` and `Chapter Twenty-four.`, while filenames remain `chapter-001.txt` and `chapter-024.txt`.
3. Run the same EPUB without TTS options:

   ```bash
   make ebook-parse BOOK=book.epub
   ```

4. Confirm the atomically replaced output uses decimal headings such as `Chapter 1.` and numeric filenames remain unchanged.
5. Repeat with a legally obtained Portuguese EPUB:

   ```bash
   make ebook-parse BOOK=book.epub TTS=true TTS_LANG=pt
   ```

6. Confirm headings such as `Capítulo Um.` and `Capítulo Vinte e quatro.` sound natural when passed through the downstream TTS POC, without requiring EPUB language metadata.
7. Run `make ebook-parser-test`, then `make test`.
8. Confirm `git status --short` lists no private `.epub`, extracted chapter text, generated CSS, `bin/`, or `obj/` content.

## Definition of Done

- `Requirements.md`, `Plan.md`, and `Validation.md` agree on exact English and Portuguese behavior, range, fallback, and non-TTS compatibility.
- `INumberToWordsConverter` and its deterministic 0–999 implementation are covered by focused xUnit theory data.
- TTS-normalized books carry explicit heading-number language without using EPUB metadata or label inference.
- Word conversion affects only the generated heading; numeric chapter identity, filenames, ordering, response data, and atomic publication remain unchanged.
- `make ebook-parser-test` and `make test` pass through Docker.
- The parser README documents the new behavior and its POC boundaries.
- No runtime package, container, database, UI, Microsoft Agent Framework, or external service is added.
- No private publication or extracted prose is committed or logged.
- `Specs/Roadmap.md` remains unchanged because the user classified this work as a standalone POC.

## Rollback Plan

- Revert the nullable heading-number language field, converter registration/files, normalizer propagation, writer formatting branch, related tests, and README text as one code change.
- The preserved default numeric representation means rollback requires no data migration, output-directory conversion, API change, or infrastructure action.
- Rerun `make ebook-parse BOOK=<book.epub>` without TTS options to atomically replace any word-heading output with the existing `Chapter N.` format.
