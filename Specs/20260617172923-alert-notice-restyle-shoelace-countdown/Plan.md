# Plan: Alert Notice Restyle with Shoelace

## Table of Contents

- [Summary](#summary)
- [Technical Approach](#technical-approach)
- [Component Breakdown](#component-breakdown)
- [Dependencies](#dependencies)
- [Flow](#flow)
- [Risk Assessment](#risk-assessment)

## Summary

Replace the default Shoelace `<sl-alert>` visual appearance in `_Alert.cshtml` with the warm sepia notice design from `notifcation-example.html`, and switch from `toast()` to `show()` so the `#alert` container in `_Layout.cshtml` governs position. All colour overrides go into a new `notice.sass` Sass component via CSS `::part()` selectors. No countdown, no controller changes, no schema changes.

## Technical Approach

### Shoelace `show()` vs `toast()`

The current partial calls `me.toast()` via Hyperscript, which moves the `<sl-alert>` into a Shoelace-managed portal appended to the document body. This bypasses the `#alert` div entirely and makes custom positioning impossible from CSS. Switching to `me.show()` leaves the alert inside the `#alert` div, which is already `position: fixed` in `_Layout.cshtml`. No position change is made to the `#alert` container.

### Visual restyle via CSS `::part()` and custom properties

Shoelace exposes `::part()` selectors for its shadow DOM that allow full visual overrides without touching the component internals:

- `sl-alert::part(base)` — outer container (background, border, border-radius, font)
- `sl-alert::part(icon)` — the slot that wraps the icon (padding, alignment)
- `sl-alert::part(message)` — the text content area (color, font-size)
- `sl-alert::part(close-button__base)` — the close button (color)

The warm palette from `notifcation-example.html` is declared as CSS custom properties on `sl-alert`:

```css
--notice-bg: rgba(60, 56, 52, 0.92)
--notice-border: rgba(190, 170, 150, 0.24)
--notice-text: #e8e1da
--notice-muted: #c9c0b8
--notice-success: #8fbc8f
--notice-error: #b96f68
```

Variant-specific left accent border is set by targeting `sl-alert[variant="success"]` and `sl-alert[variant="danger"]` on the `::part(base)` selector. The `variant` attribute is kept on `<sl-alert>` so Shoelace's native accessible live-region behaviour (`role="status"` vs `role="alert"`) continues to work correctly.

### Sass design

The new `notice.sass` follows the existing component convention (see `button.sass`, `input.sass`). It:

1. Declares CSS custom property defaults on `sl-alert`
2. Applies shared shadow-DOM overrides via `sl-alert::part(base)`, `::part(message)`, `::part(close-button__base)`
3. Sets variant-specific `border-left-color` on `sl-alert[variant="success"]::part(base)` and `sl-alert[variant="danger"]::part(base)`
4. Does **not** use `!important` — attribute + `::part()` specificity on the host element is sufficient

### `_Alert.cshtml` markup

```html
<sl-alert
  variant="@(Model.ok ? "success" : "danger")"
  closable
  _="on load call me.show()"
>
  <sl-icon slot="icon" name="@(Model.ok ? "check2-circle" : "exclamation-octagon")"></sl-icon>
  <strong>@(Model.ok ? "Saved" : "Not saved")</strong><br />
  @Model.message
</sl-alert>
```

The `variant` attribute stays so Shoelace handles accessible semantics. No `duration` or `countdown` attributes are added.

## Component Breakdown

**Existing files to modify:**

- [WebApp/Views/Shared/Components/_Alert.cshtml](../../WebApp/Views/Shared/Components/_Alert.cshtml) — add `variant="@(Model.ok ? "success" : "danger")"`, change Hyperscript from `me.toast()` to `me.show()`, add `closable`, add `<sl-icon slot="icon">`.
- [WebApp/Styles/main.sass](../../WebApp/Styles/main.sass) — add `@use "./Components/notice"` after the existing component imports.

**New files to create:**

- `WebApp/Styles/Components/notice.sass` — palette custom properties, `::part()` overrides for `base`, `message`, `close-button__base`, and variant-specific `border-left-color` rules.

**No changes to:**

- `WebApp/Views/Shared/_Layout.cshtml` — `#alert` position is out of scope.
- Any controller, service, model, or migration file.

## Dependencies

- Shoelace `2.19.1` / `2.20.0` (already loaded in `_Layout.cshtml` via CDN) — `<sl-alert>` `::part()` selectors and the `variant` attribute are available in these versions.
- Hyperscript `0.9.12` (already loaded in `_Layout.cshtml`) — `on load call me.show()` syntax is unchanged.
- `AspNetCore.SassCompiler 1.93.2` (already configured) — compiles `notice.sass` during build with no additional setup.

## Flow

```mermaid
sequenceDiagram
    participant Browser
    participant HTMX
    participant Controller
    participant PartialView as _Alert.cshtml

    Browser->>HTMX: Form submit (e.g. save profile)
    HTMX->>Controller: POST /UserProfile/Upsert
    Controller-->>PartialView: PartialView("_Alert.cshtml", (true, "Profile saved."))
    PartialView-->>HTMX: <sl-alert variant="success" closable ...>
    HTMX->>Browser: Swap into #alert div (top-4 right-4)
    Browser->>Browser: Hyperscript fires "on load call me.show()"
    Browser->>Browser: Shoelace renders warm notice; notice stays visible
    Browser->>Browser: User clicks × to close
```

## Risk Assessment

| Risk | Evidence | Mitigation |
| --- | --- | --- |
| Shoelace `::part()` selectors may not override all internal styles without `!important` | Shadow DOM specificity is self-contained; host selectors with `::part()` have high but finite specificity | Test each `::part()` override visually; escalate to overriding `--sl-color-*` Shoelace tokens scoped to `sl-alert` if a part resists override |
| Switching from `toast()` to `show()` changes how HTMX re-injection behaves | With `toast()`, Shoelace clones and moves the element; with `show()` the element stays in the DOM — HTMX replaces `#alert` content on each response, which should reinitialize the element cleanly | Verify that a second form submit correctly replaces a previously visible notice without leaving ghost elements in the DOM |
| Left accent border may conflict with Shoelace's own variant border token | Shoelace applies its variant colour via internal tokens; `::part(base)` override should win as it targets the shadow host boundary | Check rendered styles in DevTools; if needed, set `--sl-color-success-600` and `--sl-color-danger-600` scoped to `sl-alert` to neutralise internal tokens |
