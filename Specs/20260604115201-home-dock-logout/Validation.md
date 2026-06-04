# Validation: Home Dock Logout

## Table of Contents

- [Validation: Home Dock Logout](#validation-home-dock-logout)
  - [Acceptance Criteria](#acceptance-criteria)
  - [Test Cases](#test-cases)
  - [Manual Verification](#manual-verification)
  - [Definition of Done](#definition-of-done)
  - [Rollback Plan](#rollback-plan)

## Acceptance Criteria

| Requirement | Acceptance Criterion |
| --- | --- |
| FR1 | Rendered pages no longer include the `header id="header"` markup from `_TopNavbar.cshtml`, and `_Layout.cshtml` no longer references `Components/_TopNavbar`. |
| FR2 | When an authenticated user renders `Home/Index`, the bottom dock shows the logout control immediately after `My Profile`. |
| FR3 | The logout form posts to `Identity` `/Account/Logout` with `method="post"` and returns to `Home/Index`. |
| FR4 | The logout control uses `sl-button variant="default" outline` at default (medium) size with the `box-arrow-right` prefix icon and a Shoelace tooltip labelled `Logout` — size, color, and hover behavior match the other dock buttons. |
| FR5 | The existing `Talk with Our AI`, `Upload Notes`, `See Your Notes`, and `My Profile` dock buttons keep their current `hx-get`, `hx-target`, `hx-swap`, and `onclick` behavior. |

## Test Cases

**Unit tests:**

- No new controller or service unit tests are required because this is a Razor composition change with no controller, service, EF Core, or Microsoft Agent Framework behavior.
- Existing tests in `WebApp.Tests/Controllers/ChatControllerTests.cs`, `WebApp.Tests/Controllers/BookContextControllerTests.cs`, and service tests should continue to pass through `make test`.

**Integration tests:**

- No new database or Microsoft Agent Framework integration tests are required.
- ⚠️ TODO: If the project later adds Razor view rendering tests, add coverage that authenticated `Home/Index` markup contains the logout form after `My Profile` and that `_Layout.cshtml` does not render `_TopNavbar`.

## Manual Verification

1. Start the local stack with the OS-appropriate make target, for example `make docker-run` on Linux or SteamOS.
2. Sign in to the application.
3. Open the home page.
4. Confirm the old top navbar/header is not visible.
5. Confirm the bottom dock order is `Talk with Our AI`, `Upload Notes`, `See Your Notes`, `My Profile`, then the logout icon button.
6. Confirm the logout button shows `Logout` as a text label and matches the size, color, and hover style of the other dock buttons.
7. Click the logout control and confirm the user is signed out and returned to the home index route.
8. Sign in again and confirm the chat, upload, notes, and profile dock buttons still load their views.
9. Run `make test` to verify the existing regression suite.

## Definition of Done

- Requirements, Plan, and Validation docs are present in this spec folder.
- `_Layout.cshtml` no longer renders `Components/_TopNavbar`.
- `_TopNavbar.cshtml` is removed after all references are gone.
- `Index.cshtml` renders the authenticated logout form after `My Profile` with the existing TopNavbar button style.
- Existing tests pass through `make test`, or any failure is documented with the failing command and reason.
- No generated CSS, `.env`, `bin/`, or `obj/` files are committed.

## Rollback Plan

- Restore the `<partial name="Components/_TopNavbar" />` line in `WebApp/Views/Shared/_Layout.cshtml`.
- Restore `WebApp/Views/Shared/Components/_TopNavbar.cshtml` if it was deleted.
- Remove the logout form added to `WebApp/Views/Home/Index.cshtml`.
