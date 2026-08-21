# Plan: EPUB 2 NCX Chapter Support

## Summary

Extend package navigation discovery with an EPUB 2 NCX fallback while keeping EPUB 3 navigation preferred. Split chapter discovery by navigation format, reuse common candidate validation/output extraction, and add a narrowly constrained NCX route for `Chapter N` entries backed by matching numeric XHTML headings.

## Technical Approach

- Extend package metadata with a navigation kind and path. Resolve EPUB 3 `properties="nav"` first; only when absent, resolve the spine `toc` attribute to a manifest NCX item.
- Parse NCX through the existing secure, bounded `LoadXml` method. Select one `navMap`, enumerate descendant `navPoint` elements in document order, and read direct label/content children so nested navigation remains deterministic.
- Skip non-`Chapter N` entries before resolving their resources. For chapter entries, normalize and validate the NCX URI against the navigation path and XHTML manifest set.
- For whole-document targets, locate a unique matching numeric heading and use it as the extraction boundary. For fragment targets, resolve the fragment and reuse numeric-boundary validation.
- If strict resolver-disabled XML parsing fails on XHTML `&nbsp;`, perform one bounded retry after replacing only that entity with `&#160;`; keep DTD processing ignored and `XmlResolver` null.
- Reuse duplicate number/target checks and paragraph extraction. Populate each output chapter with its declared NCX number.
- Extend the synthetic builder with an EPUB 2 package, NCX, and multiple separate XHTML resources; add positive and negative unit tests.

## Standards and Platform Evidence

- The [IDPF OPF 2.0.1 specification](https://idpf.org/epub/20/spec/OPF_2.0_latest.htm) defines the spine `toc` reference to the manifest NCX item and describes NCX as the EPUB 2 declarative navigation structure.
- The [DAISY Z39.86 NCX specification](https://daisy.org/activities/standards/daisy/daisy-3/z39-86-2005-r2012-specifications-for-the-digital-talking-book/) defines `navPoint`, `navLabel`, and `content src`, including nested navigation points.
- [.NET `XmlReader.Create`](https://learn.microsoft.com/dotnet/api/system.xml.xmlreader.create?view=net-10.0) documents configured readers and recommends controlling external resource access with `XmlResolver`; the existing parser sets it to `null`, bounds characters, and ignores DTDs, so NCX can safely reuse that path.

## Files

- `services/EbookParseService.Api/Services/EpubChapterParser.cs`
- `services/EbookParseService.Tests/SyntheticEpubBuilder.cs`
- `services/EbookParseService.Tests/EpubChapterParserTests.cs`
- `README.md`
- `services/EbookParseService.Api/README.md`

## Risks

- Broad NCX acceptance could misclassify front matter. Mitigation: require `Chapter N`, manifest XHTML membership, and a matching numeric heading.
- Whole-document targets do not provide a fragment boundary. Mitigation: require a unique matching heading and extract only content after it.
- Some valid EPUB 2 books use named chapter labels. They remain unsupported until a future spec defines equally reliable evidence.
