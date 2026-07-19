# Requirements: Multiple Free Ollama Agents

## Table of Contents

- [Requirements: Multiple Free Ollama Agents](#requirements-multiple-free-ollama-agents)
  - [Problem Statement](#problem-statement)
  - [User Stories](#user-stories)
  - [Functional Requirements](#functional-requirements)
  - [Non-Functional Requirements](#non-functional-requirements)
  - [Out of Scope](#out-of-scope)
  - [Open Questions](#open-questions)

## Problem Statement

`WebApp/Program.cs` currently registers two chat agents: `"premium"` for Azure OpenAI and `"free"` for one local Ollama model configured by `Ollama:OllamaModel` (`qwen3.5:4b`). `WebApp/Controllers/ChatController.cs`, `WebApp/Views/Chat/_AgentIndicator.cshtml`, and `WebApp/wwwroot/js/site.js` normalize all non-premium selections back to `"free"`, so readers cannot choose among multiple free local models. The app needs distinct free local agents for Qwen, Llama, Phi-4, and Granite while keeping the existing Microsoft Agent Framework provider-aware flow easy to extend with future Ollama models.

> **Amendment (implementation-time discovery):** The initial model picks for the second and third free agents were DeepSeek R1 (`deepseek-r1:1.5b`) and Gemma 3 (`gemma3:4b`). Manual verification showed both fail at send time with `does not support tools` — Ollama's `deepseek-r1` and `gemma3` families carry no `tools` capability tag at any size (confirmed against `ollama.com/library`), and every tool call in this app (`GenerateBookContext`, `GetBookNotesWithAnalysis`, `GetRelevantBookNotes`) is mandatory, not optional. The requirements, plan, and validation below were updated in place to the confirmed tool-calling models: `llama3.2:3b`, `phi4-mini:3.8b`, and `granite4:3b`, adding a fourth free agent rather than dropping one, since none of the three is a drop-in size/vendor match for the original two.

## User Stories

- Given a signed-in reader in chat, when they open the agent selector, then they can choose Premium ChatGPT or one of several free local agents: Qwen, Llama, Phi-4, or Granite.
- Given a reader selects `free-llama3`, when they send a chat message, then the Microsoft Agent Framework turn and book-context generation both use the local Ollama `llama3.2:3b` model.
- Given a reader selects `free-granite4`, when the app persists the assistant response, then `ChatMessage.AgentType` records `free-granite4` and the chat UI shows a friendly free-agent label without exposing the word "Ollama".
- Given a developer wants to add another free local model later, when they update the model catalog/configuration, then they do not need to duplicate normalization, labels, menu rendering, or keyed registration logic in several unrelated places.

## Functional Requirements

1. FR1 - Replace the single `"free"` agent option with four distinct free local agent keys: `free-qwen`, `free-llama3`, `free-phi4`, and `free-granite4`; keep `"premium"` as the Azure OpenAI key.
2. FR2 - Preserve backwards compatibility by normalizing legacy persisted/cached `"free"` values to `free-qwen`, so existing users continue on Qwen unless they choose another model.
3. FR3 - Register one Microsoft Agent Framework `ChatClientAgent` and one `IChatClient` per free model in `WebApp/Program.cs`, using Ollama model names `qwen3.5:4b`, `llama3.2:3b`, `phi4-mini:3.8b`, and `granite4:3b`. All four models must support native Ollama tool calling, since `GenerateBookContext`, `GetBookNotesWithAnalysis`, and `GetRelevantBookNotes` are attached to every chat turn as mandatory tools.
4. FR4 - Apply the same Ollama chat options used by the current Qwen path to every free local model: `Temperature = 0`, `think = false`, and `num_ctx` from `Ollama:NumCtx`.
5. FR5 - `ChatController.NormalizeAgentKey`, `GetAgentLabel`, `BotMessageViewModel.AgentLabel`, and the client-side `normalizeAgentKey` in `WebApp/wwwroot/js/site.js` accept only the supported keys and default invalid or empty values to `free-qwen`.
6. FR6 - `_AgentIndicator.cshtml` renders separate selectable entries for Qwen, Llama, Phi-4, and Granite, marks each as free/local/private in user-facing copy, and does not display the provider name "Ollama" in the visible labels or subtitles.
7. FR7 - `ChatController.Send` persists the exact selected free key (`free-qwen`, `free-llama3`, `free-phi4`, or `free-granite4`) to `activeagent:{userId}` and to assistant `ChatMessage.AgentType`.
8. FR8 - `BookContextAgentTool` and `BookContextService.GenerateAndSaveAsync` continue to receive the selected `agentKey`, so generated literary context uses the exact selected free model or Premium model for that turn.
9. FR9 - `docker-compose.yml` pulls all required local models at startup: `qwen3.5:4b`, `llama3.2:3b`, `phi4-mini:3.8b`, `granite4:3b`, and `mxbai-embed-large`.
10. FR10 - The implementation introduces a single local model catalog or equivalent focused abstraction that centralizes supported keys, labels, model names, provider category, and defaults so future free Ollama models can be added with minimal localized changes.

## Non-Functional Requirements

- SOLID / Open-Closed: model metadata, key validation, labels, and keyed service registration should be centralized behind a narrow catalog/registry instead of repeated switch statements in controllers, Razor views, and JavaScript.
- Testability: unit tests must fake chat clients/agents where possible; test coverage should prove key normalization, label mapping, provider resolution, and selected-agent persistence without calling real Ollama or Azure endpoints.
- Backwards compatibility: existing chat history rows with `AgentType = "free"` and existing Redis `activeagent:{userId}` values of `"free"` must still render and route as Qwen.
- Local-first behavior: all free agents run through the existing local model runtime; embeddings remain on `mxbai-embed-large`.
- UI accessibility: the agent selector must retain keyboard/menu semantics through Shoelace, clear checked state, and compact labels that fit in the existing chat dock on desktop and mobile.
- Docker-first workflow: verification commands must use Make/Docker patterns from `AGENTS.md`; model pulling is performed by the `ollama` service startup command.

## Out of Scope

- Adding non-Ollama local runtimes.
- Adding more Azure OpenAI deployments or a Premium model picker.
- Per-user custom model names, arbitrary model entry forms, or admin model management.
- Database migrations beyond using the existing nullable `ChatMessage.AgentType` string column.
- Changing the embedding model or pgvector lookup behavior.
- Automatic fallback between local models if the selected model is unavailable.

## Open Questions

- None. Discovery resolved that Qwen, Llama, Phi-4, and Granite should be separate free agents; keys should be `free-qwen`, `free-llama3`, `free-phi4`, and `free-granite4`; all local models should auto-pull; book context generation should use the selected model; and the implementation should make future model additions easy. (Originally DeepSeek R1 and Gemma 3 were chosen for two of the free slots; both were dropped mid-implementation after manual verification showed neither supports Ollama tool calling at any size — see the amendment note in the Problem Statement.)
