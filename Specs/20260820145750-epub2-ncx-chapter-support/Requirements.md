# Requirements: EPUB 2 NCX Chapter Support

## Problem Statement

The isolated EPUB chapter parser requires an EPUB 3 XHTML navigation document. Older publications can instead declare an NCX table of contents through the OPF spine. A repaired local copy of *The Room* follows this older structure and has 65 ordered `Chapter N` NCX entries, each targeting a separate manifest XHTML document with a matching numeric heading. The parser must support this explicit structure without falling back to filenames or treating every spine resource as a chapter.

## Functional Requirements

1. The parser shall continue to prefer and support its existing EPUB 3 `properties="nav"` navigation flow without behavior regressions.
2. When no EPUB 3 navigation item exists, the parser shall resolve the OPF spine `toc` identifier to exactly one manifest item with media type `application/x-dtbncx+xml`.
3. A missing, unknown, or incorrectly typed spine NCX reference shall return an unsupported-structure error.
4. NCX chapter discovery shall read `navMap`/`navPoint` entries in document order and use each direct `navLabel` text and direct `content src` target.
5. Only NCX labels in the explicit `Chapter N` form shall qualify for this fallback; cover, contents, dedication, author information, and other entries shall be ignored.
6. A qualifying NCX target shall resolve to a manifest-declared XHTML resource inside the archive.
7. Both whole-document NCX targets and targets containing fragments shall be supported.
8. A whole-document target shall qualify only when the target XHTML contains a matching numeric heading; its narrative boundary begins after that heading.
9. A fragment target shall qualify only when the resolved element or its supported structural boundary contains the matching numeric heading.
10. Duplicate chapter numbers or duplicate targets, missing content targets, missing fragments, mismatched headings, unsafe paths, and empty chapter prose shall fail without publishing partial output.
11. NCX entries shall produce `ParsedChapter` numbers from their declared `Chapter N` labels, yielding deterministic `chapter-001.txt`, `chapter-002.txt`, and subsequent output names.
12. Tests shall use synthetic non-copyrighted EPUB fixtures and cover successful whole-document NCX chapters plus invalid NCX references, labels, targets, headings, and duplicates.
13. Documentation shall describe EPUB 2 NCX support and its constrained `Chapter N`/matching-heading rule.
14. XHTML using the common `&nbsp;` entity from an external XHTML DTD shall be normalized to its numeric character reference without resolving or loading the external DTD.

## Non-Functional Requirements

- NCX and XHTML shall use the existing bounded `XmlReader` path with DTD processing ignored, external resolution disabled, and document character limits enforced.
- NCX resources shall remain subject to existing archive count, per-entry, aggregate-size, normalized-path, manifest, and encryption checks.
- No third-party EPUB/XML package shall be added.
- Existing atomic output replacement and sanitized Problem Details behavior shall remain unchanged.

## Out of Scope

- Treating arbitrary NCX labels or every OPF spine item as a chapter.
- Inferring chapter identity from filenames such as `chapter_001.html` without matching NCX and heading evidence.
- Converting the publication to EPUB 3 or modifying source EPUB contents.
- Fixed-layout EPUBs, malformed HTML recovery, DRM removal, PDFs, MOBI/AZW, OCR, or translation.
