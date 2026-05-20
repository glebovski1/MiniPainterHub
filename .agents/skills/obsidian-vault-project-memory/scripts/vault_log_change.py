#!/usr/bin/env python3
"""Append a structured vault change log entry."""

from __future__ import annotations

import argparse
import sys
from datetime import datetime
from pathlib import Path


VALID_TYPES = {"create", "update", "merge", "archive", "rename", "structure", "reflection"}


def parse_files(values: list[str]) -> list[str]:
    files: list[str] = []
    for value in values:
        for item in value.split(","):
            item = item.strip()
            if item:
                files.append(item)
    return files or ["unspecified"]


def main() -> int:
    parser = argparse.ArgumentParser(description="Append a vault change log entry.")
    parser.add_argument("--vault", required=True, help="Path to the vault root.")
    parser.add_argument("--type", required=True, choices=sorted(VALID_TYPES))
    parser.add_argument("--title", required=True)
    parser.add_argument("--files", nargs="*", default=[])
    parser.add_argument("--reason", required=True)
    parser.add_argument("--summary", required=True)
    parser.add_argument("--source", required=True)
    parser.add_argument("--follow-up", default="")
    args = parser.parse_args()

    vault = Path(args.vault).resolve()
    if not vault.exists() or not vault.is_dir():
        print(f"Vault does not exist or is not a directory: {vault}", file=sys.stderr)
        return 2

    now = datetime.now()
    log_dir = vault / "_logs" / "vault-changes"
    log_dir.mkdir(parents=True, exist_ok=True)
    log_file = log_dir / f"{now:%Y-%m-%d}.md"

    files = parse_files(args.files)
    file_lines = "\n".join(f"  - `{file}`" for file in files)
    follow_up = args.follow_up.strip() or "none"
    entry = (
        f"\n## {now:%H:%M} — {args.title}\n\n"
        f"- Type: {args.type}\n"
        f"- Files changed:\n{file_lines}\n"
        f"- Reason:\n  - {args.reason}\n"
        f"- Summary:\n  - {args.summary}\n"
        f"- Source:\n  - {args.source}\n"
        f"- Follow-up:\n  - {follow_up}\n"
    )

    if not log_file.exists():
        log_file.write_text(f"# Vault Changes - {now:%Y-%m-%d}\n", encoding="utf-8")
    with log_file.open("a", encoding="utf-8") as handle:
        handle.write(entry)

    print(str(log_file))
    return 0


if __name__ == "__main__":
    sys.exit(main())
