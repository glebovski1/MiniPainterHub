#!/usr/bin/env python3
"""Archive a vault note without permanent deletion."""

from __future__ import annotations

import argparse
import re
import sys
from datetime import date
from pathlib import Path


def ensure_inside(path: Path, root: Path) -> None:
    try:
        path.resolve().relative_to(root.resolve())
    except ValueError:
        raise SystemExit(f"Refusing to operate outside vault: {path}")


def unique_destination(path: Path) -> Path:
    if not path.exists():
        return path
    stem = path.stem
    suffix = path.suffix
    parent = path.parent
    counter = 1
    while True:
        candidate = parent / f"{stem}-{counter}{suffix}"
        if not candidate.exists():
            return candidate
        counter += 1


def first_heading(text: str, fallback: str) -> str:
    match = re.search(r"^#\s+(.+)$", text, flags=re.MULTILINE)
    return match.group(1).strip() if match else fallback


def redirect_stub(title: str, canonical: str, today: str) -> str:
    return (
        "---\n"
        "type: redirect\n"
        "status: merged\n"
        f"merged_into: \"[[{canonical}]]\"\n"
        f"date: {today}\n"
        "---\n\n"
        f"# {title}\n\n"
        f"This note was merged into [[{canonical}]] on {today}.\n"
    )


def main() -> int:
    parser = argparse.ArgumentParser(description="Archive a vault note without deletion.")
    parser.add_argument("--vault", required=True, help="Path to the vault root.")
    parser.add_argument("--note", required=True, help="Note path, relative to vault or absolute.")
    parser.add_argument("--reason", required=True)
    parser.add_argument("--canonical", default="")
    parser.add_argument("--redirect", action="store_true")
    args = parser.parse_args()

    vault = Path(args.vault).resolve()
    if not vault.exists() or not vault.is_dir():
        print(f"Vault does not exist or is not a directory: {vault}", file=sys.stderr)
        return 2

    note = Path(args.note)
    if not note.is_absolute():
        note = vault / note
    note = note.resolve()
    ensure_inside(note, vault)

    if not note.exists() or not note.is_file():
        print(f"Note does not exist: {note}", file=sys.stderr)
        return 2
    if note.suffix.lower() != ".md":
        print(f"Refusing to archive non-Markdown file: {note}", file=sys.stderr)
        return 2
    if args.redirect and not args.canonical:
        print("--redirect requires --canonical", file=sys.stderr)
        return 2

    today = date.today().isoformat()
    relative = note.relative_to(vault)
    archive_path = vault / "_archive" / "deleted" / today / relative
    archive_path = unique_destination(archive_path)
    archive_path.parent.mkdir(parents=True, exist_ok=True)

    original = note.read_text(encoding="utf-8", errors="ignore")
    reason = args.reason
    if args.canonical and "[[" not in reason:
        reason = f"{reason}; superseded by [[{args.canonical}]]"
    header = f"> Archived by Codex on {today}.\n> Reason: {reason}.\n\n"
    archive_path.write_text(header + original, encoding="utf-8")

    if args.redirect:
        title = first_heading(original, note.stem)
        note.write_text(redirect_stub(title, args.canonical, today), encoding="utf-8")
    else:
        note.unlink()

    print(
        {
            "archived": relative.as_posix(),
            "archivePath": archive_path.relative_to(vault).as_posix(),
            "redirectCreated": bool(args.redirect),
        }
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
