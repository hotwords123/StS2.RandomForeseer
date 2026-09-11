#!/usr/bin/env python3
"""Remove Godot UID sidecars whose source resource no longer exists.

Godot stores a resource UID in a sibling file such as ``Example.cs.uid``.
This script removes only orphaned ``*.uid`` files; it does not rewrite valid
UIDs or touch Godot's generated ``.godot`` cache.

Examples::

    uv run scripts/clean-invalid-uids.py --dry-run
    uv run scripts/clean-invalid-uids.py
"""

from __future__ import annotations

import argparse
from pathlib import Path


GENERATED_DIRECTORIES = frozenset({".git", ".godot", "artifacts", "bin", "obj"})


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Remove orphaned Godot .uid sidecar files from a repository."
    )
    parser.add_argument(
        "--root",
        type=Path,
        default=None,
        help="Repository root (defaults to the parent of this script's directory).",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="List orphaned files without deleting them.",
    )
    return parser.parse_args()


def repository_root(argument: Path | None) -> Path:
    root = argument.resolve() if argument is not None else Path(__file__).resolve().parents[1]
    if not root.is_dir():
        raise SystemExit(f"Repository root does not exist or is not a directory: {root}")
    return root


def is_generated_path(path: Path, root: Path) -> bool:
    try:
        relative_parts = path.relative_to(root).parts
    except ValueError:
        return True
    return any(part in GENERATED_DIRECTORIES for part in relative_parts)


def orphan_uid_files(root: Path) -> list[Path]:
    orphaned: list[Path] = []
    for uid_path in root.rglob("*.uid"):
        if not uid_path.is_file() or is_generated_path(uid_path, root):
            continue

        source_path = uid_path.with_name(uid_path.name.removesuffix(".uid"))
        if not source_path.is_file():
            orphaned.append(uid_path)
    return sorted(orphaned)


def main() -> int:
    args = parse_args()
    root = repository_root(args.root)
    orphaned = orphan_uid_files(root)

    action = "Would remove" if args.dry_run else "Removing"
    for uid_path in orphaned:
        relative_path = uid_path.relative_to(root).as_posix()
        print(f"{action}: {relative_path}")
        if not args.dry_run:
            uid_path.unlink()

    if not orphaned:
        print("No orphaned .uid files found.")
    else:
        verb = "would be removed" if args.dry_run else "removed"
        print(f"{len(orphaned)} orphaned .uid file(s) {verb}.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
