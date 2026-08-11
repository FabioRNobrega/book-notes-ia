from __future__ import annotations

import pytest

from app.chunking import chunk_text
from conftest import EN_PREVIEW_TEXT, PT_PREVIEW_TEXT


@pytest.mark.parametrize(
    ("path", "expected"),
    [("config/preview.txt", EN_PREVIEW_TEXT), ("config/pt-preview.txt", PT_PREVIEW_TEXT)],
)
def test_configured_preview_text_is_exact(path: str, expected: str) -> None:
    configured = open(path, encoding="utf-8").read().strip()

    assert configured == expected


@pytest.mark.parametrize("text", [EN_PREVIEW_TEXT, PT_PREVIEW_TEXT])
def test_chunking_preserves_text_order_and_paragraph_boundaries(text: str) -> None:
    chunks = chunk_text(text, max_chars=280)
    paragraphs = text.split("\n\n")
    reconstructed_paragraphs = []
    current = []

    for chunk in chunks:
        assert len(chunk.text) <= 280
        current.append(chunk.text)
        if chunk.paragraph_break_after:
            reconstructed_paragraphs.append(" ".join(current))
            current = []
    reconstructed_paragraphs.append(" ".join(current))

    assert reconstructed_paragraphs == [" ".join(value.split()) for value in paragraphs]
    assert sum(chunk.paragraph_break_after for chunk in chunks) == 2


def test_oversized_sentence_splits_without_losing_words() -> None:
    text = " ".join(f"word{index}" for index in range(100))

    chunks = chunk_text(text, max_chars=80)

    assert " ".join(chunk.text for chunk in chunks) == text
    assert all(len(chunk.text) <= 80 for chunk in chunks)
