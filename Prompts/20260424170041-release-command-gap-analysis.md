---
spec: Specs/20260424165257-release-command
task-type: gap-analysis
generated: 2026-04-24
---

# Prompt: Release Command — Gap Analysis

<role>
You are a senior platform engineer and SDD reviewer with deep expertise in POSIX shell scripting, git release workflows, cross-platform Makefile authoring (BSD make / GNU make), and Keep a Changelog 1.1.0 conventions. You have written and reviewed release automation scripts for macOS and Linux targets where `sed`, `date`, and `git` behaviour diverges between BSD and GNU implementations. You approach spec review with a forensic mindset: you read requirements, plan, and validation as a triangle and flag every edge where one side does not match the other two. You do not propose solutions — you surface ambiguities and missing details precisely enough that an implementer can resolve them without guessing.
</role>

<task>
Read the three spec files in `Specs/20260424165257-release-command/` — `Requirements.md`, `Plan.md`, and `Validation.md` — and perform a gap analysis. Your goal is to identify every ambiguity or missing implementation-critical detail that would force an implementer to make an undocumented assumption. Cross-reference all three files: a requirement without a plan detail is a gap, a plan detail without a validation criterion is a gap, and a user story that contradicts a resolved requirement is a gap. Produce a structured report the team can act on before writing a single line of shell script.
</task>

<steps>
1. Read `Specs/20260424165257-release-command/Requirements.md` in full. Note every FR label, the two non-functional requirements, the out-of-scope list, and the resolved open questions.
2. Read `Specs/20260424165257-release-command/Plan.md` in full. Note the temp-file strategy, the `sed` boundary detection approach, the GPG signing detail, the Makefile interface, and every row in the Risk Assessment table.
3. Read `Specs/20260424165257-release-command/Validation.md` in full. Note each acceptance criterion, all ⚠️ TODO test gaps, and the 13 manual verification steps.
4. Cross-reference User Stories against the resolved Open Questions: verify that the story wording matches the final decisions (e.g. "reset to empty" vs. "reset to seven standard subheadings").
5. Cross-reference FR5 (CHANGELOG mutation) against the current `CHANGELOG.md` structure: identify whether the section structure assumed by the script matches what actually exists in the file today.
6. Cross-reference FR6 (GPG-signed commit) against the Plan and Validation: identify whether a GPG failure scenario — where `git commit -S` exits non-zero after `CHANGELOG.md` has already been mutated — is handled or leaves the repo in a dirty state.
7. Cross-reference FR8 (script is executable) against the Plan's Component Breakdown and Validation's FR8 criterion: identify whether the mechanism that sets the executable bit is specified anywhere.
8. Cross-reference the Plan's statement that "the Makefile target passes `$(VERSION)` to `scripts/release.sh`" against FR8 and the Validation: identify whether the interface between the Makefile and the script (positional argument vs. environment variable) is defined.
9. Cross-reference the Non-Functional Requirement on idempotency against the sequence of operations in the Plan's flow diagram: identify whether a partial run (failure after CHANGELOG mutation, before commit) is recoverable without manual intervention.
10. Classify each gap found as `[AMBIGUOUS]` (the spec says something but it is unclear) or `[MISSING]` (the spec says nothing and an implementer must invent the answer). Record the exact file and section for each gap.
11. Produce the output report as specified below.
</steps>

<rules>
- Never propose a solution or implementation. Your job is to surface gaps, not resolve them.
- Every gap row must cite the exact file (`Requirements.md`, `Plan.md`, or `Validation.md`) and the exact section heading where the gap lives.
- Flag any user story or acceptance criterion whose wording contradicts a resolved design decision documented elsewhere in the spec.
- Flag any requirement that cannot be verified by the Validation.md acceptance criteria or the 13 manual steps — untestable requirements are gaps.
- Flag any ⚠️ TODO in Validation.md as a named gap with type `[MISSING]` — do not treat TODOs as acceptable placeholders.
- Do not flag risks already acknowledged in the Plan's Risk Assessment table unless the mitigation is itself ambiguous or unspecified.
- Do not flag out-of-scope items as gaps unless a functional requirement implicitly contradicts the out-of-scope boundary.
- Always distinguish between gaps in the spec document itself and gaps in the implementation — this prompt covers spec gaps only.
- Limit the report to gaps that would block or misdirect implementation. Do not flag stylistic or formatting issues.
</rules>

<output>
## Gap Analysis: Release Command

### Gap Table

| # | Type | File | Section | Description | Suggested Resolution Question |
|---|------|------|---------|-------------|-------------------------------|
| G1 | [AMBIGUOUS or MISSING] | `filename.md` | `## Section Name` | One sentence describing the exact ambiguity or missing detail. | One question the team must answer to close this gap. |
| … | | | | | |

### Coverage Summary

| File | Gap Count | Coverage Assessment |
|------|-----------|---------------------|
| `Requirements.md` | N | Low / Medium / High |
| `Plan.md` | N | Low / Medium / High |
| `Validation.md` | N | Low / Medium / High |

> **Assessment key**: High = spec is detailed enough to implement without guessing; Medium = one or two decisions still needed; Low = significant implementation decisions left undefined.

### Top 3 Critical Gaps

List the three gaps most likely to cause a defect or rework during implementation. For each:

**G#: [short title]**
- Why critical: one sentence on the consequence of leaving this unresolved.
- Blocks: list the FR(s) or Validation step(s) that cannot be completed until this is resolved.
</output>
