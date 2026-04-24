#!/bin/sh
set -e

VERSION="$1"
CHANGELOG="CHANGELOG.md"

# FR2: VERSION must be provided
if [ -z "$VERSION" ]; then
  echo "Usage: $(basename "$0") <version>  (e.g. $(basename "$0") 1.0.0)" >&2
  exit 1
fi

# FR3: working tree must be clean
if [ -n "$(git status --porcelain)" ]; then
  echo "Error: working tree is not clean. Commit or stash changes before releasing." >&2
  exit 1
fi

# FR4: tag must not already exist
if git tag --list | grep -qx "v$VERSION"; then
  echo "Error: tag v$VERSION already exists." >&2
  exit 1
fi

# FR6: GPG signing key must be configured
SIGNING_KEY="$(git config user.signingkey 2>/dev/null || true)"
if [ -z "$SIGNING_KEY" ]; then
  echo "Error: no GPG signing key configured (git config user.signingkey is empty)." >&2
  exit 1
fi

# Guard: ## [Unreleased] section must exist
if ! grep -q '^## \[Unreleased\]' "$CHANGELOG"; then
  echo "Error: '## [Unreleased]' section not found in $CHANGELOG." >&2
  exit 1
fi

# Warn if not on main branch
BRANCH="$(git rev-parse --abbrev-ref HEAD)"
if [ "$BRANCH" != "main" ]; then
  echo "Warning: releasing from branch '$BRANCH', not 'main'." >&2
fi

TODAY="$(date +%Y-%m-%d)"

# Back up CHANGELOG.md; restore it automatically if anything fails after this point
BACKUP="$(mktemp)"
cp "$CHANGELOG" "$BACKUP"
trap 'cp "$BACKUP" "$CHANGELOG"; rm -f "$BACKUP"; echo "Restored $CHANGELOG after failure." >&2' EXIT

# FR5: mutate CHANGELOG.md
# - Reset ## [Unreleased] to seven standard subheadings
# - Insert ## [VERSION] - DATE below it with the old unreleased content
TMPFILE="$(mktemp)"
awk -v version="$VERSION" -v today="$TODAY" '
  BEGIN { in_unreleased = 0; collected = "" }
  /^## \[Unreleased\]/ {
    in_unreleased = 1
    print "## [Unreleased]"
    print ""
    print "### Added"
    print ""
    print "### Changed"
    print ""
    print "### Deprecated"
    print ""
    print "### Removed"
    print ""
    print "### Fixed"
    print ""
    print "### Security"
    next
  }
  in_unreleased && /^## / {
    in_unreleased = 0
    print ""
    print "## [" version "] - " today
    if (length(collected) > 0) { print collected }
    print $0
    next
  }
  in_unreleased {
    collected = (collected == "" ? $0 : collected "\n" $0)
    next
  }
  { print }
  END {
    if (in_unreleased) {
      print ""
      print "## [" version "] - " today
      if (length(collected) > 0) { print collected }
    }
  }
' "$CHANGELOG" > "$TMPFILE"
mv "$TMPFILE" "$CHANGELOG"

# FR7: GPG-signed commit
git add "$CHANGELOG"
git commit -S -m "chore(release): v$VERSION"

# Commit succeeded — disarm the restore trap, keep only backup cleanup
trap 'rm -f "$BACKUP"' EXIT
rm -f "$BACKUP"

# FR8: annotated tag
git tag -a "v$VERSION" -m "Release v$VERSION"

echo "Tagged v$VERSION. Run: git push && git push --tags"
