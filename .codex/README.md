# Codex

This folder stores project-owned Codex skill copies versioned alongside the repo.

Commands are defined once in `.agents/commands/` (the agent-agnostic source of truth). The `claude-commands` skill reads from there so Codex and Claude Code share the same command definitions without duplication.

Codex global skills are installed under:

```text
~/.codex/skills/
```

To make a repo skill available in Codex, copy it to your user-level skills directory:

```bash
mkdir -p ~/.codex/skills
cp -R .codex/skills/claude-commands ~/.codex/skills/
```

After installing or updating a skill, start a new Codex session so the skill list is refreshed.

## Available Skills

- `claude-commands` — lets Codex handle slash commands such as `/new-spec`, `/implement-spec`, `/pr-description`, and `/prompt-generator` by reading the matching Markdown file from `.agents/commands/`.
