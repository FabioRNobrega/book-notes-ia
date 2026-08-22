from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
import re


CHAPTER_PATTERN = re.compile(r"^chapter-(\d{3})\.txt$")
MAX_TRACKS = 999


class AudiobookProjectError(ValueError):
    pass


@dataclass(frozen=True)
class AudiobookTrack:
    number: int
    source_name: str
    source_path: Path
    output_name: str
    output_path: Path


@dataclass(frozen=True)
class AudiobookProject:
    book_id: str
    book_name: str
    source_dir: Path
    destination_dir: Path
    tracks: tuple[AudiobookTrack, ...]


def build_project(
    input_root: Path,
    output_root: Path,
    book_id: str,
    book_name: str,
    intro_name: str,
    outro_name: str,
) -> AudiobookProject:
    resolved_input = input_root.resolve()
    if not resolved_input.is_dir():
        raise AudiobookProjectError("Audiobook input root is missing")

    _validate_component(book_id, "BOOK")
    source_dir = (resolved_input / book_id).resolve()
    if source_dir.parent != resolved_input or not source_dir.is_dir():
        raise AudiobookProjectError("BOOK must select an existing direct child folder")

    _validate_text_name(intro_name, "BOOK_INTRO")
    _validate_text_name(outro_name, "BOOK_OUTRO")
    if intro_name == outro_name:
        raise AudiobookProjectError("BOOK_INTRO and BOOK_OUTRO must be distinct")

    intro_path = _contained_text(source_dir, intro_name, "BOOK_INTRO")
    outro_path = _contained_text(source_dir, outro_name, "BOOK_OUTRO")
    if CHAPTER_PATTERN.fullmatch(intro_name) or CHAPTER_PATTERN.fullmatch(outro_name):
        raise AudiobookProjectError("Intro and outro cannot also be chapter files")

    chapters: list[tuple[int, Path]] = []
    for candidate in source_dir.iterdir():
        match = CHAPTER_PATTERN.fullmatch(candidate.name)
        if match is None:
            continue
        resolved = candidate.resolve()
        if resolved.parent != source_dir or not resolved.is_file():
            raise AudiobookProjectError(
                f"Chapter path is not a contained file: {candidate.name}"
            )
        chapters.append((int(match.group(1)), resolved))

    chapters.sort(key=lambda item: item[0])
    expected = list(range(1, len(chapters) + 1))
    actual = [number for number, _ in chapters]
    if not chapters:
        raise AudiobookProjectError("The book folder contains no chapter-NNN.txt files")
    if actual != expected:
        raise AudiobookProjectError(
            "Chapter numbers must start at 001 and remain contiguous"
        )

    _validate_book_name(book_name)
    resolved_output = output_root.resolve()
    resolved_output.mkdir(parents=True, exist_ok=True)
    destination = (resolved_output / book_name).resolve()
    if destination.parent != resolved_output:
        raise AudiobookProjectError("BOOK_NAME escaped the audiobook output root")
    if destination.exists() and not destination.is_dir():
        raise AudiobookProjectError("The audiobook destination is not a directory")
    destination.mkdir(parents=False, exist_ok=True)

    sources = [intro_path, *(path for _, path in chapters), outro_path]
    if len(sources) > MAX_TRACKS:
        raise AudiobookProjectError("An audiobook may contain at most 999 tracks")
    for path in sources:
        _validate_text(path)

    tracks = tuple(
        AudiobookTrack(
            number=index,
            source_name=source.name,
            source_path=source,
            output_name=f"{book_name} {index:03d}.wav",
            output_path=destination / f"{book_name} {index:03d}.wav",
        )
        for index, source in enumerate(sources, start=1)
    )
    return AudiobookProject(
        book_id=book_id,
        book_name=book_name,
        source_dir=source_dir,
        destination_dir=destination,
        tracks=tracks,
    )


def _validate_component(value: str, label: str) -> None:
    if not value or value in (".", "..") or "/" in value or "\\" in value:
        raise AudiobookProjectError(f"{label} must be a safe direct-child name")
    if any(ord(character) < 32 or ord(character) == 127 for character in value):
        raise AudiobookProjectError(f"{label} cannot contain control characters")


def _validate_text_name(value: str, label: str) -> None:
    _validate_component(value, label)
    if not value.lower().endswith(".txt"):
        raise AudiobookProjectError(f"{label} must end in .txt")


def _validate_book_name(value: str) -> None:
    _validate_component(value, "BOOK_NAME")
    if not value.strip():
        raise AudiobookProjectError("BOOK_NAME cannot be blank")


def _contained_text(source_dir: Path, name: str, label: str) -> Path:
    path = (source_dir / name).resolve()
    if path.parent != source_dir or not path.is_file():
        raise AudiobookProjectError(
            f"{label} must select an existing file directly inside BOOK"
        )
    return path


def _validate_text(path: Path) -> None:
    try:
        content = path.read_text(encoding="utf-8")
    except UnicodeDecodeError as error:
        raise AudiobookProjectError(
            f"Text input is not valid UTF-8: {path.name}"
        ) from error
    except OSError as error:
        raise AudiobookProjectError(f"Text input could not be read: {path.name}") from error
    if not content.strip():
        raise AudiobookProjectError(f"Text input is empty: {path.name}")
