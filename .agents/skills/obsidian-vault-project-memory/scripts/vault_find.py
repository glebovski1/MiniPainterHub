#!/usr/bin/env python3
"""Locate an existing Obsidian vault for a repository.

This script is read-only. It checks repo-local config, Obsidian markers, common
folder names, and small root-level docs for vault path hints.
"""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path
from typing import Any


COMMON_FOLDERS = [
    "knowledge",
    "vault",
    "notes",
    "docs/vault",
    "obsidian",
    "project-memory",
    "ObsidianVault",
]

DEFAULT_INDEXES = [
    "00-index.md",
    "README.md",
    "map-of-content.md",
    "architecture-map.md",
]


def as_posix_relative(path: Path, root: Path) -> str:
    try:
        return path.resolve().relative_to(root.resolve()).as_posix()
    except ValueError:
        return str(path.resolve())


def has_obsidian_config(path: Path) -> bool:
    return (path / ".obsidian").is_dir()


def load_config(repo: Path) -> dict[str, Any]:
    config_path = repo / ".codex" / "vault-memory.json"
    if not config_path.exists():
        return {}
    try:
        return json.loads(config_path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        return {"_error": f"Invalid JSON in {config_path}: {exc}"}


def config_candidate(repo: Path, config: dict[str, Any]) -> dict[str, Any] | None:
    raw_root = config.get("vaultRoot")
    if not raw_root or not isinstance(raw_root, str):
        return None
    path = Path(raw_root)
    if not path.is_absolute():
        path = repo / path
    return {
        "path": as_posix_relative(path, repo),
        "source": ".codex/vault-memory.json",
        "exists": path.exists(),
        "hasObsidianConfig": has_obsidian_config(path),
    }


def obsidian_marker_candidates(repo: Path) -> list[dict[str, Any]]:
    candidates: list[dict[str, Any]] = []
    if has_obsidian_config(repo):
        candidates.append(
            {
                "path": ".",
                "source": ".obsidian marker",
                "exists": True,
                "hasObsidianConfig": True,
            }
        )
    for child in repo.iterdir():
        if child.is_dir() and has_obsidian_config(child):
            candidates.append(
                {
                    "path": as_posix_relative(child, repo),
                    "source": ".obsidian marker",
                    "exists": True,
                    "hasObsidianConfig": True,
                }
            )
    return candidates


def common_folder_candidates(repo: Path) -> list[dict[str, Any]]:
    candidates: list[dict[str, Any]] = []
    for name in COMMON_FOLDERS:
        path = repo / name
        if path.exists() and path.is_dir():
            candidates.append(
                {
                    "path": as_posix_relative(path, repo),
                    "source": "common folder",
                    "exists": True,
                    "hasObsidianConfig": has_obsidian_config(path),
                }
            )
    return candidates


def mentioned_path_candidates(repo: Path) -> list[dict[str, Any]]:
    docs = [
        repo / "README.md",
        repo / "AGENTS.md",
        repo / "AGENT.md",
        repo / "docs" / "README.md",
    ]
    pattern = re.compile(
        r"(?i)(?:vault|obsidian)[^\n`'\"]{0,80}?[`'\"]?([A-Za-z0-9_./ -]*(?:vault|obsidian)[A-Za-z0-9_./ -]*)[`'\"]?"
    )
    candidates: list[dict[str, Any]] = []
    for doc in docs:
        if not doc.exists() or not doc.is_file():
            continue
        try:
            text = doc.read_text(encoding="utf-8", errors="ignore")
        except OSError:
            continue
        for match in pattern.finditer(text):
            raw = match.group(1).strip(" .`'\"")
            if not raw or len(raw) > 120:
                continue
            path = Path(raw)
            if not path.is_absolute():
                path = repo / path
            if path.exists() and path.is_dir():
                candidates.append(
                    {
                        "path": as_posix_relative(path, repo),
                        "source": as_posix_relative(doc, repo),
                        "exists": True,
                        "hasObsidianConfig": has_obsidian_config(path),
                    }
                )
    return candidates


def dedupe(candidates: list[dict[str, Any]]) -> list[dict[str, Any]]:
    seen: set[str] = set()
    result: list[dict[str, Any]] = []
    for candidate in candidates:
        key = candidate["path"]
        if key in seen:
            continue
        seen.add(key)
        result.append(candidate)
    return result


def suggested_config(vault_root: str | None, config: dict[str, Any] | None = None) -> dict[str, Any]:
    config = config or {}
    reflection = config.get("reflection") if isinstance(config.get("reflection"), dict) else {}
    indexes = config.get("indexes") if isinstance(config.get("indexes"), list) else DEFAULT_INDEXES
    return {
        "vaultRoot": vault_root
        or config.get("vaultRoot")
        or "TODO: set relative or absolute path to existing Obsidian vault",
        "archiveDir": config.get("archiveDir", "_archive"),
        "logsDir": config.get("logsDir", "_logs"),
        "indexes": indexes,
        "preferredNoteStyle": config.get("preferredNoteStyle", "obsidian-wikilinks"),
        "deleteMode": config.get("deleteMode", "archive-first"),
        "maxInitialNotesToRead": config.get("maxInitialNotesToRead", 7),
        "reflection": {
            "enabled": reflection.get("enabled", True),
            "useSubagentWhenAvailable": reflection.get("useSubagentWhenAvailable", True),
            "logDir": reflection.get("logDir", "_logs/reflections"),
        },
    }


def choose_candidate(
    config: dict[str, Any], candidates: list[dict[str, Any]]
) -> tuple[str | None, str, str]:
    config_root = config_candidate(Path.cwd(), config)
    if config_root and config_root.get("exists") and config_root.get("hasObsidianConfig"):
        return config_root["path"], "high", "Configured vaultRoot exists and contains .obsidian/."
    if config_root and config_root.get("exists"):
        return config_root["path"], "medium", "Configured vaultRoot exists but no .obsidian/ marker was found."
    if config_root:
        return None, "low", "Configured vaultRoot does not exist."

    obsidian_candidates = [c for c in candidates if c.get("hasObsidianConfig")]
    if len(obsidian_candidates) == 1:
        return (
            obsidian_candidates[0]["path"],
            "high",
            "Exactly one vault candidate contains .obsidian/.",
        )
    if len(obsidian_candidates) > 1:
        return None, "low", "Multiple candidates contain .obsidian/."

    existing = [c for c in candidates if c.get("exists")]
    if len(existing) == 1:
        return existing[0]["path"], "medium", "Exactly one common or mentioned vault candidate exists."
    if len(existing) > 1:
        return None, "low", "Multiple vault candidates exist without a clear .obsidian/ marker."
    return None, "none", "No existing vault candidate was found."


def main() -> int:
    repo = Path.cwd()
    config = load_config(repo)
    candidates: list[dict[str, Any]] = []

    config_root = config_candidate(repo, config)
    if config_root:
        candidates.append(config_root)
    candidates.extend(obsidian_marker_candidates(repo))
    candidates.extend(common_folder_candidates(repo))
    candidates.extend(mentioned_path_candidates(repo))
    candidates = dedupe(candidates)

    detected, confidence, reason = choose_candidate(config, candidates)
    result = {
        "detectedVaultRoot": detected,
        "confidence": confidence,
        "candidates": candidates,
        "reason": reason,
        "hasObsidianConfig": bool(
            detected and (repo / detected).exists() and has_obsidian_config(repo / detected)
        ),
        "suggestedConfig": suggested_config(detected, config),
    }

    if config.get("_error"):
        result["configError"] = config["_error"]

    print(json.dumps(result, indent=2))
    return 0


if __name__ == "__main__":
    sys.exit(main())
