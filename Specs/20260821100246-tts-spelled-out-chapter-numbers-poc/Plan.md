# Plan: TTS Spelled-Out Chapter Numbers POC

## Table of Contents

- [Summary](#summary)
- [Technical Approach](#technical-approach)
- [Component Breakdown](#component-breakdown)
- [Dependencies](#dependencies)
- [Microsoft Learn Evidence](#microsoft-learn-evidence)
- [Flow](#flow)
- [Risk Assessment](#risk-assessment)

## Summary

Extend the existing opt-in EPUB TTS normalization path with a deterministic English/Portuguese cardinal-number converter for generated headings. TTS output will say `Chapter One.` or `Capítulo Um.` while the parser's integer model, numeric filenames, non-TTS rendering, and atomic writer behavior remain unchanged.

## Technical Approach

Add a narrow `INumberToWordsConverter` with a `TryConvert(int number, string? language, out string words)` contract and a stateless `NumberToWordsConverter` implementation. The converter will compose values rather than store 1,000 complete strings: language-specific lookup tables cover units, teens, tens, and hundreds; small private methods apply English hyphenation and Portuguese `e` conjunction rules. The supported domain is exactly 0–999. Invalid values or languages return `false`, keeping range policy explicit and allowing the renderer's safe numeric fallback. No culture-sensitive parsing, reflection, localization resource lookup, or external package is needed.

`ParsedEpubBook` will gain nullable explicit heading-number language metadata, defaulting to `null` so all existing parser construction and non-TTS behavior remains source-compatible. `TtsTextNormalizer.Normalize` already owns selection from validated `TtsTextOptions`; it will set both the existing `ChapterLabel` and the new heading-number language in the returned immutable record. It will not perform linguistic number composition itself. This preserves the rule that TTS language comes only from `TTS_LANG` and avoids brittle inference from `ChapterLabel` or EPUB metadata.

`ChapterOutputWriter` remains responsible for final text rendering and filesystem publication. It will depend on `INumberToWordsConverter`, ask for word conversion only when the book carries an explicit heading-number language, and otherwise use `chapter.Number.ToString(CultureInfo.InvariantCulture)`. A failed `TryConvert` also uses invariant decimal digits. The existing filename expression `chapter-{chapter.Number:000}.txt`, summary number, ordering, UTF-8 rendering, staging validation, replacement, rollback, cleanup, and character counts remain untouched. Conversion therefore cannot abort publication for an out-of-range future chapter number.

`Program.cs` will register the focused converter behind its interface. The implementation contains immutable lookup data and no request or scoped dependency, so it is safe to share under the service's existing singleton-oriented registration pattern. `TtsTextNormalizer` continues to be the controller-facing normalization dependency; `EpubController`, `ParseEpubRequest`, `TtsTextOptions`, and the Make command require no contract change.

This follows SOLID boundaries: `TtsTextNormalizer` owns TTS preparation and explicit language propagation, `INumberToWordsConverter` owns cardinal-language composition, and `ChapterOutputWriter` owns final document rendering and atomic publication. The converter can be exhaustively unit-tested without filesystem or HTTP setup, while writer tests verify only integration between number selection and rendered output. No EF Core, MVC UI, Microsoft Agent Framework, cache, database, or user-owned data path participates.

## Component Breakdown

**Existing files to modify:**

- `services/EbookParseService.Api/Models/ParsedEpubBook.cs` — add nullable explicit heading-number language metadata whose default preserves non-TTS decimal headings.
- `services/EbookParseService.Api/Services/TtsTextNormalizer.cs` — propagate the validated `en` or `pt` language into the immutable normalized book alongside `ChapterLabel`.
- `services/EbookParseService.Api/Services/ChapterOutputWriter.cs` — inject the converter, select word or invariant-decimal heading text, and retain numeric filenames and atomic publication.
- `services/EbookParseService.Api/Program.cs` — register the converter behind its focused interface.
- `services/EbookParseService.Api/README.md` — document the TTS word range, examples, fallback, and unchanged filenames.
- `services/EbookParseService.Tests/TtsTextNormalizerTests.cs` — verify explicit heading-number language propagation and unchanged source data.
- `services/EbookParseService.Tests/ChapterOutputWriterTests.cs` — verify English and Portuguese word headings, non-TTS decimals, above-range fallback, numeric filenames, and final character counts.

**New files to create:**

- `services/EbookParseService.Api/Services/INumberToWordsConverter.cs` — narrow conversion contract with non-throwing unsupported-input reporting.
- `services/EbookParseService.Api/Services/NumberToWordsConverter.cs` — deterministic English and Portuguese cardinal composition for 0–999.
- `services/EbookParseService.Tests/NumberToWordsConverterTests.cs` — boundary, irregular-form, conjunction, hyphenation, range, and unsupported-language coverage.

No controller, request model, Makefile, Docker Compose, migration, UI, Microsoft Agent Framework, Chatterbox, or runtime-package change is required.

## Dependencies

- The existing `TTS=true TTS_LANG=en|pt` request path and validated `TtsTextOptions` value.
- The existing isolated `ebook-parser` .NET 10 container and `make ebook-parser-test` workflow.
- The existing ASP.NET Core dependency-injection container and xUnit test infrastructure.
- No external runtime, model, network, database, or additional package dependency.

## Microsoft Learn Evidence

- [Service lifetimes in .NET dependency injection](https://learn.microsoft.com/dotnet/core/extensions/dependency-injection/service-lifetimes#singleton) states that singleton services must be thread-safe and are often appropriate for stateless services. The planned converter has immutable lookup data, no mutable state, and no scoped dependency, which supports registration within the isolated API's existing singleton service pattern.

The repository's .NET 10, constructor-injection, immutable-record, and Docker-only execution constraints are preserved. No discrepancy with the retrieved Microsoft guidance was identified.

## Flow

```mermaid
sequenceDiagram
    actor Developer
    participant Makefile
    participant Controller as EpubController
    participant Parser as IEpubChapterParser
    participant Normalizer as ITtsTextNormalizer
    participant Writer as ChapterOutputWriter
    participant Converter as INumberToWordsConverter
    participant Output as data/output/book/chapter-NNN.txt

    Developer->>Makefile: ebook-parse BOOK=book.epub TTS=true TTS_LANG=pt
    Makefile->>Controller: POST tts=true, ttsLanguage=pt
    Controller->>Parser: ParseAsync(fileName)
    Parser-->>Controller: ParsedEpubBook with integer chapters
    Controller->>Normalizer: Normalize(book, TtsTextOptions("pt"))
    Normalizer-->>Controller: label=Capítulo, heading language=pt
    Controller->>Writer: PublishAsync(normalized book)
    Writer->>Converter: TryConvert(chapter.Number, "pt")
    Converter-->>Writer: Vinte e quatro
    Writer->>Output: Write Capítulo Vinte e quatro; keep chapter-024.txt
    Writer-->>Developer: Safe numeric chapter summary
```

## Risk Assessment

| Risk | Evidence | Mitigation |
| --- | --- | --- |
| Portuguese hundreds are grammatically incorrect. | Portuguese distinguishes exact `cem` from compositional `cento` and has irregular hundred names such as `quinhentos`. | Encode explicit hundred stems, special-case exact 100, and test every hundred boundary plus remainders. |
| English output sounds unnatural or varies by region. | English permits regional forms such as “one hundred and twenty-four.” | Use the discovery-approved neutral form without `and`, hyphenate compound tens, and assert exact examples. |
| Heading language is inferred from display text or unreliable EPUB metadata. | The existing TTS spec requires exact caller-supplied `TTS_LANG`, while `ParsedEpubBook.Language` may differ. | Carry explicit nullable heading-number language from `TtsTextOptions`; never parse `ChapterLabel` or reuse EPUB language. |
| Adding display words changes stable chapter identity. | `ChapterOutputWriter` currently uses `ParsedChapter.Number` for ordering, filenames, summaries, and heading text. | Replace only the heading-number rendering expression; keep every identity and filename path integer-based and cover them in writer tests. |
| A future book has a chapter above 999. | The POC intentionally bounds linguistic support, but parser validation currently has no 999 maximum. | Return `false` outside the converter range and render invariant decimal digits without failing publication. |
| Shared service state causes cross-request corruption. | The isolated API registers its parser, normalizer, and writer as singletons. | Keep converter tables immutable and conversion locals method-scoped; verify no mutable cache or per-request state is introduced. |
| The new POC unintentionally changes raw output. | `ParsedEpubBook` is constructed throughout parser and tests without TTS metadata. | Default heading-number language to `null` and assert existing `Chapter N.` behavior separately. |
