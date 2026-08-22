from __future__ import annotations

from pathlib import Path

import pytest

from app.audiobook_project import AudiobookProjectError, build_project
from conftest import write_audiobook_book


def test_project_orders_intro_chapters_and_outro_with_three_digits(
    audiobook_roots: tuple[Path, Path],
) -> None:
    input_root, output_root = audiobook_roots
    write_audiobook_book(input_root, chapter_count=100)

    project = build_project(
        input_root, output_root, "sample-book", "Sample Book", "intro.txt", "outro.txt"
    )

    assert [track.source_name for track in project.tracks] == [
        "intro.txt",
        *(f"chapter-{number:03d}.txt" for number in range(1, 101)),
        "outro.txt",
    ]
    assert project.tracks[0].output_name == "Sample Book 001.wav"
    assert project.tracks[-1].output_name == "Sample Book 102.wav"


@pytest.mark.parametrize(
    ("book_id", "book_name", "intro", "outro", "message"),
    [
        ("../sample-book", "Sample", "intro.txt", "outro.txt", "BOOK"),
        ("sample-book", "../Sample", "intro.txt", "outro.txt", "BOOK_NAME"),
        ("sample-book", "Sample", "../intro.txt", "outro.txt", "BOOK_INTRO"),
        ("sample-book", "Sample", "intro.txt", "intro.txt", "distinct"),
    ],
)
def test_project_rejects_unsafe_or_overlapping_names(
    audiobook_roots: tuple[Path, Path],
    book_id: str,
    book_name: str,
    intro: str,
    outro: str,
    message: str,
) -> None:
    input_root, output_root = audiobook_roots
    write_audiobook_book(input_root)

    with pytest.raises(AudiobookProjectError, match=message):
        build_project(input_root, output_root, book_id, book_name, intro, outro)


def test_project_rejects_chapter_gap_and_invalid_text(
    audiobook_roots: tuple[Path, Path],
) -> None:
    input_root, output_root = audiobook_roots
    book_dir = write_audiobook_book(input_root, chapter_count=2)
    (book_dir / "chapter-001.txt").unlink()

    with pytest.raises(AudiobookProjectError, match="contiguous"):
        build_project(
            input_root, output_root, "sample-book", "Sample", "intro.txt", "outro.txt"
        )

    (book_dir / "chapter-001.txt").write_bytes(b"\xff")
    with pytest.raises(AudiobookProjectError, match="UTF-8"):
        build_project(
            input_root, output_root, "sample-book", "Sample", "intro.txt", "outro.txt"
        )

    (book_dir / "chapter-001.txt").write_text("Chapter one.", encoding="utf-8")
    (book_dir / "intro.txt").write_text("   ", encoding="utf-8")
    with pytest.raises(AudiobookProjectError, match="empty"):
        build_project(
            input_root, output_root, "sample-book", "Sample", "intro.txt", "outro.txt"
        )


def test_project_rejects_symlink_escape(
    audiobook_roots: tuple[Path, Path], tmp_path: Path
) -> None:
    input_root, output_root = audiobook_roots
    book_dir = write_audiobook_book(input_root)
    outside = tmp_path / "outside.txt"
    outside.write_text("outside", encoding="utf-8")
    (book_dir / "intro.txt").unlink()
    (book_dir / "intro.txt").symlink_to(outside)

    with pytest.raises(AudiobookProjectError, match="directly inside"):
        build_project(
            input_root, output_root, "sample-book", "Sample", "intro.txt", "outro.txt"
        )
