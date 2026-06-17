# Validation: Supertonic Text-to-Speech Service

## Table of Contents

- [Validation: Supertonic Text-to-Speech Service](#validation-supertonic-text-to-speech-service)
  - [Acceptance Criteria](#acceptance-criteria)
  - [Test Cases](#test-cases)
  - [Manual Verification](#manual-verification)
  - [Definition of Done](#definition-of-done)
  - [Rollback Plan](#rollback-plan)

## Acceptance Criteria

| Requirement | Acceptance Criterion |
| --- | --- |
| FR1 | `services/tts-service/TtsService.Api` exposes `POST /tts/synthesize` and returns a valid `audio/wav` response for supported text/language/voice input when assets are mounted. |
| FR2 | `make docker-run`, `make docker-run-mac`, and `make docker-run-windows` include the TTS service through the base Compose file. |
| FR3 | Supertonic assets are ignored by Git, documented as Git LFS downloads, mounted read-only, and configured through `Supertonic:AssetsPath`. |
| FR4 | EF migration creates `chat_message_audio` with FK, unique constraint, and indexes; PostgreSQL rejects duplicate `{chat_message_id, language, voice}` rows. |
| FR5 | WebApp audio service uses `UserProfile.PreferredLanguage` and voice preference, returns cached audio when matching metadata exists, and calls the TTS client only on cache miss. |
| FR6 | Generated WAV bytes are stored through filesystem-backed audio storage mounted as a Docker volume, while PostgreSQL stores references and metadata only. |
| FR7 | Audio retrieval returns `404` or `403` equivalent behavior when the chat message is missing, not an assistant message, or belongs to another user. |
| FR8 | Assistant messages render a play control and no audio request is made until the user activates it. |
| FR9 | The profile form saves `female` or `male` to `UserProfile` and reloads the selected value on the next profile edit. |
| FR10 | The play control exposes loading and failure states without breaking the existing chat append, scroll, and context-ring behavior. |
| FR11 | TTS generation maps language and voice type together: `pt`/female -> `F3`, `pt`/male -> `M3`, `en`/female -> `F3`, and `en`/male -> `M3`. |
| FR12 | Assistant response text is generated in the user's preferred language even when the referenced book or source context is in another language. |
| FR13 | Unit, controller, and integration tests cover the new service boundaries, profile voice persistence, user isolation, cache behavior, and database constraints. |

## Test Cases

**Unit tests:**

- `services/tts-service/TtsService.Tests/VoiceResolverTests.cs`: verify `en`, `pt`, `pt-br`, `english`, and `portuguese` normalize correctly and default to `F3`/`M3`.
- `services/tts-service/TtsService.Tests/VoiceResolverTests.cs`: verify English/Portuguese plus male/female combinations resolve to `M3`/`F3` as expected.
- `WebApp.Tests/Services/FileSystemAudioStorageTests.cs`: verify storage writes, reads, deletes, rejects path traversal keys, and returns stable storage references.
- `WebApp.Tests/Services/ChatMessageAudioServiceTests.cs`: cache hit returns stored audio bytes without calling `ITtsClient`.
- `WebApp.Tests/Services/ChatMessageAudioServiceTests.cs`: cache miss loads assistant text, resolves `UserProfile.PreferredLanguage` plus voice preference, calls `ITtsClient` with the matching language and voice, writes audio through `IAudioStorage`, and inserts metadata.
- `WebApp.Tests/Services/ChatMessageAudioServiceTests.cs`: duplicate generation conflict re-reads existing metadata and removes any orphan file from the losing request.
- `WebApp.Tests/Services/ChatMessageAudioServiceTests.cs`: other-user, missing, and non-assistant messages do not generate or return audio.
- `WebApp.Tests/Services/TtsClientTests.cs`: verify request body and `audio/wav` response handling with a fake `HttpMessageHandler`.
- `WebApp.Tests/Controllers/UserProfileControllerTests.cs`: verify profile create/update persists the selected voice preference and falls back to `female` when omitted.

**Controller tests:**

- `WebApp.Tests/Controllers/ChatControllerTests.cs`: authenticated user can request audio for their assistant message.
- `WebApp.Tests/Controllers/ChatControllerTests.cs`: unauthenticated or cross-user request is rejected.
- `WebApp.Tests/Controllers/ChatControllerTests.cs`: TTS/storage failures return a controlled error response.
- `WebApp.Tests/Controllers/ChatControllerProfileTests.cs`: verify generated Microsoft Agent Framework instructions explicitly require replies in the user's preferred language.

**View and JavaScript checks:**

- Render `WebApp/Views/Chat/Chat.cshtml` with historical assistant messages and verify a play control includes the message id.
- Render `WebApp/Views/Chat/_BotMessage.cshtml` for the latest HTMX response and verify the play control coexists with `agent-response` and `context-ring` OOB fragments.
- Exercise `WebApp/wwwroot/js/site.js` click-to-play behavior manually or through a lightweight browser test if the project adds browser tooling later.

**Integration tests:**

- `WebApp.Tests/Integration/...`: use PostgreSQL to verify the `chat_message_audio` FK, cascade behavior, unique constraint, and lookup indexes.
- Optional Docker Compose smoke test: with Supertonic assets mounted, call `tts` service directly and verify WAV response headers and non-empty bytes. This test must not be required by normal `make test` unless assets are present and the test is explicitly enabled.
- End-to-end manual integration: create a chat message, press play, verify first request generates audio and second request reads cached filesystem storage metadata.

## Manual Verification

1. Download Supertonic assets outside Git:

   ```bash
   mkdir -p services/tts-service/assets
   cd services/tts-service/assets
   git lfs install
   git clone https://huggingface.co/Supertone/supertonic-3 supertonic-3
   ```

2. Start the stack with the OS-specific Make target documented in `AGENTS.md`, for example on Linux/SteamOS:

   ```bash
   make docker-run
   ```

3. Confirm `webapp`, `tts`, `postgres`, `redis`, and `ollama` containers are running, and that the named audio storage volume is mounted into `webapp`.
4. Sign in, open chat, and send a prompt that produces an assistant answer.
5. Open `My Profile`, choose a preferred language and a male/female voice preference, save, then reload the profile to confirm both values persisted.
6. Ask about a book whose original language differs from the preferred language and confirm the assistant answers in the preferred language.
7. Confirm the assistant message shows a play control and audio does not start automatically.
8. Press play and confirm audio uses the user's preferred language plus voice type, for example Portuguese + Male uses the Portuguese `M3` voice.
9. Press play again and confirm logs or metadata show cached audio is reused instead of regenerated.
10. Stop the stack with the matching down target, such as `make docker-down`.

## Definition of Done

- Requirements, Plan, and Validation docs are updated in this spec folder.
- The TTS API is a separate service under `services/tts-service/`.
- Docker Compose and Make workflows bring the TTS service up with the rest of the stack.
- Supertonic model assets remain outside Git and are mounted read-only for local development.
- `chat_message_audio` metadata is persisted in PostgreSQL with FK, uniqueness, and lookup indexes.
- Audio bytes are persisted through `IAudioStorage` using the filesystem-backed Docker volume implementation.
- User profile stores a male/female voice preference and the profile UI can edit it.
- Microsoft Agent Framework responses use the user's preferred language regardless of source book language.
- Chat UI has explicit user-initiated playback with loading and error states.
- Audio generation is user-scoped, profile-language and profile-voice driven, cached, and covered by tests.
- `make test` passes through `docker-compose.test.yml`.

## Rollback Plan

- Disable the chat play controls by reverting the Razor and `site.js` changes.
- Remove or disable the WebApp TTS service registration and audio endpoint in `WebApp/Program.cs` and `ChatController`.
- Leave `chat_message_audio` metadata unused, or roll back the EF migration if no production data needs preservation.
- Stop including the `tts` service in `docker-compose.yml` if local startup is affected.
- Keep generated audio storage data outside Git so rollback does not require repository cleanup.
