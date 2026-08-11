# Validation: Chatterbox Voice Versioning and Progress POC

## Acceptance Criteria

| Requirement | Acceptance Criterion |
| --- | --- |
| FR1 | No changes appear under WebApp, Supertonic, database migrations, Microsoft Agent Framework, base Compose, or Roadmap. |
| FR2 | Replacing the root Portuguese reference with a different audible WAV produces a different UUID and leaves the first voice directory byte-for-byte unchanged. |
| FR3 | Every new voice contains a reference whose SHA-256 equals metadata and the source used to create conditioning. |
| FR4 | Returning to an earlier reference checksum reuses its UUID; compatibility changes rebuild only that UUID from its archive. |
| FR5 | Two Portuguese voices list successfully; a duplicate language/checksum metadata pair fails deterministically. |
| FR6 | Selecting each Portuguese UUID generates from that voice; unknown, malformed, or cross-language IDs are rejected. |
| FR7 | `/voices` and the Make list command return both Portuguese IDs without audio/tensor contents. |
| FR8 | Each voice retains separate conditioning, archived reference, metadata, and output after creating another voice. |
| FR9 | `/progress` reports the required fields and final completed/failed state. |
| FR10 | Percentages never decrease and synthesis visibly advances by chunk. |
| FR11 | Captured Make-client output contains progress before final JSON. |
| FR12 | `LANGUAGE`, optional `VOICE_ID`, and voice listing route correctly. |
| FR13 | Short/silent references return 422; near-silent fake output returns 500 and preserves the prior preview. |
| FR14 | A previous-format matching voice receives an archive without changing its UUID/conditioning; a nonmatching missing archive fails safely. |
| FR15 | Logs and docs distinguish created, loaded, and regenerated voices without sensitive contents. |
| FR16 | Focused tests pass without loading/downloading the model. |

## Test Cases

**Unit/API tests:**

- Extend `test_voice_store.py` for two same-language voices, checksum selection, exact UUID selection, archived reference checksums, backfill, duplicate detection, and atomic rollback.
- Extend `test_api.py` for new-reference UUID creation, old-voice selection, voice listing/filtering, progress success/failure, short/silent reference rejection, and near-silent output preservation.
- Add `test_progress.py` for monotonic snapshots, chunk percentages, client polling/rendering, and failure output.
- Retain exact preview, chunking, persistence, path containment, and English/Portuguese isolation tests.

**Integration/manual:**

1. Run `make chatterbox-test` offline and confirm all fake tests pass.
2. Run `make test` and confirm WebApp/Supertonic regressions pass.
3. Run a preview and confirm progress prints before completion.
4. Preserve the current Portuguese voice, replace the root reference with a different authorized recording, rerun, and confirm two Portuguese IDs exist.
5. Run each ID explicitly and listen to both outputs.
6. Confirm private artifacts remain ignored and no temporary files remain.

## Definition of Done

- FR1–FR16 are implemented and tested.
- Existing successful voices are never overwritten merely because the root reference changes.
- Multiple voices per language are discoverable and selectable.
- Each new voice retains its reference, conditioning, metadata, and independent output.
- Progress is visible during real generation at honest stage/chunk granularity.
- Short/silent references and near-silent outputs fail safely.
- Focused and full Docker test suites pass.
- Documentation explains that the already overwritten historical voice is unrecoverable without its old reference/backup.
- Roadmap remains unchanged.

## Rollback Plan

- Revert POC source/Make/docs while leaving all ignored voice directories untouched.
- Previous-format voice folders remain readable by the prior implementation; archived `reference.wav` files become inert extra private files.
- Never delete voice directories during rollback.
