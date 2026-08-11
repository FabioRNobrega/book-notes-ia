# Validation: Persistent Chatterbox Voice Conditioning POC

## Table of Contents

- [Validation: Persistent Chatterbox Voice Conditioning POC](#validation-persistent-chatterbox-voice-conditioning-poc)
  - [Acceptance Criteria](#acceptance-criteria)
  - [Test Cases](#test-cases)
  - [Manual Verification](#manual-verification)
  - [Definition of Done](#definition-of-done)
  - [Rollback Plan](#rollback-plan)

## Acceptance Criteria

| Requirement | Acceptance Criterion |
| --- | --- |
| FR1 | A focused diff shows no changes to `WebApp/`, `services/TtsService.Api/`, `services/TtsService.Tests/`, database migrations, Microsoft Agent Framework code, `docker-compose.yml`, or OS-specific application Compose files. |
| FR2 | Profile tests prove `en` resolves only `reference.wav`/`preview.txt` and `pt` resolves only `pt-reference.wav`/`pt-preview.txt`. |
| FR3 | Responses and engine calls contain only `en` or `pt`; `pt-br` is rejected and never passed to Chatterbox. |
| FR4 | A first successful preview creates one valid UUID directory containing `conditioning.pt` and metadata and returns that exact voice ID. |
| FR5 | The metadata file contains all specified identity, compatibility, checksum, format, and timestamp fields, and both recorded checksums match the corresponding files. |
| FR6 | A second service instance using the same temporary data root loads the same `.pt` and returns the same voice ID without calling fake-engine reference preparation. |
| FR7 | After changing only the selected preview text, the next output is synthesized from the changed text while the voice ID, `.pt` checksum, and preparation-call count remain unchanged. |
| FR8 | Changing each compatibility input in isolation causes one automatic re-preparation and atomic metadata/conditioning replacement while preserving the voice ID and the other profile's checksums. |
| FR9 | Missing or corrupt trusted conditioning triggers one rebuild from the fixed WAV; forced rebuild failure preserves any prior valid artifact/output and returns a concise non-success response. |
| FR10 | Tests simulating save, validation, and metadata publication failures leave no published partial state and preserve the previous valid pair. |
| FR11 | No route accepts a `.pt` body/path; path-containment and invalid UUID tests cannot escape `/data/voices`; the concrete load operation uses Chatterbox `Conditionals.load()` and its restricted `weights_only=True` path. |
| FR12 | English and Portuguese produce different voice IDs and artifact directories, and regeneration/failure of one leaves every checksum and association for the other unchanged. |
| FR13 | Successful outputs validate as non-empty PCM WAV files at `data/outputs/<voice-id>/preview-en.wav` or `preview-pt.wav`; a failed request preserves the last valid output. |
| FR14 | `POST /preview` defaults to English, accepts `?language=pt`, and returns every specified diagnostic with conditioning status accurately reporting `created`, `regenerated`, or `loaded`. |
| FR15 | On Linux/SteamOS, `make chatterbox-preview` runs English and `make chatterbox-preview LANGUAGE=pt` runs Portuguese, each checking the correct reference and printing the returned voice ID and host output path. |
| FR16 | The Portuguese reference checksum is identical before and after its local rename, `config/pt-preview.txt` exactly preserves the supplied passage, and no `pt-br` filenames remain in active configuration/documentation. |
| FR17 | Health/log output and both READMEs describe two profiles and persistent conditioning without emitting reference/conditioning contents; service docs explain survival, invalidation, deletion, and text-versus-voice behavior. |
| FR18 | `make chatterbox-test` passes with fakes and temporary directories while offline and without accessing the real model cache. |

## Test Cases

**Unit tests:**

- `services/ChatterboxTtsService/tests/test_voice_store.py` — verify UUID generation, stable profile resolution, required metadata, reference/conditioning SHA-256, model/schema invalidation, corrupt or missing artifact recovery decisions, duplicate profile rejection, atomic publication failure, UUID validation, root containment, and profile isolation.
- `services/ChatterboxTtsService/tests/test_api.py` — verify English default, Portuguese selection, `pt-br`/unknown rejection, first-use `created`, subsequent `loaded`, changed-text reuse, `regenerated`, same-ID behavior, simulated restart, correct fake-engine calls, serialized requests, and concise preparation/load/synthesis errors.
- `services/ChatterboxTtsService/tests/test_output.py` — verify language/voice-specific destinations, valid WAV checks, atomic replacement, temporary cleanup, reference preservation, and prior-output preservation after failure.
- `services/ChatterboxTtsService/tests/test_chunking.py` — verify exact tracked English and Portuguese passages, complete ordered content, supported punctuation/UTF-8 handling, and deterministic chunk boundaries.
- Settings/profile tests — verify only `en` and `pt`, exact fixed filenames, voices/output root containment, CPU/V3 constraints, and missing/empty preview rejection.
- Fake-engine contract tests — verify preparation and save happen only on create/regenerate, load happens on reuse, synthesis receives the selected conditioning, and no real `.pt` deserialization is needed.

**Integration tests:**

- Run `make chatterbox-test` in the isolated Compose project with `HF_HUB_OFFLINE=1` and an empty temporary model cache; the suite must pass because it uses fakes.
- Validate `docker compose -f docker-compose.chatterbox.yml config` so the existing bind mounts and shared runtime settings remain valid without one hard-coded language.
- Run a real English first-use preview, stop the container, restart offline, change only `config/preview.txt`, and run again. Confirm the same voice ID and `.pt` checksum with `loaded` status.
- Run a real Portuguese first-use preview using the supplied Portuguese reference and general multilingual V3 `language_id="pt"`; confirm its voice ID/artifacts/output differ from English.
- Replace a copied test reference or temporarily exercise model-metadata mismatch in an isolated data backup, then confirm automatic regeneration retains the voice ID. Restore personal local artifacts after the check.

## Manual Verification

1. Confirm `services/ChatterboxTtsService/data/reference.wav` and `services/ChatterboxTtsService/data/pt-reference.wav` are readable 16-bit PCM WAV files and remain ignored by Git.
2. Run `make chatterbox-test` and confirm the focused fake-engine suite passes without downloading or loading the real Chatterbox model.
3. Run `make chatterbox-preview` on Linux/SteamOS. Record the returned English voice ID, `conditioning_status`, conditioning checksum/path, output path, elapsed time, and output duration.
4. Confirm `data/voices/<english-voice-id>/conditioning.pt` and metadata exist, the output is at `data/outputs/<english-voice-id>/preview-en.wav`, and the source reference checksum is unchanged.
5. Run `make chatterbox-down`, then `HF_HUB_OFFLINE=1 make chatterbox-preview`. Confirm the same English voice ID is returned with `conditioning_status: loaded` and no conditioning regeneration/model download occurs.
6. Change only the English `config/preview.txt` text, rerun the English preview, listen to the new output, and confirm the voice ID and `.pt` checksum remain unchanged. Restore the approved tracked text afterward.
7. Run `make chatterbox-preview LANGUAGE=pt`. Confirm Chatterbox receives `language_id: pt`, a different voice ID and `.pt` are created, and the resulting `preview-pt.wav` reads the exact Portuguese passage intelligibly.
8. Run the Portuguese preview again after a container restart and confirm `conditioning_status: loaded`, the same Portuguese voice ID, and an unchanged `.pt` checksum.
9. In a disposable copy of the local data artifacts, replace one reference WAV with another valid recording and rerun its profile. Confirm `conditioning_status: regenerated`, the stable voice ID, new reference/conditioning checksums, and no change to the other language's artifacts.
10. Force a fake-engine conditioning or synthesis failure and confirm the last valid `.pt`, metadata, and preview remain readable and no temporary artifacts remain.
11. Run `make test` to confirm all WebApp and Supertonic regression tests remain green.
12. Review `git status --short` and confirm no reference WAV, `.pt`, voice metadata, generated output, or model cache is trackable.

## Definition of Done

- `Requirements.md`, `Plan.md`, and `Validation.md` accurately describe the implemented behavior in this spec folder.
- All FR1–FR18 acceptance criteria are satisfied.
- `make chatterbox-test` passes without model acquisition, and real English/Portuguese preview persistence is manually verified in Docker.
- `make test` passes with no regression to WebApp or Supertonic.
- English and Portuguese use separate stable voice IDs, conditioning, fixed preview text, references, and output paths.
- Compatible `.pt` conditioning survives container restart/rebuild and is reused when only text changes.
- Reference/model/schema incompatibility triggers safe automatic regeneration with the same logical voice ID.
- Only service-created, integrity-checked, root-contained `.pt` files can be loaded through restricted Chatterbox deserialization.
- Personal recordings, conditioning, metadata, outputs, and model artifacts remain local and Git-ignored.
- Documentation and Mermaid flows match the final implementation and security boundary.
- The normal application stack, database, WebApp, Supertonic, and Microsoft Agent Framework behavior remain unchanged.
- `Specs/Roadmap.md` remains unchanged for this POC, as explicitly requested.

## Rollback Plan

- Revert the POC source, Make, Compose, documentation, and tracked Portuguese preview changes from this spec while leaving the ignored `data/` directory untouched.
- Restore the prior English-only `Settings`, `POST /preview`, engine reference-conditioning behavior, and shared `synthetic-preview.wav` path.
- Rename the ignored Portuguese recording back to its prior local filename only if the old workflow requires it; reference audio bytes must not be deleted during rollback.
- Existing `data/voices/` and `data/outputs/` artifacts can remain as inert ignored files during code rollback and be recovered later. Delete them only through an explicit, separately confirmed cleanup because they contain sensitive and potentially irreplaceable derived voice data.
- `make chatterbox-down` stops only the isolated POC and does not affect the normal application stack or erase bind-mounted data.
