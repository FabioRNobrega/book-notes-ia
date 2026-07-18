# Requirements: TTS Audio Download

## Table of Contents

- [Requirements: TTS Audio Download](#requirements-tts-audio-download)
  - [Table of Contents](#table-of-contents)
  - [Problem Statement](#problem-statement)
  - [User Stories](#user-stories)
  - [Functional Requirements](#functional-requirements)
  - [Non-Functional Requirements](#non-functional-requirements)
  - [Out of Scope](#out-of-scope)
  - [Open Questions](#open-questions)

## Problem Statement

The chat TTS widget (`WebApp/Views/Chat/_TtsAudioPlayer.cshtml`, wired up in `WebApp/wwwroot/js/site.js`) lets a user play a generated assistant response as audio, with play/pause, progress seeking, and per-message position memory (added in [20260718175413-tts-audio-progress-seeking](../20260718175413-tts-audio-progress-seeking/Requirements.md)). There is currently no way to save that audio locally — the browser only ever holds it in memory as a `Blob`/`ObjectURL` for playback (`site.js:412-426`) and it is discarded once the widget resets. Users who want to keep or share a spoken response have no export path.

## User Stories

- Given an assistant message with TTS available, when I click the download control, then the browser saves the message's generated audio as a local file without interrupting any other message currently playing.
- Given I have already played a message's audio this session, when I click download for that same message, then the app reuses the audio already fetched instead of requesting it again.
- Given I have not yet played a message's audio, when I click download, then the app fetches the audio from the server on demand (same as the first click of play) and then saves it.
- Given audio generation or fetch fails, when I click download, then I see an inline error state on the download control instead of a silent failure or a broken browser download.

## Functional Requirements

1. FR1 - `WebApp/Views/Chat/_TtsAudioPlayer.cshtml` must render a download icon button next to the existing play/pause `sl-icon-button`, inside the same `.tts-widget` used by both `Chat.cshtml` and `_BotMessage.cshtml`.
2. FR2 - The download control must be available for every assistant message that has a `MessageId` (i.e. wherever the TTS widget already renders today), independent of whether that message's audio has been played yet.
3. FR3 - Clicking download must fetch `GET /chat/messages/{messageId}/audio` (the existing `ChatController.GetMessageAudio` endpoint) only if audio for that widget has not already been fetched in this session; if the widget is the currently loaded/playing widget with an active `ObjectURL`, that same blob must be reused instead of issuing a second request.
4. FR4 - Once the audio bytes are available, `WebApp/wwwroot/js/site.js` must trigger a native browser file download (temporary `<a download>` element) using the content type already returned by the endpoint (`audio/wav`), with a `.wav` file extension — no MP3 transcoding is performed by this feature.
5. FR5 - The downloaded filename must be derived from the message id, e.g. `book-response-{messageId}.wav`, so it is stable and free of characters that are unsafe for a filename.
6. FR6 - Clicking download must never start, pause, or otherwise affect playback state of the widget's own audio or any other widget's currently playing audio.
7. FR7 - The download control must show its own transient loading indicator (e.g. a spinner swapped in for the download icon) while a fetch it triggered is in flight, and its own brief error indicator if that fetch fails — reusing the shared `.tts-loading`/`.tts-error` widget-level banners would incorrectly imply playback itself is broken, so the download state must stay visually scoped to the download button and must never disable or hide the play control.
8. FR8 - `ChatController.GetMessageAudio`, `IChatMessageAudioService`, `IAudioStorage`, `ChatMessageAudio`, and the TTS synthesis/caching flow must remain unchanged — this feature is a client-side save of bytes already served by the existing endpoint.

## Non-Functional Requirements

- Accessibility: the download button must have an accessible label (e.g. `aria-label`/`label` of "Download audio") distinct from the play button's label, and must be keyboard-operable like the existing `sl-icon-button` controls.
- No new dependency: no new NuGet package, npm package, native binary, or Docker image change is introduced; the feature is implemented entirely with the existing `fetch`/`Blob`/`URL.createObjectURL` primitives already used by `site.js`.
- SOLID / architecture: the change is confined to a Razor partial and the existing client-side TTS module in `site.js`; no new controller action, service, or EF Core model is needed since FR8 keeps the server surface untouched.
- Consistency: the download control must reuse the existing `.tts-widget` state-machine conventions (`setAudioState`, loading/error markup patterns) rather than introducing a parallel state model.

## Out of Scope

- Converting WAV audio to MP3 or any other codec/container. The user explicitly chose to skip server-side conversion (see project decision below); the downloaded file is WAV audio with a `.wav` extension.
- Persisting a converted or re-encoded file server-side, or adding any new storage/database column.
- Bulk/whole-conversation download, or downloading multiple messages at once.
- Changing TTS synthesis, voice/language selection, or the audio cache keyed by `{ChatMessageId, Language, Voice}`.
- Download availability outside the chat TTS widget (e.g. a notes/library-level export).

## Open Questions

- None outstanding. During discovery the user explicitly rejected WAV→MP3 conversion (no encoder exists anywhere in the stack today; adding one means either shelling out to `ffmpeg` or pulling in a native LAME wrapper) in favor of directly downloading the audio bytes the frontend already has access to. The feature therefore ships a "Download audio" (`.wav`) control, not a literal MP3 export — flagged here since the original ask referenced "mp3" specifically.
