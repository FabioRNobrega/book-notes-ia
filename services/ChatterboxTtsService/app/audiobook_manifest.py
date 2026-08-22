from __future__ import annotations

from dataclasses import asdict, dataclass, replace
import hashlib
import json
import os
from pathlib import Path
import tempfile
from uuid import UUID

from .audiobook_project import AudiobookProject, AudiobookTrack
from .audio_validation import OutputWriteError, validate_output
from .settings import AUDIOBOOK_LANGUAGE_ID, AUDIOBOOK_MODEL_NAME
from .voice_store import VoiceCompatibility


SCHEMA_VERSION = 2
MANIFEST_NAME = "audiobook-manifest.json"


class AudiobookManifestError(RuntimeError):
    pass


@dataclass(frozen=True)
class ManifestTrack:
    number: int
    source_name: str
    output_name: str
    source_sha256: str
    status: str = "pending"
    output_sha256: str | None = None
    output_duration_seconds: float | None = None


@dataclass(frozen=True)
class AudiobookManifest:
    schema_version: int
    status: str
    book_id: str
    book_name: str
    language_id: str
    voice_id: str
    model_name: str
    model_revision: str
    source_revision: str
    conditioning_format_version: int
    synthesis_seed: int | None
    tracks: tuple[ManifestTrack, ...]

    @classmethod
    def from_path(cls, path: Path) -> "AudiobookManifest":
        try:
            raw = json.loads(path.read_text(encoding="utf-8"))
            expected = set(cls.__dataclass_fields__)
            if not isinstance(raw, dict):
                raise ValueError("Unexpected manifest fields")
            if raw.get("schema_version") == 1:
                if set(raw) != expected - {"synthesis_seed"}:
                    raise ValueError("Unexpected legacy manifest fields")
                raw["synthesis_seed"] = None
            elif set(raw) != expected:
                raise ValueError("Unexpected manifest fields")
            raw_tracks = raw.pop("tracks")
            if not isinstance(raw_tracks, list):
                raise ValueError("Manifest tracks must be a list")
            track_fields = set(ManifestTrack.__dataclass_fields__)
            tracks: list[ManifestTrack] = []
            for item in raw_tracks:
                if not isinstance(item, dict) or set(item) != track_fields:
                    raise ValueError("Unexpected manifest track fields")
                tracks.append(ManifestTrack(**item))
            manifest = cls(tracks=tuple(tracks), **raw)
            manifest.validate()
            return manifest
        except (OSError, json.JSONDecodeError, TypeError, ValueError) as error:
            raise AudiobookManifestError("Audiobook manifest is invalid") from error

    def validate(self) -> None:
        if self.schema_version not in (1, SCHEMA_VERSION):
            raise ValueError("Unsupported manifest schema")
        if self.status not in ("in_progress", "completed"):
            raise ValueError("Invalid manifest status")
        if (
            self.language_id != AUDIOBOOK_LANGUAGE_ID
            or self.model_name != AUDIOBOOK_MODEL_NAME
        ):
            raise ValueError("Invalid manifest synthesis settings")
        if not all(
            isinstance(value, str) and value
            for value in (
                self.book_id,
                self.book_name,
                self.voice_id,
                self.model_revision,
                self.source_revision,
            )
        ):
            raise ValueError("Invalid manifest identity")
        UUID(self.voice_id)
        for revision in (self.model_revision, self.source_revision):
            if len(revision) != 40 or any(
                character not in "0123456789abcdef" for character in revision
            ):
                raise ValueError("Invalid manifest revision")
        if not isinstance(self.conditioning_format_version, int) or isinstance(
            self.conditioning_format_version, bool
        ) or self.conditioning_format_version < 1:
            raise ValueError("Invalid conditioning format version")
        if self.schema_version == 1:
            if self.synthesis_seed is not None:
                raise ValueError("Legacy manifest cannot contain a synthesis seed")
        elif (
            not isinstance(self.synthesis_seed, int)
            or isinstance(self.synthesis_seed, bool)
            or not 0 < self.synthesis_seed <= 2_147_483_647
        ):
            raise ValueError("Invalid manifest synthesis seed")
        if not self.tracks or len(self.tracks) > 999:
            raise ValueError("Invalid manifest track count")
        for expected_number, track in enumerate(self.tracks, start=1):
            if track.number != expected_number:
                raise ValueError("Manifest track order is invalid")
            if track.status not in ("pending", "completed"):
                raise ValueError("Manifest track status is invalid")
            if not _safe_file_name(track.source_name, ".txt") or not _safe_file_name(
                track.output_name, ".wav"
            ):
                raise ValueError("Manifest track path is invalid")
            _validate_digest(track.source_sha256)
            if track.status == "completed":
                if track.output_sha256 is None:
                    raise ValueError("Completed track has no output checksum")
                _validate_digest(track.output_sha256)
                if (
                    not isinstance(track.output_duration_seconds, (int, float))
                    or isinstance(track.output_duration_seconds, bool)
                    or track.output_duration_seconds <= 0
                ):
                    raise ValueError("Completed track has invalid duration")
            elif (
                track.output_sha256 is not None
                or track.output_duration_seconds is not None
            ):
                raise ValueError("Pending track contains output metadata")
        if len({track.source_name for track in self.tracks}) != len(self.tracks):
            raise ValueError("Manifest contains duplicate sources")
        if len({track.output_name for track in self.tracks}) != len(self.tracks):
            raise ValueError("Manifest contains duplicate outputs")


