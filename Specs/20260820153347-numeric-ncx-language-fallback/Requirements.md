# Requirements: Numeric NCX and Chapter Language Fallback

## Problem Statement

The repaired Portuguese *Neuromancer* EPUB contains a valid title but omits package-level `dc:language`. Its spine references an EPUB 2 NCX whose 24 chapters are nested beneath part entries and labeled with plain Arabic numbers (`1` through `24`). Each chapter targets a manifest XHTML document whose matching numeric heading explicitly declares `xml:lang="pt-br"`. The parser must accept this explicit structure without guessing from the filename, prose, author, or every spine item.

## Functional Requirements

1. Package `dc:title` shall remain required.
2. A non-empty package `dc:language` shall remain authoritative for EPUB 2 and EPUB 3 parsing.
3. When package language is absent for an NCX publication, the parser shall require every accepted chapter target to resolve one non-empty in-scope `xml:lang` value.
4. All inferred chapter language values shall match case-insensitively; otherwise parsing shall fail as invalid EPUB metadata.
5. A missing language on any accepted chapter shall prevent language inference and fail parsing.
6. EPUB 3 publications without package language shall retain the existing invalid-metadata behavior; this fallback is limited to validated NCX chapters.
7. NCX chapter labels shall accept either the existing `Chapter N` form or the plain Arabic-number form `N`.
8. Plain numeric labels shall retain every existing safeguard: spine-referenced NCX, manifest XHTML membership, safe target resolution, matching numeric heading, unique number/target, non-empty prose, and declared-number output.
9. Nested numeric `navPoint` entries shall be processed in NCX document order while nonnumeric part, coda, front-matter, and back-matter entries remain excluded.
10. Tests shall cover successful inferred language and numeric labels plus missing, partial, and inconsistent `xml:lang` declarations.
11. The real local Portuguese EPUB shall produce exactly chapters 1 through 24 with inferred language `pt-br`.

## Non-Functional Requirements

- Language inference shall read only parsed, validated XML attributes via LINQ to XML; it shall not inspect prose or filenames.
- Existing bounded resolver-disabled `XmlReader` security configuration shall remain unchanged.
- No new package dependency or external language-detection service shall be added.
- Errors shall remain sanitized and shall not expose private content or internal paths.

## Out of Scope

- Statistical language detection or filename-based language inference.
- Accepting arbitrary named NCX chapters.
- Extracting part headings, prefaces, glossaries, notes, or other nonnumeric entries.
- Modifying the source EPUB metadata.

