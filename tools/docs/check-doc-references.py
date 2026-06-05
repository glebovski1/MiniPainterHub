#!/usr/bin/env python3
"""Check active Markdown links and repo-path references in project docs."""

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path
from urllib.parse import unquote


WIKILINK_RE = re.compile(r"(?<!!)\[\[([^\]]+)\]\]")
MDLINK_RE = re.compile(r"(?<!!)\[[^\]]+\]\(([^)]+)\)")
INLINE_CODE_RE = re.compile(r"`([^`\n]+)`")

IGNORED_DIRS = {
    ".git",
    ".obsidian",
    ".trash",
    "bin",
    "node_modules",
    "obj",
    "output",
    "test-results",
    "tmp",
    "_archive",
}

KNOWN_PATH_ROOTS = {
    ".agents",
    ".codex",
    ".github",
    "docs",
    "e2e",
    "infra",
    "MiniPainterHub.Common",
    "MiniPainterHub.Server",
    "MiniPainterHub.Server.Tests",
    "MiniPainterHub.WebApp",
    "MiniPainterHub.WebApp.Tests",
    "ObsidianVault",
    "tools",
}

KNOWN_ROOT_FILES = {
    "AGENT.md",
    "AGENTS.md",
    "Directory.Packages.props",
    "MiniPainterHub.sln",
    "README.md",
}

GENERATED_OUTPUT_PATH_PREFIXES = {
    "e2e/perf-results",
    "e2e/test-results",
}


def iter_active_markdown(repo: Path):
    roots = [
        repo / "AGENT.md",
        repo / "AGENTS.md",
        repo / "README.md",
        repo / "docs",
        repo / "ObsidianVault",
        repo / ".agents",
    ]

    for root in roots:
        if not root.exists():
            continue
        if root.is_file():
            yield root
            continue
        for path in root.rglob("*.md"):
            if any(part in IGNORED_DIRS for part in path.relative_to(repo).parts):
                continue
            yield path


def strip_link_target(target: str) -> str:
    target = target.split("|", 1)[0]
    target = target.split("#", 1)[0]
    target = unquote(target.strip())
    if target.startswith("<") and target.endswith(">"):
        target = target[1:-1].strip()
    return target


def is_external(target: str) -> bool:
    lowered = target.lower()
    return (
        "://" in lowered
        or lowered.startswith("mailto:")
        or lowered.startswith("tel:")
        or lowered.startswith("app://")
        or lowered.startswith("plugin://")
        or lowered.startswith("#")
    )


def build_vault_note_index(repo: Path) -> set[str]:
    vault = repo / "ObsidianVault"
    index: set[str] = set()
    if not vault.exists():
        return index

    for note in vault.rglob("*.md"):
        if any(part in IGNORED_DIRS for part in note.relative_to(repo).parts):
            continue
        rel = note.relative_to(vault).as_posix()
        no_ext = rel[:-3] if rel.endswith(".md") else rel
        index.add(no_ext.lower())
        index.add(note.stem.lower())
    return index


def wiki_exists(target: str, note_index: set[str]) -> bool:
    clean = strip_link_target(target)
    if not clean:
        return True
    clean = clean[:-3] if clean.lower().endswith(".md") else clean
    return clean.lower() in note_index or Path(clean).stem.lower() in note_index


def markdown_link_exists(target: str, source: Path, repo: Path) -> bool:
    clean = strip_link_target(target)
    if not clean or is_external(clean):
        return True

    candidate = (repo / clean.lstrip("/")) if clean.startswith("/") else (source.parent / clean)
    if candidate.exists():
        return True

    if candidate.suffix == "" and candidate.with_suffix(".md").exists():
        return True

    return False


def normalize_repo_candidate(value: str) -> str | None:
    candidate = value.strip().strip(".,;:")
    if not candidate or is_external(candidate):
        return None
    if any(marker in candidate for marker in ("*", "{", "}", "...", "$(")):
        return None
    if candidate.startswith("<") and candidate.endswith(">"):
        candidate = candidate[1:-1].strip()
    candidate = candidate.replace("\\", "/")
    candidate = candidate.split("#", 1)[0].strip("/")
    if not candidate:
        return None
    if is_generated_output_candidate(candidate):
        return None

    if candidate in KNOWN_ROOT_FILES:
        return candidate

    first_segment = candidate.split("/", 1)[0]
    if first_segment not in KNOWN_PATH_ROOTS:
        return None

    return candidate


def is_generated_output_candidate(candidate: str) -> bool:
    return any(
        candidate == prefix or candidate.startswith(f"{prefix}/")
        for prefix in GENERATED_OUTPUT_PATH_PREFIXES
    )


def repo_path_exists(candidate: str, repo: Path) -> bool:
    path = repo / candidate
    if path.exists():
        return True
    if path.suffix == "" and path.with_suffix(".md").exists():
        return True
    return False


def main() -> int:
    parser = argparse.ArgumentParser(description="Check active docs for stale links and repo paths.")
    parser.add_argument("--repo", default=".", help="Repository root.")
    parser.add_argument("--format", choices=["json", "text"], default="text")
    args = parser.parse_args()

    repo = Path(args.repo).resolve()
    if not repo.exists() or not repo.is_dir():
        print(f"Repository does not exist or is not a directory: {repo}", file=sys.stderr)
        return 2

    note_index = build_vault_note_index(repo)
    issues: list[dict[str, str]] = []

    for doc in iter_active_markdown(repo):
        text = doc.read_text(encoding="utf-8", errors="ignore")
        rel = doc.relative_to(repo).as_posix()

        if (repo / "ObsidianVault") in doc.parents:
            for match in WIKILINK_RE.finditer(text):
                target = match.group(1)
                if not wiki_exists(target, note_index):
                    issues.append({"file": rel, "type": "wikilink", "target": target})

        for match in MDLINK_RE.finditer(text):
            target = match.group(1)
            if not markdown_link_exists(target, doc, repo):
                issues.append({"file": rel, "type": "markdown-link", "target": target})

        for match in INLINE_CODE_RE.finditer(text):
            candidate = normalize_repo_candidate(match.group(1))
            if candidate is not None and not repo_path_exists(candidate, repo):
                issues.append({"file": rel, "type": "repo-path", "target": candidate})

    result = {
        "repo": str(repo),
        "checkedDocs": sum(1 for _ in iter_active_markdown(repo)),
        "issueCount": len(issues),
        "issues": issues,
    }

    if args.format == "json":
        print(json.dumps(result, indent=2))
    else:
        print(f"Checked docs: {result['checkedDocs']}")
        print(f"Issues: {len(issues)}")
        for issue in issues:
            print(f"- {issue['file']}: {issue['type']} -> {issue['target']}")

    return 1 if issues else 0


if __name__ == "__main__":
    sys.exit(main())
