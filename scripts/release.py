#!/usr/bin/env python3
"""Build a configured source branch and publish its GitHub release from main."""

import argparse
import hashlib
import json
import zipfile
from datetime import datetime, timezone
from pathlib import Path

from release_tools.common import (
    MOD_ID,
    Branch,
    Manifest,
    Version,
    artifact_child,
    branches,
    build_tree,
    changelog_section,
    choose_branch,
    clean_output,
    command_main,
    ensure_clean,
    git,
    main_root,
    read_json,
    read_text,
    run,
    source_manifest,
    source_sha,
    source_text,
    validate_manifest,
    worktrees,
    write_text,
)


def game_data(root: Path, game_version: str) -> Path:
    result = run(
        "dotnet",
        "msbuild",
        str(root / f"{MOD_ID}.csproj"),
        "-nologo",
        "-getProperty:Sts2Dir,Sts2DataDir,Sts2ApiSignatureRoot",
        cwd=root,
        capture=True,
    )
    properties = json.loads(result.stdout)["Properties"]
    signature_root = properties.get("Sts2ApiSignatureRoot")
    candidates = []
    if signature_root:
        candidates.append(root / signature_root / f"v{game_version}/data_sts2_windows_x86_64")
    install = root / properties["Sts2Dir"]
    release_info = install / "release_info.json"
    if release_info.is_file() and read_json(release_info).get("version", "").removeprefix("v") == game_version:
        candidates.append(root / properties["Sts2DataDir"])
    for candidate in candidates:
        if all((candidate / name).is_file() for name in ("sts2.dll", "0Harmony.dll", "Steamworks.NET.dll")):
            return candidate.resolve()
    raise ValueError(
        f"No assemblies for game v{game_version}. Archive them under Sts2ApiSignatureRoot/v{game_version}/data_sts2_windows_x86_64."
    )


def validate_package(package: Path, manifest: Manifest) -> dict[str, str]:
    packaged = validate_manifest(read_json(package / f"{MOD_ID}.json"))
    for key in ("id", "version", "min_game_version", "dependencies", "has_dll", "has_pck"):
        if packaged.get(key) != manifest.get(key):
            raise ValueError(f"Cached package {key} does not match source manifest: {package}")
    required = ["build-info.txt"]
    if manifest.get("has_dll"):
        required += [f"{MOD_ID}.dll", f"{MOD_ID}.pdb"]
    if manifest.get("has_pck"):
        required += [f"{MOD_ID}.pck"]
    for filename in required:
        if not (package / filename).is_file():
            raise ValueError(f"Missing package file: {package / filename}")
    return dict(line.split(": ", 1) for line in read_text(package / "build-info.txt").splitlines() if ": " in line)


def github_body(
    content: str, branch: Branch, config: dict[str, Branch], manifests: dict[str, Manifest]
) -> tuple[str, bool]:
    latest = Version(manifests[branch.name]["version"]) == max(Version(item["version"]) for item in manifests.values())
    if branch.name == "public" and not latest:
        public = config["public"].game_version
        beta = config["public-beta"].game_version
        content += (
            f"\n\n---\n\n**注意：** 这是正式版（v{public}）Mod 的更新版本，测试版（v{beta}）玩家无需更新。\n"
            f"**Note:** This is an update release for the public branch (v{public}) version of the mod. "
            f"Players on the public-beta branch (v{beta}) do not need to update."
        )
    return content, latest


def package_zip(package: Path, archive: Path) -> None:
    files = {file.name: file for file in package.iterdir() if file.is_file()}
    if archive.exists():
        with zipfile.ZipFile(archive) as existing:
            if set(existing.namelist()) != set(files) or any(
                existing.read(name) != file.read_bytes() for name, file in files.items()
            ):
                raise ValueError(f"Existing release ZIP differs from immutable package: {archive}")
        return
    with zipfile.ZipFile(archive, "w", compression=zipfile.ZIP_DEFLATED) as output:
        for name, file in sorted(files.items()):
            output.write(file, name)