class AudiobookManifestRepository:
    def __init__(self, destination_dir: Path) -> None:
        self.destination_dir = destination_dir.resolve()
        self.path = self.destination_dir / MANIFEST_NAME

    def load(self) -> AudiobookManifest | None:
        if not self.path.exists():
            return None
        if self.path.resolve().parent != self.destination_dir:
            raise AudiobookManifestError("Audiobook manifest escaped its destination")
        return AudiobookManifest.from_path(self.path)

    def save(self, manifest: AudiobookManifest) -> None:
        manifest.validate()
        descriptor, name = tempfile.mkstemp(
            prefix=".audiobook-manifest-",
            suffix=".json",
            dir=self.destination_dir,
        )
        temporary = Path(name)
        try:
            with os.fdopen(descriptor, "w", encoding="utf-8") as stream:
                json.dump(asdict(manifest), stream, indent=2, sort_keys=True)
                stream.write("\n")
                stream.flush()
                os.fsync(stream.fileno())
            AudiobookManifest.from_path(temporary)
            os.replace(temporary, self.path)
        except Exception:
            try:
                os.close(descriptor)
            except OSError:
                pass
            raise
        finally:
            temporary.unlink(missing_ok=True)


def create_manifest(
    project: AudiobookProject,
    language_id: str,
    voice_id: str,
    compatibility: VoiceCompatibility,
    synthesis_seed: int = 1234,
    previous: AudiobookManifest | None = None,
    force: bool = False,
) -> AudiobookManifest:
    previous_by_number = (
        {track.number: track for track in previous.tracks}
        if previous is not None and manifests_are_compatible(
            previous,
            project,
            language_id,
            voice_id,
            compatibility,
            synthesis_seed,
        )
        else {}
    )
    tracks: list[ManifestTrack] = []
    for plan in project.tracks:
        source_sha256 = sha256_file(plan.source_path)
        old = previous_by_number.get(plan.number)
        if (
            not force
            and old is not None
            and old.source_name == plan.source_name
            and old.output_name == plan.output_name
            and old.source_sha256 == source_sha256
        ):
            tracks.append(old)
        else:
            tracks.append(
                ManifestTrack(
                    number=plan.number,
                    source_name=plan.source_name,
                    output_name=plan.output_name,
                    source_sha256=source_sha256,
                )
            )
    return AudiobookManifest(
        schema_version=SCHEMA_VERSION,
        status="in_progress",
        book_id=project.book_id,
        book_name=project.book_name,
        language_id=language_id,
        voice_id=voice_id,
        model_name=compatibility.model_name,
        model_revision=compatibility.model_revision,
        source_revision=compatibility.source_revision,
        conditioning_format_version=compatibility.format_version,
        synthesis_seed=synthesis_seed,
        tracks=tuple(tracks),
    )


def manifests_are_compatible(
    manifest: AudiobookManifest,
    project: AudiobookProject,
    language_id: str,
    voice_id: str,
    compatibility: VoiceCompatibility,
    synthesis_seed: int = 1234,
) -> bool:
    identity_matches = (
        manifest.schema_version == SCHEMA_VERSION
        and manifest.book_id == project.book_id
        and manifest.book_name == project.book_name
        and manifest.language_id == language_id
        and manifest.voice_id == voice_id
        and manifest.model_name == compatibility.model_name
        and manifest.model_revision == compatibility.model_revision
        and manifest.source_revision == compatibility.source_revision
        and manifest.conditioning_format_version == compatibility.format_version
        and manifest.synthesis_seed == synthesis_seed
    )
    mapping = tuple((item.source_name, item.output_name) for item in manifest.tracks)
    expected = tuple((item.source_name, item.output_name) for item in project.tracks)
    return identity_matches and mapping == expected


def track_can_be_skipped(
    track: ManifestTrack,
    plan: AudiobookTrack,
    minimum_pcm_peak: int,
) -> bool:
    if track.status != "completed" or track.output_sha256 is None:
        return False
    if track.source_sha256 != sha256_file(plan.source_path):
        return False
    if plan.output_path.resolve().parent != plan.output_path.parent.resolve():
        return False
    if not plan.output_path.is_file() or sha256_file(plan.output_path) != track.output_sha256:
        return False
    try:
        validate_output(plan.output_path, minimum_pcm_peak)
    except (OutputWriteError, OSError):
        return False
    return True


def complete_track(
    manifest: AudiobookManifest,
    number: int,
    output_sha256: str,
    duration_seconds: float,
) -> AudiobookManifest:
    tracks = list(manifest.tracks)
    current = tracks[number - 1]
    tracks[number - 1] = replace(
        current,
        status="completed",
        output_sha256=output_sha256,
        output_duration_seconds=round(duration_seconds, 3),
    )
    return replace(manifest, tracks=tuple(tracks))


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        while chunk := stream.read(1024 * 1024):
            digest.update(chunk)
    return digest.hexdigest()


def _safe_file_name(value: str, suffix: str) -> bool:
    return (
        isinstance(value, str)
        and value not in ("", ".", "..")
        and value.lower().endswith(suffix)
        and "/" not in value
        and "\\" not in value
        and not any(ord(character) < 32 or ord(character) == 127 for character in value)
    )


def _validate_digest(value: str) -> None:
    if not isinstance(value, str) or len(value) != 64 or any(
        character not in "0123456789abcdef" for character in value
    ):
        raise ValueError("Manifest checksum is invalid")
