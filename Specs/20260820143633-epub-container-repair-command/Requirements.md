# Requirements: EPUB Container Repair Command

## Problem Statement

Some locally obtained EPUB files contain the correct `mimetype` marker but were repackaged with that entry after other ZIP entries. The chapter parser correctly rejects these archives before reading publication metadata. Developers need a Docker-first Make command that repairs only the EPUB container packaging while preserving the original private file.

## Functional Requirements

1. `make repair-ebook BOOK=<book.epub>` shall repair a source located directly under `services/EbookParseService.Api/data/input/`.
2. The command shall accept only the same safe base `.epub` filenames accepted by `ebook-parse` and shall reject missing files and symbolic-link sources.
3. The repaired filename shall be `<source-stem>-fixed.epub` in the same input directory.
4. The command shall never modify or delete the source and shall refuse to overwrite an existing repaired destination.
5. The repaired ZIP shall contain `mimetype` as its first entry, stored without compression, with the exact content `application/epub+zip`.
6. All other archive entries and their content shall be retained, except that an existing `mimetype` entry is replaced by the conforming marker.
7. The command shall reject invalid ZIP archives, duplicate paths, symbolic links, and rooted, traversal-based, or backslash-containing entry paths before extraction.
8. Work shall occur in a unique temporary directory under the private input tree and shall be cleaned on success or failure.
9. The repair shall run in the isolated EPUB Compose image so developers do not need host ZIP utilities.
10. Documentation shall explain that this repairs container packaging only; it does not upgrade EPUB 2 navigation or repair publication structure.

## Non-Functional Requirements

- The source and repaired publications remain ignored private data.
- The command must work through the repository's Make-managed Docker/Podman environment.
- Expected validation failures must produce concise messages and a nonzero exit status.

## Out of Scope

- EPUB 2 NCX-to-EPUB 3 navigation conversion.
- DRM removal, malformed XHTML recovery, chapter inference, or content modification.
- Overwriting or deleting any existing user publication.
