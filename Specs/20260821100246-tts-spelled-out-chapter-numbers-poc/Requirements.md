# Requirements: TTS Spelled-Out Chapter Numbers POC

## Table of Contents

- [Problem Statement](#problem-statement)
- [User Stories](#user-stories)
- [Functional Requirements](#functional-requirements)
- [Non-Functional Requirements](#non-functional-requirements)
- [Out of Scope](#out-of-scope)
- [Open Questions](#open-questions)

## Problem Statement

The isolated EPUB parser currently renders decimal chapter numbers even when its opt-in TTS normalization mode localizes the generated label, producing headings such as `Chapter 1.` and `Capítulo 1.`. Although a synthesizer may pronounce digits, the TTS-ready text should make the intended English or Portuguese reading explicit and deterministic. When a developer runs `make ebook-parse BOOK=book.epub TTS=true TTS_LANG=en|pt`, generated chapter headings need natural cardinal words while parser identity, chapter ordering, filenames, non-TTS output, and atomic publication remain unchanged. This follow-up POC intentionally supersedes only the decimal-heading portion of FR14 and the matching number-conversion exclusion in `Specs/20260820160524-chatterbox-tts-text-normalization-poc/Requirements.md`.

## User Stories

- Given chapter 1 and `TTS=true TTS_LANG=en`, when the EPUB is published, then its text heading is `Chapter One.` while its filename remains `chapter-001.txt`.
- Given chapter 24 and `TTS=true TTS_LANG=pt`, when the EPUB is published, then its text heading is `Capítulo Vinte e quatro.` while its filename remains `chapter-024.txt`.
- Given the same EPUB without TTS mode, when it is published, then its heading remains `Chapter 24.` and all existing default behavior is preserved.
- Given a chapter number above the POC's word-conversion range, when TTS output is published, then the heading safely retains invariant decimal digits instead of failing the complete book.

## Functional Requirements

1. FR1 — The existing command `make ebook-parse BOOK=<book.epub> TTS=true TTS_LANG=en` shall render each generated chapter heading with its integer number written as an English cardinal word.
2. FR2 — The existing command `make ebook-parse BOOK=<book.epub> TTS=true TTS_LANG=pt` shall render each generated chapter heading with its integer number written as a Portuguese cardinal word.
3. FR3 — The word converter shall support every integer from 0 through 999 for both exact language identifiers `en` and `pt`, even though published EPUB chapter numbers remain subject to the existing positive-number validation.
4. FR4 — English output shall use natural sentence-case cardinal forms: 0–19 words, hyphenated compound tens such as `Twenty-four`, and hundreds without the optional conjunction `and`, such as `One hundred twenty-four`.
5. FR5 — Portuguese output shall use natural sentence-case cardinal forms with `e` conjunctions where required, including `Vinte e quatro`, exact 100 as `Cem`, 101 as `Cento e um`, and the irregular hundred forms through 900.
6. FR6 — TTS headings shall retain the existing localized labels and terminal punctuation, producing the exact shape `Chapter <EnglishWords>.` for `en` and `Capítulo <PortugueseWords>.` for `pt`.
7. FR7 — Chapter filenames, `ParsedChapter.Number`, response chapter numbers, chapter ordering, zero padding, output slug, UTF-8 encoding, and published character-count calculation shall remain numeric and otherwise unchanged.
8. FR8 — When TTS mode is absent or false, headings shall retain the existing invariant-decimal form `Chapter N.`; EPUB metadata shall not activate number-to-word conversion.
9. FR9 — Heading word selection shall use only the already validated explicit `TTS_LANG`; the implementation shall not infer language by comparing `ChapterLabel`, inspecting EPUB metadata, or examining chapter prose.
10. FR10 — For a number below 0, above 999, or an unsupported language, the focused converter shall report that it cannot convert the value rather than returning an incorrect word.
11. FR11 — If a published TTS chapter number cannot be converted under FR10, heading rendering shall fall back to `number.ToString(CultureInfo.InvariantCulture)` and shall not fail or partially publish the book.
12. FR12 — The existing `TtsTextNormalizer` shall carry the explicit TTS heading-number language into the normalized book representation without mutating the source `ParsedEpubBook`.
13. FR13 — Number conversion shall be owned by a focused C# service separate from `TtsTextNormalizer`, `EpubController`, `EpubChapterParser`, and filesystem publication mechanics.
14. FR14 — The feature shall add no runtime NuGet package, network call, language model call, database access, or new container.
15. FR15 — The existing staging, replacement, rollback, stale-file removal, and safe-response guarantees shall remain in effect for the complete final rendered chapter set.
16. FR16 — Focused automated tests shall cover all lexical boundaries and composition rules in both languages, TTS heading integration, numeric fallback, unchanged non-TTS output, numeric filenames, and final character counts.
17. FR17 — The EPUB parser README shall document spelled-out TTS headings, the 0–999 conversion range, natural language examples, numeric fallback, and unchanged numeric filenames.

## Non-Functional Requirements

- Conversion shall be deterministic, culture-independent, synchronous, and free of mutable shared state.
- The converter shall be safe for concurrent use by the existing ASP.NET Core request pipeline.
- Language word tables and composition rules shall remain readable and independently unit-testable; chapter formatting rules shall not be embedded in controller actions or EPUB discovery code.
- Synthetic test data shall be used; no private EPUB prose or generated chapter content shall be committed or logged.
- This standalone POC shall not add a phase to `Specs/Roadmap.md`, as confirmed during discovery on 2026-08-21.

## Out of Scope

- Languages other than exact `en` and `pt`.
- Ordinal headings such as `Chapter First` or `Capítulo Primeiro`.
- Numbers above 999, negative numbers, decimals, Roman numerals, or parsing number words from source EPUB headings.
- Renaming `chapter-NNN.txt` files or changing chapter identifiers, order, discovery, extraction, or EPUB validation.
- Changing the TTS command/API contract, punctuation normalization, Chatterbox synthesis, voices, chunking, or audio generation.
- Choosing regional English variants that insert `and` into hundreds or applying Portuguese grammatical gender to cardinal chapter numbers.

## Open Questions

- None. Discovery decisions were confirmed on 2026-08-21.
