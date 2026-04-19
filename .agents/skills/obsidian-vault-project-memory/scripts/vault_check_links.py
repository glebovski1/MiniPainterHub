#!/usr/bin/env python3
"""Check likely broken internal Markdown and Obsidian wikilinks."""

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path
from urllib.parse import unquote


WIKILINK_RE = re.compile(r"(?<!!)\[\[([^\]]+)\]\]")
MDLINK_RE = re.compile(r"(?<!!)\[[^\]]+\]\(([^)]+)\)")
IGNORED_DIRS = {".git", ".obsidian", ".trash"}


def iter_markdown(vault: Path):
    for path in vault.rglob("*.md"):
        if any(part in IGNORED_DIRS for part in path.parts):
            continue
        yield path


def strip_link_target(target: str) -> str:
    target = target.split("|", 1)[0]
    target = target.split("#", 1)[0]
    return unquote(target.strip())


def build_note_index(vault: Path) -> set[str]:
    index: set[str] = set()
    for note in iter_markdown(vault):
        rel = note.relative_to(vault).as_posix()
        no_ext = rel[:-3] if rel.endswith(".md") else rel
        index.add(no_ext.lower())
        index.add(note.stem.lower())
    return index


def is_external(target: str) -> bool:
    lowered = target.lower()
    return (
        "://" in lowered
        or lowered.startswith("mailto:")
        or lowered.startswith("#")
        or lowered.startswith("tel:")
    )


def wiki_exists(target: str, note_index: set[str]) -> bool:
    clean = strip_link_target(target)
    if not clean:
        return True
    clean = clean[:-3] if clean.lower().endswith(".md") else clean
    return clean.lower() in note_index or Path(clean).stem.lower() in note_index


def markdown_exists(target: str, source: Path, vault: Path, note_index: set[str]) -> bool:
    clean = strip_link_target(target)
    if not clean or is_external(clean):
        return True
    if clean.startswith("<") and clean.endswith(">"):
        clean = clean[1:-1]
    if clean.startswith("/"):
        candidate = vault / clean.lstrip("/")
    else:
        candidate = source.parent / clean
    if candidate.exists():
        return True
    if candidate.suffix == "":
        if candidate.with_suffix(".md").exists():
            return True
        rel_no_ext = candidate.relative_to(vault).as_posix() if vault in candidate.parents else clean
        return rel_no_ext.lower() in note_index or Path(clean).stem.lower() in note_index
    return False


def main() -> int:
    parser = argparse.ArgumentParser(description="Check likely broken internal vault links.")
    parser.add_argument("--vault", default=".", help="Path to vault root.")
    parser.add_argument("--format", choices=["json", "text"], default="json")
    args = parser.parse_args()

    vault = Path(args.vault).resolve()
    if not vault.exists() or not vault.is_dir():
        print(f"Vault does not exist or is not a directory: {vault}", file=sys.stderr)
        return 2

    note_index = build_note_index(vault)
    broken: list[dict[str, str]] = []
    for note in iter_markdown(vault):
        text = note.read_text(encoding="utf-8", errors="ignore")
        rel = note.relative_to(vault).as_posix()
        for match in WIKILINK_RE.finditer(text):
            target = match.group(1)
            if not wiki_exists(target, note_index):
                broken.append({"file": rel, "type": "wikilink", "target": target})
        for match in MDLINK_RE.finditer(text):
            target = match.group(1)
            if not markdown_exists(target, note, vault, note_index):
                broken.append({"file": rel, "type": "markdown", "target": target})

    result = {"vault": str(vault), "brokenCount": len(broken), "brokenLinks": broken}
    if args.format == "json":
        print(json.dumps(result, indent=2))
    else:
        print(f"Broken links: {len(broken)}")
        for item in broken:
            print(f"- {item['file']}: {item['type']} -> {item['target']}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
