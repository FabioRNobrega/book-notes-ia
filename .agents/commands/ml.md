---
description: Answer a question using the Microsoft Learn MCP server as the authoritative source, citing links.
---

# ml

Answer `$ARGUMENTS` using the Microsoft Learn MCP server
(`microsoft-learn`) as the source of truth, instead of relying on
memory or general web knowledge.

## Usage

```
/ml <question>
```

Example: `/ml how do I configure minimal API authentication in .NET 10?`

---

## What you must do

### Step 1 — Search

Call `mcp__microsoft-learn__microsoft_docs_search` with the user's
question (or a focused rephrasing of it) to retrieve relevant
documentation chunks.

### Step 2 — Go deeper if needed

If the search results are incomplete, ambiguous, or the user needs a
full tutorial/troubleshooting steps/prerequisites, call
`mcp__microsoft-learn__microsoft_docs_fetch` on the most relevant
URL(s) from Step 1 to pull the full page content.

If the question is about code usage or the user wants a runnable
snippet, also call `mcp__microsoft-learn__microsoft_code_sample_search`.

### Step 3 — Answer

Write the answer grounded strictly in what the retrieved docs say.
Do not fall back on unstated prior knowledge to fill gaps — if the
docs don't cover something, say so explicitly rather than guessing.

### Step 4 — Cite sources

End the answer with a "Sources" section listing the Microsoft Learn
URL(s) actually used, e.g.:

```
Sources:
- [source-doc-title-here](https://learn.microsoft.com/...)
```

If no relevant Microsoft Learn content was found, say so plainly
instead of answering from general knowledge.
