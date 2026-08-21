# Plan: Numeric NCX and Chapter Language Fallback

## Summary

Allow NCX labels to use either `Chapter N` or `N`. Defer missing package language only for NCX publications, collect the effective `xml:lang` scope for every accepted chapter target, and use it only when all chapters explicitly agree.

## Technical Approach

- Split title and language validation in package reading. Keep title mandatory and retain an empty language value until the navigation kind is known.
- Reject missing package language immediately for EPUB 3.
- In NCX discovery, parse a chapter number through either the existing `Chapter N` regex or the existing plain numeric regex.
- Resolve effective language from the validated target/boundary using the nearest `xml:lang` on the element or its ancestors. For a structural fragment boundary, also inspect its matching numeric heading.
- Record one language for every accepted candidate. If package language is absent, require complete coverage and one case-insensitive distinct value before constructing the parsed book.
- Preserve the first explicit spelling (`pt-br` in the inspected publication) rather than guessing or applying culture-specific canonicalization.

## Evidence

- The inspected OPF has `dc:title` but no `dc:language`; its NCX contains 24 numeric labels and every matching target heading declares `xml:lang="pt-br"`.
- [.NET XML language scope](https://learn.microsoft.com/dotnet/api/system.xml.xmltextreader.xmllang?view=net-10.0) defines `xml:lang` as inherited scope. LINQ to XML ancestor traversal provides the equivalent tree query for the validated target.
- [LINQ to XML security](https://learn.microsoft.com/dotnet/standard/linq/linq-xml-security) recommends loading untrusted XML through a bounded configured `XmlReader`; the existing parser continues to do so.

## Files

- `services/EbookParseService.Api/Services/EpubChapterParser.cs`
- `services/EbookParseService.Tests/EpubChapterParserTests.cs`
- `services/EbookParseService.Tests/SyntheticEpubBuilder.cs`
- `README.md`
- `services/EbookParseService.Api/README.md`

