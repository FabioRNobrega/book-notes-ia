# Validation: Chatterbox TTS Text Normalization POC

## Table of Contents

- [Acceptance Criteria](#acceptance-criteria)
- [Test Cases](#test-cases)
- [Manual Verification](#manual-verification)
- [Definition of Done](#definition-of-done)
- [Rollback Plan](#rollback-plan)

## Acceptance Criteria

| Requirement | Acceptance Criterion |
| --- | --- |
| FR1 | The documented Make command accepts `TTS=true TTS_LANG=en` and `TTS=true TTS_LANG=pt` and sends the selected values to the API. |
| FR2 | TTS mode with an empty value, `pt-BR`, `eng`, or another unsupported value fails clearly before output publication. |
| FR3 | A mismatch between EPUB metadata and `TTS_LANG` applies rules selected by `TTS_LANG`, with no fallback or override from EPUB metadata. |
| FR4 | An invocation without TTS mode produces byte-equivalent output for an unchanged synthetic fixture compared with the pre-feature renderer behavior. |
| FR5 | `POST /api/epubs/parse` binds filename, TTS opt-in, and TTS language while retaining all existing filename restrictions. |
| FR6 | Punctuation transformations exist only in the focused normalizer service; the parser, controller, writer, and Makefile contain no normalization rule set. |
| FR7 | The controller supplies a validated `TtsTextOptions` value to the normalizer after parsing and before publication. |
| FR8 | A standalone, whitespace-padded `***` paragraph is absent from TTS output and its surrounding paragraphs remain separately rendered. |
| FR9 | In `pt` mode, leading en/em dialogue dashes disappear while dialogue text and terminal punctuation are unchanged. |
| FR10 | In `pt` mode, an inline spaced dialogue dash becomes comma-space without prior terminal punctuation and becomes one space after `.`, `!`, `?`, or ellipsis. |
| FR11 | In `en` mode, the same leading and inline dash fixtures remain unchanged except for universal whitespace normalization. |
| FR12 | Three-or-more ASCII periods become a comma before following paragraph text or a period at paragraph end, without joining adjacent words. |
| FR13 | Repeated horizontal whitespace collapses, outer whitespace is trimmed, and distinct input paragraphs are not merged. |
| FR14 | TTS output remains UTF-8 without BOM, uses `Chapter N.` for `en` and `Capítulo N.` for `pt`, retains decimal numbering and ordered `chapter-NNN.txt` names, and leaves non-TTS headings as `Chapter N.`. |
| FR15 | TTS mode publishes only `data/output/<book-slug>/chapter-NNN.txt`; no raw sibling or `tts/` directory is created. |
| FR16 | A failed TTS normalization/publication leaves the bytes of a previously successful output unchanged and leaves no staging directory. |
| FR17 | The success response reports safe metadata and normalized character counts but contains no chapter prose; logs contain no prose. |
| FR18 | Focused automated tests exercise the Make/API contract, normalizer rules, default behavior, and rollback path. |
| FR19 | The parser README accurately documents the TTS POC command, accepted languages, normalization behavior, replacement semantics, and lack of Chatterbox synthesis. |

## Test Cases

**Unit tests:**

- `services/EbookParseService.Tests/TtsTextNormalizerTests.cs`
  - Removes only an exact trimmed standalone `***` paragraph and preserves surrounding paragraphs.
  - Does not remove asterisks embedded in prose or other unmatched content.
  - Removes leading en/em dialogue markers in `pt` while preserving words and punctuation.
  - Converts inline Portuguese dialogue dashes according to preceding terminal punctuation.
  - Preserves dialogue dashes in `en`.
  - Selects `Chapter` for `en` and `Capítulo` for `pt` independently of EPUB metadata.
  - Normalizes repeated ASCII periods at mid-paragraph and paragraph-end positions.
  - Collapses horizontal whitespace without merging paragraphs.
  - Rejects unsupported languages and does not mutate its input records.
- `services/EbookParseService.Tests/EpubControllerTests.cs`
  - Skips the normalizer when TTS is false or omitted.
  - Requires `ttsLanguage` when TTS is true.
  - Rejects values other than exact `en` and `pt` with sanitized HTTP 400 Problem Details.
  - Passes the explicit language to the normalizer even when EPUB metadata differs.
  - Publishes only the normalized book returned by the normalizer.
  - Does not invoke the writer when normalization fails.
- `services/EbookParseService.Tests/ChapterOutputWriterTests.cs`
  - Renders the supplied localized chapter label while retaining decimal numbering, deterministic UTF-8 rendering, normalized character counts, atomic replacement, and failure rollback.

**Command contract checks:**

- `make -n ebook-parse BOOK=fixture.epub TTS=true TTS_LANG=pt` shows JSON containing `tts: true` and `ttsLanguage: "pt"`.
- Missing or unsupported `TTS_LANG` in TTS mode exits nonzero with concise usage guidance.
- A default dry run does not enable TTS mode.

**Integration tests:**

- Run `make ebook-parser-test` to execute the isolated .NET 10 parser suite in Docker.
- Parse a synthetic EPUB through the running container in default and TTS modes and compare the single published destination.
- No PostgreSQL, Redis, Ollama, Microsoft Agent Framework, Supertonic, or Chatterbox integration test is required.

## Manual Verification

1. Put a legally obtained Portuguese EPUB in `services/EbookParseService.Api/data/input/`.
2. Preserve any existing output needed for comparison outside the deterministic book directory.
3. Run:

   ```bash
   make ebook-parse BOOK=book.epub TTS=true TTS_LANG=pt
   ```

4. Inspect `services/EbookParseService.Api/data/output/<book-slug>/chapter-001.txt` and confirm that it starts with `Capítulo 1.`, standalone `***` markers and Portuguese dialogue-marker dashes are normalized, and repeated periods follow the specified rule.
5. Confirm there is no second raw or `tts/` directory.
6. Run the same command without `TTS_LANG` and confirm it fails without changing the successful chapter files.
7. Run `make ebook-parse BOOK=book.epub` and confirm the existing non-TTS behavior remains available.
8. Optionally send a short, legally usable excerpt from the normalized output to the existing Chatterbox preview workflow and listen for punctuation regressions; audio generation is observational and not part of this POC's automated acceptance gate.

## Definition of Done

- Requirements, Plan, and Validation are updated in this spec folder.
- `TTS=true TTS_LANG=en|pt` is implemented and documented.
- The normalizer is a focused, injected, stateless C# service.
- All FRs have automated or explicit manual coverage.
- `make ebook-parser-test` passes in Docker.
- Existing non-TTS parser tests and behavior remain green.
- No copyrighted EPUB or generated chapter output is committed.
- No Chatterbox call, package, database, migration, new container, parallel output tree, or roadmap entry is introduced.
- Microsoft-specific design decisions remain supported by the official evidence linked in `Plan.md`.

## Rollback Plan

- Revert the request fields, controller coordination, normalizer registration/files, Make variables, tests, and README documentation together.
- Continue invoking `make ebook-parse BOOK=<book.epub>` without TTS fields; this is the preserved default path.
- No database, migration, external state, or Chatterbox change requires rollback.
- If a TTS parse produced undesirable text, rerun the default command for the same book; the writer atomically replaces the deterministic output directory with the regular chapter set.
