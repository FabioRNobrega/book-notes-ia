# Requirements: Persistent Chatterbox Voice Conditioning POC

## Table of Contents

- [Requirements: Persistent Chatterbox Voice Conditioning POC](#requirements-persistent-chatterbox-voice-conditioning-poc)
  - [Problem Statement](#problem-statement)
  - [User Stories](#user-stories)
  - [Functional Requirements](#functional-requirements)
  - [Non-Functional Requirements](#non-functional-requirements)
  - [Out of Scope](#out-of-scope)
  - [Open Questions](#open-questions)

## Problem Statement

The isolated `services/ChatterboxTtsService/` proof of concept currently supports only one English reference, recreates voice conditioning from `data/reference.wav` on every `POST /preview`, keeps that conditioning only in process memory, and overwrites one shared `data/synthetic-preview.wav`. The POC needs to prove that two fixed local voices—English and Portuguese—can each receive a stable generated voice ID, persist service-generated Chatterbox conditioning in the Git-ignored bind-mounted data directory, reuse that conditioning after container restarts and for changed preview text, and keep their generated outputs separate. This remains an isolated developer POC and must not alter the ASP.NET Core WebApp, Supertonic service, database, user profile, premium authorization, or normal Docker stack.

## User Stories

- Given the English reference recording has never been prepared, when a developer runs an English preview, then the service generates a voice ID, persists conditioning, and produces an English preview.
- Given English conditioning already exists and is compatible, when the developer changes `config/preview.txt` and generates again, then the same voice ID and conditioning are reused for the new speech.
- Given the Portuguese reference recording has never been prepared, when a developer runs a Portuguese preview, then the service creates a separate Portuguese voice ID and conditioning and reads `config/pt-preview.txt`.
- Given either Chatterbox container is rebuilt or restarted, when a compatible preview is requested, then the voice conditioning is loaded from `/data` instead of being rebuilt from the reference WAV.
- Given a reference recording or pinned model identity changes, when the associated preview is requested, then the stale conditioning is replaced safely while the other language voice remains unchanged.

## Functional Requirements

1. FR1 — The feature shall remain inside the isolated `services/ChatterboxTtsService/` POC and `docker-compose.chatterbox.yml`; it shall not modify the existing Supertonic service, WebApp, database, Microsoft Agent Framework behavior, or base application Compose files.
2. FR2 — The service shall expose exactly two allowlisted preview language profiles: `en`, backed by `data/reference.wav` and `config/preview.txt`, and `pt`, backed by `data/pt-reference.wav` and `config/pt-preview.txt`.
3. FR3 — The POC shall use Chatterbox's supported language IDs `en` and `pt`; it shall not expose `pt-br` as a Chatterbox language ID.
4. FR4 — On the first successful preview for a language profile, the service shall generate a UUID voice ID and persist the profile's Chatterbox conditioning as a `.pt` artifact under `data/voices/<voice-id>/`.
5. FR5 — Each persisted voice shall include service-generated metadata recording at least the voice ID, profile language, reference SHA-256, conditioning SHA-256, Chatterbox source revision, model repository revision, model name, conditioning format version, and creation/update timestamps.
6. FR6 — On subsequent previews, the service shall reuse compatible persisted conditioning for that language profile without reprocessing its reference WAV, including after a container restart or image rebuild.
7. FR7 — Changing only `config/preview.txt` or `config/pt-preview.txt` shall produce speech for the new text with the same voice ID and existing conditioning artifact.
8. FR8 — If the selected reference WAV checksum, pinned Chatterbox source revision, pinned model repository revision, model name, or conditioning format version differs from metadata, the service shall regenerate that profile's conditioning automatically and atomically while retaining its stable voice ID.
9. FR9 — A missing or unreadable conditioning artifact whose trusted metadata otherwise exists shall be rebuilt automatically from the validated profile reference; if rebuilding fails, the prior valid conditioning and preview shall not be replaced.
10. FR10 — Conditioning and metadata writes shall use temporary files followed by validation and atomic replacement so an interrupted preparation cannot publish a partial `.pt` or metadata file.
11. FR11 — The service shall only load conditioning files that it created beneath the configured `/data/voices` root, using the pinned Chatterbox `Conditionals.load()` path with restricted `torch.load(..., weights_only=True)` deserialization; the API shall not accept uploaded `.pt` files or caller-provided filesystem paths.
12. FR12 — English and Portuguese conditioning shall remain independent: preparing, loading, regenerating, or failing one profile shall not overwrite or select the other profile's voice ID or artifacts.
13. FR13 — Successful previews shall be written beneath `data/outputs/<voice-id>/preview-<language>.wav`, with temporary output validation and atomic replacement preserving the last valid output after failures.
14. FR14 — `POST /preview` shall accept an allowlisted language selection, default to `en` for backward-compatible POC usage, and return the selected language, stable voice ID, conditioning source (`created`, `regenerated`, or `loaded`), conditioning path, output path, model diagnostics, elapsed time, output duration, chunk count, and peak memory without returning tensor or voice data.
15. FR15 — `make chatterbox-preview` shall continue to default to English and shall support `LANGUAGE=pt`; the command shall validate the corresponding fixed local reference, start/reuse the isolated service, wait for readiness, invoke the preview, and print the voice ID and output path.
16. FR16 — The current local `data/pt-br-reference.wav` shall be renamed to `data/pt-reference.wav`, and the current `config/pt-br-preview.txt` shall become tracked `config/pt-preview.txt` without changing the supplied Portuguese passage.
17. FR17 — `/health`, service logs, and documentation shall report both supported profiles and explain persistent voice IDs, `.pt` reuse, automatic invalidation, local retention, deletion implications, and the difference between preview text and reference audio.
18. FR18 — Automated tests shall use a fake conditioning/synthesis engine and temporary storage; they shall not download Chatterbox models or deserialize real untrusted `.pt` content.

## Non-Functional Requirements

- The `data/` and `models/` bind mounts must remain Git-ignored and survive `make chatterbox-down`, container restarts, and image rebuilds. Manual deletion or disk failure remains possible and must be documented.
- Reference WAVs, conditioning tensors, metadata, and outputs are sensitive biometric-derived local data. Their contents must not appear in logs, API responses, Git, or Docker image layers.
- Language, voice, and artifact paths must be derived from an internal allowlist and validated UUIDs rather than concatenated from unchecked request input.
- Only one real model operation may mutate the singleton Chatterbox model's in-memory conditionals at a time. Existing serialized inference behavior must be preserved.
- Conditioning reuse should avoid reference feature extraction but is not expected to make CPU waveform generation real-time; no hard synthesis latency target applies to this POC.
- Persistence, compatibility checks, and inference must remain separated behind narrow, fakeable Python responsibilities instead of accumulating filesystem and serialization behavior in FastAPI route handlers.
- No new runtime package or infrastructure service is required; the pinned Chatterbox/PyTorch implementation already provides conditioning save/load support.

## Out of Scope

- WebApp integration, Razor views, user profiles, premium entitlements, Identity ownership, EF Core entities/migrations, or database storage.
- Uploading arbitrary reference recordings through HTTP or Make, preparing an unlimited voice catalog, renaming voices, or deleting voices through an API.
- Accepting, importing, or sharing caller-supplied `.pt` files.
- Arbitrary synthesis text in an HTTP request; this slice continues to read the two tracked preview text files.
- A dedicated Brazilian Portuguese model checkpoint or `pt-br` language identifier; the POC uses multilingual V3 with `language_id="pt"` and a Portuguese reference recording.
- Mixing English and Portuguese under one voice ID; the two fixed profiles intentionally use separate references and conditioning.
- GPU/ROCm/Vulkan acceleration, streaming generation, production deployment, encryption/key management, backups, or cloud/object storage.
- Updating `Specs/Roadmap.md`; the user explicitly requested that this remain an unroadmapped POC.

## Open Questions

- None. The POC uses two fixed reference profiles, automatic first-use preparation and invalidation, language-specific outputs, and no Roadmap entry.
