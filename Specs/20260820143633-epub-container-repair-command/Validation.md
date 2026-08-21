# Validation: EPUB Container Repair Command

## Acceptance Criteria

- A valid but reordered local EPUB produces `<stem>-fixed.epub` and leaves the source unchanged.
- The repaired archive's first entry is `mimetype`, its exact content is `application/epub+zip`, and it is stored without compression.
- Missing `BOOK`, unsafe names, wrong extensions, missing sources, symbolic links, invalid ZIPs, unsafe entry paths, and existing destinations fail without publishing or overwriting output.
- Temporary repair directories do not remain after success or expected failure.
- The command starts only a one-off isolated EPUB parser container.
- Documentation states that EPUB navigation/content structure is not converted.

## Verification

1. Run `bash -n services/EbookParseService.Api/repair-ebook.sh`.
2. Dry-run Make validation for missing, unsafe, and valid filenames.
3. Run the target against a private reordered EPUB and verify the original checksum is unchanged.
4. Inspect the repaired archive with `zipinfo` and `unzip` inside the container.
5. Invoke the target again and confirm it refuses to overwrite the repaired file.
6. Run the focused EPUB parser tests.

## Rollback

Remove the Make target and repair script, revert the image dependencies and documentation, and leave all private source/repaired EPUB files untouched.
