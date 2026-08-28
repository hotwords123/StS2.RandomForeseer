#!/usr/bin/env python3
"""Prepare a release from the manifest's version metadata.

Examples::

    uv run scripts/prepare-release.py --patch --commit
    uv run scripts/prepare-release.py --version 0.14.0 --commit
    uv run scripts/prepare-release.py --sync

``RandomForeseer.json`` is the source of truth for the Mod version, minimum
game version, and STS2-RitsuLib dependency. The script only changes the Mod
version when ``--version`` or a bump flag is supplied; ``--sync`` propagates
the existing manifest values without changing them. It never creates tags,
packages, releases, or pushes.
"""

from __future__ import annotations

import argparse
import json
import re
import subprocess
import sys
from pathlib import Path


SEMVER = re.compile(r"^(\d+)\.(\d+)\.(\d+)(?:-[0-9A-Za-z][0-9A-Za-z.-]*)?$")
CONVENTIONAL_COMMIT = re.compile(
    r"^(?:build|chore|ci|docs|feat|fix|perf|refactor|revert|style|test)"
    r"(?:\([^)]+\))?!?: .+"
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Prepare and stage a Random Foreseer release.")
    version_group = parser.add_mutually_exclusive_group(required=True)
    version_group.add_argument("--version", help="Set the Mod version explicitly.")
    version_group.add_argument("--major", action="store_true", help="Bump the major component.")
    version_group.add_argument("--minor", action="store_true", help="Bump the minor component.")
    version_group.add_argument("--patch", action="store_true", help="Bump the patch component.")
    version_group.add_argument("--sync", action="store_true", help="Only sync files from RandomForeseer.json.")
    parser.add_argument("--message", help="Conventional Commit message (default: chore(release): prepare vX.Y.Z).")
    parser.add_argument("--commit", action="store_true", help="Commit changes after staging them.")
    parser.add_argument("--allow-dirty", action="store_true", help="Allow unrelated existing changes.")
    parser.add_argument("--dry-run", action="store_true", help="Show changes without writing or staging.")
    args = parser.parse_args()
    if args.version and not SEMVER.fullmatch(args.version):
        parser.error(f"--version must be a SemVer value: {args.version}")
    if args.message and not CONVENTIONAL_COMMIT.fullmatch(args.message):
        parser.error("--message must use Conventional Commits format, e.g. 'chore(release): prepare vX.Y.Z'")
    return args


def run_git(root: Path, *arguments: str, check: bool = True) -> subprocess.CompletedProcess[str]:
    result = subprocess.run(
        ["git", *arguments], cwd=root, text=True, encoding="utf-8", capture_output=True
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


def update_file(root: Path, relative_path: str, transformations: list[tuple[str, str, str]], *, write: bool) -> bool:
    path = root / relative_path
    original = read_text(path)
    updated = original
    for pattern, replacement, label in transformations:
        updated = replace_once(updated, pattern, replacement, label)
    if updated != original and write:
        write_text(path, updated)
    return updated != original


def bump_version(version: str, component: str) -> str:
    match = SEMVER.fullmatch(version)
    if not match:
        raise RuntimeError(f"Manifest version is not a supported SemVer value: {version}")
    major, minor, patch = (int(value) for value in match.groups())
    if component == "major":
        major, minor, patch = major + 1, 0, 0
    elif component == "minor":
        minor, patch = minor + 1, 0
    else:
        patch += 1
    return f"{major}.{minor}.{patch}"


def read_manifest(root: Path) -> tuple[dict, str]:
    manifest = json.loads(read_text(root / "RandomForeseer.json"))
    version = manifest.get("version")
    game_version = manifest.get("min_game_version")
    dependencies = manifest.get("dependencies") or []
    ritsu_lib_version = next(
        (item.get("min_version") for item in dependencies if item.get("id") == "STS2-RitsuLib"), None
    )
    if not isinstance(version, str) or not SEMVER.fullmatch(version):
        raise RuntimeError("RandomForeseer.json must contain a valid SemVer 'version'.")
    if not isinstance(game_version, str) or not SEMVER.fullmatch(game_version):
        raise RuntimeError("RandomForeseer.json must contain a valid SemVer 'min_game_version'.")
    if not isinstance(ritsu_lib_version, str) or not SEMVER.fullmatch(ritsu_lib_version):
        raise RuntimeError("RandomForeseer.json must declare a valid STS2-RitsuLib min_version.")
    return manifest, ritsu_lib_version


def ensure_clean(root: Path, allow_dirty: bool) -> None:
    if allow_dirty:
        return
    if run_git(root, "diff", "--quiet", check=False).returncode or run_git(
        root, "diff", "--cached", "--quiet", check=False
    ).returncode:
        raise RuntimeError("Working tree and index must be clean; use --allow-dirty to override.")


def update_changelog(root: Path, mod_version: str, *, write: bool) -> bool:
    path = root / "CHANGELOG.md"
    original = read_text(path)
    release_heading = f"## v{mod_version}"
    if re.search(rf"^##\s+{re.escape(release_heading[3:])}\s*$", original, re.MULTILINE):
        return False
    updated = replace_once(original, r"^##\s+Unreleased\s*$", release_heading, "Unreleased changelog heading")
    if write:
        write_text(path, updated)
    return True


def main() -> int:
    args = parse_args()
    root = Path(__file__).resolve().parent.parent
    ensure_clean(root, args.allow_dirty)
    manifest, ritsu_lib_version = read_manifest(root)
    current_version = manifest["version"]
    if args.version:
        target_version = args.version
    elif args.sync:
        target_version = current_version
    else:
        component = "major" if args.major else "minor" if args.minor else "patch"
        target_version = bump_version(current_version, component)

    changed: list[str] = []
    write = not args.dry_run
    if target_version != current_version and update_file(
        root,
        "RandomForeseer.json",
        [(r'("version"\s*:\s*")[^"]+("\s*,)', rf"\g<1>{target_version}\g<2>", "manifest version")],
        write=write,
    ):
        changed.append("RandomForeseer.json")

    transformations = {
        "RandomForeseer.csproj": [
            (
                r'(<PackageReference\s+Include="STS2\.RitsuLib"\s+Version=")[^"]+("\s+GeneratePathProperty=)',
                rf"\g<1>{ritsu_lib_version}\g<2>",
                "csproj RitsuLib dependency",
            )
        ],
        "README.md": [
            (r"(\|\s*当前版本\s*\|\s*`)[^`]+(`\s*\|)", rf"\g<1>{target_version}\g<2>", "Chinese Mod version"),
            (r"(\|\s*最低游戏版本\s*\|\s*`)[^`]+(`\s*\|)", rf"\g<1>{manifest['min_game_version']}\g<2>", "Chinese game version"),
            (r"(\|\s*RitsuLib 依赖\s*\|\s*`)[^`]+(`\s*\|)", rf"\g<1>{ritsu_lib_version}\g<2>", "Chinese RitsuLib version"),
        ],
        "README.en.md": [
            (r"(\|\s*Current version\s*\|\s*`)[^`]+(`\s*\|)", rf"\g<1>{target_version}\g<2>", "English Mod version"),
            (r"(\|\s*Minimum game version\s*\|\s*`)[^`]+(`\s*\|)", rf"\g<1>{manifest['min_game_version']}\g<2>", "English game version"),
            (r"(\|\s*RitsuLib dependency\s*\|\s*`)[^`]+(`\s*\|)", rf"\g<1>{ritsu_lib_version}\g<2>", "English RitsuLib version"),
        ],
        "workshop/description.txt": [
            (
                r"(\[\*]测试版：\[b]v)[^\[]+(\[/b].*?Mod 版本 \[b]v)[^\[]+(\[/b])",
                rf"\g<1>{manifest['min_game_version']}\g<2>{target_version}\g<3>",
                "Chinese public-beta versions",
            )
        ],
        "workshop/description.en.txt": [
            (
                r"(\[\*]public-beta: \[b]v)[^\[]+(\[/b] \(mod version \[b]v)[^\[]+(\[/b])",
                rf"\g<1>{manifest['min_game_version']}\g<2>{target_version}\g<3>",
                "English public-beta versions",
            )
        ],
    }
    for path, rules in transformations.items():
        if update_file(root, path, rules, write=write):
            changed.append(path)

    if not args.sync and update_changelog(root, target_version, write=write):
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
        message = args.message or f"chore(release): prepare v{target_version}"
        run_git(root, "commit", "--only", "-m", message, "--", *changed)
        print(f"Created commit: {message}")
    else:
        print("Staged release metadata changes (pass --commit to commit them).")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, RuntimeError, json.JSONDecodeError) as error:
        print(f"error: {error}", file=sys.stderr)
        raise SystemExit(1)
