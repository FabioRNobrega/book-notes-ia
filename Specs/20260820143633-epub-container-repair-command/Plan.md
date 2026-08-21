# Plan: EPUB Container Repair Command

## Summary

Add a narrowly scoped shell utility to the isolated parser image and expose it through `make repair-ebook BOOK=...`. The utility validates the request and archive paths, extracts into private temporary storage, writes a conforming marker, rebuilds the ZIP with the marker first and uncompressed, verifies the result, and publishes it without replacing an existing file.

## Technical Approach

- Install `zip` and `unzip` in the existing EPUB parser runtime image and copy in an executable `repair-ebook.sh` utility.
- Reuse Make's safe base-filename checks before building and running a one-off Compose container with the private `/data` bind mount.
- Repeat all filename validation inside the container so the script is safe when invoked directly.
- Preflight ZIP entry names before extraction, use `mktemp` beneath `/data/input`, and register cleanup traps.
- Rebuild with `zip -0` for `mimetype`, then append all other entries with normal compression and directory entries omitted.
- Verify archive integrity, first-entry ordering, marker content, and storage method before moving the result into place.

## Files

- `Makefile` — register and implement `repair-ebook`.
- `services/EbookParseService.Api/Dockerfile` — provide ZIP tools and install the repair utility.
- `services/EbookParseService.Api/repair-ebook.sh` — validation and deterministic repair workflow.
- `README.md` and `services/EbookParseService.Api/README.md` — usage, output name, and scope.

## Risks

- Whole-archive extraction can be unsafe, so entry paths are rejected before extraction and temporary work remains inside the ignored input tree.
- An interrupted run must not expose a partial destination, so the completed archive is staged on the same filesystem and moved only after verification.
- The repaired container can still be structurally unsupported by the parser; documentation calls this out explicitly.

