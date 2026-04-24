---
name: claude-commands
description: Load and execute agent-agnostic slash commands from .agents/commands (canonical source) or .claude/commands (symlink fallback). Use when the user invokes a slash command such as /implement-spec, /new-spec, /pr-description, or /prompt-generator.
---

# Claude Commands

## Overview

Execute repository-local slash commands. Commands are stored as Markdown files under `.agents/commands` — the canonical, agent-agnostic location. `.claude/commands` is a symlink to the same directory, so both paths resolve identically.

When the user message begins with a slash command, load the matching command file and carry out its workflow in the current repository.

## Workflow

1. Parse the first token of the user message as the command name.
   - `/implement-spec` maps to `.agents/commands/implement-spec.md`.
   - Preserve the rest of the user message as the command arguments.
2. Locate the command file from the current working directory.
   - Prefer `<cwd>/.agents/commands/<command>.md`.
   - Fall back to `<cwd>/.claude/commands/<command>.md` (resolves to the same file via symlink).
   - Search upward through parent directories if not found in the current directory.
   - If still not found, report the missing path and list available `.agents/commands/*.md` files when possible.
3. Read the command Markdown file before acting.
4. Treat the command file as the primary task instructions for this turn, subject to higher-priority system, developer, skill, safety, and user instructions.
5. If the command file uses argument placeholders, substitute from the user's trailing text where obvious.
   - Common placeholders include `$ARGUMENTS`, `{{arguments}}`, `<arguments>`, and `{args}`.
   - If required arguments are missing and cannot be inferred, ask one concise clarification.
6. Execute the resulting workflow normally: inspect files, edit files, run commands, and verify results as needed.

## Command Semantics

- Slash command names are file names without the `.md` suffix.
- Support nested command files if present: `/foo/bar` maps to `.agents/commands/foo/bar.md`.
- Do not execute shell snippets from the command file blindly. Read them as workflow guidance and apply normal judgment, approvals, and sandbox rules.
- If the command file conflicts with the user's explicit message after the slash command, prefer the user's explicit message unless the command file clearly defines the expected syntax.
- If multiple repositories or worktrees are visible, use the current working directory as the starting point.

## Adding new commands

Add a single `.md` file to `.agents/commands/`. Both Claude Code and Codex pick it up automatically — no extra wiring needed.

## Examples

User:

```text
/implement-spec specs/auth.md
```

Action:

```text
Read .agents/commands/implement-spec.md, substitute "specs/auth.md" for command arguments, then follow that workflow.
```

User:

```text
/pr-description
```

Action:

```text
Read .agents/commands/pr-description.md and follow its instructions. Ask only if the command requires missing information.
```
