from __future__ import annotations

import argparse
import json
import re
import shutil
import subprocess
import sys
import uuid
from collections.abc import Callable, Generator
from contextlib import contextmanager
from dataclasses import dataclass
from functools import total_ordering
from pathlib import Path
from typing import Any, NotRequired, TypedDict

ROOT = Path(__file__).resolve().parents[2]
MOD_ID = "RandomForeseer"
SEMVER = re.compile(r"(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-([0-9A-Za-z.-]+))?")


class Dependency(TypedDict):
    id: str
    min_version: NotRequired[str]


class Manifest(TypedDict):
    id: str
    version: str
    min_game_version: str
    dependencies: list[Dependency]
    has_dll: NotRequired[bool]
    has_pck: NotRequired[bool]


@total_ordering
class Version:
    def __init__(self, value: str) -> None:
        match = SEMVER.fullmatch(value)
        if not match:
            raise ValueError(f"Invalid semantic version: {value}")
        self.value = value
        major, minor, patch, pre = match.groups()
        identifiers = pre.split(".") if pre else []
        if any(not item or (item.isdigit() and len(item) > 1 and item[0] == "0") for item in identifiers):
            raise ValueError(f"Invalid prerelease version: {value}")
        # The leading identifier tag keeps numeric and textual prerelease values separate.
        self.key: tuple[int, int, int, bool, tuple[tuple[int, Any], ...]] = (
            int(major),
            int(minor),
            int(patch),
            not pre,
            tuple((0, int(item)) if item.isdigit() else (1, item) for item in identifiers),
        )

    def __eq__(self, other: object) -> bool:
        if not isinstance(other, Version):
            return NotImplemented
        return self.key == other.key

    def __lt__(self, other: object) -> bool:
        if not isinstance(other, Version):
            return NotImplemented
        return self.key < other.key


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig")


def write_text(path: Path, content: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content.replace("\r\n", "\n"), encoding="utf-8", newline="\n")


def read_json(path: Path) -> Any:
    return json.loads(read_text(path))


def write_json(path: Path, value: object) -> None:
    write_text(path, json.dumps(value, ensure_ascii=False, indent=2) + "\n")


def run(
    *arguments: str, cwd: Path = ROOT, capture: bool = False, check: bool = True
) -> subprocess.CompletedProcess[str]:
    result = subprocess.run(
        [str(arg) for arg in arguments],
        cwd=cwd,
        capture_output=capture,
        text=True,
        encoding="utf-8",
        errors="replace",
    )
    if check and result.returncode:
        details = (result.stderr or result.stdout or "").strip() if capture else ""
        raise RuntimeError(f"Command failed ({result.returncode}): {' '.join(map(str, arguments))}\n{details}")
    return result


def git(root: Path, *arguments: str, check: bool = True) -> str:
    return run("git", *arguments, cwd=root, capture=True, check=check).stdout.strip()


def worktrees(root: Path) -> dict[str, Path]:
    result: dict[str, Path] = {}
    for entry in git(root, "worktree", "list", "--porcelain").split("\n\n"):
        values = dict(line.split(" ", 1) for line in entry.splitlines() if " " in line)
        if values.get("branch", "").startswith("refs/heads/"):
            result[values["branch"][11:]] = Path(values["worktree"])
    return result


def main_root() -> Path:
    root = worktrees(ROOT).get("main")
    if root is None:
        raise RuntimeError("Keep main checked out in a worktree and run its Python release tools.")
    return root


@dataclass(frozen=True)
class Branch:
    name: str
    git_branch: str
    game_version: str


def branches(root: Path) -> dict[str, Branch]:
    config = read_json(root / "workshop/branches.json")
    if set(config) != {"public", "public-beta"}:
        raise ValueError("workshop/branches.json must define public and public-beta.")

    result: dict[str, Branch] = {}
    for name, entry in config.items():
        Version(entry["game_version"])
        git(root, "check-ref-format", "--branch", entry["git_branch"])
        result[name] = Branch(name, entry["git_branch"], entry["game_version"])
    if len({branch.git_branch for branch in result.values()}) != len(result):
        raise ValueError("Each game branch must map to a distinct Git branch.")
    return result


