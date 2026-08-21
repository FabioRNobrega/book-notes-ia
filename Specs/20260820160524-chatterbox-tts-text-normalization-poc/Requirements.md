# Requirements: Chatterbox TTS Text Normalization POC

## Table of Contents

- [Problem Statement](#problem-statement)
- [User Stories](#user-stories)
- [Functional Requirements](#functional-requirements)
- [Non-Functional Requirements](#non-functional-requirements)
- [Out of Scope](#out-of-scope)
- [Open Questions](#open-questions)

## Problem Statement

The isolated EPUB parser currently preserves punctuation that is useful for reading but can produce unreliable narration when its chapter files are passed to Chatterbox. In the observed Portuguese output, standalone `***` scene separators and en-dash dialogue notation may be pronounced or paced incorrectly, while repeated periods and inconsistent whitespace may produce unstable pauses. The POC needs an explicit opt-in mode that uses a caller-supplied `en` or `pt` language, applies deterministic C# text normalization after EPUB extraction, and atomically publishes only the resulting Chatterbox-ready chapter files in the existing output location.

## User Stories

- Given a supported EPUB, when a developer runs `make ebook-parse BOOK=book.epub TTS=true TTS_LANG=pt`, then the existing output directory contains only Portuguese-normalized chapter files suitable for sending to Chatterbox.
- Given the same command without `TTS=true`, when parsing completes, then the parser retains its existing non-TTS output behavior.
- Given TTS mode without a supported explicit language, when the command or API request is evaluated, then it fails clearly without relying on the EPUB language and without replacing prior output.
- Given Portuguese dialogue written with leading and inline dashes, when TTS normalization runs, then dialogue and attribution remain separate and readable without raw dialogue-marker dashes.
- Given a standalone `***` scene separator, when TTS normalization runs, then the symbols are absent while the surrounding paragraphs remain separate.
- Given Portuguese TTS mode, when a chapter is published, then its generated heading starts with `Capítulo` instead of the hardcoded English `Chapter` label.

## Functional Requirements

1. FR1 — The `ebook-parse` Make target shall accept `TTS=true` and `TTS_LANG=en|pt`; the documented TTS invocation shall be `make ebook-parse BOOK=<book.epub> TTS=true TTS_LANG=<en|pt>`.
2. FR2 — When `TTS=true`, `TTS_LANG` shall be mandatory and limited to the exact Chatterbox language identifiers `en` and `pt`; a missing or unsupported value shall fail with a concise invalid-request error before any output is published.
3. FR3 — TTS language selection shall come only from `TTS_LANG`; EPUB package or chapter language metadata shall not override or supply the TTS normalization language.
4. FR4 — When TTS mode is absent or false, the existing API, parsing, rendering, response, and output behavior shall remain unchanged.
5. FR5 — `ParseEpubRequest` and `POST /api/epubs/parse` shall carry the TTS opt-in and optional language fields while preserving the existing base-filename input boundary.
6. FR6 — TTS normalization shall be implemented behind a focused C# service interface, separate from `EpubChapterParser`, `EpubController`, and `ChapterOutputWriter`; request coordination shall not place punctuation rules in the controller or Makefile.
7. FR7 — The TTS normalizer shall receive an explicit `TtsTextOptions` value containing the validated requested language and shall operate on the already extracted chapter paragraphs before publication.
8. FR8 — For both supported languages, a paragraph whose trimmed content is exactly `***` shall be removed as spoken content while its preceding and following narrative paragraphs remain distinct; no asterisk shall appear in the published TTS output for that separator.
9. FR9 — For `pt`, a leading en dash (`–`) or em dash (`—`) used as a dialogue marker shall be removed with adjacent whitespace normalized, without deleting the following dialogue text or its terminal punctuation.
10. FR10 — For `pt`, a spaced inline en dash or em dash between dialogue and following narration shall become a comma and one space when the preceding dialogue has no terminal `.`, `!`, `?`, or ellipsis; when terminal punctuation is already present, the dash shall be removed and exactly one separating space retained.
11. FR11 — For `en`, dialogue-dash-specific transformations from FR9 and FR10 shall not run; English punctuation other than the agreed universal transformations shall be preserved.
12. FR12 — For both languages, a run of three or more ASCII periods shall be normalized to one pause mark without deleting or concatenating its adjacent words: a comma when more text follows in the same paragraph, or a period when the run ends the paragraph.
13. FR13 — For both languages, incidental horizontal whitespace introduced or exposed by normalization shall collapse to one space, leading and trailing whitespace shall be removed, and paragraph boundaries shall remain separate.
14. FR14 — Chapter numbering, UTF-8-without-BOM encoding, filenames, and output slug shall be preserved in TTS mode; generated headings shall be `Chapter N.` for `en` and `Capítulo N.` for `pt`, while non-TTS output shall remain `Chapter N.`.
15. FR15 — TTS-mode output shall use the existing `data/output/<book-slug>/chapter-NNN.txt` location and shall be the only published version; no parallel raw or `tts/` output tree shall be created.
16. FR16 — The existing staging, replacement, rollback, and stale-file removal guarantees shall apply to the complete normalized chapter set, so normalization failure preserves the last successfully published output.
17. FR17 — Normalization shall not log, return through the API, or otherwise expose chapter prose; the existing success response may continue to report only safe metadata and per-file character counts calculated from the published normalized text.
18. FR18 — Focused automated tests shall cover request validation, Make command forwarding, language dispatch, every agreed punctuation transformation, unchanged non-TTS behavior, and preservation of prior output on failure.
19. FR19 — The parser README shall document TTS mode, mandatory explicit languages, output replacement behavior, supported transformations, and the distinction between text preparation and Chatterbox audio synthesis.

## Non-Functional Requirements

- The normalizer shall be deterministic, culture-independent, and free of network, model, database, filesystem, and Chatterbox dependencies.
- The normalizer shall be stateless and safe for singleton registration in the existing ASP.NET Core dependency-injection container.
- The design shall preserve single responsibility: parsing discovers and extracts chapters, normalization prepares text, the writer publishes files, and the controller only coordinates the request.
- The POC shall add no runtime NuGet package or new container.
- Tests shall use synthetic prose and punctuation fixtures; copyrighted chapter text shall remain ignored local data and shall not be committed.

## Out of Scope

- Calling Chatterbox, generating audio, choosing a voice, or changing `ChatterboxTtsService`.
- SSML, timed pause metadata, speaker detection, voice switching, or dialogue attribution analysis.
- Sentence or paragraph chunking for model input limits.
- Languages other than exact `en` and `pt` identifiers.
- Linguistic rewriting, translation, spelling correction, or replacing every dash regardless of context.
- Converting chapter numbers into language-specific words such as `One` or `Um`; headings retain decimal digits.
- Persisting both raw and TTS-normalized chapter versions.
- Changing EPUB chapter discovery, EPUB metadata validation, repair behavior, or archive security limits.
- Adding this isolated POC to `Specs/Roadmap.md`.

## Open Questions

- None. Discovery decisions were confirmed on 2026-08-20.
