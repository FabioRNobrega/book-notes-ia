# Requirements: Home Dock Logout

## Table of Contents

- [Requirements: Home Dock Logout](#requirements-home-dock-logout)
  - [Problem Statement](#problem-statement)
  - [User Stories](#user-stories)
  - [Functional Requirements](#functional-requirements)
  - [Non-Functional Requirements](#non-functional-requirements)
  - [Out of Scope](#out-of-scope)
  - [Open Questions](#open-questions)

## Problem Statement

The authenticated logout action currently lives in `WebApp/Views/Shared/Components/_TopNavbar.cshtml`, which is rendered globally by `WebApp/Views/Shared/_Layout.cshtml`. The home experience already concentrates the primary actions in the bottom dock in `WebApp/Views/Home/Index.cshtml`, so users should be able to sign out from that same control group, immediately after `My Profile`, while the separate top navbar partial is removed from the layout.

## User Stories

- Given I am signed in on the home page, when I look at the bottom dock, then I see a logout control immediately after `My Profile`.
- Given I click the home dock logout control, when the form posts, then ASP.NET Core Identity signs me out and returns me to the home index route.
- Given the layout renders any MVC view, when the page loads, then the old top navbar partial is no longer displayed.
- Given I am not signed in, when the home page renders, then the logout action is not shown.

## Functional Requirements

1. FR1 - `WebApp/Views/Shared/_Layout.cshtml` must stop rendering `WebApp/Views/Shared/Components/_TopNavbar.cshtml`.
2. FR2 - `WebApp/Views/Home/Index.cshtml` must render a logout form immediately after the `My Profile` dock button for authenticated users only.
3. FR3 - The new logout form must post to the existing ASP.NET Core Identity logout page using `asp-area="Identity"`, `asp-page="/Account/Logout"`, and the current home index return URL pattern.
4. FR4 - The new logout control must match the dock button visual treatment: `sl-button variant="default" outline` at the default (medium) size, the `box-arrow-right` prefix icon, and a Shoelace tooltip labelled `Logout` in place of a visible text label.
5. FR5 - Removing `_TopNavbar.cshtml` must not break the home chat, upload, notes, or profile mode buttons in `WebApp/Views/Home/Index.cshtml`.

## Non-Functional Requirements

- The change must remain UI-only: no controller, service, EF Core, PostgreSQL, Redis, Ollama, or Microsoft Agent Framework behavior should change.
- The logout form must keep ASP.NET Core Identity tag-helper behavior so antiforgery and routing continue to follow existing framework conventions.
- The dock must remain responsive and horizontally scrollable as needed on small viewports.
- Generated CSS under `WebApp/wwwroot/css` must not be committed.

## Out of Scope

- Redesigning the home dock or changing the chat composer layout.
- Changing Identity login, registration, or account management pages.
- Adding a global replacement navbar elsewhere in the application.
- Modifying chat, notes import, book context generation, embeddings, or Microsoft Agent Framework flows.

## Open Questions

- None.
