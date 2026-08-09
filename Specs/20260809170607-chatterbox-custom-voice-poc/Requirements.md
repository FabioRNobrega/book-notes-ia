# Requirements: Chatterbox Custom Voice Proof of Concept

## Table of Contents

- [Requirements: Chatterbox Custom Voice Proof of Concept](#requirements-chatterbox-custom-voice-proof-of-concept)
  - [Problem Statement](#problem-statement)
  - [User Stories](#user-stories)
  - [Functional Requirements](#functional-requirements)
  - [Non-Functional Requirements](#non-functional-requirements)
  - [Out of Scope](#out-of-scope)
  - [Open Questions](#open-questions)

## Problem Statement

The repository currently has a working local Supertonic 3 sidecar under `services/TtsService.Api/`, but it has no isolated way to prove that Chatterbox can clone a local reference recording on the target SteamOS/AMD hardware. Before adding user profiles, premium entitlements, uploads, persistence, or chat-time provider routing, the project needs a Docker-only proof of concept that reads the supplied `reference.wav`, synthesizes a fixed English literary-context passage, writes a WAV preview beside the reference recording, and reports enough runtime evidence to decide whether Chatterbox is viable on this machine.

## User Stories

- Given the developer has placed `reference.wav` in the Chatterbox POC data directory, when they run `make chatterbox-preview`, then the Chatterbox container generates `synthetic-preview.wav` in that same local directory.
- Given the Chatterbox model has not been downloaded, when the developer runs the preview command for the first time, then the pinned runtime downloads the required model artifacts into a persistent local cache and reports progress or a clear failure.
- Given the model artifacts were downloaded successfully, when the developer runs the preview again without network access, then synthesis uses the persistent cache instead of requiring Hugging Face.
- Given the reference recording is missing or unreadable, when preview generation is requested, then the service returns a clear error and does not leave a corrupt preview file.
- Given preview generation completes, when the developer inspects the output and service response, then they can see the selected device, elapsed synthesis time, output duration, and output path before listening to the WAV manually.

## Functional Requirements

1. FR1 — The implementation must add an isolated Python 3.11 Chatterbox service without modifying or removing the existing `tts`/Supertonic service, `WebApp`, user profiles, chat audio routing, PostgreSQL schema, or Microsoft Agent Framework behavior.
2. FR2 — The implementation must relocate the untracked repository-root `reference.wav` to `services/ChatterboxTtsService/data/reference.wav`; the local data directory and its voice artifacts must remain ignored by Git.
3. FR3 — The Chatterbox service must expose `GET /health`, returning whether the process is alive, whether the Chatterbox model is ready, and which inference device is selected.
4. FR4 — The Chatterbox service must expose `POST /preview`, with no user-supplied synthesis text required, and use `services/ChatterboxTtsService/data/reference.wav` as its reference-audio input.
5. FR5 — `POST /preview` must synthesize the following complete English text, preserving its sentence order and paragraph boundaries; the engine may split it into safe inference chunks and insert short silences when joining those chunks:

   > The book Jennifer Government by Max Barry (2003) explores a dystopian future where the U.S. government has been fully privatized and corporate power dominates society. This reflects the rise of neoliberalism in late‑20th/early‑21st century America, emphasizing themes like:
   >
   > Corporate control over governance: The novel critiques how corporations exert influence over public institutions.
   > Loss of individual freedom: Citizens are surveilled and commodified under a system that prioritizes profit over people.
   > Government as an extension of business: Authority is wielded by corporate entities rather than traditional state structures.
   >
   > Barry’s satire highlights the tensions between capitalism, power dynamics, and personal autonomy in contemporary culture.

6. FR6 — The POC must use Chatterbox Multilingual V3 with `language_id="en"` and CPU inference; AMD GPU/ROCm acceleration is not part of this slice.
7. FR7 — Successful preview generation must atomically replace `services/ChatterboxTtsService/data/synthetic-preview.wav` with a non-empty, valid RIFF/WAVE file, leaving `reference.wav` unchanged.
8. FR8 — A successful `POST /preview` response must report the output path, inference device, total elapsed synthesis seconds, output duration seconds, and the number of text chunks generated; logs must provide the same diagnostic timing without logging model tensors or audio contents.
9. FR9 — The Makefile must provide `make chatterbox-preview`, which starts or reuses the isolated Chatterbox Compose service, waits for readiness, invokes `POST /preview`, and reports the local preview path to the developer.
10. FR10 — The Makefile must provide focused commands for Chatterbox POC logs, tests, and teardown without invoking the existing destructive `docker-down*` targets or removing the main application volumes.
11. FR11 — The first successful run may download Chatterbox artifacts from Hugging Face, but the package version and model selection must be pinned and the downloaded artifacts must persist in an ignored local cache that supports a later offline restart and synthesis check.
12. FR12 — Missing input, invalid/unreadable audio, model-download failure, model-load failure, synthesis failure, and output-write failure must produce a non-success response with a concise diagnostic; a failed request must not replace the last valid preview.
13. FR13 — The service must accept the supplied reference recording as observed during discovery: PCM 16-bit stereo WAV, 44.1 kHz, approximately 22.3 seconds and 3.9 MB; any conversion needed by Chatterbox must happen internally without changing the source file.
14. FR14 — Automated tests must exercise health reporting, fixed-text configuration, safe chunk ordering, input validation, atomic output behavior, response metadata, and error mapping through a fake synthesis engine so normal tests do not load or download the real model.

## Non-Functional Requirements

- The POC must be Docker-first and must not require Python, pip, PyTorch, FFmpeg, or Chatterbox to be installed on the host.
- Chatterbox model loading must happen once per running service process rather than once per preview request.
- The service must serialize preview inference for this POC so concurrent requests cannot contend for the same output path or partially overwrite one another.
- Local model, reference-audio, generated-audio, Python cache, and test-artifact directories must not be committed.
- Dependency versions must be reproducible. At minimum, `chatterbox-tts` must be pinned to the selected tested version, and the resolved model snapshot/revision must be recorded during implementation.
- The implementation must report peak or container-visible memory and elapsed-time observations during manual validation; no real-time latency target is imposed for this exploratory slice.
- The design must keep API coordination, Chatterbox inference, configuration/path resolution, and WAV output concerns separable enough to replace the real engine with a fake in tests.

## Out of Scope

- Changes to `UserProfile`, Identity, premium authorization, subscription or billing behavior.
- Browser recording, audio upload UI, Razor views, Sass, HTMX, or JavaScript changes.
- A `UserVoice` EF Core entity, database migration, per-user storage, consent records, or deletion workflows.
- Routing chat audio between Supertonic and Chatterbox.
- Replacing or modifying the existing Supertonic service.
- Multiple custom voices, arbitrary request text, public API exposure, authentication, or production deployment.
- Portuguese or other language validation in this first POC.
- AMD ROCm, Vulkan, CUDA, Apple MPS, or other GPU acceleration.
- Automatic filesystem watching; generation is explicitly triggered by the Make command/API.
- Adding this exploratory POC as a prioritized phase in `Specs/Roadmap.md`.

## Open Questions

- ⚠️ TODO: After the real preview is generated, decide whether its voice similarity, intelligibility, generation time, and memory use are acceptable enough to justify a second integration spec.
- Resolved during implementation: the Chatterbox source is pinned to official commit `5de7a54aa4e5e2baadb0182dde554908b48b85c2`, and the Hugging Face model repository is pinned independently to snapshot `5bb1f6ee58e50c3b8d408bc82a6d3740c2db6e18`.
