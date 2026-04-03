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

    function scrollChatToBottom() {
        const container = document.getElementById("chat-container");

        if (!container) {
            return;
        }

        window.requestAnimationFrame(() => {
            container.scrollTop = container.scrollHeight;
        });
    }

    document.addEventListener("DOMContentLoaded", () => {
        setMode("ai");
    });

    document.body.addEventListener("htmx:afterSwap", (event) => {
        const target = event.detail.target;

        if (!(target instanceof HTMLElement)) {
            return;
        }

        if (target.id === "chat-container" || target.id === "chat") {
            scrollChatToBottom();
        }
    });

    window.BookNotesChat = {
        setMode
    };
})();
