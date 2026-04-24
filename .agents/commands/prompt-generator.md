---
description: Generate a structured agent prompt (role/task/steps/rules/output) from a spec folder and save it to Prompts/
---

# prompt-generator

Generate a structured agent prompt from an existing SDD spec folder and save it as a markdown file under `Prompts/`.

## Usage

```sh
/prompt-generator <spec-folder-path>
```

Examples:

- `/prompt-generator Specs/20260422000000-improve-build-time-across-all-services`
- `/prompt-generator` (no argument — the agent will ask for the spec path, then present a selection for task type)

---

## What you must do

### Step 1 — Parse the spec path

Extract from `$ARGUMENTS`:

- **spec-path**: the folder path containing `Plan.md`, `Requirements.md`, and `Validation.md`.

If `$ARGUMENTS` is empty or no spec path is identifiable, ask the user:
> "Which spec folder should I generate a prompt for?"

Wait for the answer before proceeding.

### Step 2 — Select task type interactively

Use the `AskUserQuestion` tool with `multiSelect: true` and the following configuration:

- **question**: `"Which task type should the prompt instruct the agent to perform?"`
- **header**: `"Task type"`
- **multiSelect**: `true`
- **options** (in this order):
  1. label: `gap-analysis` — description: `Find ambiguities and missing details in the spec before implementation.`
  2. label: `implementation-review` — description: `Review an implementation against the spec requirements.`
  3. label: `validation-check` — description: `Verify acceptance criteria are covered by tests.`
  4. label: `security-review` — description: `Identify security gaps in the spec or implementation.`

Note: `refactor-guide` (generate step-by-step instructions for a targeted refactor) is available via the auto-added "Other" input.

Wait for the answer before proceeding. If the user selects multiple types, generate a separate prompt file for each type and report all created files at the end.

### Step 3 — Read the spec

Read all three files from the spec folder:

1. `Requirements.md` — functional requirements, user stories, non-functional requirements, out-of-scope.
2. `Plan.md` — implementation strategy, file list, dependencies, flow diagram, risk assessment.
3. `Validation.md` — acceptance criteria, test cases, definition of done, rollback plan.

Do NOT skip any file. Cross-reference them before writing the prompt.

### Step 4 — Infer domain expertise

Based on what you read, determine the technical domain of the spec (e.g. CI/CD & build systems, database schema migration, frontend UI, API design, security hardening). This determines the `<role>` the generated prompt will assign to the agent.

### Step 5 — Write the prompt

Generate the prompt content using the following XML-tag structure. Every section must be specific to the spec you read — never write generic boilerplate.

```xml
<role>
[2–4 sentences. Assign the agent a specific expert persona that matches the domain.
Include the relevant stack/tooling expertise. Describe the agent's working style:
rigorous, detail-oriented, opinionated, etc.]
</role>

<task>
[2–5 sentences. State exactly what the agent must do with this specific spec.
Name the spec folder path explicitly. List the files the agent must read.
State the goal clearly — what question should be answered or what artifact produced?]
</task>

<steps>
[Numbered list of concrete, ordered steps. Each step is one specific action.
Reference file names, sections, or concepts from the spec you read.
Include a cross-referencing step. End with a synthesis or output step.]
</steps>

<rules>
[Bulleted constraints. What must the agent NOT do? What must it always do?
Scope constraints (e.g. "focus only on infrastructure concerns").
Format constraints (e.g. "always reference the file and section").
At least 5 rules, all specific to this task type and domain.]
</rules>

<output>
[Define the exact output format. Include markdown headings, table schemas, 
emoji markers, or section names. The agent must know precisely what to return.
Match the level of detail to the task type.]
</output>
```

**Prompt type templates to adapt:**

#### gap-analysis

- Role: senior engineer + SDD reviewer with deep expertise in the domain.
- Task: identify ambiguous requirements and missing implementation-critical details.
- Steps: read all files → cross-reference → classify each gap as [AMBIGUOUS] or [MISSING] → locate the exact file/section → explain why it's a gap → suggest a resolution question.
- Rules: no solutions, no assumptions, always cite file+section, flag untestable requirements.
- Output: gap table (type / file / section / description / suggested resolution) + coverage summary per file (Low/Medium/High) + top 3 critical gaps.

#### implementation-review

- Role: senior engineer specialised in the domain, experienced in spec compliance.
- Task: compare the current implementation against each FR in the spec. Flag deviations.
- Steps: list FRs → for each FR find the corresponding code → verify behaviour matches → flag gaps or deviations.
- Rules: reference real file paths and line numbers, do not suggest rewrites unless the FR is violated.
- Output: compliance table (FR / status / evidence / deviation) + summary.

#### validation-check

- Role: QA engineer + test architect.
- Task: verify every FR in Requirements.md has a test case in Validation.md and a corresponding test file.
- Steps: enumerate FRs → check Validation.md for coverage → check test files exist → flag missing coverage.
- Rules: only flag genuine missing coverage, not style issues.
- Output: coverage matrix (FR / validation criterion / test file / status).

#### security-review

- Role: security engineer with expertise in the domain's attack surface.
- Task: identify security gaps in the spec (missing auth, injection risks, secrets handling, etc.).
- Steps: read spec → identify trust boundaries → enumerate assets → flag missing controls.
- Rules: OWASP-focused, cite the specific section, do not flag theoretical risks without spec evidence.
- Output: finding table (severity / file / section / description / recommendation).

### Step 6 — Determine the output filename

Format: `YYYYMMDDHHMMSS-<spec-slug>-<task-type>.md`

Where:

- `YYYYMMDDHHMMSS` is the current timestamp.
- `<spec-slug>` is extracted from the spec folder name (strip the `YYYYMMDDHHMMSS-` prefix if present).
- `<task-type>` is the task type selected in Step 2.

Example: `20260423153045-improve-build-time-across-all-services-gap-analysis.md`

If the user selected multiple task types in Step 2, repeat Steps 5–6 for each type and produce one file per type.

### Step 7 — Write the file

Save the prompt to `Prompts/<filename>.md` with this header:

```markdown
---
spec: <relative path to spec folder>
task-type: <task-type>
generated: <today's date>
---

# Prompt: <Spec Title> — <Task Type>

<role>
...
</role>

<task>
...
</task>

<steps>
...
</steps>

<rules>
...
</rules>

<output>
...
</output>
```

### Step 8 — Report back

Tell the user:

- Each file that was created (with a clickable path).
- The task type(s) used.
- One sentence per file summarising what the generated prompt instructs the agent to do.
- Optionally: a note on any assumptions made when inferring the role from the spec content.
