# Requirements: EPUB Chapter Text Parser POC

## Table of Contents

- [Problem Statement](#problem-statement)
- [User Stories](#user-stories)
- [Functional Requirements](#functional-requirements)
- [Non-Functional Requirements](#non-functional-requirements)
- [Out of Scope](#out-of-scope)
- [Open Questions](#open-questions)

## Problem Statement

The repository has an untracked `services/EbookParceService/data/Neuromancer.epub` experiment but no application capable of turning an EPUB publication into narration-ready chapter text. The sample demonstrates why treating each XHTML resource or spine item as a chapter is incorrect: `EPUB/text/ch002.xhtml` contains chapters 1 through 7, while `EPUB/nav.xhtml` identifies their actual targets with fragment links. The POC needs an isolated Dockerized .NET 10 API that reads a local EPUB from a private data folder, follows EPUB 3 container, package, spine, and navigation metadata, extracts only explicitly identifiable chapters, and atomically writes one UTF-8 text file per chapter without connecting to Chatterbox, WebApp, or a database.

## User Stories

- Given a valid EPUB 3 publication in the local input folder, when a developer runs the Make parse command, then the service writes one ordered text file for every explicitly identified chapter.
- Given the sample publication whose numbered chapters share larger XHTML files, when it is parsed, then chapter boundaries follow navigation fragment targets rather than physical XHTML filenames.
- Given chapter 1, when its output is opened, then it starts with `Chapter 1.` followed by clean paragraph text suitable for later TTS synthesis.
- Given an EPUB whose chapter structure cannot be established from EPUB semantics or the agreed numbered navigation format, when parsing is requested, then the API reports an unsupported-structure error and writes no guessed output.
- Given a previously successful output and a later parse failure, when the failure occurs, then the previous complete chapter directory remains intact.

## Functional Requirements

1. FR1 — The existing experimental folder shall be corrected from `services/EbookParceService/` to the isolated `services/EbookParseService.Api/` .NET 10 Web API boundary.
2. FR2 — The POC shall remain independent of WebApp, PostgreSQL, Redis, Microsoft Agent Framework, Supertonic, and `ChatterboxTtsService`; no runtime call or shared persistence shall connect them.
3. FR3 — Source EPUBs shall be placed manually under `services/EbookParseService.Api/data/input/`, and generated text shall be written under `services/EbookParseService.Api/data/output/`; both private trees shall be ignored by Git, including the existing `Neuromancer.epub` and generated copyrighted text.
4. FR4 — `POST /api/epubs/parse` shall accept a JSON request containing only an input base filename and shall reject absolute paths, directory separators, traversal segments, missing files, non-`.epub` extensions, and paths that resolve outside the configured input directory.
5. FR5 — A Make target shall build/start the isolated parser container, wait for readiness, and invoke the parse endpoint with `BOOK=<file.epub>`; the normal input shall remain a local bind-mounted file rather than a multipart upload.
6. FR6 — The service shall expose a lightweight health endpoint that confirms process readiness without reading a book or returning private content.
7. FR7 — The parser shall open the EPUB as a read-only ZIP stream and validate the EPUB `mimetype` entry and exact `application/epub+zip` value before processing publication XML.
8. FR8 — The parser shall read `META-INF/container.xml` to locate the package document rather than assuming `EPUB/content.opf` or another fixed package path.
9. FR9 — The package reader shall parse the EPUB title, language, manifest, spine order, and the manifest item whose properties identify the EPUB 3 navigation document.
10. FR10 — Chapter discovery shall use the EPUB navigation document's `nav` element with `epub:type="toc"` and resolve its ordered links, relative resource paths, and fragment identifiers within the archive.
11. FR11 — A navigation target shall qualify as a chapter only when its target or structural ancestor is explicitly marked with EPUB chapter semantics, or when its navigation label and resolved target heading use the agreed Arabic-numbered chapter form demonstrated by the sample; arbitrary filenames, resource sizes, heading levels, and broad keyword guesses shall not create chapters.
12. FR12 — Part, title-page, table-of-contents, coda heading, and other non-chapter navigation entries shall not become chapter text files unless they independently satisfy the explicit chapter rule.
13. FR13 — The parser shall support multiple chapter targets inside one XHTML content document and shall stop each extraction at its structural section boundary or the next validated chapter target, without copying adjacent chapters.
14. FR14 — Each output shall be deterministically named `chapter-NNN.txt`, ordered by the navigation document, and begin with `Chapter N.` followed by a blank line and that chapter's narrative content.
15. FR15 — Text normalization shall preserve paragraph boundaries and meaningful Unicode punctuation while removing markup, duplicated numeric headings, scripts, styles, navigation controls, image-only content, and footnote backlink controls; it shall collapse incidental inline whitespace without merging separate paragraphs.
16. FR16 — Empty chapters, duplicate chapter numbers, missing navigation targets, targets outside declared XHTML manifest resources, malformed required XML/XHTML, and ambiguous chapter boundaries shall fail the parse instead of publishing partial or guessed output.
17. FR17 — Successful output shall be staged in a temporary sibling directory and atomically replace the deterministic `data/output/<book-slug>/` directory as one complete set; failure shall remove temporary artifacts and preserve the last successful directory.
18. FR18 — The success response shall report source filename, EPUB title, language, output directory, chapter count, and per-chapter number, filename, and character count, but shall not return or log chapter text.
19. FR19 — Expected invalid-input, invalid-EPUB, encrypted/DRM, unsupported-structure, and conflicting-output conditions shall use concise ASP.NET Core Problem Details responses without internal paths, XML fragments, archive contents, or stack traces.
20. FR20 — The parser shall reject encrypted or DRM-protected publication content for this POC with a clear unsupported-content response rather than attempting to decrypt it.
21. FR21 — Configurable limits shall bound source file size, archive entry count, individual XML/XHTML uncompressed size, and aggregate declared uncompressed size before content parsing to reduce path traversal, ZIP bomb, XML entity, and resource-exhaustion risks.
22. FR22 — Required EPUB XML/XHTML shall be parsed through a configured `XmlReader` with external resolution disabled, DTD content ignored or prohibited safely for EPUB XHTML, and document character limits applied before building LINQ to XML trees.
23. FR23 — Automated tests shall construct synthetic EPUB fixtures at test time, including several numbered chapters in one XHTML resource, and shall not copy, embed, snapshot, or assert excerpts from `Neuromancer.epub`.
24. FR24 — The new API and test projects shall be added to `book-notes-ia.sln`, the isolated parser tests shall run through Docker, and the repository `make test` regression workflow shall include them.
25. FR25 — Service and root README documentation shall describe local input/output ownership, supported EPUB 3 structures, Make usage, deterministic replacement, limitations, copyright/privacy expectations, and the absence of Chatterbox integration.

## Non-Functional Requirements

- The service must target .NET 10 and run only through Docker/Make in the repository's supported development workflow.
- The controller must coordinate HTTP concerns only; EPUB/archive parsing and atomic output publication must live behind narrow injectable interfaces suitable for xUnit fakes.
- The implementation should use .NET platform libraries (`System.IO.Compression`, `System.Xml`, and `System.Xml.Linq`) and must not add an EPUB or HTML parsing package unless implementation proves a standards-compliant fixture cannot be handled safely with those APIs.
- Archive resources must be read selectively from streams; the implementation must not extract an entire EPUB filesystem to a temporary directory.
- Output must be stable and deterministic for the same source publication, except for response timing or diagnostic identifiers.
- Logs may contain safe book metadata, chapter numbers, counts, and timings, but not narrative text or unchecked archive/request paths.
- The source EPUB and extracted chapters are local copyrighted content and must never be committed as application or test fixtures.

## Out of Scope

- Multipart HTTP uploads, a browser upload UI, authenticated user ownership, database persistence, and cloud storage.
- Calling Chatterbox, Supertonic, or generating audio from the chapter text.
- EPUB 2 publications that provide only NCX navigation, fixed-layout EPUBs, comics, PDFs, MOBI/AZW files, malformed HTML recovery, and OCR.
- DRM removal, decryption, password handling, or bypassing publisher protections.
- Translating chapter content or localizing the fixed `Chapter N.` prefix.
- Guessing chapters from filenames such as `ch001.xhtml`, file sizes, page counts, or every spine item.
- Extracting front matter, title pages, copyright pages, dedications, part headings, appendices, or coda headings as separate files unless a future spec defines them.
- WebApp, book library, Kindle clipping import, or product-roadmap integration.
- Updating `Specs/Roadmap.md`; the user explicitly keeps this work as an isolated POC.

## Open Questions

- None. The POC uses a corrected service name, local-folder input, explicit EPUB-oriented chapter discovery without broad guessing, atomic replacement, and no Roadmap entry.
