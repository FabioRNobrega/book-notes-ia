# Requirements: Chatterbox Audiobook Batch Generation POC

## Table of Contents

- [Problem Statement](#problem-statement)
- [User Stories](#user-stories)
- [Functional Requirements](#functional-requirements)
- [Non-Functional Requirements](#non-functional-requirements)
- [Out of Scope](#out-of-scope)
- [Open Questions](#open-questions)

## Problem Statement

`EbookParseService.Api` can publish an ordered set of TTS-normalized chapter files under `services/EbookParseService.Api/data/output/<book>/`, while `ChatterboxTtsService` can preserve a selected voice and synthesize only its fixed preview text. There is no single local command that consumes a prepared book folder, adds a required introduction and outro, reuses one Chatterbox voice across the complete run, and publishes one reliably ordered WAV per part. The POC needs a resumable Docker-first batch workflow that creates a playable audiobook folder under `/home/deck/Music` by default without involving the WebApp or exposing arbitrary-text synthesis over HTTP.

## User Stories

- Given a prepared parser output folder containing an introduction, contiguous numbered chapters, and an outro, when the developer runs `make create-audio-book` with the required book and voice values, then one English WAV is generated for every part in introduction/chapter/outro order.
- Given a book with more than nine parts, when synthesis completes, then every output uses a three-digit sequence so filesystem and player ordering remain stable.
- Given an interrupted or partially failed generation, when the developer reruns the same command, then completed compatible tracks are skipped and generation resumes at the first missing, changed, or invalid track.
- Given an existing audiobook generated with the same project inputs, when the developer runs the command with `FORCE=true`, then every expected track is regenerated.
- Given invalid paths, missing parts, incompatible manifest state, or an unavailable voice, when the developer runs the command, then it fails with a concise diagnostic without replacing a previously valid track with invalid audio.

## Functional Requirements

1. FR1 — The `Makefile` shall expose `create-audio-book` with required `BOOK`, `BOOK_NAME`, `TTS_LANG`, `VOICE_ID`, `BOOK_INTRO`, and `BOOK_OUTRO` values; `AUDIOBOOK_ROOT` shall default to `/home/deck/Music`, and `FORCE` shall default to `false`.
2. FR2 — The command shall accept only `TTS_LANG=en` and shall always use the pinned Chatterbox Multilingual V3 model, regardless of an ambient `CHATTERBOX_MODEL` value.
3. FR3 — `BOOK` shall identify one direct child folder under `services/EbookParseService.Api/data/output`; it shall not accept an absolute path, parent traversal, or nested path.
4. FR4 — `BOOK_INTRO` and `BOOK_OUTRO` shall each identify a distinct, non-empty UTF-8 `.txt` file directly inside the selected `BOOK` folder and shall be required for every run.
5. FR5 — The batch runner shall discover files matching `chapter-NNN.txt`, require chapter numbers to start at 1 and remain contiguous without duplicates, and order the source parts as introduction, ascending chapters, then outro.
6. FR6 — `BOOK_NAME` shall be required and shall control both the destination directory name and output filename prefix, while validation shall reject empty names, `.`/`..`, path separators, control characters, and names that escape the configured audiobook root.
7. FR7 — The command shall create `<AUDIOBOOK_ROOT>/<BOOK_NAME>/` and name tracks `<BOOK_NAME> 001.wav`, `<BOOK_NAME> 002.wav`, and so on using a fixed three-digit sequence; the introduction shall be track 001 and the outro shall be the final track.
8. FR8 — One batch process shall load Chatterbox Multilingual V3 once and resolve/load the selected English voice conditioning once before synthesizing the ordered tracks sequentially.
9. FR9 — Each source part shall use the existing `chunk_text` rules and `SynthesisEngine.synthesize` path so long text is split into bounded chunks and combined into one WAV for that part with the existing sentence and paragraph silence behavior.
10. FR10 — Every generated track shall first be written to a temporary file in its destination directory, validated as readable, non-empty, audible 16-bit PCM WAV audio, and atomically moved to its final filename only after validation succeeds.
11. FR11 — The destination shall contain an atomically written `audiobook-manifest.json` recording the book identity, display name, language, voice ID, model name and revisions, ordered source-to-track mapping, source SHA-256, output SHA-256, output duration, and per-track completion state.
12. FR12 — On a normal rerun, a track shall be skipped only when the manifest settings and mapping remain compatible, its source checksum is unchanged, and its recorded output exists, matches its output checksum, and passes WAV validation; changed or invalid individual tracks shall be regenerated.
13. FR13 — A normal rerun shall fail before synthesis when the existing manifest has incompatible book, display-name, language, voice, model, or ordered track-set metadata; `FORCE=true` shall permit replacement by regenerating every expected track and publishing current manifest state.
14. FR14 — `FORCE` shall accept only `true` or `false`; after a forced run completes successfully, obsolete WAV paths recorded by the previous manifest may be removed, but unrelated files in the destination shall remain untouched.
15. FR15 — The runner shall persist manifest progress after every successful track so interruption or later-track failure leaves completed tracks resumable; it shall return a non-zero exit code on failure and remove its current temporary WAV without deleting earlier valid tracks.
16. FR16 — Console output shall identify preflight completion, model and voice readiness, current overall track number, source filename, chunk progress, skipped tracks, completed output paths, and a concise final success or failure summary without logging source prose or voice-conditioning content.
17. FR17 — Docker Compose shall mount the parser output root read-only inside the Chatterbox container and mount `AUDIOBOOK_ROOT` as the writable audiobook destination, while the Make target shall continue using the repository's detected `DOCKER_HOST` and isolated Chatterbox Compose project.
18. FR18 — The Chatterbox Docker test suite shall cover project discovery/order, validation and containment, three-digit naming, manifest compatibility, checksum-based resume, force regeneration, atomic output behavior, failure recovery, and one-time model/conditioning reuse using fakes rather than downloading or executing the real model.
19. FR19 — `services/ChatterboxTtsService/README.md` shall document the input layout, complete command, variable contract, output layout, resume/force behavior, English/V3 limitation, privacy considerations, and manual interruption/recovery procedure.
20. FR20 — Preview and audiobook synthesis shall use the same explicit Multilingual V3 sampling parameters and a positive configurable `SEED` that defaults to `1234`; each chunk shall be deterministically seeded so identical text, chunking, voice conditioning, model revision, and seed produce identical audio in both workflows.
21. FR21 — The audiobook manifest shall record the synthesis seed and treat a seed change or a legacy seedless manifest as incompatible unless `FORCE=true`; known upstream `LoRACompatibleLinear` and `torch.backends.cuda.sdp_kernel()` FutureWarnings shall be narrowly suppressed without hiding unrelated warnings or changing dependencies.

## Non-Functional Requirements

- All EPUB text, introductions, outros, voice artifacts, manifests, and generated audio remain local and ignored by Git; source prose and biometric-derived conditioning content must not appear in logs or tests.
- Input and output path containment must be checked after path resolution. Caller-controlled filenames must never select arbitrary host or container files.
- The input mount must be read-only. Only the selected destination below the mounted audiobook root may be written.
- Generation is intentionally sequential because the current engine holds one selected conditioning state and CPU inference is memory-intensive.
- The orchestration, project/manifest storage, and synthesis responsibilities must remain independently testable. High-level audiobook behavior must depend on the existing `SynthesisEngine` protocol or narrow collaborators that can be replaced with fakes.
- The implementation must introduce no new runtime package, database, WebApp dependency, queue, or network-facing route.
- Existing `/preview`, `/voices`, `/progress`, `chatterbox-preview`, and Chatterbox voice persistence behavior must remain backward compatible.
- A real-model audiobook run is a manual validation because model loading and CPU synthesis are too expensive for the automated suite.

## Out of Scope

- Parsing an EPUB and generating its audiobook in the same command.
- Automatically writing, extracting, or making the introduction or outro optional.
- Portuguese or any language other than English.
- Chatterbox Nano, language-specific checkpoints, Supertonic, GPU acceleration, or concurrent synthesis.
- A new HTTP audiobook endpoint, WebApp UI, authentication, database persistence, background jobs, or Microsoft Agent Framework integration.
- MP3/M4B conversion, joining tracks, embedded covers, chapter metadata tags, loudness mastering, or publishing to a media library.
- Selective start/end track controls in the initial POC.
- Adopting pre-existing unmanifested WAVs as successfully generated tracks.

## Open Questions

- None. The POC decisions were confirmed during discovery on 2026-08-21.
