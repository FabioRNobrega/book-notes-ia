// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

(function () {
    function setComposerEnabled(enabled) {
        const composer = document.getElementById("chat-composer");
        const form = document.getElementById("chat-form");
        const message = document.getElementById("message");

        if (!composer || !form) {
            return;
        }

        composer.classList.toggle("hidden", !enabled);

        form.querySelectorAll("input, button").forEach((element) => {
            element.disabled = !enabled;
        });

        if (enabled) {
            window.setTimeout(() => message?.focus(), 0);
        }
    }

    function setActiveModeButton(mode) {
        document.querySelectorAll("[data-chat-mode-button]").forEach((button) => {
            const isActive = button.getAttribute("data-chat-mode-button") === mode;
            button.setAttribute("variant", isActive ? "primary" : "default");
            button.toggleAttribute("outline", true);
        });
    }

    function setMode(mode) {
        const isAiMode = mode === "ai";
        setComposerEnabled(isAiMode);
        setActiveModeButton(mode);
    }

    function scrollContainerToBottom(container) {
        if (!container) {
            return;
        }

        window.requestAnimationFrame(() => {
            container.scrollTop = container.scrollHeight;
        });
    }

    function scrollContainerToTop(container) {
        if (!container) {
            return;
        }

        window.requestAnimationFrame(() => {
            container.scrollTop = 0;
        });
    }

    function scrollChatToBottom() {
        scrollContainerToBottom(document.getElementById("chat-container"));
    }

    function isChatView(container) {
        return !!container?.querySelector("#chat");
    }

    function syncContainerScroll(container) {
        if (!container) {
            return;
        }

        if (isChatView(container)) {
            scrollContainerToBottom(container);
            return;
        }

        scrollContainerToTop(container);
    }

    function appendChatHtml(html) {
        const chat = document.getElementById("chat");

        if (!chat) {
            return;
        }

        const wrapper = document.createElement("div");
        wrapper.innerHTML = html;

        while (wrapper.firstElementChild) {
            const node = wrapper.firstElementChild;
            wrapper.removeChild(node);
            chat.appendChild(node);

            if (window.htmx) {
                window.htmx.process(node);
            }
        }

        scrollChatToBottom();
    }

    async function importChatFile(fileInput) {
        const file = fileInput.files?.[0];

        if (!file) {
            return;
        }

        const formData = new FormData();
        formData.append("file", file, file.name);

        try {
            const response = await fetch("/notes/import-chat", {
                method: "POST",
                body: formData
            });

            const html = await response.text();
            appendChatHtml(html);
        } catch (error) {
            console.error("Chat file import failed", error);
            appendChatHtml(`<div class="flex flex-col max-w-[75%]"><div class="text-white/90 px-4 py-3 text-lg break-words" style="background-color: var(--background-transparency-default); border-radius: var(--book-radius);"><p>We couldn't import this file right now. Please try again.</p></div></div>`);
        } finally {
            fileInput.value = "";
        }
    }

    document.addEventListener("DOMContentLoaded", () => {
        setMode("ai");
    });

    document.addEventListener("change", (event) => {
        const target = event.target;

        if (!(target instanceof HTMLInputElement)) {
            return;
        }

        if (target.matches("[data-chat-file-input]")) {
            void importChatFile(target);
        }
    });

    document.body.addEventListener("htmx:afterSwap", (event) => {
        const target = event.detail.target;

        if (!(target instanceof HTMLElement)) {
            return;
        }

        if (target.id === "chat-container") {
            syncContainerScroll(target);
        }
    });

    document.body.addEventListener("htmx:afterSettle", () => {
        const container = document.getElementById("chat-container");
        const agentResponse = document.getElementById("agent-response");

        if (!container || !agentResponse || agentResponse.querySelector("sl-spinner")) {
            return;
        }

        agentResponse.scrollIntoView({ behavior: "smooth", block: "start" });

        window.setTimeout(() => document.getElementById("message")?.focus(), 0);
    });

    // Shared audio instance — only one message plays at a time
    let currentAudio = null;
    let currentObjectUrl = null;

    function cleanupCurrentAudio() {
        if (currentAudio) {
            currentAudio.pause();
            currentAudio = null;
        }
        if (currentObjectUrl) {
            URL.revokeObjectURL(currentObjectUrl);
            currentObjectUrl = null;
        }
    }

    function setPlayButtonState(button, state) {
        const icon = button.querySelector("sl-icon");
        if (!icon) return;

        button.disabled = state === "loading";
        icon.setAttribute("name",
            state === "loading" ? "hourglass-split"
            : state === "error" ? "exclamation-circle"
            : state === "playing" ? "pause-circle"
            : "play-circle");
        button.title =
            state === "loading" ? "Loading audio…"
            : state === "error" ? "Audio unavailable"
            : state === "playing" ? "Playing…"
            : "Listen to this response";
    }

    async function handlePlayClick(button) {
        const messageId = button.getAttribute("data-audio-message-id");
        if (!messageId) return;

        cleanupCurrentAudio();
        setPlayButtonState(button, "loading");

        try {
            const response = await fetch(`/chat/messages/${messageId}/audio`);

            if (!response.ok) {
                setPlayButtonState(button, "error");
                return;
            }

            const blob = await response.blob();
            const url = URL.createObjectURL(blob);
            currentObjectUrl = url;

            const audio = new Audio(url);
            currentAudio = audio;

            // Guard against stale closures: only act if this audio is still the active one.
            audio.addEventListener("ended", () => {
                if (currentAudio === audio) {
                    setPlayButtonState(button, "play");
                    cleanupCurrentAudio();
                }
            });

            audio.addEventListener("error", () => {
                if (currentAudio === audio) {
                    setPlayButtonState(button, "error");
                    cleanupCurrentAudio();
                }
            });

            setPlayButtonState(button, "playing");
            await audio.play();
        } catch {
            setPlayButtonState(button, "error");
            cleanupCurrentAudio();
        }
    }

    document.body.addEventListener("click", (event) => {
        const button = event.target.closest(".tts-play-btn");
        if (button instanceof HTMLButtonElement) {
            void handlePlayClick(button);
        }
    });

    window.BookNotesChat = {
        setMode
    };
})();
