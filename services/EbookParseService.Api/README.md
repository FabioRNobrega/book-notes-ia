# EPUB Chapter Text Parser POC

This isolated .NET 10 API converts a supported local EPUB 2 or EPUB 3 publication into one UTF-8 text file per explicitly identified chapter. It does not call WebApp, a database, Supertonic, or Chatterbox.

## Run the POC

Place a legally obtained book in the private input folder, then run:

```bash
cp /your/local/path/book.epub services/EbookParseService.Api/data/input/book.epub
make ebook-parse BOOK=book.epub
```

To publish only text normalized for the Chatterbox POC, opt in with an explicit supported language:

```bash
make ebook-parse BOOK=book.epub TTS=true TTS_LANG=pt
make ebook-parse BOOK=book.epub TTS=true TTS_LANG=en
```

`TTS_LANG` is mandatory when `TTS=true` and accepts only the exact Chatterbox identifiers `en` and `pt`. It intentionally does not fall back to EPUB language metadata. TTS mode removes standalone `***` scene-marker symbols while retaining the paragraph boundary, normalizes repeated periods and horizontal whitespace, and applies Portuguese dialogue-dash punctuation rules for `pt`. English dialogue dashes are preserved.

Generated TTS headings write chapter numbers in natural cardinal words from 0 through 999. English uses forms such as `Chapter One.`, `Chapter Twenty-four.`, and `Chapter One hundred twenty-four.`; Portuguese uses forms such as `Capítulo Um.`, `Capítulo Vinte e quatro.`, `Capítulo Cem.`, and `Capítulo Cento e um.`. A chapter number outside that range safely retains invariant decimal digits. The numeric chapter identity and zero-padded filename remain unchanged, so `Chapter Twenty-four.` or `Capítulo Vinte e quatro.` is still published as `chapter-024.txt`.

TTS mode publishes only the normalized files to the same output directory, atomically replacing any previous chapter set. It does not create a raw copy or a `tts/` subdirectory. This step prepares plain text only; it does not call Chatterbox, choose a voice, split synthesis chunks, or generate audio.

If the parser reports a nonconforming EPUB media-type marker because the marker is missing or reordered, run:

```bash
make repair-ebook BOOK=book.epub
make ebook-parse BOOK=book-fixed.epub
```

The repair command runs in a one-off isolated container, leaves the source untouched, and refuses to overwrite an existing `book-fixed.epub`. It corrects only the EPUB ZIP marker and entry ordering. It does not convert EPUB 2 `toc.ncx` navigation, add EPUB 3 chapter semantics, remove DRM, or repair publication content.

The command builds and starts only `docker-compose.ebook-parser.yml`, waits for `GET /health`, and sends the base filename to `POST /api/epubs/parse`. Generated files appear under:

```text
services/EbookParseService.Api/data/output/<book-slug>/chapter-001.txt
```

Run the focused synthetic test suite with `make ebook-parser-test`, inspect service logs with `make ebook-parser-logs`, and stop the isolated service with `make ebook-parser-down`.

## Supported chapter discovery

The parser reads the EPUB media-type marker, `META-INF/container.xml`, the declared package document, manifest, and spine. It prefers an EPUB 3 navigation document; when none exists, it supports the EPUB 2 NCX document referenced by the spine's `toc` attribute.

EPUB 3 chapter order and boundaries come from table-of-contents fragment links. This supports several chapter targets inside one XHTML file.

A target is a chapter only if either:

- its structural section/article has `epub:type="chapter"`; or
- its TOC label is an Arabic number and its target section/heading contains the same number.

For EPUB 2 NCX, an entry is a chapter only when its label is `Chapter N` or plain `N`, its target is a manifest-declared XHTML document, and that document or fragment contains the matching numeric heading. Whole-document, fragment, and nested NCX targets are supported. Non-chapter NCX entries are ignored. If the package omits `dc:language`, every accepted chapter must have the same explicit in-scope `xml:lang`; language is never guessed from filenames or prose. XHTML `&nbsp;` is normalized locally without resolving an external DTD.

Part headings, title pages, coda entries, filenames, resource size, spine membership alone, and generic heading words are not used as guesses. An EPUB with no supported explicit structure or no reliable language fails without publishing output. EPUB 2 books with arbitrary named chapter labels, fixed-layout publications, malformed HTML recovery, PDFs, MOBI/AZW, OCR, and translation are outside this POC.

## Output and replacement

Without TTS mode, each file starts with `Chapter N.`. With TTS mode, the generated label and supported number are written in the explicit requested language. A blank line and normalized narrative paragraphs follow the heading. Markup, duplicate numeric headings, scripts, styles, navigation controls, media-only content, and footnote controls are excluded. A complete result is written to a sibling staging directory and then replaces `data/output/<book-slug>/` as a unit. If parsing, optional TTS normalization, or staging fails, the last successful output remains available.

## Privacy, copyright, and security

The entire `data/` tree is ignored by Git. Source books and extracted prose remain local and must not be committed, copied into tests, or pasted into logs. Only use publications you are legally permitted to process. API responses contain metadata, filenames, and character counts—not chapter prose.

The input filename is restricted to a base `.epub` name. Archive resource paths are normalized, XML is read with external resolution disabled, and configurable limits bound the source file, entry count, individual resource size, aggregate uncompressed size, and XML characters. Publications declaring encrypted content are rejected; the service does not remove DRM.

Configuration is under `EpubParser` in `appsettings.json` and can be overridden with environment variables. The container uses `/data/input` and `/data/output` through a single private bind mount.
