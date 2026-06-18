# Requirements: Alert Notice Restyle with Shoelace

## Table of Contents

- [Problem Statement](#problem-statement)
- [User Stories](#user-stories)
- [Functional Requirements](#functional-requirements)
- [Non-Functional Requirements](#non-functional-requirements)
- [Out of Scope](#out-of-scope)
- [Open Questions](#open-questions)

## Problem Statement

The current `WebApp/Views/Shared/Components/_Alert.cshtml` renders a Shoelace `<sl-alert>` with default Shoelace visual tokens (white background, coloured left band, system-font). It is injected via HTMX into a `#alert` div fixed at `top-4 right-4` in `_Layout.cshtml`, and uses Hyperscript `on load call me.toast()` to pop it into a Shoelace-managed portal appended to the document body. This look does not match the warm, sepia, serif aesthetic of the rest of the application. The Shoelace `<sl-alert>` component already provides the correct semantic structure — the only change needed is to override its default color tokens and shadow-DOM parts via CSS `::part()` selectors so the palette matches the warm-notice reference design in `notifcation-example.html`.

## User Stories

- Given I save my profile, when the controller returns the `_Alert.cshtml` partial, then I see a warm sepia-tinted notice with a success accent that stays visible until I dismiss it.
- Given the agent resets a chat session, when the controller returns the `_Alert.cshtml` partial, then I see a danger-accented notice that stays on screen until I click the close button.
- Given a notice appears, when I want to dismiss it, then I can click the close button and the notice disappears immediately.
- Given I am reading the app on a small screen, when a notice appears, then it does not overflow the viewport and remains readable.

## Functional Requirements

1. **FR1** — `_Alert.cshtml` must use Shoelace `<sl-alert>` with `variant="@(Model.ok ? "success" : "danger")"` and `closable`, calling `me.show()` via Hyperscript so the element stays inside the `#alert` container rather than being moved into the Shoelace body portal.
2. **FR2** — The notice visual design must follow `notifcation-example.html`: dark warm background (`rgba(60, 56, 52, 0.92)`), warm border, left accent border (`3px solid`) coloured by variant, and serif typography (`--font-family-base`). This is achieved by overriding Shoelace `::part()` selectors in Sass.
3. **FR3** — Two semantic variants must be supported via the existing `bool ok` model field: `ok=true` maps to `variant="success"` with the success accent (`#8fbc8f`); `ok=false` maps to `variant="danger"` with the error accent (`#b96f68`).
4. **FR4** — Notice styles must be extracted into a new Sass component `WebApp/Styles/Components/notice.sass` and imported in `WebApp/Styles/main.sass`.
5. **FR5** — The `#alert` container in `WebApp/Views/Shared/_Layout.cshtml` keeps its existing `top-4 right-4` position unchanged. Only the visual appearance of the notice changes; layout position is out of scope.

## Non-Functional Requirements

- **Visual consistency**: the notice palette uses the same CSS custom property names already defined in `_variables.sass` and `_colors.sass` where possible; new palette tokens are scoped to target `sl-alert` elements to avoid leaking into other elements.
- **No new runtime dependencies**: the feature uses Shoelace (already loaded via CDN in `_Layout.cshtml`) and the project's existing Sass compiler — no new npm packages, CDN scripts, or NuGet packages are introduced.
- **Sass source is authoritative**: styles are written to `WebApp/Styles/Components/notice.sass`; the generated `wwwroot/css/` output must not be edited directly and is excluded from Git.
- **Controller interface unchanged**: no changes to `UserProfileController`, `ChatController`, or any other controller that calls `PartialView("~/Views/Shared/Components/_Alert.cshtml", (bool, string))`. The `(bool ok, string message)` tuple model is preserved exactly.

## Out of Scope

- Auto-dismiss countdown (`duration`, `countdown` attributes) — no timer or countdown bar is added; the notice stays until the user dismisses it manually.
- Adding `info`, `neutral`, and `warning` variant support — the current controller interface only produces two states (`ok = true` / `ok = false`). Extending to additional variants would require changes to the tuple model and all call sites and is deferred.
- Animating the notice entrance/exit beyond Shoelace's default built-in show/hide animation.
- Stacking multiple simultaneous notices — HTMX replaces the `#alert` container content on each response; queueing is not required.
- Changing where the Shoelace CDN scripts are loaded or upgrading the Shoelace version.

## Open Questions

- ✅ Countdown: removed — notice stays visible until the user closes it manually.
- ✅ Position: keep `top-4 right-4` unchanged — only colors and fonts change.
