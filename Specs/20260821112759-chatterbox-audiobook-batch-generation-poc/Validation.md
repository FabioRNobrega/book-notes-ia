# Validation: Chatterbox Audiobook Batch Generation POC

## Table of Contents

- [Acceptance Criteria](#acceptance-criteria)
- [Test Cases](#test-cases)
- [Manual Verification](#manual-verification)
- [Definition of Done](#definition-of-done)
- [Rollback Plan](#rollback-plan)

## Acceptance Criteria

| Requirement | Acceptance Criterion |
| --- | --- |
| FR1 | Running the target without any required value fails before Compose starts and shows the complete usage; omitting `AUDIOBOOK_ROOT` uses `/home/deck/Music`, and omitting `FORCE` behaves as `false`. |
| FR2 | `TTS_LANG=pt`, another language, or a non-V3 engine is rejected; an ambient `CHATTERBOX_MODEL=nano` cannot make the target run Nano. |
| FR3 | A valid direct-child `BOOK` resolves beneath `/books`; absolute, nested, traversal, missing, and symlink-escape selections fail before model loading. |
| FR4 | Missing, duplicate, empty, non-UTF-8, non-`.txt`, nested, or identical intro/outro selections fail before model loading. |
| FR5 | `chapter-001.txt` through the final contiguous numeric chapter are ordered numerically; missing 001, a gap, a duplicate numeric identity, or no chapters fails. |
| FR6 | A valid `BOOK_NAME` creates exactly one contained destination; blank, dot, traversal, separator, control-character, and resolved escape names fail. |
| FR7 | For N chapters, exactly N+2 expected output names are planned; intro is `BOOK_NAME 001.wav`, chapter 1 is `BOOK_NAME 002.wav`, and outro is `BOOK_NAME {N+2:03d}.wav`. |
| FR8 | A fake engine/store records exactly one model load and one conditioning selection/load for a multi-track job. |
| FR9 | Every regenerated source is passed through `chunk_text`; the fake engine receives its ordered chunks and configured sentence/paragraph silence values once per track. |
| FR10 | A valid temporary WAV atomically replaces its target; empty, corrupt, silent, or wrong-format output leaves the previous valid target unchanged and no temporary WAV behind. |
| FR11 | A successful or partially successful job leaves parseable versioned JSON with the required identity, compatibility, mapping, hashes, duration, and completion fields; writes use atomic replacement. |
| FR12 | An unchanged valid track is skipped; a changed source, changed output bytes, missing output, or invalid WAV regenerates only the affected track in a compatible job. |
| FR13 | Incompatible manifest identity/settings/mapping fails without synthesis when `FORCE=false`; the same project proceeds and regenerates all tracks with `FORCE=true`. |
| FR14 | Only exact `true`/`false` force values are accepted; after successful force completion, obsolete files owned by the old manifest are removed and unrelated destination files remain. |
| FR15 | After a forced synthesis failure on a later track, the process exits non-zero, the active temporary file is removed, earlier published tracks and manifest progress remain, and the next compatible run resumes. |
| FR16 | Captured output reports preflight, readiness, track/chunk progress, skips, completions, and final status without containing any source prose. |
| FR17 | Compose presents parser output read-only at the fixed input root and the chosen host audiobook root writable at the fixed output root; the Make target uses the isolated Chatterbox project. |
| FR18 | `make chatterbox-test` executes all audiobook tests with fake inference and performs no model download or real synthesis. |
| FR19 | The README contains a copyable command, exact layouts and variables, recovery steps, limitations, and local privacy warning consistent with implemented behavior. |
| FR20 | Preview and audiobook calls pass the same seed and explicit quality parameters; a deterministic fake/model seam proves that identical chunks and seed receive identical per-chunk seeds, while changing `SEED` changes those seeds. |
| FR21 | The manifest persists the seed, a changed or legacy seed is incompatible without force, legacy manifests remain readable for forced migration, and only the two named upstream FutureWarnings are filtered. |

## Test Cases

**Unit tests:**

- `services/ChatterboxTtsService/tests/test_audiobook_project.py`
  - Builds intro/chapter/outro plans in exact numeric order and produces fixed three-digit names.
  - Rejects missing/empty/invalid UTF-8 parts, chapter gaps and duplicates, unsafe basenames, traversal, symlink escapes, and unsafe display names.
  - Handles enough chapters to verify padding remains stable beyond 99 tracks.
- `services/ChatterboxTtsService/tests/test_audiobook_manifest.py`
  - Round-trips the versioned schema and rejects malformed, unsupported, incomplete, or unsafe entries.
  - Classifies compatible, source-changed, output-changed, and structurally incompatible state.
  - Proves failed manifest replacement preserves the previous valid file and removes temporary artifacts.
- `services/ChatterboxTtsService/tests/test_audiobook_service.py`
  - Uses `FakeEngine` and a temporary voice store to verify one load/conditioning operation and sequential per-part synthesis.
  - Verifies chunk forwarding, atomic WAV publication, per-track manifest persistence, checksum resume, force regeneration, failure recovery, and owned stale-file cleanup.
  - Verifies unmanifested outputs are not silently trusted and unrelated files are never removed.
- `services/ChatterboxTtsService/tests/test_audiobook_client.py`
  - Verifies required configuration, English-only validation, fixed V3 enforcement, exact force parsing, exit codes, and non-sensitive summaries.
- `services/ChatterboxTtsService/tests/test_engine.py`
  - Verifies deterministic per-chunk seeding, explicit V3 generation parameters, and narrow warning filters without loading the real model.
- `services/ChatterboxTtsService/tests/test_api.py`
  - Retains coverage for `/preview`, voice selection, conditioning creation/reuse/recovery, serialized inference, and HTTP error mapping after shared behavior is extracted.
- `services/ChatterboxTtsService/tests/test_output.py`
  - Retains readable/audible PCM validation and previous-output preservation tests against the extracted shared helper.

**Docker integration checks:**

- `make chatterbox-test` builds the real service image and runs the complete Python suite inside its declared container environment with fake inference.
- Inspect the effective Compose configuration to confirm `/books` is read-only, `/audiobooks` is writable, `AUDIOBOOK_ROOT` defaults correctly, and the audiobook target forces V3.
- No PostgreSQL, Redis, Ollama, ASP.NET Core, Microsoft Agent Framework, or WebApp integration test is required because those systems are outside this isolated POC.

## Manual Verification

1. Create or select a small legally usable English EPUB and produce narration-ready chapter text:

   ```bash
   make ebook-parse BOOK=sample.epub TTS=true TTS_LANG=en
   ```

2. In the resulting `services/EbookParseService.Api/data/output/<book>/` folder, add non-empty UTF-8 `SampleIntro.txt` and `SampleOutro.txt` files. Use only disposable, non-sensitive test prose for validation.
3. Confirm an English Multilingual V3 voice exists:

   ```bash
   make chatterbox-voices LANGUAGE=en
   ```

4. Run the audiobook command with that canonical UUID:

   ```bash
   make create-audio-book \
     BOOK=<book> \
     BOOK_NAME="Sample Book" \
     TTS_LANG=en \
     VOICE_ID=<english-voice-uuid> \
     BOOK_INTRO=SampleIntro.txt \
     BOOK_OUTRO=SampleOutro.txt
   ```

5. Confirm `/home/deck/Music/Sample Book/` contains `Sample Book 001.wav` through the expected final three-digit track plus a parseable `audiobook-manifest.json`. Play the intro, at least one chapter, and the outro; verify order, audible speech, consistent voice, and paragraph pauses.
6. Rerun the identical command. Confirm all tracks are verified and skipped without real synthesis.
7. Change one disposable source text file and rerun. Confirm only its mapped track is regenerated and the manifest hashes change for that track.
8. Interrupt generation during a disposable track, then rerun. Confirm the temporary file is absent, prior completed tracks remain, and generation resumes.
9. Run with `FORCE=true`. Confirm every expected track is regenerated, unrelated files in the destination remain, and the completed manifest reflects the current inputs.
10. Verify an alternate root with `AUDIOBOOK_ROOT=/tmp/book-notes-audiobooks` and confirm no output is written to the default Music folder for that run.
11. Run representative invalid cases (Portuguese, unsafe book path, missing intro, chapter gap, invalid UUID, incompatible manifest) and confirm each fails before destructive publication with concise output that does not echo prose.
12. Run the automated suite:

    ```bash
    make chatterbox-test
    ```

## Definition of Done

- `Requirements.md`, `Plan.md`, and `Validation.md` describe the implemented behavior without unresolved TODOs.
- Every FR1–FR19 acceptance criterion passes.
- `make chatterbox-test` passes without real model inference or downloading model artifacts.
- A short real English Multilingual V3 book run passes the manual generation, playback, resume, interruption, force, and alternate-root checks.
- Existing Chatterbox preview, voice listing, progress, voice persistence, and output rollback tests still pass.
- Parser input remains read-only to the audiobook container; output and manifest paths remain contained below the selected audiobook root.
- No private text, voice artifact, manifest, WAV, model cache, generated CSS, `bin/`, or `obj/` content is committed.
- No WebApp, database, Microsoft Agent Framework, Supertonic, or public HTTP behavior is introduced.
- README documentation matches the final Make and Docker behavior.

## Rollback Plan

- Remove the `create-audio-book` target and its audiobook-specific variables from `Makefile`.
- Remove the parser-input and audiobook-output mounts added to `docker-compose.chatterbox.yml` if no other workflow uses them.
- Revert the audiobook modules/tests and restore preview-local conditioning and WAV-validation helpers in `services/ChatterboxTtsService/app/main.py` if the shared extraction caused a regression.
- Existing parsed text, voice conditioning, model cache, and preview outputs are independent and remain usable after rollback.
- Generated audiobook folders under `AUDIOBOOK_ROOT` are local user data and are not automatically deleted during rollback; the user can retain or manually remove them after deciding whether a backup is needed.
