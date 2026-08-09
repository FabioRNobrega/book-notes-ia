from __future__ import annotations

from app.chunking import chunk_text
from conftest import PREVIEW_TEXT


def test_configured_preview_text_is_exact() -> None:
    configured = open("config/preview.txt", encoding="utf-8").read().strip()

    assert configured == PREVIEW_TEXT


def test_chunking_preserves_text_order_and_paragraph_boundaries() -> None:
    chunks = chunk_text(PREVIEW_TEXT, max_chars=280)
    paragraphs = PREVIEW_TEXT.split("\n\n")
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

