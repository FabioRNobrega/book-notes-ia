---
description: Generate a PR description from staged/committed changes using the project style guide.
---

# pr-description

Generate a pull request description based on the current branch's
changes versus the main branch.

## Usage

```
/pr-description
```

Optional: `/pr-description <extra context or focus hint>`

---

## What you must do

### Step 1 — Gather the diff

Run these commands to understand what changed:

1. `git diff main...HEAD` — full diff of all commits on this branch.
2. `git log --oneline main..HEAD` — list of commits included.
3. `git diff --stat main...HEAD` — files changed at a glance.

If `$ARGUMENTS` is provided, treat it as extra context or a focus
hint (e.g. a specific service, ticket number, or emphasis area) and
factor it into the description.

### Step 2 — Understand the before state

Read the changed files enough to describe what the code did *before*
this PR. Focus on the user-visible or system-visible behaviour that
was lacking, broken, or absent. Do not describe implementation
details — describe observable effects. Also use the current chat as
source for the context.

### Step 3 — Write the description

Follow this format exactly:

```
Previously, <one or two sentences describing the old state or
problem — what the user or system experienced before this change>.

Changes:
- <concise bullet: what changed and why it matters>
- <concise bullet>
- ...
```

**Formatting rules (mandatory):**
- Maximum 72 characters per line — hard wrap, no exceptions.
- Each bullet starts with a capital letter, no trailing period.
- Use plain present tense for bullets ("Ensure", "Add", "Fix",
  "Sort", "Remove") — not past tense.
- The "Previously" paragraph must be prose, not bullets.
- Do not include implementation details (function names, file paths,
  variable names) unless they are user-facing.
- Do not include a title — only the body text.
- Aim for 3–7 bullets; group closely related changes into one bullet.

### Step 4 — Output

Print the description inside a fenced code block so the user can
copy it directly. Then, below the code block, add a brief note if
there are any open questions or areas where you lacked context.

Example output format:

~~~
```
Previously, the groups list was displayed in a random order every
time it was opened, making it harder for clients to locate a
specific group. In addition, members inside each group were not
consistently ordered.

Changes:
- Ensure groups are always sorted alphabetically, with numeric
  values respected in the ordering
- Insert newly created groups directly in the correct order
- Automatically focus on the newly created group
- Expand the details table for the new group to allow immediate
  member management
- Sort group members alphabetically for consistency
```
~~~

### Step 5 — Open the PR

After the user confirms the description, open the PR on GitHub
using `gh pr create`. Derive a short title (≤ 70 chars) from the
branch name and the "Changes" bullets. Pass the body via heredoc
and always append the attribution line at the end.

```bash
/home/linuxbrew/.linuxbrew/bin/gh pr create \
  --base main \
  --title "<title>" \
  --body "$(cat <<'EOF'
<description body>

EOF
)"
```

If `gh` is not authenticated, prompt the user to run:

```bash
/home/linuxbrew/.linuxbrew/bin/gh auth login
```

Then retry `gh pr create` once authentication is confirmed.