def choose_branch(parser: argparse.ArgumentParser, value: str | None, config: dict[str, Branch]) -> Branch:
    if value:
        return config[value]
    current = run("git", "branch", "--show-current", cwd=Path.cwd(), capture=True, check=False)
    matches = [branch for branch in config.values() if branch.git_branch == current.stdout.strip()]
    if len(matches) != 1:
        parser.error("Cannot infer the game branch from the current Git branch; specify --branch.")
    return matches[0]


def source_sha(root: Path, branch: Branch) -> str:
    return git(root, "rev-parse", f"refs/heads/{branch.git_branch}^{{commit}}")


def source_text(root: Path, sha: str, path: str) -> str:
    return git(root, "show", f"{sha}:{path}") + "\n"


def validate_manifest(manifest: Manifest) -> Manifest:
    if manifest.get("id") != MOD_ID:
        raise ValueError(f"Expected manifest id {MOD_ID}.")
    Version(manifest["version"])
    Version(manifest["min_game_version"])
    for dependency in manifest.get("dependencies", []):
        if not dependency.get("id") or "version" in dependency:
            raise ValueError("Dependencies require id and the min_version field, not legacy version.")
        if dependency.get("min_version"):
            Version(dependency["min_version"])
    return manifest


def source_manifest(root: Path, branch: Branch) -> Manifest:
    return validate_manifest(json.loads(source_text(root, source_sha(root, branch), f"{MOD_ID}.json")))


def ensure_clean(root: Path) -> None:
    if git(root, "status", "--porcelain"):
        raise RuntimeError(f"Working tree is not clean: {root}")


def editable_tree(root: Path, branch_name: str) -> Path:
    existing = worktrees(root).get(branch_name)
    if existing:
        return existing

    parent = root / "temp/release-worktrees"
    write_text(
        parent / "PURPOSE.txt",
        "Persistent source worktrees for release preparation. Review and commit changes before removing.\n",
    )
    write_text(parent / ".gdignore", "")
    target = parent / branch_name.replace("/", "-")
    if target.exists():
        raise RuntimeError(f"Unregistered release worktree path already exists: {target}")
    git(root, "worktree", "add", str(target), branch_name)
    return target


@contextmanager
def build_tree(root: Path, sha: str) -> Generator[Path, None, None]:
    parent = root / "temp/release-builds"
    write_text(parent / ".gdignore", "")
    target = parent / uuid.uuid4().hex
    git(root, "worktree", "add", "--detach", str(target), sha)
    try:
        if (root / "local.props").exists():
            shutil.copy2(root / "local.props", target / "local.props")
        yield target
    finally:
        if not target.resolve().is_relative_to(parent.resolve()):
            raise RuntimeError("Refusing to remove a worktree outside the release build directory.")
        git(root, "worktree", "remove", "--force", str(target))


def artifact_child(root: Path, value: str | Path) -> Path:
    path = (root / value).resolve()
    artifacts = (root / "artifacts").resolve()
    if path == artifacts or not path.is_relative_to(artifacts):
        raise ValueError(f"Generated output must be below artifacts/: {path}")
    return path


def clean_output(root: Path, path: Path) -> None:
    artifact_child(root, path)
    if path.exists():
        shutil.rmtree(path)
    path.mkdir(parents=True)


def changelog_section(content: str, tag: str) -> str:
    match = re.search(rf"^##\s+{re.escape(tag)}\s*\n(.*?)(?=^##\s+|\Z)", content, re.M | re.S)
    if not match or not match[1].strip():
        raise ValueError(f"Missing or empty CHANGELOG section: {tag}")
    return re.sub(r"<br\s*/?>\r?\n([ \t]+)", r"\n\1", match[1].strip(), flags=re.I)


def notes(root: Path, version: str) -> str:
    path = root / f"artifacts/{MOD_ID}-v{version}-notes.md"
    if not path.is_file() or not (content := read_text(path).strip()):
        raise ValueError(f"Missing or empty release notes: {path}. Run release.py first.")
    return content


def command_main(action: Callable[[], None]) -> None:
    try:
        action()
    except (OSError, ValueError, RuntimeError, KeyError) as error:
        print(f"error: {error}", file=sys.stderr)
        raise SystemExit(1)
