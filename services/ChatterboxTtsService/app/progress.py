from __future__ import annotations

from dataclasses import asdict, dataclass
import threading
from uuid import uuid4


@dataclass(frozen=True)
class ProgressSnapshot:
    operation_id: str | None = None
    status: str = "idle"
    stage: str = "idle"
    message: str = "No preview is running"
    percent: int = 0
    language_id: str | None = None
    voice_id: str | None = None
    chunk_index: int = 0
    chunk_count: int = 0
    error: str | None = None

    def to_dict(self) -> dict[str, object]:
        return asdict(self)


class ProgressTracker:
    def __init__(self) -> None:
        self._lock = threading.Lock()
        self._snapshot = ProgressSnapshot()

    def snapshot(self) -> ProgressSnapshot:
        with self._lock:
            return self._snapshot

    def start(self, language_id: str) -> ProgressSnapshot:
        with self._lock:
            self._snapshot = ProgressSnapshot(
                operation_id=str(uuid4()),
                status="running",
                stage="starting",
                message="Preview request started",
                percent=0,
                language_id=language_id,
            )
            return self._snapshot

    def update(
        self,
        stage: str,
        message: str,
        percent: int,
        *,
        voice_id: str | None = None,
        chunk_index: int | None = None,
        chunk_count: int | None = None,
    ) -> ProgressSnapshot:
        with self._lock:
            current = self._snapshot
            bounded = max(current.percent, min(99, max(0, percent)))
            self._snapshot = ProgressSnapshot(
                operation_id=current.operation_id,
                status="running",
                stage=stage,
                message=message,
                percent=bounded,
                language_id=current.language_id,
                voice_id=voice_id if voice_id is not None else current.voice_id,
                chunk_index=(
                    chunk_index if chunk_index is not None else current.chunk_index
                ),
                chunk_count=(
                    chunk_count if chunk_count is not None else current.chunk_count
                ),
            )
            return self._snapshot

    def complete(self, message: str = "Preview completed") -> ProgressSnapshot:
        with self._lock:
            current = self._snapshot
            self._snapshot = ProgressSnapshot(
                operation_id=current.operation_id,
                status="completed",
                stage="completed",
                message=message,
                percent=100,
                language_id=current.language_id,
                voice_id=current.voice_id,
                chunk_index=current.chunk_count,
                chunk_count=current.chunk_count,
            )
            return self._snapshot

    def fail(self, error: str) -> ProgressSnapshot:
        with self._lock:
            current = self._snapshot
            self._snapshot = ProgressSnapshot(
                operation_id=current.operation_id,
                status="failed",
                stage="failed",
                message="Preview failed",
                percent=current.percent,
                language_id=current.language_id,
                voice_id=current.voice_id,
                chunk_index=current.chunk_index,
                chunk_count=current.chunk_count,
                error=error,
            )
            return self._snapshot
