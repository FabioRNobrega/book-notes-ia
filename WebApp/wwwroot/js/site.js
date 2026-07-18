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

    function normalizeAgentKey(value) {
        return value === "premium" ? "premium" : "free";
    }

    function syncAgentInputs(agentKey) {
        const normalized = normalizeAgentKey(agentKey);
        const hiddenInput = document.getElementById("active-agent-input");
        if (hiddenInput instanceof HTMLInputElement) {
            hiddenInput.value = normalized;
        }
        return normalized;
    }

    function updateAgentTrigger(indicator, agentKey) {
        if (!(indicator instanceof HTMLElement)) {
            return;
        }

        const isPremium = agentKey === "premium";
        const dot = indicator.querySelector("#agent-trigger-dot");
        const label = indicator.querySelector("#agent-trigger-label");

        if (dot) {
            dot.classList.toggle("bg-amber-300", isPremium);
            dot.classList.toggle("bg-emerald-400", !isPremium);
        }
        if (label) {
            label.textContent = isPremium ? "Premium" : "Free";
        }

        indicator.setAttribute("data-active-agent", agentKey);
    }

    async function persistAgentSelection(agentKey) {
        const normalized = syncAgentInputs(agentKey);
        const indicator = document.getElementById("active-agent-indicator");

        if (indicator && typeof indicator.hide === "function") {
            indicator.hide();
        }
        updateAgentTrigger(indicator, normalized);

        try {
            const response = await fetch("/chat/agent", {
                method: "POST",
                headers: {
                    "Content-Type": "application/x-www-form-urlencoded"
                },
                body: new URLSearchParams({ agentKey: normalized })
            });

            if (!response.ok) {
                return;
            }

            const html = await response.text();
            const wrapper = document.createElement("div");
            wrapper.innerHTML = html;
            const nextIndicator = wrapper.querySelector("#active-agent-indicator");
            const currentIndicator = document.getElementById("active-agent-indicator");

            if (nextIndicator && currentIndicator) {
                currentIndicator.replaceWith(nextIndicator);
            }
        } catch (error) {
            console.error("Agent selection update failed", error);
        }
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
        const indicator = document.getElementById("active-agent-indicator");
        if (indicator) {
            syncAgentInputs(indicator.getAttribute("data-active-agent"));
        }
    });

    document.addEventListener("sl-select", (event) => {
        const menu = event.target;
        if (menu?.id !== "agent-menu") {
            return;
        }

        const item = event.detail?.item;
        if (!item) {
            return;
        }

        void persistAgentSelection(item.value);
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

    // Shared audio instance — only one message plays at a time.
    let currentAudio = null;
    let currentObjectUrl = null;
    let currentWidget = null;

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

    function setAudioState(widget, state) {
        const tooltip = widget.querySelector(".tts-tooltip");
        const btn = widget.querySelector(".tts-play-btn");
        const loading = widget.querySelector(".tts-loading");
        const error = widget.querySelector(".tts-error");

        // Reset to idle defaults first.
        if (tooltip) tooltip.style.display = "";
        if (tooltip) tooltip.setAttribute("content", "Listen to this response");
        if (btn) btn.setAttribute("name", "play-circle");
        if (btn) btn.setAttribute("label", "Listen to this response");
        if (loading) loading.style.display = "none";
        if (error) error.style.display = "none";

        if (state === "loading") {
            if (tooltip) tooltip.style.display = "none";
            if (loading) loading.style.display = "inline-flex";
        } else if (state === "playing") {
            if (btn) btn.setAttribute("name", "pause-circle");
            if (btn) btn.setAttribute("label", "Pause");
            if (tooltip) tooltip.setAttribute("content", "Pause");
        } else if (state === "paused") {
            if (btn) btn.setAttribute("label", "Resume");
            if (tooltip) tooltip.setAttribute("content", "Resume");
        } else if (state === "error") {
            if (tooltip) tooltip.style.display = "none";
            if (error) error.style.display = "inline-flex";
        }
        // "idle" is fully handled by the reset above.
    }

    async function handlePlayClick(widget) {
        const messageId = widget.getAttribute("data-audio-message-id");
        if (!messageId) return;

        // Toggle play/pause if audio is already loaded for this widget.
        if (currentWidget === widget && currentAudio) {
            if (currentAudio.paused) {
                try {
                    await currentAudio.play();
                    setAudioState(widget, "playing");
                } catch {
                    setAudioState(widget, "error");
                    cleanupCurrentAudio();
                    currentWidget = null;
                }
            } else {
                currentAudio.pause();
                setAudioState(widget, "paused");
            }
            return;
        }

        // Reset any previously active widget before starting a new one.
        if (currentWidget && currentWidget !== widget) {
            setAudioState(currentWidget, "idle");
        }

        cleanupCurrentAudio();
        currentWidget = widget;
        setAudioState(widget, "loading");

        try {
            const response = await fetch(`/chat/messages/${messageId}/audio`);

            if (!response.ok) {
                setAudioState(widget, "error");
                currentWidget = null;
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
                    setAudioState(widget, "idle");
                    cleanupCurrentAudio();
                    currentWidget = null;
                }
            });

            audio.addEventListener("error", () => {
                if (currentAudio === audio) {
                    setAudioState(widget, "error");
                    cleanupCurrentAudio();
                    currentWidget = null;
                }
            });

            await audio.play();
            setAudioState(widget, "playing");
        } catch {
            setAudioState(widget, "error");
            cleanupCurrentAudio();
            currentWidget = null;
        }
    }

    document.body.addEventListener("click", (event) => {
        const btn = event.target.closest(".tts-play-btn");
        if (!btn) return;
        const widget = btn.closest(".tts-widget");
        if (widget) void handlePlayClick(widget);
    });

    window.BookNotesChat = {
        setMode
    };
})();
