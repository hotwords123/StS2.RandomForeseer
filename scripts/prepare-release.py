#!/usr/bin/env python3
"""Prepare source-branch versions and synchronize Workshop descriptions on main."""

import argparse
import re
from pathlib import Path

from release_tools.common import (
    MOD_ID,
    Branch,
    Version,
    branches,
    choose_branch,
    command_main,
    editable_tree,
    ensure_clean,
    git,
    main_root,
    read_text,
    source_manifest,
    source_sha,
    source_text,
    worktrees,
    write_text,
)


def replace_once(content: str, pattern: str, replacement: str, description: str) -> str:
    updated, count = re.subn(pattern, replacement, content, count=1, flags=re.M)
    if count != 1:
        raise ValueError(f"Expected one {description} in release metadata.")
    return updated


def prepare(root: Path, args: argparse.Namespace, branch: Branch, config: dict[str, Branch]) -> None:
    trees = worktrees(root)
    target_sha = source_sha(root, branch)

    def source_file(name: str) -> str:
        if branch.git_branch in trees:
            return read_text(trees[branch.git_branch] / name)
        else:
            return source_text(root, target_sha, name)

    manifest = source_manifest(root, branch)
    current = manifest["version"]
    if args.sync:
        version = current
    else:
        components = [int(part) for part in current.split("-")[0].split(".")]
        component = 0 if args.major else 1 if args.minor else 2
        components[component] += 1
        components[component + 1 :] = [0] * (2 - component)
        version = ".".join(map(str, components))

    Version(version)
    manifest["version"] = version
    ritsu = next(item["min_version"] for item in manifest["dependencies"] if item["id"] == "STS2-RitsuLib")

    # Compute every edit before writing or creating a worktree.
    edits: dict[str, str] = {}
    edits[f"{MOD_ID}.json"] = replace_once(
        source_file(f"{MOD_ID}.json"),
        r'("version"\s*:\s*")[^"]+("\s*,)',
        rf"\g<1>{version}\g<2>",
        "manifest version",
    )
    edits[f"{MOD_ID}.csproj"] = replace_once(
        source_file(f"{MOD_ID}.csproj"),
        r'(<PackageReference\s+Include="STS2\.RitsuLib"\s+Version=")[^"]+("\s+GeneratePathProperty=)',
        rf"\g<1>{ritsu}\g<2>",
        "declared RitsuLib dependency",
    )

    for filename, labels in {
        "README.md": ("当前版本", "最低游戏版本", "RitsuLib 依赖"),
        "README.en.md": ("Current version", "Minimum game version", "RitsuLib dependency"),
    }.items():
        content = source_file(filename)
        for label, value in zip(labels, (version, manifest["min_game_version"], ritsu)):
            content = replace_once(content, rf"(\|\s*{label}\s*\|\s*`)[^`]+(`\s*\|)", rf"\g<1>{value}\g<2>", label)
        edits[filename] = content

    if not args.sync:
        content = source_file("CHANGELOG.md")
        if not re.search(rf"^## v{re.escape(version)}\s*$", content, re.M):
            content = replace_once(content, r"^##[ \t]+Unreleased[ \t]*$", f"## v{version}", "Unreleased heading")
        edits["CHANGELOG.md"] = content

    versions = {name: source_manifest(root, value)["version"] for name, value in config.items()}
    versions[branch.name] = version
    workshop_edits: dict[str, str] = {}
    for filename, labels in {
        "workshop/description.txt": {"public": "正式版", "public-beta": "测试版"},
        "workshop/description.en.txt": {"public": "public", "public-beta": "public-beta"},
    }.items():
        content = read_text(root / filename)
        for name, label in labels.items():
            pattern = rf"(\[\*\]{label}[^\n]*?\[b\]v)[^\[]+(\[/b\][^\n]*?\[b\]v)[^\[]+(\[/b\])"
            content = replace_once(
                content, pattern, rf"\g<1>{config[name].game_version}\g<2>{versions[name]}\g<3>", label
            )
        workshop_edits[filename] = content

    edits = {name: value for name, value in edits.items() if value != source_file(name)}
    workshop_edits = {name: value for name, value in workshop_edits.items() if value != read_text(root / name)}
    print(f"Prepare {branch.name}: {branch.git_branch} {current} -> {version}")
    print(f"Source edits: {', '.join(edits) or 'none'}")
    print(f"Main edits: {', '.join(workshop_edits) or 'none'}")
    if args.dry_run:
        return

    existing_target = trees.get(branch.git_branch)
    ensure_clean(root)
    if existing_target and existing_target != root:
        ensure_clean(existing_target)
    if source_sha(root, branch) != target_sha:
        raise RuntimeError("Source branch changed during release preparation; retry.")

    groups: dict[Path, dict[str, str]] = {}
    if edits:
        target = editable_tree(root, branch.git_branch)
        groups[target] = dict(edits)
    if workshop_edits:
        groups.setdefault(root, {}).update(workshop_edits)

    for tree, changes in groups.items():
        for name, content in changes.items():
            write_text(tree / name, content)
        git(tree, "add", "--", *changes)
        if args.commit:
            message = f"chore(release): prepare v{version}"
            git(tree, "commit", "--only", "-m", message, "--", *changes)
        print(f"{'Committed' if args.commit else 'Staged'} in {tree}: {', '.join(changes)}")
    if not groups:
        print("No release metadata changes needed.")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--branch",
        choices=["public", "public-beta"],
        help="Default: inferred from current Git branch.",
    )
    version = parser.add_mutually_exclusive_group(required=True)
    for flag in ("major", "minor", "patch", "sync"):
        version.add_argument(f"--{flag}", action="store_true")
    parser.add_argument("--commit", action="store_true")
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    root = main_root()
    config = branches(root)
    prepare(root, args, choose_branch(parser, args.branch, config), config)


if __name__ == "__main__":
    command_main(main)
