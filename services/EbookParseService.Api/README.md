# EPUB Chapter Text Parser POC

This isolated .NET 10 API converts a local EPUB 3 publication into one UTF-8 text file per explicitly identified chapter. It does not call WebApp, a database, Supertonic, or Chatterbox.

## Run the POC

Place a legally obtained book in the private input folder, then run:

```bash
cp /your/local/path/book.epub services/EbookParseService.Api/data/input/book.epub
make ebook-parse BOOK=book.epub
```

The command builds and starts only `docker-compose.ebook-parser.yml`, waits for `GET /health`, and sends the base filename to `POST /api/epubs/parse`. Generated files appear under:

```text
services/EbookParseService.Api/data/output/<book-slug>/chapter-001.txt
```

Run the focused synthetic test suite with `make ebook-parser-test`, inspect service logs with `make ebook-parser-logs`, and stop the isolated service with `make ebook-parser-down`.

## Supported chapter discovery

The parser reads the EPUB media-type marker, `META-INF/container.xml`, the declared package document, manifest, spine, and the EPUB 3 navigation document. Chapter order and boundaries come from table-of-contents fragment links. This supports several chapter targets inside one XHTML file.

A target is a chapter only if either:

- its structural section/article has `epub:type="chapter"`; or
- its TOC label is an Arabic number and its target section/heading contains the same number.

Part headings, title pages, coda entries, filenames, resource size, and generic heading words are not used as guesses. An EPUB with no supported explicit structure fails without publishing output. EPUB 2 NCX-only books, fixed-layout publications, malformed HTML recovery, PDFs, MOBI/AZW, OCR, and translation are outside this POC.

## Output and replacement

Each file starts with `Chapter N.`, a blank line, and normalized narrative paragraphs. Markup, duplicate numeric headings, scripts, styles, navigation controls, media-only content, and footnote controls are excluded. A complete result is written to a sibling staging directory and then replaces `data/output/<book-slug>/` as a unit. If parsing or staging fails, the last successful output remains available.

## Privacy, copyright, and security

The entire `data/` tree is ignored by Git. Source books and extracted prose remain local and must not be committed, copied into tests, or pasted into logs. Only use publications you are legally permitted to process. API responses contain metadata, filenames, and character counts—not chapter prose.

The input filename is restricted to a base `.epub` name. Archive resource paths are normalized, XML is read with external resolution disabled, and configurable limits bound the source file, entry count, individual resource size, aggregate uncompressed size, and XML characters. Publications declaring encrypted content are rejected; the service does not remove DRM.

Configuration is under `EpubParser` in `appsettings.json` and can be overridden with environment variables. The container uses `/data/input` and `/data/output` through a single private bind mount.
