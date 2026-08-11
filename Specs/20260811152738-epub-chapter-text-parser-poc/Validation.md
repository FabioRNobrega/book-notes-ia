# Validation: EPUB Chapter Text Parser POC

## Table of Contents

- [Acceptance Criteria](#acceptance-criteria)
- [Test Cases](#test-cases)
- [Manual Verification](#manual-verification)
- [Definition of Done](#definition-of-done)
- [Rollback Plan](#rollback-plan)

## Acceptance Criteria

| Requirement | Acceptance Criterion |
| --- | --- |
| FR1 | The implementation contains `services/EbookParseService.Api/` targeting `net10.0`, and no `services/EbookParceService/` path remains. |
| FR2 | The parser project and isolated Compose file have no reference, dependency, network call, or volume shared with WebApp, PostgreSQL, Redis, Microsoft Agent Framework, Supertonic, or Chatterbox. |
| FR3 | `git check-ignore` confirms local input EPUBs, output text, temporary/backup publication directories, and the moved sample are ignored; no EPUB or extracted chapter is tracked. |
| FR4 | Valid base `.epub` filenames resolve under the input root, while absolute, nested, traversal, wrong-extension, and missing names return sanitized client errors. |
| FR5 | `make ebook-parse BOOK=<file.epub>` validates the local file, starts only the isolated parser, waits for health, posts JSON, and prints the returned chapter summary without multipart transfer. |
| FR6 | `GET /health` returns success and readiness without opening an EPUB or exposing content/path details. |
| FR7 | A correct first `mimetype` entry/value passes, while missing, reordered/nonconforming, or wrong-value media-type fixtures fail before XML chapter parsing. |
| FR8 | A fixture whose package document is not `EPUB/content.opf` parses via the path declared in `META-INF/container.xml`. |
| FR9 | Parser tests prove title, language, manifest, spine, and the navigation item are read from the package document with XML namespaces handled correctly. |
| FR10 | TOC links are emitted in navigation order and relative XHTML paths plus fragment identifiers resolve to the expected archive entries/elements. |
| FR11 | Semantic chapter targets and matching Arabic-numbered TOC/target headings are accepted; filename-, size-, heading-level-, and generic-keyword-only candidates are rejected. |
| FR12 | Synthetic title, part, TOC, and coda entries do not create chapter text files. |
| FR13 | A synthetic XHTML containing at least three chapters produces three isolated results with no prose leaking across structural boundaries. |
| FR14 | Chapter 1 writes `chapter-001.txt`, starts with `Chapter 1.`, contains a blank line, and chapter filenames follow navigation order with zero-padded numbers. |
| FR15 | Tests prove paragraphs and Unicode punctuation survive while duplicate headings, scripts, styles, navigation text, media-only content, and footnote backlinks are excluded. |
| FR16 | Empty, duplicate, missing, undeclared, malformed, and boundary-ambiguous fixtures fail with no published partial directory. |
| FR17 | A successful rerun replaces all old files without stale chapters; injected staging/replacement failure leaves the previous complete directory byte-for-byte intact and no temporary artifact remains. |
| FR18 | The success JSON contains safe source/book/output/chapter metadata and character counts but none of the synthetic chapter prose. |
| FR19 | Known failures return stable Problem Details statuses/details without absolute paths, XML snippets, narrative text, or stack traces. |
| FR20 | A fixture declaring protected publication content is rejected and produces no chapter output. |
| FR21 | Fixtures exceeding each configured source, count, entry, aggregate, or XML-character limit fail before publication. |
| FR22 | DTD/external-entity and oversized XML fixtures cannot resolve external resources or expand unbounded content and return sanitized invalid-EPUB errors. |
| FR23 | The test project creates all EPUB fixtures dynamically from synthetic prose and contains no `Neuromancer` excerpt, copied publication resource, or committed `.epub` fixture. |
| FR24 | Both new projects build in Docker; `make ebook-parser-test` passes the focused suite; `make test` passes WebApp, TTS, and EPUB parser suites. |
| FR25 | Both READMEs document input/output, EPUB rules, Make commands, replacement behavior, privacy/copyright, limitations, and lack of TTS integration. |

## Test Cases

**Parser unit tests:**

- `services/EbookParseService.Tests/EpubChapterParserTests.cs`: build a minimal EPUB 3 with a non-default OPF path and verify container/package discovery, metadata, manifest, spine, and TOC order.
- Store title/part entries and chapters 1–3 in one XHTML document; assert only the three numeric target sections become chapters and each contains only its own paragraphs.
- Add a semantic `epub:type="chapter"` target with a non-numeric title and verify the EPUB semantic route is supported independently of the numbered sample route.
- Add labels whose text looks chapter-like but whose target heading/fragment does not match; assert the parser rejects or excludes them rather than guessing.
- Cover relative URI normalization, percent encoding where standards-compliant, missing fragments, duplicate numbers/targets, undeclared XHTML, rooted/traversal hrefs, and ambiguous heading-only boundaries.
- Verify paragraph normalization, inline emphasis text, entities, Unicode punctuation, and exclusions for heading duplication, scripts, styles, nav, images, and footnote backlinks.
- Cover missing/wrong/reordered `mimetype`, invalid container, missing rootfile, malformed OPF/navigation/XHTML, no supported chapters, encryption declarations, corrupt ZIP data, and every configured resource limit.
- Include an external entity/DTD fixture and prove no resolver access occurs.

**Writer unit tests:**

- `services/EbookParseService.Tests/ChapterOutputWriterTests.cs`: verify deterministic slug directory, zero-padded filenames, UTF-8 content, `Chapter N.` prefix, blank-line paragraph boundaries, and response counts.
- Seed a prior output with extra stale chapters, publish a smaller successful result, and verify the result exactly matches the new chapter set.
- Inject write, validation, and replacement failures and prove the prior directory is restored byte-for-byte with no staging/backup residue.
- Verify invalid duplicate filenames, empty content, and unsafe slug/path material cannot escape the configured output root.

**Controller/API tests:**

- `services/EbookParseService.Tests/EpubControllerTests.cs`: fake `IEpubChapterParser`/writer outcomes and verify success metadata, cancellation, and categorized Problem Details mappings.
- Verify the API never serializes chapter text or internal exception/path details.
- Verify health does not invoke parsing services.

**Make/Docker integration checks:**

- Dry-run `make ebook-parse` with missing `BOOK`, a nested/traversal value, and a valid filename; verify validation and the isolated Compose/API commands.
- Run `docker compose -f docker-compose.ebook-parser.yml config` through the Make-managed environment and confirm `/data` is the only private bind mount and no application services are dependencies.
- Run `make ebook-parser-test` and then `make test`.
- Run the real sample only as a manual ignored-data validation; automated tests must not depend on it.

## Manual Verification

1. Confirm the source publication is legally available for local use and that `git check-ignore services/EbookParseService.Api/data/input/Neuromancer.epub` identifies the parser data ignore rule.
2. Place the local file at `services/EbookParseService.Api/data/input/Neuromancer.epub` after the implementation renames/restructures the folder.
3. Run `make ebook-parse BOOK=Neuromancer.epub` on SteamOS/Linux and confirm the isolated container becomes healthy and reports title `Neuromancer`, language `en-US`, and 24 chapters.
4. List `services/EbookParseService.Api/data/output/neuromancer/` and confirm it contains exactly `chapter-001.txt` through `chapter-024.txt`, with no title or part files.
5. Open chapters 1, 7, 8, 13, and 24. Confirm each starts with the correct `Chapter N.` sentence, retains readable paragraph breaks, and does not contain prose from the preceding/following chapter.
6. Confirm `PART ONE`, `PART TWO`, `PART THREE`, `PART FOUR`, and `CODA` navigation headings were not emitted as separate files or duplicated into chapter prose.
7. Temporarily copy the successful output aside, rerun the same command, and confirm the deterministic directory/file set is replaced cleanly without duplicates or stale files.
8. Run the command with an unsupported EPUB structure or deliberately invalid local test archive and confirm the prior Neuromancer output remains intact and the API returns a concise error.
9. Run `git status --ignored --short services/EbookParseService.Api/data` and confirm the input book, extracted text, staging directories, and backups are ignored and no copyrighted prose is staged.
10. Confirm no Chatterbox, Supertonic, WebApp, database, Redis, Ollama, or Microsoft Agent Framework container is started or called by the parser command.

## Definition of Done

- Requirements, Plan, and Validation are present in this spec folder and reflect all confirmed discovery answers.
- `services/EbookParseService.Api/` replaces the misspelled experimental path and targets .NET 10.
- The sample produces exactly 24 navigation-derived chapter files with TTS-oriented text and no part/title files.
- Unsupported structures fail rather than using filename/spine/heading guesses.
- Input/output data is private and ignored; automated fixtures are synthetic and copyright-safe.
- Archive/XML input limits, path validation, safe XML readers, encrypted-content rejection, sanitized Problem Details, and atomic rollback are implemented and tested.
- `make ebook-parse`, `make ebook-parser-test`, and the expanded `make test` pass through Docker/Make.
- Service/root documentation explains usage and limitations.
- WebApp, database, Microsoft Agent Framework, and both TTS implementations remain unchanged.
- `Specs/Roadmap.md` remains unchanged because the user explicitly classifies this as a POC.

## Rollback Plan

- Stop/remove only the isolated parser project with `make ebook-parser-down`.
- Revert the new parser API/test projects, isolated Compose file, Make targets, solution/test-runner registrations, README entries, and parser-specific `.gitignore` rules.
- Do not delete the user's local source EPUB or extracted chapter directory during code rollback; move the private `data/` tree outside the repository first if removing the renamed service folder.
- No database migration, WebApp registration, Microsoft Agent Framework configuration, or TTS route requires rollback because none is changed.
