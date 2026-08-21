#!/bin/sh

set -eu

input_root=/data/input
book=${1:-}

fail() {
    echo "Error: $*" >&2
    exit 1
}

if [ -z "$book" ]; then
    fail "Usage: repair-ebook.sh book.epub"
fi

case "$book" in
    *[!A-Za-z0-9._\ -]*|.*)
        fail "BOOK must be a safe base filename"
        ;;
esac

case "$book" in
    *.epub|*.EPUB) ;;
    *) fail "BOOK must end in .epub" ;;
esac

source_path="$input_root/$book"
[ -f "$source_path" ] || fail "Missing $source_path"
[ ! -L "$source_path" ] || fail "The source EPUB must not be a symbolic link"

stem=${book%.*}
output_name="${stem}-fixed.epub"
output_path="$input_root/$output_name"
[ ! -e "$output_path" ] || fail "Refusing to overwrite existing $output_path"

work_dir=$(mktemp -d "$input_root/.ebook-repair.XXXXXX")
expanded_dir="$work_dir/expanded"
staged_path="$work_dir/$output_name"
entries_path="$work_dir/entries.txt"
expected_marker="$work_dir/expected-mimetype"
actual_marker="$work_dir/actual-mimetype"

cleanup() {
    rm -rf -- "$work_dir"
}
trap cleanup EXIT HUP INT TERM

unzip -tq "$source_path" >/dev/null || fail "The source is not a valid ZIP archive"
unzip -Z1 "$source_path" > "$entries_path" || fail "Unable to read ZIP entries"

while IFS= read -r entry; do
    case "$entry" in
        /*|\\*|*\\*|..|../*|*/..|*/../*|[A-Za-z]:/*)
            fail "The archive contains an unsafe entry path"
            ;;
    esac
done < "$entries_path"

if sort "$entries_path" | uniq -d | grep -q .; then
    fail "The archive contains duplicate entry paths"
fi

if zipinfo -l "$source_path" \
    | awk '$1 ~ /^l/ { found = 1 } END { exit(found ? 0 : 1) }'; then
    fail "The archive contains symbolic links"
fi

mkdir -p "$expanded_dir"
unzip -qq -o "$source_path" -d "$expanded_dir" || fail "Unable to extract the source archive"

if find "$expanded_dir" -type l -print -quit | grep -q .; then
    fail "The archive contains symbolic links"
fi

if [ -d "$expanded_dir/mimetype" ]; then
    fail "The archive contains an invalid mimetype directory"
fi

printf %s 'application/epub+zip' > "$expanded_dir/mimetype"

(
    cd "$expanded_dir"
    zip -q -X -0 "$staged_path" mimetype
    zip -q -X -r -9 "$staged_path" . -x mimetype
) || fail "Unable to rebuild the EPUB archive"

zip -T "$staged_path" >/dev/null || fail "The repaired EPUB failed its ZIP integrity check"
[ "$(unzip -Z1 "$staged_path" | sed -n '1p')" = "mimetype" ] \
    || fail "The repaired EPUB does not begin with the mimetype marker"
[ "$(unzip -Z1 "$staged_path" | grep -Fxc mimetype)" -eq 1 ] \
    || fail "The repaired EPUB contains multiple mimetype markers"
printf %s 'application/epub+zip' > "$expected_marker"
unzip -p "$staged_path" mimetype > "$actual_marker"
cmp -s "$expected_marker" "$actual_marker" \
    || fail "The repaired EPUB has an invalid mimetype value"
unzip -lv "$staged_path" mimetype \
    | awk '$NF == "mimetype" { found = 1; if ($2 != "Stored") exit 1 } END { if (!found) exit 1 }' \
    || fail "The repaired EPUB mimetype marker is compressed"

mv -n "$staged_path" "$output_path"
[ ! -e "$staged_path" ] || fail "The repaired destination appeared during publication; nothing was overwritten"

echo "Repaired EPUB: $output_path"
