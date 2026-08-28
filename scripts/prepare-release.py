#!/usr/bin/env python3
"""Update release metadata, stage the changes, and optionally commit them.

Typical usage (run from the repository root)::

    uv run scripts/prepare-release.py \
        --mod-version 0.13.10 \
        --game-version 0.111.0 \
        --ritsu-lib-version 0.5.12 \
        --commit

The script updates the manifest, project dependency, bilingual README files,
the current public-beta Workshop descriptions, and the changelog heading.
Public-branch metadata and active Workshop game versions can be updated with
the corresponding optional arguments.  No tag, package, push, or release is
created by this script.
"""

from __future__ import annotations

import argparse
import re
import subprocess
import sys
from pathlib import Path


SEMVER = re.compile(r"^\d+\.\d+\.\d+(?:-[0-9A-Za-z][0-9A-Za-z.-]*)?$")
CONVENTIONAL_COMMIT = re.compile(
    r"^(?:build|chore|ci|docs|feat|fix|perf|refactor|revert|style|test)"
    r"(?:\([^)]+\))?!?: .+"
)

TARGET_FILES = (
    "RandomForeseer.json",
    "RandomForeseer.csproj",
    "CHANGELOG.md",
    "README.md",
    "README.en.md",
    "workshop/description.txt",
    "workshop/description.en.txt",
    "workshop/active-game-versions.txt",
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Prepare and stage a Random Foreseer release.",
    )
    parser.add_argument("--mod-version", required=True, help="New Mod SemVer.")
    parser.add_argument(
        "--game-version",
        required=True,
        help="New minimum game version and current public-beta game version.",
    )
    parser.add_argument(
        "--ritsu-lib-version",
        required=True,
        help="New minimum STS2-RitsuLib version.",
    )
    parser.add_argument(
        "--public-game-version",
        help="Optional public-branch game version to write in Workshop descriptions.",
    )
    parser.add_argument(
        "--public-mod-version",
        help="Optional public-branch Mod version (requires --public-game-version).",
    )
    parser.add_argument(
        "--active-game-version",
        dest="active_game_versions",
        action="append",
        help="Replace active-game-versions.txt entries; repeat for each active version.",
    )
    parser.add_argument(
        "--message",
        help="Conventional Commit message (default: chore(release): prepare vX.Y.Z).",
    )
    parser.add_argument(
        "--commit",
        action="store_true",
        help="Commit the release-preparation changes after staging them.",
    )
    parser.add_argument(
        "--allow-dirty",
        action="store_true",
        help="Allow unrelated existing working-tree or index changes.",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Show planned changes without writing, staging, or committing.",
    )
    args = parser.parse_args()

    for name in ("mod_version", "game_version", "ritsu_lib_version"):
        value = getattr(args, name)
        if not SEMVER.fullmatch(value):
            parser.error(f"{name.replace('_', '-')} must be a SemVer value: {value}")
    if args.public_game_version and not args.public_mod_version:
        parser.error("--public-mod-version is required with --public-game-version")
    if args.public_mod_version and not args.public_game_version:
        parser.error("--public-game-version is required with --public-mod-version")
    for value in args.active_game_versions or ():
        if not SEMVER.fullmatch(value):
            parser.error(f"active game versions must be SemVer values: {value}")

    args.message = args.message or f"chore(release): prepare v{args.mod_version}"
    if not CONVENTIONAL_COMMIT.fullmatch(args.message):
        parser.error("--message must use Conventional Commits format, e.g. 'chore(release): prepare vX.Y.Z'")
    return args


def run_git(root: Path, *arguments: str, check: bool = True) -> subprocess.CompletedProcess[str]:
    result = subprocess.run(
        ["git", *arguments],
        cwd=root,
        text=True,
        encoding="utf-8",
        capture_output=True,
    )
    if check and result.returncode:
        details = (result.stderr or result.stdout).strip()
        raise RuntimeError(f"git {' '.join(arguments)} failed: {details}")
    return result


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def write_text(path: Path, content: str) -> None:
    path.write_text(content.replace("\r\n", "\n"), encoding="utf-8", newline="\n")


def replace_once(content: str, pattern: str, replacement: str, label: str) -> str:
    updated, count = re.subn(pattern, replacement, content, count=1, flags=re.MULTILINE | re.DOTALL)
    if count != 1:
        raise RuntimeError(f"Expected exactly one {label} to update, found {count}.")
    return updated


def update_file(
    root: Path,
    relative_path: str,
    transformations: list[tuple[str, str, str]],
    *,
    write: bool = True,
) -> bool:
    path = root / relative_path
    original = read_text(path)
    updated = original
    for pattern, replacement, label in transformations:
        updated = replace_once(updated, pattern, replacement, label)
    if updated != original and write:
        write_text(path, updated)
    return updated != original


def update_active_versions(root: Path, versions: list[str], *, write: bool = True) -> bool:
    path = root / "workshop/active-game-versions.txt"
    original = read_text(path)
    comments = [line for line in original.replace("\r\n", "\n").splitlines() if line.startswith("#")]
    lines = comments + versions
    updated = "\n".join(lines) + "\n"
    if updated != original and write:
        write_text(path, updated)
    return updated != original


