# Local release and Steam Workshop workflow

Release tooling and Workshop metadata are maintained on `main`. Each source branch owns its
manifest, CHANGELOG, and README version information. GitHub distributes individual version packages;
Workshop distributes the packages needed by both active game branches.

## Configuration

`workshop/branches.json` defines `public` and `public-beta`:

- `git_branch`: the local source branch for that game branch.
- `game_version`: the active game version and the API version used for its build.

The corresponding `RandomForeseer.json` supplies the Mod version, minimum supported game version,
and dependency requirements. The minimum supported game version may differ from the build API version.
Update this configuration when the active game branches change.

Configure local game paths and Godot in `local.props`. Archived game assemblies belong under
`Sts2ApiSignatureRoot/vX.Y.Z/data_sts2_windows_x86_64`. Builds require assemblies matching the configured
game version. GitHub publishing requires an authenticated `gh`; Workshop publishing requires Steam
to be online and the official uploader to be available.

Workshop titles, descriptions, and image are under `workshop/`. Local settings belong in the ignored
`workshop/config.local.json`, overriding the example `config.json`:

- `uploaderPath`: uploader executable.
- `itemId`: existing Workshop item ID.
- `workspaceDir`: prepared upload directory below `artifacts/`.
- `packagesRoot`: retained version packages.

Relative paths are resolved from main.

## Prepare a release

Keep main checked out and use its tools. Preparation and release infer `--branch` from the current
Git branch when it matches the configuration; otherwise specify `public` or `public-beta` explicitly.
Workshop always requires an explicit branch.

```powershell
uv run scripts/prepare-release.py --branch public --patch --dry-run
uv run scripts/prepare-release.py --branch public --patch --commit
```

Use `--major` or `--minor` for the corresponding version increment. `--sync` updates metadata for the
existing version. Preparation updates the source branch's manifest, dependency reference, README
version tables, and CHANGELOG heading. Dependency versions follow that branch's manifest.
The Chinese and English Workshop descriptions on main are synchronized with both branches.

Without `--commit`, review and commit the staged edits in each reported worktree. With `--commit`,
source metadata is committed on its source branch and Workshop descriptions on main. Main releases
combine those changes in one commit. Start with clean worktrees and finish one preparation before another.

## Publish a GitHub Release

```powershell
uv run scripts/release.py --branch public --dry-run
uv run scripts/release.py --branch public
```

The version comes from the source manifest. Release builds use the configured source branch and game API.
The tag identifies the source commit used for the package.

Standard packages are retained at `artifacts/packages/X.Y.Z/RandomForeseer/`, containing the manifest,
DLL, PDB, PCK, and `build-info.txt`. The release also produces these files under `artifacts/`:

- `RandomForeseer-vX.Y.Z.zip` and its `.sha256` checksum.
- `RandomForeseer-vX.Y.Z-notes.md`: release changes for Workshop.
- `RandomForeseer-vX.Y.Z-github-notes.md`: GitHub Release text.

Lower-version public releases include a bilingual notice identifying the public and public-beta game
versions and are not marked as GitHub's latest release. A version with a prerelease suffix is published
as a prerelease.

Use `--package-only` to build and retain local release artifacts. Use `--skip-build` to publish an
existing package. Version packages are immutable; an existing published version is reused rather than rebuilt.

## Publish a Workshop update

```powershell
uv run scripts/workshop.py --branch public --prepare-only
# Review artifacts/workshop/workshop.json and content/.
uv run scripts/workshop.py --branch public --skip-prepare
```

Omit `--prepare-only` to prepare and upload together. `--dry-run` previews the package selections and notes.
`--skip-prepare` uploads the reviewed workspace; its notes must match the explicit branch.

Workshop includes the highest compatible retained package for each active game version.
`--branch` selects the update notes, while both game branches participate in package selection.
Notes come from the corresponding release artifacts and contain the release changes without the
GitHub-only branch notice. Keep the relevant packages and notes available before preparing an upload.

For a feature released on both branches:

1. Commit the implementation on main and backport it to the compatibility source branch.
2. Prepare and publish each branch's release.
3. Prepare Workshop with `--branch both --prepare-only` and review both versions' notes.
4. Upload once with `--branch both --skip-prepare`.

See [Workshop variant distribution](../docs/workshop-variant-distribution.md) for package structure
and runtime version selection.
