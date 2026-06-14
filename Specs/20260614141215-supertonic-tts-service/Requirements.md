# Requirements: Supertonic Text-to-Speech Service

## Table of Contents

- [Requirements: Supertonic Text-to-Speech Service](#requirements-supertonic-text-to-speech-service)
  - [Problem Statement](#problem-statement)
  - [User Stories](#user-stories)
  - [Functional Requirements](#functional-requirements)
  - [Non-Functional Requirements](#non-functional-requirements)
  - [Out of Scope](#out-of-scope)
  - [Resolved Decisions](#resolved-decisions)

## Problem Statement

The chat experience in `WebApp/Controllers/ChatController.cs` persists assistant text responses to `chat_message` and renders them through `WebApp/Views/Chat/Chat.cshtml` and `WebApp/Views/Chat/_BotMessage.cshtml`, but there is no way for a reader to listen to those responses. The project needs a local-first Text-to-Speech path that keeps synthesis separate from Microsoft Agent Framework chat orchestration, uses the user's configured preferred language and voice type, caches generated audio, and starts with the same Docker-first workflow used by `webapp`, `ollama`, `postgres`, and `redis`.

## User Stories

- Given an authenticated reader receives an assistant response, when they press the play control on that message, then the app generates or retrieves speech audio for that exact assistant message.
- Given a reader has a preferred language in `UserProfile.PreferredLanguage`, when audio is requested for an assistant message, then speech is synthesized in that language instead of a caller-selected language.
- Given a reader selects a male or female voice preference in their profile, when audio is requested for an assistant message, then speech is synthesized with the matching Supertonic preset voice.
- Given a reader's preferred language differs from a book's original language, when the Microsoft Agent Framework agent answers about that book, then the response text uses the reader's preferred language.
- Given audio has already been generated for the same assistant message, language, and voice, when the reader presses play again, then the app reuses the cached audio reference instead of regenerating speech.
- Given the Docker stack starts through `make docker-run`, `make docker-run-mac`, or `make docker-run-windows`, when the command completes, then the TTS service is running alongside the existing app services.
- Given Supertonic model assets are large runtime artifacts, when the project is checked into Git, then those model files remain outside the main repository and are mounted read-only into the TTS container.

## Functional Requirements

1. FR1 - Add a dedicated `.NET 9` Supertonic TTS API under `services/tts-service/` with an HTTP endpoint that accepts text, language, and voice options and returns `audio/wav`.
2. FR2 - Register the TTS API as a Docker Compose service so every normal Make run/build/down target that starts or manages the app stack includes TTS.
3. FR3 - Keep Supertonic model assets outside Git, document downloading them from Hugging Face with Git LFS, mount the asset directory read-only into the TTS container, and configure the service with an asset path.
4. FR4 - Add a `chat_message_audio` PostgreSQL table with `Id`, required `ChatMessageId` FK to `chat_message`, `Language`, `Voice`, `StorageKey`, `ContentType`, `ByteLength`, optional `DurationSeconds`, `ContentHash`, `CreatedAt`, and `UpdatedAt`; enforce `UNIQUE(chat_message_id, language, voice)` and indexes for message/language/voice lookups.
5. FR5 - Add WebApp service abstractions that resolve the authenticated user's assistant `ChatMessage`, determine language from `UserProfile.PreferredLanguage`, determine voice from a persisted user profile voice preference, look up existing `chat_message_audio`, and call the TTS HTTP API only when cached audio is missing.
6. FR6 - Persist generated audio through a filesystem-backed audio storage implementation mounted as a Docker volume for development, and store only metadata plus storage references in PostgreSQL.
7. FR7 - Add a user-scoped MVC endpoint that returns playable audio for an assistant message only when the message belongs to the authenticated user.
8. FR8 - Add a visible play control to assistant messages in `WebApp/Views/Chat/Chat.cshtml` and `WebApp/Views/Chat/_BotMessage.cshtml`; playback must be user-initiated and never autoplay.
9. FR9 - Add a user profile voice preference with supported values `female` and `male`, persist it on `UserProfile`, render it in `WebApp/Views/UserProfile/Upsert.cshtml` as a simple selector or checkbox-style control, and include it in profile create/update binding.
10. FR10 - Surface loading and failure states for audio playback without breaking the existing HTMX out-of-band chat response and context-ring updates.
11. FR11 - Map `female` to Supertonic voice `F3` and `male` to `M3` for every supported language, so Portuguese + Female uses `pt` + `F3`, Portuguese + Male uses `pt` + `M3`, English + Female uses `en` + `F3`, and English + Male uses `en` + `M3`.
12. FR12 - Ensure Microsoft Agent Framework chat responses are instructed to answer in the user's `PreferredLanguage` regardless of the original language of the book, highlight, synopsis, or retrieved context.
13. FR13 - Add tests covering voice/language resolution, profile voice persistence, cache hit versus generation behavior, user isolation for audio retrieval, database constraints, and chat partial rendering of the play control.

## Non-Functional Requirements

- TTS synthesis must remain outside Microsoft Agent Framework orchestration; the agent decides response text and the TTS service converts final text to audio.
- Microsoft Agent Framework instructions must keep response language aligned with `UserProfile.PreferredLanguage` before audio is generated.
- User-owned chat messages and audio metadata must always be scoped by `UserId` before returning or generating audio.
- Controllers should coordinate HTTP flow only; business behavior belongs in focused services behind interfaces.
- Supertonic and ONNX Runtime details must stay inside the TTS service adapter so `WebApp` depends on an HTTP contract rather than model runtime types.
- Audio generation should be idempotent for `{chat_message_id, language, voice}` and resilient to repeated play clicks.
- The TTS service should load model assets once per process lifetime where the Supertonic adapter supports it.
- Docker and Make changes must preserve OS-specific Ollama overrides and the existing `DOCKER_HOST` auto-detection.
- The TTS service may include a deterministic placeholder adapter only as a development fallback while the Supertonic C# example is being wired; placeholder mode must be explicitly configured and must not be the default when assets are present.

## Out of Scope

- Autoplaying assistant responses.
- Calling Supertonic directly from `ChatController` or from Microsoft Agent Framework tools.
- Streaming partial audio while synthesis is still running.
- A full voice picker with individual Supertonic preset selection beyond the simple male/female profile preference.
- More than the two profile voice types `female` and `male`.
- Committing Supertonic model assets, generated audio files, or local audio storage data to Git.
- Cloud TTS providers.
- MinIO or S3-compatible storage in the first implementation.

## Resolved Decisions

- Development audio storage will use a filesystem-backed implementation mounted as a Docker volume.
- The WebApp will depend on a clean audio storage abstraction so production providers such as S3-compatible storage can be added later without changing controller or chat UI code.
- A non-authoritative code example reference is copied into this spec folder at [CodeExample.md](CodeExample.md). It is not a tutorial or source of truth for scope.