def update_changelog(root: Path, mod_version: str, *, write: bool = True) -> bool:
    path = root / "CHANGELOG.md"
    original = read_text(path)
    release_heading = f"## v{mod_version}"
    if re.search(rf"^##\s+{re.escape(release_heading[3:])}\s*$", original, re.MULTILINE):
        return False
    updated = replace_once(
        original,
        r"^##\s+Unreleased\s*$",
        release_heading,
        "Unreleased changelog heading",
    )
    if write:
        write_text(path, updated)
    return True


def ensure_clean(root: Path, allow_dirty: bool) -> None:
    if allow_dirty:
        return
    unstaged = run_git(root, "diff", "--quiet", check=False).returncode
    staged = run_git(root, "diff", "--cached", "--quiet", check=False).returncode
    if unstaged or staged:
        raise RuntimeError("Working tree and index must be clean; use --allow-dirty to override.")


def main() -> int:
    args = parse_args()
    root = Path(__file__).resolve().parent.parent
    ensure_clean(root, args.allow_dirty)

    changed: list[str] = []
    if update_file(
        root,
        "RandomForeseer.json",
        [
            (r'("version"\s*:\s*")[^"]+("\s*,)', rf"\g<1>{args.mod_version}\g<2>", "manifest version"),
            (r'("min_game_version"\s*:\s*")[^"]+("\s*,)', rf"\g<1>{args.game_version}\g<2>", "manifest minimum game version"),
            (
                r'("id"\s*:\s*"STS2-RitsuLib".*?"min_version"\s*:\s*")[^"]+("\s*)',
                rf"\g<1>{args.ritsu_lib_version}\g<2>",
                "manifest RitsuLib dependency",
            ),
        ],
        write=not args.dry_run,
    ):
        changed.append("RandomForeseer.json")

    if update_file(
        root,
        "RandomForeseer.csproj",
        [
            (
                r'(<PackageReference\s+Include="STS2\.RitsuLib"\s+Version=")[^"]+("\s+GeneratePathProperty=)',
                rf"\g<1>{args.ritsu_lib_version}\g<2>",
                "csproj RitsuLib dependency",
            )
        ],
        write=not args.dry_run,
    ):
        changed.append("RandomForeseer.csproj")

    for path, labels in (
        ("README.md", ("当前版本", "最低游戏版本", "RitsuLib 依赖")),
        ("README.en.md", ("Current version", "Minimum game version", "RitsuLib dependency")),
    ):
        values = (args.mod_version, args.game_version, args.ritsu_lib_version)
        transformations = [
            (rf"(\|\s*{re.escape(label)}\s*\|\s*`)[^`]+(`\s*\|)", rf"\g<1>{value}\g<2>", label)
            for label, value in zip(labels, values)
        ]
        if update_file(root, path, transformations, write=not args.dry_run):
            changed.append(path)

    workshop_transformations = [
        (
            r"(\[\*]测试版：\[b]v)[^\[]+(\[/b].*?Mod 版本 \[b]v)[^\[]+(\[/b])",
            rf"\g<1>{args.game_version}\g<2>{args.mod_version}\g<3>",
            "Chinese public-beta Workshop version",
        ),
        (
            r"(\[\*]public-beta: \[b]v)[^\[]+(\[/b] \(mod version \[b]v)[^\[]+(\[/b])",
            rf"\g<1>{args.game_version}\g<2>{args.mod_version}\g<3>",
            "English public-beta Workshop version",
        ),
    ]
    if args.public_game_version:
        workshop_transformations.extend(
            [
                (
                    r"(\[\*]正式版：\[b]v)[^\[]+(\[/b].*?Mod 版本 \[b]v)[^\[]+(\[/b])",
                    rf"\g<1>{args.public_game_version}\g<2>{args.public_mod_version}\g<3>",
                    "Chinese public Workshop version",
                ),
                (
                    r"(\[\*]public: \[b]v)[^\[]+(\[/b] \(mod version \[b]v)[^\[]+(\[/b])",
                    rf"\g<1>{args.public_game_version}\g<2>{args.public_mod_version}\g<3>",
                    "English public Workshop version",
                ),
            ]
        )
    for path, transformation in (
        ("workshop/description.txt", workshop_transformations[0:1] + (workshop_transformations[2:3] if args.public_game_version else [])),
        ("workshop/description.en.txt", workshop_transformations[1:2] + (workshop_transformations[3:4] if args.public_game_version else [])),
    ):
        if update_file(root, path, transformation, write=not args.dry_run):
            changed.append(path)

    if args.active_game_versions:
        if update_active_versions(root, args.active_game_versions, write=not args.dry_run):
            changed.append("workshop/active-game-versions.txt")

    if update_changelog(root, args.mod_version, write=not args.dry_run):
        changed.append("CHANGELOG.md")

    if args.dry_run:
        print("Would update: " + (", ".join(changed) if changed else "no files"))
        return 0

    if changed:
        run_git(root, "add", "--", *changed)
    else:
        print("No release metadata changes were needed.")

    if args.commit:
        if not changed:
            raise RuntimeError("Cannot create a release commit because no files changed.")
        run_git(root, "commit", "--only", "-m", args.message, "--", *changed)
        print(f"Created commit: {args.message}")
    else:
        print("Staged release metadata changes (pass --commit to commit them).")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, RuntimeError) as error:
        print(f"error: {error}", file=sys.stderr)
        raise SystemExit(1)
