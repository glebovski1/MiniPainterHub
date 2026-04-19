#!/usr/bin/env python3
"""Reflect on recent vault changes and append a reflection log."""

from __future__ import annotations

import argparse
import json
import re
import sys
from collections import defaultdict
from datetime import datetime, timedelta
from pathlib import Path


IGNORED_DIRS = {".git", ".obsidian", ".trash"}
DEFAULT_INDEXES = ["00-index.md", "README.md", "map-of-content.md", "architecture-map.md"]


def iter_markdown(vault: Path):
    for path in vault.rglob("*.md"):
        if any(part in IGNORED_DIRS for part in path.parts):
            continue
        yield path


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8", errors="ignore")


def title_for(path: Path, text: str) -> str:
    fm_title = re.search(r"(?m)^title:\s*(.+)$", text)
    if fm_title:
        return fm_title.group(1).strip().strip("\"'")
    h1 = re.search(r"(?m)^#\s+(.+)$", text)
    if h1:
        return h1.group(1).strip()
    return path.stem


def normalized_name(path: Path) -> str:
    words = re.findall(r"[a-z0-9]+", path.stem.lower())
    return " ".join(words)


def changed_files(vault: Path, explicit: list[str], max_age_hours: int) -> list[Path]:
    if explicit:
        result = []
        for item in explicit:
            path = Path(item)
            if not path.is_absolute():
                path = vault / path
            if path.exists() and path.suffix.lower() == ".md":
                result.append(path.resolve())
        return result
    cutoff = datetime.now() - timedelta(hours=max_age_hours)
    result = []
    for note in iter_markdown(vault):
        try:
            modified = datetime.fromtimestamp(note.stat().st_mtime)
        except OSError:
            continue
        if modified >= cutoff:
            result.append(note)
    return result


def duplicate_titles(notes: list[Path]) -> dict[str, list[str]]:
    titles: dict[str, list[str]] = defaultdict(list)
    for note in notes:
        text = read_text(note)
        titles[title_for(note, text).lower()].append(note.as_posix())
    return {title: paths for title, paths in titles.items() if len(paths) > 1}


def similar_names(notes: list[Path]) -> dict[str, list[str]]:
    names: dict[str, list[str]] = defaultdict(list)
    for note in notes:
        key = normalized_name(note)
        if key:
            names[key].append(note.as_posix())
    return {name: paths for name, paths in names.items() if len(paths) > 1}


def empty_notes(notes: list[Path]) -> list[str]:
    result = []
    for note in notes:
        text = re.sub(r"(?s)^---.*?---", "", read_text(note)).strip()
        if len(text) < 20:
            result.append(note.as_posix())
    return result


def large_notes(notes: list[Path]) -> list[str]:
    return [note.as_posix() for note in notes if len(read_text(note)) > 20000]


def missing_indexes(vault: Path) -> list[str]:
    return [name for name in DEFAULT_INDEXES if not (vault / name).exists()]


def archived_without_reason(vault: Path) -> list[str]:
    archive = vault / "_archive" / "deleted"
    if not archive.exists():
        return []
    result = []
    for note in archive.rglob("*.md"):
        text = read_text(note)[:500]
        if "Reason:" not in text:
            result.append(note.relative_to(vault).as_posix())
    return result


def noisy_session_logs(notes: list[Path]) -> list[str]:
    noisy = []
    noise_terms = ["command output", "transcript", "chat log", "every command", "stdout"]
    for note in notes:
        lower = read_text(note).lower()
        if "session" not in note.as_posix().lower() and "log" not in note.as_posix().lower():
            continue
        if sum(1 for term in noise_terms if term in lower) >= 2 or lower.count("```") > 20:
            noisy.append(note.as_posix())
    return noisy


def score_context(changed_count: int) -> int:
    if changed_count <= 7:
        return 1
    if changed_count <= 20:
        return 2
    if changed_count <= 50:
        return 3
    if changed_count <= 100:
        return 4
    return 5