def release(root: Path, args: argparse.Namespace, branch: Branch, config: dict[str, Branch]) -> None:
    manifests = {name: source_manifest(root, value) for name, value in config.items()}
    manifest = manifests[branch.name]
    version = manifest["version"]
    tag = f"v{version}"
    sha = source_sha(root, branch)
    package = artifact_child(root, f"artifacts/packages/{version}/{MOD_ID}")

    if args.skip_build:
        info = validate_package(package, manifest)
        if info.get("git-ref") != branch.git_branch:
            raise ValueError("Cached package came from a different source branch.")
        sha = git(root, "rev-parse", f"{info['git-commit']}^{{commit}}")
        git(root, "merge-base", "--is-ancestor", sha, source_sha(root, branch))
        if json.loads(source_text(root, sha, f"{MOD_ID}.json")) != manifest:
            raise ValueError("Cached package source manifest differs from the configured branch.")

    content = changelog_section(source_text(root, sha, "CHANGELOG.md"), tag)
    body, latest = github_body(content, branch, config, manifests)
    latest = latest and "-" not in version
    print(f"Release {tag}: {branch.name}, {branch.git_branch}@{sha}, game v{branch.game_version}, latest={latest}")
    if args.dry_run:
        print(f"Package: {package}\nGitHub notes:\n{body}")
        return

    ensure_clean(root)
    target = worktrees(root).get(branch.git_branch)
    if target and target != root:
        ensure_clean(target)
    tag_exists = bool(git(root, "tag", "--list", tag))
    if tag_exists and git(root, "rev-parse", f"refs/tags/{tag}^{{commit}}") != sha:
        raise ValueError(f"Existing {tag} points at a different source commit.")

    remote_exists = False
    if not args.package_only:
        remote = git(root, "ls-remote", "--tags", "origin", f"refs/tags/{tag}", f"refs/tags/{tag}^{{}}")
        if remote:
            remote_refs = {line.split()[1]: line.split()[0] for line in remote.splitlines()}
            remote_sha = remote_refs.get(f"refs/tags/{tag}^{{}}", remote_refs[f"refs/tags/{tag}"])
            if remote_sha != sha:
                raise ValueError(f"Remote {tag} points at a different source commit.")
            remote_exists = True

        run("gh", "auth", "status", cwd=root, capture=True)

    if not args.skip_build:
        if package.exists():
            raise ValueError(f"Version package already exists; use --skip-build to reuse it: {package}")
        assemblies = game_data(root, branch.game_version)
        staging = artifact_child(root, f"artifacts/release-staging/{version}/{MOD_ID}")
        clean_output(root, staging)
        with build_tree(root, sha) as tree:
            run(
                "dotnet",
                "publish",
                str(tree / f"{MOD_ID}.csproj"),
                "-c",
                "Release",
                "-o",
                str(staging),
                f"-p:Sts2DataDir={assemblies}",
                "-p:SyncManifestDependenciesOnBuild=false",
                cwd=tree,
            )
            ensure_clean(tree)

        build_info = (
            f"mod-version: {version}\nmin-game-version: {manifest['min_game_version']}\n"
            f"build-game-version: {branch.game_version}\ngit-commit: {sha}\ngit-ref: {branch.git_branch}\n"
            f"built-at: {datetime.now(timezone.utc).isoformat()}\n"
        )
        write_text(staging / "build-info.txt", build_info)
        validate_package(staging, manifest)
        package.parent.mkdir(parents=True, exist_ok=True)
        staging.rename(package)

    archive = root / f"artifacts/{MOD_ID}-{tag}.zip"
    package_zip(package, archive)
    checksum = archive.with_suffix(".zip.sha256")
    write_text(
        checksum,
        f"{hashlib.sha256(archive.read_bytes()).hexdigest()}  {archive.name}\n",
    )
    write_text(root / f"artifacts/{MOD_ID}-{tag}-notes.md", content + "\n")
    github_notes = root / f"artifacts/{MOD_ID}-{tag}-github-notes.md"
    write_text(github_notes, body + "\n")

    if not args.package_only:
        if not tag_exists:
            git(root, "tag", "-a", tag, sha, "-m", f"{MOD_ID} {tag}")
        if not remote_exists:
            git(root, "push", "origin", tag)
        existing = run("gh", "release", "view", tag, "--json", "assets", cwd=root, capture=True, check=False)
        if existing.returncode == 0:
            assets = {item["name"]: item for item in json.loads(existing.stdout)["assets"]}
            for file in (archive, checksum):
                if file.name not in assets or assets[file.name]["size"] != file.stat().st_size:
                    raise ValueError(
                        f"Existing GitHub release has missing or different assets: {tag}; no assets were overwritten."
                    )
            print(f"GitHub release {tag} already exists; no changes made to it.")
        else:
            if "not found" not in (existing.stderr + existing.stdout).lower():
                raise RuntimeError(f"Could not inspect GitHub release: {existing.stderr}")
            arguments = [
                "gh",
                "release",
                "create",
                tag,
                str(archive),
                str(checksum),
                "--verify-tag",
                "--title",
                tag,
                "--notes-file",
                str(github_notes),
                f"--latest={str(latest).lower()}",
            ]
            if "-" in version:
                arguments.append("--prerelease")
            run(*arguments, cwd=root)
    print(f"Release package retained: {package}\nZIP: {archive}")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--branch",
        choices=["public", "public-beta"],
        help="Default: inferred from current Git branch.",
    )
    for flag in ("package-only", "skip-build", "dry-run"):
        parser.add_argument(f"--{flag}", action="store_true")
    args = parser.parse_args()

    root = main_root()
    config = branches(root)
    release(root, args, choose_branch(parser, args.branch, config), config)


if __name__ == "__main__":
    command_main(main)
