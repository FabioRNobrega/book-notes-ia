# Requirements: TTS Audio Progress Seeking

## Table of Contents

- [Requirements: TTS Audio Progress Seeking](#requirements-tts-audio-progress-seeking)
  - [Table of Contents](#table-of-contents)
  - [Problem Statement](#problem-statement)
  - [User Stories](#user-stories)
  - [Functional Requirements](#functional-requirements)
  - [Non-Functional Requirements](#non-functional-requirements)
  - [Out of Scope](#out-of-scope)
  - [Open Questions](#open-questions)

## Problem Statement

Assistant TTS messages currently expose only a play/pause icon in `WebApp/Views/Chat/Chat.cshtml`, `WebApp/Views/Chat/_BotMessage.cshtml`, and the shared playback logic in `WebApp/wwwroot/js/site.js`. Once generated audio begins, the user cannot see elapsed progress, know the total duration, or seek forward/backward within a response, which makes longer spoken answers difficult to scan or replay.

## User Stories

- Given an assistant message with TTS available, when audio is generating or loading, then I see the audio control area reserved with a disabled progress state so the layout does not jump.
- Given an assistant message is playing, when time advances, then I see a progress bar and elapsed/total timestamp update in sync with the audio.
- Given generated audio is loaded, when I click or drag the progress bar, then playback seeks to the selected position.
- Given I pause an assistant message and play another message, when I return to the first message during the same page session, then its previous position is remembered.
- Given I reload the chat page, when previously played messages render again, then their in-memory playback positions may reset to the beginning.

## Functional Requirements

1. FR1 - The chat TTS widget in `WebApp/Views/Chat/Chat.cshtml` and `WebApp/Views/Chat/_BotMessage.cshtml` must render a progress control and timestamp area for assistant messages with `MessageId`.
2. FR2 - The progress control must show a disabled/loading state while `/chat/messages/{messageId}/audio` is being fetched and before the browser knows the audio duration.
3. FR3 - Once audio metadata is available, the UI must display elapsed and total duration text in `m:ss` or `h:mm:ss` format as appropriate.
4. FR4 - While audio plays, `WebApp/wwwroot/js/site.js` must update the progress value and elapsed time from the active `HTMLAudioElement`.
5. FR5 - Users must be able to seek by clicking the progress track and by dragging the control thumb.
6. FR6 - The player must remember each message's playback position in client-side memory for the lifetime of the loaded page, including when switching between assistant messages.
7. FR7 - When playback ends, the active message must return to an idle/playable state and reset that message's remembered position to the start.
8. FR8 - Loading and error states must continue to work without exposing a broken or interactive seek bar.
9. FR9 - The audio player behavior must be structured so the UI state and seek logic can be reused by another message-level audio widget without duplicating all event handling.
10. FR10 - The existing `ChatController.GetMessageAudio`, `IChatMessageAudioService`, audio cache, and TTS synthesis flow must remain unchanged unless a future implementation finds a browser metadata issue that requires response header adjustment.

## Non-Functional Requirements

- Accessibility: the seek control must have an accessible label, keyboard-operable seeking, and play/pause labels that reflect the current action.
- Responsive layout: the widget must fit inside the existing chat message footer on mobile and desktop without overlapping the agent label or response-time text.
- Testability: time formatting, state transitions, and per-message position memory should be implemented in small JavaScript functions that can be reasoned about manually and covered by future frontend tests if a JS test harness is added.
- SOLID: server-side audio generation and storage responsibilities must stay in `ChatMessageAudioService`; controller code must continue to coordinate HTTP flow only; reusable playback behavior belongs in a focused client-side module.
- Performance: progress updates must use native audio events such as `timeupdate`, `loadedmetadata`, `seeking`, and `ended`, avoiding polling loops that continue after playback stops.

## Out of Scope

- Persisting playback position to PostgreSQL, Redis, localStorage, or the user's profile.
- Changing TTS synthesis quality, voices, audio format, or Supertonic service behavior.
- Adding transcript highlighting, captions, waveform rendering, playback speed, skip buttons, or download controls.
- Replacing the existing `/chat/messages/{messageId}/audio` endpoint.

## Open Questions

- None. The initial implementation should show the control immediately in a disabled/loading state, include timestamps, support click and drag seeking, remember positions only for the current page session, and keep the player reusable.
