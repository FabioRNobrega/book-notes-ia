# Validation: Chatterbox Custom Voice Proof of Concept

## Table of Contents

- [Validation: Chatterbox Custom Voice Proof of Concept](#validation-chatterbox-custom-voice-proof-of-concept)
  - [Acceptance Criteria](#acceptance-criteria)
  - [Test Cases](#test-cases)
  - [Manual Verification](#manual-verification)
  - [Definition of Done](#definition-of-done)
  - [Automated Test Results](#automated-test-results)
  - [Rollback Plan](#rollback-plan)

## Acceptance Criteria

| Requirement | Acceptance Criterion |
| --- | --- |
| FR1 | A diff shows only the isolated Chatterbox POC, Make/ignore/docs, and this spec changing; `docker-compose.yml`, `services/TtsService.Api/`, `WebApp/`, database migrations, and Microsoft Agent Framework code remain unchanged. |
| FR2 | `reference.wav` exists at `services/ChatterboxTtsService/data/reference.wav`, no root copy remains after the authorized move, and `git status` does not show the relocated recording or generated data as trackable content. |
| FR3 | `GET /health` returns a successful structured response containing process health, model readiness, and `device: "cpu"`; readiness is false or the health check is non-ready until model load completes. |
| FR4 | `POST /preview` requires no text payload and reads the fixed `/data/reference.wav`; changing an unrelated file does not change the selected reference path. |
| FR5 | Tests prove the configured text matches the approved passage and that chunk concatenation preserves every paragraph/sentence in order; manual listening confirms the generated audio reads the complete passage rather than truncating after the first chunk. |
| FR6 | Service diagnostics identify Chatterbox Multilingual V3, `language_id: "en"`, and CPU inference; no ROCm/CUDA/Vulkan configuration is required. |
| FR7 | After success, `synthetic-preview.wav` exists beside `reference.wav`, has a RIFF/WAVE header and non-zero duration, and the source reference checksum is unchanged; a forced failed run leaves the previous preview checksum unchanged. |
| FR8 | The preview response and logs contain output path, CPU device, elapsed synthesis seconds, output duration seconds, and chunk count without dumping audio content or model tensors. |
| FR9 | On Linux/SteamOS, one `make chatterbox-preview` invocation starts/reuses the POC service, waits for readiness, triggers generation, exits successfully, and prints `services/ChatterboxTtsService/data/synthetic-preview.wav`. |
| FR10 | Focused Make targets can show Chatterbox logs, run fake-engine tests, and tear down only the Chatterbox Compose project; the main app containers and volumes remain intact. |
| FR11 | A first online run populates the ignored model cache; after restarting with Hugging Face offline mode enabled, `/health` becomes ready and another preview succeeds without a model download. The resolved snapshot/revision is recorded. |
| FR12 | Tests cover each specified missing/invalid/model/synthesis/write failure as a non-success response, and atomic-output tests prove failures do not replace a known-good preview. |
| FR13 | The real supplied 22.308-second, stereo, 44.1 kHz PCM WAV completes preprocessing/synthesis without modifying its byte checksum. |
| FR14 | The new Python test target passes without downloading or loading the real Chatterbox model, demonstrating that a fake engine covers API, chunking, validation, metadata, and output behavior. |

## Test Cases

**Python unit/API tests:**

- `services/ChatterboxTtsService/tests/test_api.py` — verify alive/not-ready/ready health states, successful preview metadata, fixed input selection, serialized request behavior, and concise HTTP error mapping with a fake engine.
- `services/ChatterboxTtsService/tests/test_chunking.py` — verify the exact configured passage is read as UTF-8, every sentence remains in order, paragraph boundaries produce pauses, and no content is silently truncated or duplicated.
- `services/ChatterboxTtsService/tests/test_output.py` — verify non-empty RIFF/WAVE validation, temporary-file cleanup, atomic replacement, preservation of the reference checksum, and preservation of a prior preview after forced failure.
- Settings tests should reject paths escaping `/data`, absent preview text, and unsupported language/device overrides for this fixed POC.

**Docker integration checks:**

- Build the service from a clean Chatterbox image context and run its fake-engine test target entirely in Docker.
- Start only `docker-compose.chatterbox.yml`, wait for its health check, and verify that no `webapp`, `tts`, `ollama`, `postgres`, or `redis` dependency is required.
- Execute one real online preview to populate the persistent cache and one real offline-mode restart/preview to prove the cached model is sufficient.
- Run the existing `make test` regression suite to confirm the unchanged ASP.NET Core/Supertonic solution still passes.
- ⚠️ TODO: The real Chatterbox synthesis check remains manual/optional in routine CI because downloading and executing the 500M model would make the normal test path heavyweight and network-dependent.

**Observed real online run (2026-08-09, SteamOS/AMD CPU):**

- Chatterbox source revision: `5de7a54aa4e5e2baadb0182dde554908b48b85c2` (metadata version `0.1.7`).
- Hugging Face model snapshot: `5bb1f6ee58e50c3b8d408bc82a6d3740c2db6e18`.
- Device/language/model: CPU / `en` / Multilingual V3.
- Complete preview: 4 chunks, 45.180 seconds of WAV audio.
- Total synthesis time: 425.359 seconds (approximately 9.4 times slower than real time).
- Peak process memory reported by the service: 6,781.2 MB.
- Model-ready host observation: approximately 10 GiB RAM remained available with negligible swap use while the formerly running application stack was still present; the user subsequently stopped that stack for the final run.
- ⚠️ TODO: Qualitative similarity, intelligibility, pacing, and chunk-join quality require the user to listen to the generated local WAV.

**Observed cached-offline run (2026-08-09):**

- `HF_HUB_OFFLINE=1 make chatterbox-preview` loaded all six model files from the 3.1 GiB ignored persistent cache without Hugging Face HTTP requests.
- The secondary `spacy-pkuseg` tokenizer data was persisted under `services/ChatterboxTtsService/models/.pkuseg/` by setting the container home to the mounted model directory.
- Offline preview: 4 chunks, 47.540 seconds of WAV audio, generated in 445.684 seconds with 6,786.4 MB reported peak process memory.
- The successful offline preview atomically replaced the first online preview; the reference checksum remained `62b8cdde47d62f7d7cbf708922f5c0e4cb62e0e2e0c9a55a8cc83260bb56fb2d`.

## Manual Verification

1. Confirm the implementation moved the local recording and kept it ignored:

   ```bash
   file services/ChatterboxTtsService/data/reference.wav
   git status --short
   ```

2. Stop unrelated high-memory workloads if a clean CPU/RAM benchmark is desired. Do not run the existing `make docker-down`, because it removes main-stack volumes.
3. From the repository root, run:

   ```bash
   make chatterbox-preview
   ```

4. On the first run, allow the image/dependency/model downloads to finish. Confirm the Make command waits for readiness rather than posting while the model is still loading.
5. Confirm the command reports CPU, elapsed synthesis time, output duration, chunk count, and this output path:

   ```text
   services/ChatterboxTtsService/data/synthetic-preview.wav
   ```

6. Validate the output container-side or with host inspection tools:

   ```bash
   file services/ChatterboxTtsService/data/synthetic-preview.wav
   ffprobe -v error -show_entries format=duration -of default=noprint_wrappers=1 services/ChatterboxTtsService/data/synthetic-preview.wav
   ```

7. Listen to the complete preview and record qualitative observations for intelligibility, similarity to `reference.wav`, pacing, unwanted repetition, truncation, and artifacts at chunk joins.
8. Run the focused log and test targets and confirm they do not affect the main Compose stack:

   ```bash
   make chatterbox-logs
   make chatterbox-test
   ```

9. Record container memory and CPU evidence after/between synthesis runs using the runtime selected by the Makefile.
10. Restart the POC with Hugging Face offline mode enabled and run the preview again. Confirm it succeeds from the persistent local cache and record the resolved model snapshot/revision.
11. Temporarily rename `reference.wav`, invoke the preview, and confirm the response is a clear failure that does not replace an existing valid `synthetic-preview.wav`; restore the reference afterward.
12. Tear down only the POC:

    ```bash
    make chatterbox-down
    ```

13. Run the existing regression suite:

    ```bash
    make test
    ```

## Definition of Done

- `Requirements.md`, `Plan.md`, and `Validation.md` in this spec reflect the implemented POC and any discoveries made while integrating the real package.
- The supplied reference recording is relocated to the ignored POC data directory and remains unchanged by synthesis.
- The isolated Docker service exposes working health and preview routes.
- `make chatterbox-preview` produces the complete English preview as a valid WAV in the local data directory.
- The package dependency set and resolved Chatterbox model snapshot/revision are pinned or recorded sufficiently for reproducible cached use.
- Fake-engine automated tests pass without downloading the real model.
- Real online and cached-offline preview runs are manually verified on the target SteamOS/AMD machine.
- Generation time, output duration, chunk count, container memory, and qualitative voice results are recorded in this spec or an adjacent validation artifact.
- Existing Supertonic behavior and the full existing `make test` suite remain unchanged and passing.
- README and Make help/comments document the POC commands, cache behavior, paths, and expected first-run cost.
- No personal recording, generated WAV, downloaded model, cache, or Python build artifact is tracked by Git.
- Microsoft-specific design evidence remains not applicable because this POC does not alter any Microsoft technology path.

## Automated Test Results

- `make chatterbox-test`: 17 passed, 0 failed; the real model is neither loaded nor downloaded by this suite.
- `make test`: 190 WebApp tests and 44 Supertonic TTS tests passed, 0 failed, 0 skipped.
- The existing compiler warning in `PlaceholderTtsService` about its unread `defaults` parameter remains unchanged and does not fail the suite.

## Rollback Plan

- Run `make chatterbox-down` to stop and remove only the isolated Chatterbox POC containers/network.
- Revert the Chatterbox-specific Makefile, `.gitignore`, and README entries; delete `docker-compose.chatterbox.yml` and `services/ChatterboxTtsService/` code/configuration.
- Move the ignored `services/ChatterboxTtsService/data/reference.wav` back to the repository root if the developer wants to retain it outside the removed service directory.
- The ignored model cache and generated preview may be deleted separately after confirming their exact Chatterbox paths; they are not shared with Supertonic, Ollama, PostgreSQL, Redis, or WebApp volumes.
- No database rollback, application configuration rollback, or Supertonic restoration is required because those paths are not changed by this POC.
