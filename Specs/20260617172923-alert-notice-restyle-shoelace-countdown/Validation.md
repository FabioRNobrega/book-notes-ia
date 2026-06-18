# Validation: Alert Notice Restyle with Shoelace

## Table of Contents

- [Acceptance Criteria](#acceptance-criteria)
- [Test Cases](#test-cases)
- [Manual Verification](#manual-verification)
- [Definition of Done](#definition-of-done)
- [Rollback Plan](#rollback-plan)

## Acceptance Criteria

| Requirement | Acceptance Criterion |
| --- | --- |
| FR1 | `_Alert.cshtml` rendered HTML contains `variant="success"` or `variant="danger"`, `closable`, and Hyperscript `on load call me.show()`; inspecting the DOM after injection shows the element stays inside `#alert`, not in a Shoelace portal at the body root. No `duration` or `countdown` attributes are present. |
| FR2 | The rendered notice has a dark warm background (`rgba(60, 56, 52, 0.92)`), warm muted border, a coloured left accent border, and uses the app's serif font family — verified visually against the reference `notifcation-example.html` in this spec folder. |
| FR3 | A successful action (e.g. saving a profile) renders `variant="success"` with a green-tinted left accent (`#8fbc8f`). A failed action renders `variant="danger"` with a red-tinted left accent (`#b96f68`). |
| FR4 | `WebApp/Styles/Components/notice.sass` exists and `WebApp/Styles/main.sass` imports it. The generated `wwwroot/css/main.css` contains `::part(base)` selectors targeting `sl-alert[variant="success"]` and `sl-alert[variant="danger"]`. |
| FR5 | The `#alert` div in `_Layout.cshtml` retains its existing `top-4 right-4` Tailwind classes unchanged. |

## Test Cases

**Unit tests:**

- No new unit tests are required — this is a pure UI/CSS restyle with no service, model, or controller logic changes.
- ⚠️ TODO: If a snapshot or UI test suite is added in future, add a test that asserts `_Alert.cshtml` renders with the correct `variant` value and no `duration`/`countdown` attributes for each `ok` value.

**Integration tests:**

- Existing controller tests that call `PartialView("~/Views/Shared/Components/_Alert.cshtml", ...)` continue to pass unchanged — the `(bool ok, string message)` tuple model is preserved (`WebApp.Tests/Controllers/ChatControllerTests.cs`, `UserProfileController` test coverage).
- Run `make test` to confirm all existing tests pass after the Razor and Sass changes.

## Manual Verification

1. Start the app: `make docker-run` (Linux/SteamOS).
2. Open `http://localhost:8080` and log in.
3. Navigate to **My Profile** (`/UserProfile/Upsert`) and save the form.
4. Observe that a notice appears at the **top-right** corner of the viewport (`top-4 right-4`).
5. Confirm the notice background is dark warm (`rgba(60, 56, 52, 0.92)`), font is serif, and the left border is green (`#8fbc8f`).
6. Confirm the notice stays visible indefinitely — no countdown bar, no auto-dismiss.
7. Click the × close button and confirm the notice dismisses immediately.
8. Submit the form again; confirm a second notice replaces the first without ghost elements in the DOM (inspect `#alert` in browser DevTools).
9. Trigger a failure case (e.g. log out mid-session and use the chat reset endpoint): confirm the notice left border is red (`#b96f68`).
10. Resize the browser to a narrow viewport (~375 px); confirm the notice does not overflow the viewport width.

## Definition of Done

- Requirements, Plan, and Validation docs are updated in this spec folder.
- All existing tests pass: `make test` exits with no failures.
- `WebApp/Views/Shared/Components/_Alert.cshtml` uses `variant`, `closable`, and `me.show()`; no `duration` or `countdown` attributes.
- `WebApp/Styles/Components/notice.sass` exists and is imported in `main.sass`.
- `WebApp/Views/Shared/_Layout.cshtml` `#alert` div position is unchanged (`top-4 right-4`).
- Visual verification steps 3–10 above pass in a running `make docker-run` stack.
- No `wwwroot/css/` generated CSS files are committed to Git.

## Rollback Plan

- Revert `WebApp/Views/Shared/Components/_Alert.cshtml` to restore the original Hyperscript `on load call me.toast()` and remove the `variant`/`closable` attributes.
- Delete `WebApp/Styles/Components/notice.sass` and remove its `@use` line from `main.sass`.
- No database migrations, EF model changes, or service registrations are involved — rollback is limited to two Razor/Sass file edits.
