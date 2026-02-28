#!/usr/bin/env python3
"""Create a Codex skill skeleton in repo/global/both locations."""

from __future__ import annotations

import argparse
from dataclasses import dataclass
import os
from pathlib import Path
import re
import shutil
import sys

NAME_PATTERN = re.compile(r"^[a-z0-9][a-z0-9-]{0,62}$")


@dataclass
class Args:
    name: str
    description: str
    target: str
    repo_root: str | None
    display_name: str | None
    short_description: str | None
    default_prompt: str | None
    force: bool


def _codex_home() -> str:
    return os.environ.get("CODEX_HOME", os.path.expanduser("~/.codex"))


def _default_repo_root() -> str:
    # tools/skills/init-skill.py -> repo root is 2 levels up.
    return str(Path(__file__).resolve().parents[2])


def _title_from_name(name: str) -> str:
    return " ".join(part.capitalize() for part in name.split("-"))


def _target_roots(target: str, repo_root: str) -> list[str]:
    global_root = os.path.join(_codex_home(), "skills")
    repo_skills_root = os.path.join(repo_root, ".agents", "skills")
    if target == "global":
        return [global_root]
    if target == "repo":
        return [repo_skills_root]
    if target == "both":
        return [global_root, repo_skills_root]
    raise ValueError(f"Unknown target: {target}")


def _validate_name(name: str) -> None:
    if not NAME_PATTERN.fullmatch(name):
        raise ValueError("Skill name must match ^[a-z0-9][a-z0-9-]{0,62}$.")


def _skill_md(name: str, description: str) -> str:
    return (
        "---\n"
        f'name: "{name}"\n'
        f'description: "{description}"\n'
        "---\n\n"
        f"# {name}\n\n"
        "## When to use\n"
        "- Add concise trigger guidance for this skill.\n\n"
        "## Workflow\n"
        "1. Add the key workflow step(s).\n"
        "2. Keep steps deterministic where possible.\n"
        "3. Reference bundled scripts/references only when needed.\n\n"
        "## Notes\n"
        "- Keep this file lean.\n"
        "- Move detailed docs to `references/`.\n"
        "- Put deterministic helpers in `scripts/`.\n"
    )


def _openai_yaml(
    name: str,
    display_name: str | None,
    short_description: str | None,
    default_prompt: str | None,
) -> str:
    display = display_name or _title_from_name(name)
    short = short_description or "Skill description placeholder"
    prompt = default_prompt or f"Use ${name} to handle this task."
    return (
        "interface:\n"
        f'  display_name: "{display}"\n'
        f'  short_description: "{short}"\n'
        f'  default_prompt: "{prompt}"\n'
    )


def _ensure_empty_or_reset(path: str, force: bool) -> None:
    if not os.path.exists(path):
        return
    if not force:
        raise FileExistsError(f"Destination already exists: {path}")
    shutil.rmtree(path)


def _create_skill(
    root: str,
    name: str,
    description: str,
    display_name: str | None,
    short_description: str | None,
    default_prompt: str | None,
    force: bool,
) -> str:
    skill_dir = os.path.join(root, name)
    _ensure_empty_or_reset(skill_dir, force)
    os.makedirs(os.path.join(skill_dir, "agents"), exist_ok=True)
    os.makedirs(os.path.join(skill_dir, "scripts"), exist_ok=True)
    os.makedirs(os.path.join(skill_dir, "references"), exist_ok=True)
    os.makedirs(os.path.join(skill_dir, "assets"), exist_ok=True)

    with open(os.path.join(skill_dir, "SKILL.md"), "w", encoding="utf-8", newline="\n") as f:
        f.write(_skill_md(name, description))

    with open(
        os.path.join(skill_dir, "agents", "openai.yaml"), "w", encoding="utf-8", newline="\n"
    ) as f:
        f.write(_openai_yaml(name, display_name, short_description, default_prompt))

    gitkeep = os.path.join(skill_dir, "assets", ".gitkeep")
    with open(gitkeep, "w", encoding="utf-8", newline="\n") as f:
        f.write("")

    return skill_dir


def _parse_args(argv: list[str]) -> Args:
    parser = argparse.ArgumentParser(description="Create a Codex skill skeleton.")
    parser.add_argument("--name", required=True, help="Skill name (kebab-case)")
    parser.add_argument("--description", required=True, help="Skill trigger description")
    parser.add_argument(
        "--target",
        choices=["global", "repo", "both"],
        default="repo",
        help="Create the skill under ~/.codex/skills, ./.agents/skills, or both",
    )
    parser.add_argument("--repo-root", help="Repository root; default inferred from script path")
    parser.add_argument("--display-name", help="agents/openai.yaml interface.display_name")
    parser.add_argument(
        "--short-description", help="agents/openai.yaml interface.short_description"
    )
    parser.add_argument("--default-prompt", help="agents/openai.yaml interface.default_prompt")
    parser.add_argument("--force", action="store_true", help="Overwrite existing skill folder")
    return parser.parse_args(argv, namespace=Args("", "", "repo", None, None, None, None, False))


def main(argv: list[str]) -> int:
    args = _parse_args(argv)
    try:
        _validate_name(args.name)
        repo_root = os.path.abspath(args.repo_root or _default_repo_root())
        roots = _target_roots(args.target, repo_root)
        created = []
        for root in roots:
            os.makedirs(root, exist_ok=True)
            path = _create_skill(
                root=root,
                name=args.name,
                description=args.description,
                display_name=args.display_name,
                short_description=args.short_description,
                default_prompt=args.default_prompt,
                force=args.force,
            )
            created.append(path)
        for path in created:
            print(f"Created {path}")
        return 0
    except (ValueError, FileExistsError) as exc:
        print(f"Error: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))

