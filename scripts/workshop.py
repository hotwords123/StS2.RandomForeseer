#!/usr/bin/env python3
"""Prepare and upload the multi-version Workshop package using main's tooling."""

import argparse
import shutil
from copy import deepcopy
from pathlib import Path

from release_tools.common import (
    MOD_ID,
    Branch,
    Manifest,
    Version,
    artifact_child,
    branches,
    clean_output,
    command_main,
    main_root,
    notes,
    read_json,
    read_text,
    run,
    source_manifest,
    write_json,
)

Package = tuple[Manifest, Path]


def select_packages(
    root: Path, config: dict[str, Branch], packages_root: Path
) -> tuple[list[Package], dict[str, str], dict[str, Manifest]]:
    manifests = {name: source_manifest(root, branch) for name, branch in config.items()}
    maximum = max(Version(manifest["version"]) for manifest in manifests.values())
    packages: list[Package] = []
    for directory in packages_root.iterdir():
        if not directory.is_dir() or Version(directory.name) > maximum:
            continue
        package = directory / MOD_ID
        manifest = read_json(package / f"{MOD_ID}.json")
        packages.append((manifest, package))

    selected: dict[str, Package] = {}
    mapping: dict[str, str] = {}
    for name, branch in config.items():
        compatible = [item for item in packages if Version(item[0]["min_game_version"]) <= Version(branch.game_version)]
        if not compatible:
            raise ValueError(f"No package supports {name} game v{branch.game_version}.")
        manifest, package = max(compatible, key=lambda item: Version(item[0]["version"]))
        selected[manifest["version"]] = (manifest, package)
        mapping[name] = manifest["version"]
        print(f"{name} game {branch.game_version} selects Mod {manifest['version']}")
    return (
        sorted(selected.values(), key=lambda item: Version(item[0]["version"])),
        mapping,
        manifests,
    )


def change_note(root: Path, branch_name: str, manifests: dict[str, Manifest], mapping: dict[str, str]) -> str:
    targets = [branch_name] if branch_name != "both" else ["public", "public-beta"]
    sections = []
    for name in targets:
        version = manifests[name]["version"]
        if mapping[name] != version:
            raise ValueError(
                f"{name} expects Mod {version}, but its selected package is {mapping[name]}; build the prepared release first."
            )
        heading = f"## {name} — v{version}" if branch_name == "both" else f"## v{version}"
        sections.append(f"{heading}\n\n{notes(root, version)}")
    return "\n\n".join(sections)


def prepare(
    root: Path, args: argparse.Namespace, config: dict[str, Branch], workspace: Path, packages_root: Path
) -> None:
    selected, mapping, manifests = select_packages(root, config, packages_root)
    note = change_note(root, args.branch, manifests, mapping)
    source = root / "workshop"
    metadata = read_json(source / "workshop.json")
    metadata["changeNote"] = note

    for language, filename in (
        ("english", "description.en.txt"),
        ("schinese", "description.txt"),
    ):
        metadata.setdefault("localized", {}).setdefault(language, {})["description"] = read_text(
            source / filename
        ).rstrip("\r\n")
    image = source / "image.png"
    print(f"Change notes: {args.branch}; versions: {', '.join(item[0]['version'] for item in selected)}")
    if args.dry_run:
        print(f"Workspace: {workspace}\n{note}")
        return

    loader_dir = artifact_child(root, "artifacts/workshop-loader-build")
    clean_output(root, loader_dir)
    run(
        "dotnet",
        "build",
        str(root / "workshop/loader/RandomForeseer.WorkshopLoader.csproj"),
        "-c",
        "Release",
        "-o",
        str(loader_dir),
        cwd=root,
    )

    # Build first so a build failure does not destroy an already reviewed upload workspace.
    clean_output(root, workspace)
    content = workspace / "content"
    content.mkdir()
    shutil.copy2(loader_dir / "RandomForeseer.Loader.dll", content / f"{MOD_ID}.dll")
    shutil.copy2(loader_dir / "RandomForeseer.Loader.pdb", content / "RandomForeseer.Loader.pdb")
    shutil.copy2(
        root / "workshop/loader/THIRD_PARTY_NOTICES.md",
        content / "THIRD_PARTY_NOTICES.md",
    )
    composite = deepcopy(selected[-1][0])
    composite.update(
        has_pck=False,
        has_dll=True,
        min_game_version=min((item[0]["min_game_version"] for item in selected), key=Version),
        dependencies=deepcopy(selected[0][0].get("dependencies", [])),
    )
    write_json(content / f"{MOD_ID}.json", composite)

    variants = []
    for manifest, package in selected:
        relative = f"lib/{manifest['version']}"
        destination = content / relative
        destination.mkdir(parents=True)
        for filename in (
            f"{MOD_ID}.dll",
            f"{MOD_ID}.pdb",
            f"{MOD_ID}.pck",
            "build-info.txt",
        ):
            if (package / filename).is_file():
                shutil.copy2(package / filename, destination / filename)
        variants.append(
            dict(
                modVersion=manifest["version"],
                minGameVersion=manifest["min_game_version"],
                directory=relative,
                dependencies=manifest.get("dependencies", []),
            )
        )

    write_json(content / "mod-variants.manifest", dict(schema=1, variants=variants))
    write_json(workspace / "workshop.json", metadata)
    shutil.copy2(image, workspace / "image.png")
    print(f"Workshop workspace prepared: {workspace}")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--branch", required=True, choices=["public", "public-beta", "both"])
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--prepare-only", action="store_true")
    mode.add_argument("--skip-prepare", action="store_true")
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    root = main_root()
    config = branches(root)
    local = read_json(root / "workshop/config.json")
    if (root / "workshop/config.local.json").is_file():
        local.update(read_json(root / "workshop/config.local.json"))

    workspace = artifact_child(root, local["workspaceDir"])
    packages_root = (root / local["packagesRoot"]).resolve()

    if not args.skip_prepare:
        prepare(root, args, config, workspace, packages_root)
    else:
        manifests = {name: source_manifest(root, branch) for name, branch in config.items()}
        variants = read_json(workspace / "content/mod-variants.manifest")["variants"]
        mapping = {
            name: max(
                (item for item in variants if Version(item["minGameVersion"]) <= Version(branch.game_version)),
                key=lambda item: Version(item["modVersion"]),
            )["modVersion"]
            for name, branch in config.items()
        }
        expected = change_note(root, args.branch, manifests, mapping)
        if read_json(workspace / "workshop.json")["changeNote"] != expected:
            raise ValueError("Prepared notes do not match --branch; prepare and review the workspace again.")
        print(f"Using reviewed workspace: {workspace}")

    if args.prepare_only or args.dry_run:
        return

    arguments = [str(root / local["uploaderPath"]), "upload", "-w", str(workspace), "-i", str(local["itemId"])]
    run(*arguments, cwd=root)


if __name__ == "__main__":
    command_main(main)
