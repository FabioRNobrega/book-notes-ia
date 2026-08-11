# Requirements: Chatterbox Voice Versioning and Progress POC

## Problem Statement

The persistent-conditioning POC currently treats one language as one logical voice. Replacing `pt-reference.wav` therefore regenerates and overwrites the existing Portuguese `conditioning.pt`, metadata, and preview under the same UUID. In addition, `make chatterbox-preview` blocks for several minutes without visible progress, and WAV validation accepts structurally valid but silent audio. The POC must preserve every successfully created voice, make multiple voices per language usable, display meaningful generation progress, and reject silent inputs/outputs.

## User Stories

- Given one Portuguese voice already exists, when the reference WAV changes, then a second Portuguese voice is created without modifying the first.
- Given a stored voice ID, when a developer selects it, then synthesis uses that voice even if the current language reference slot contains another recording.
- Given a long CPU preview, when the developer runs the Make command, then visible stage and chunk percentages appear before completion.
- Given a silent or unusably short reference/output, when validation runs, then the operation fails without replacing a known-good voice or preview.

## Functional Requirements

1. FR1 — The change shall remain inside the isolated Chatterbox POC and shall not modify WebApp, Supertonic, the database, Microsoft Agent Framework, or the normal Compose stack.
2. FR2 — When the fixed reference checksum does not match an existing voice for the requested language, the service shall generate a new UUID rather than regenerate or overwrite another voice.
3. FR3 — Every newly published voice shall retain a private immutable copy of its source reference as `data/voices/<voice-id>/reference.wav` beside `conditioning.pt` and `metadata.json`.
4. FR4 — When the current fixed reference checksum matches exactly one stored voice, an unqualified preview shall reuse that voice; model/source/schema incompatibility shall regenerate only that same voice from its archived reference.
5. FR5 — The voice store shall permit multiple `en` or `pt` metadata records while rejecting duplicate records for the same language and reference checksum.
6. FR6 — `POST /preview` shall accept an optional canonical UUID `voice_id`; selected voices must exist and match the requested language, and synthesis shall use their archived reference/conditioning rather than the current reference slot.
7. FR7 — `GET /voices` shall return non-sensitive metadata for all stored voices, optionally filtered by `en` or `pt`, so preserved voice IDs can be discovered and selected.
8. FR8 — Outputs shall remain isolated at `data/outputs/<voice-id>/preview-<language>.wav`; creating another voice shall not replace earlier voice conditioning, archived references, metadata, or output.
9. FR9 — The service shall expose `GET /progress` with the active language, voice ID when known, stage, message, monotonic percentage, chunk index/count, running/completed/failed status, and concise error when applicable.
10. FR10 — Progress shall cover reference validation, voice resolution, conditioning creation/loading, each synthesis chunk, output validation/publication, completion, and failure. Chunk-level progress is sufficient; token-level model progress is out of scope.
11. FR11 — `make chatterbox-preview` shall use a container-local client that starts the preview request, polls `/progress`, immediately prints changed stages/percentages with flushing, and finally prints the result JSON, voice ID, and output path.
12. FR12 — The Make workflow shall continue supporting `LANGUAGE=en|pt` and shall accept optional `VOICE_ID=<uuid>`; add `make chatterbox-voices` with optional `LANGUAGE` filtering.
13. FR13 — Reference validation shall reject audio shorter than three seconds or with a near-silent PCM peak, and output validation shall reject near-silent PCM before atomic publication while preserving the last valid preview.
14. FR14 — Existing voice folders created by the previous POC shall remain valid. If their metadata checksum matches the current fixed reference, the service shall atomically backfill the archived `reference.wav`; it shall never guess or reconstruct an already overwritten historical voice.
15. FR15 — API responses, progress, logs, Make output, tests, and documentation shall clearly distinguish creating a new voice from rebuilding an existing voice and shall never expose audio/tensor contents.
16. FR16 — Automated tests shall cover multi-voice preservation, selection, listing, migration/backfill, progress monotonicity, client output, short/silent audio, near-silent output rollback, path containment, and existing persistence behavior without loading the real model.

## Non-Functional Requirements

- Progress state must be thread-safe and reflect the POC's single serialized inference operation.
- Stored references and conditionals remain sensitive, Git-ignored local artifacts.
- Archived references must be copied and atomically published; successful voice creation must not depend on the mutable root reference afterward.
- The implementation must not claim token-level percentages the Chatterbox API does not expose.
- No new runtime package or infrastructure service is required.

## Out of Scope

- Recovering the already overwritten Portuguese conditioning or previous reference without an external backup.
- Arbitrary HTTP uploads, browser UI, authentication, user ownership, premium integration, database storage, or deletion APIs.
- Multiple generated text-history versions for the same voice; rerunning one voice may replace that voice's preview WAV.
- Streaming audio, token-level model progress, GPU acceleration, or a dedicated Brazilian Portuguese model.
- Updating `Specs/Roadmap.md`; this remains an isolated POC.

## Open Questions

- None. New reference checksums create new voices; explicit voice selection keeps earlier voices usable.
