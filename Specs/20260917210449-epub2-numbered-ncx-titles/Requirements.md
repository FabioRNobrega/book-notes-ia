# Requirements: EPUB 2 Numbered NCX Titles

## Problem Statement

The isolated EPUB parser recognizes EPUB 2 NCX chapters only when their labels are `Chapter N` or plain `N`. A valid EPUB 2 publication can instead use numbered titles such as `1. Start`; those entries are skipped even when their XHTML target begins with the same chapter number. The parser must accept this constrained form without treating arbitrary named NCX entries as chapters.

## User Stories

- Given an EPUB 2 NCX entry labeled `1. Start` whose XHTML heading begins `1 START`, when parsed, then chapter 1 is published.
- Given a numbered NCX label whose XHTML heading starts with another number, when parsed, then parsing fails with sanitized unsupported-structure details.

## Functional Requirements

1. FR1 — EPUB 2 NCX discovery shall continue to accept `Chapter N` and plain `N` labels.
2. FR2 — EPUB 2 NCX discovery shall accept a positive Arabic number followed by punctuation or whitespace and non-empty title text, such as `1. Start`.
3. FR3 — A numbered-title NCX entry shall qualify only when its declared number matches the leading Arabic number of its target heading.
4. FR4 — For a qualifying whole-document NCX target, chapter prose shall include subsequent XHTML spine resources until the next qualifying whole-document NCX target.
5. FR5 — Existing NCX manifest, path, duplicate, language, non-empty prose, and output-number safeguards shall remain unchanged.
6. FR6 — Parser documentation shall describe numbered-title support and retain arbitrary unnumbered labels as unsupported.

## Non-Functional Requirements

- No package, external parser, or infrastructure dependency shall be added.
- XML remains parsed through the existing bounded resolver-disabled reader.
- Errors remain sanitized and source EPUB content remains local.

## Out of Scope

- Spelled-out, localized, or unnumbered chapter labels.
- Filename-based chapter inference.
- EPUB 2-to-EPUB 3 conversion or source-publication repair beyond the existing ZIP marker command.