def score_memory(findings: dict[str, object]) -> int:
    penalty = 0
    for key in [
        "duplicateTitles",
        "possibleOverlaps",
        "emptyNotes",
        "largeNotes",
        "archivedWithoutReason",
        "noisySessionLogs",
    ]:
        value = findings.get(key)
        if value:
            penalty += 1
    return max(1, 5 - penalty)


def append_reflection(vault: Path, task: str, findings: dict[str, object]) -> Path:
    now = datetime.now()
    log_dir = vault / "_logs" / "reflections"
    log_dir.mkdir(parents=True, exist_ok=True)
    log_file = log_dir / f"{now:%Y-%m-%d}.md"
    if not log_file.exists():
        log_file.write_text(f"# Reflections - {now:%Y-%m-%d}\n", encoding="utf-8")

    bad_patterns = []
    if findings["duplicateTitles"]:
        bad_patterns.append("Duplicate titles detected.")
    if findings["possibleOverlaps"]:
        bad_patterns.append("Possible overlapping notes detected.")
    if findings["emptyNotes"]:
        bad_patterns.append("Empty or near-empty notes detected.")
    if findings["largeNotes"]:
        bad_patterns.append("Very large notes may need splitting.")
    if findings["archivedWithoutReason"]:
        bad_patterns.append("Archived notes without a visible reason detected.")
    if findings["noisySessionLogs"]:
        bad_patterns.append("Noisy session logs detected.")
    if not bad_patterns:
        bad_patterns.append("none")

    entry = (
        f"\n## {now:%H:%M} — Reflection on {task}\n\n"
        "### What went well\n"
        "- Reflection checked recent vault edits and common maintenance risks.\n\n"
        "### Possible bad patterns detected\n"
        + "".join(f"- {item}\n" for item in bad_patterns)
        + "\n### Fixes applied now\n"
        "- none\n\n"
        "### Fixes recommended later\n"
        "- Review reported findings before applying structural changes.\n\n"
        "### Context pollution score\n"
        f"- {findings['contextPollutionScore']}\n\n"
        "### Memory quality score\n"
        f"- {findings['memoryQualityScore']}\n"
    )
    with log_file.open("a", encoding="utf-8") as handle:
        handle.write(entry)
    return log_file


def main() -> int:
    parser = argparse.ArgumentParser(description="Reflect on recent vault changes.")
    parser.add_argument("--vault", required=True, help="Path to vault root.")
    parser.add_argument("--files", nargs="*", default=[], help="Specific changed files to review.")
    parser.add_argument("--max-age-hours", type=int, default=24)
    parser.add_argument("--task", default="vault changes")
    args = parser.parse_args()

    vault = Path(args.vault).resolve()
    if not vault.exists() or not vault.is_dir():
        print(f"Vault does not exist or is not a directory: {vault}", file=sys.stderr)
        return 2

    recent = changed_files(vault, args.files, args.max_age_hours)
    all_notes = list(iter_markdown(vault))
    findings: dict[str, object] = {
        "changedFilesReviewed": [path.relative_to(vault).as_posix() for path in recent],
        "duplicateTitles": duplicate_titles(all_notes),
        "possibleOverlaps": similar_names(all_notes),
        "emptyNotes": [path.relative_to(vault).as_posix() for path in map(Path, empty_notes(recent))],
        "largeNotes": [path.relative_to(vault).as_posix() for path in map(Path, large_notes(recent))],
        "missingIndexes": missing_indexes(vault),
        "archivedWithoutReason": archived_without_reason(vault),
        "noisySessionLogs": [path.relative_to(vault).as_posix() for path in map(Path, noisy_session_logs(recent))],
    }
    findings["contextPollutionScore"] = score_context(len(recent))
    findings["memoryQualityScore"] = score_memory(findings)
    log_file = append_reflection(vault, args.task, findings)
    findings["reflectionLog"] = log_file.relative_to(vault).as_posix()

    print(json.dumps(findings, indent=2))
    return 0


if __name__ == "__main__":
    sys.exit(main())
