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

    window.BookNotesChat = {
        setMode
    };
})();
