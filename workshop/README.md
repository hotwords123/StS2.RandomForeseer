# Steam Workshop Release Workflow

This directory contains the tracked inputs for the Steam Workshop variant package. Generated packages and upload workspaces remain under the ignored `artifacts/` directory.

## Tracked files

- `active-game-versions.txt`: one currently active public/public-beta game version per line. Empty lines and lines beginning with `#` are ignored.
- `image.png`: Workshop preview image; it must remain smaller than 1 MB.
- `description.txt` and `description.en.txt`: tracked Simplified Chinese and English descriptions.
- `workshop.json`: Workshop metadata. Branch limits are intentionally omitted because runtime version dispatch replaces Steam branch filtering.
- `config.json`: tracked uploader example configuration; `config.local.json` contains ignored machine-local overrides.
- `loader/`: Workshop-only dispatcher source and its third-party notices.

The long-term design and selection rules are documented in [`docs/workshop-variant-distribution.md`](../docs/workshop-variant-distribution.md).

## Versioned package cache

`scripts/release.ps1 -Version X.Y.Z` retains the standard package at:

```text
artifacts/packages/X.Y.Z/RandomForeseer/
├─ RandomForeseer.json
├─ RandomForeseer.dll
├─ RandomForeseer.pck
└─ build-info.txt
```

The GitHub Release ZIP still contains only that version. A published SemVer package is treated as immutable: rerun release with `-SkipBuild` to reuse it. Build every compatible branch version that the active game versions require before preparing the Workshop upload.

## Preparing the Workshop package

`scripts/prepare-workshop.ps1` performs these steps:

1. Reads all packages at or below the requested Mod version from `artifacts/packages/`.
2. For each version in `active-game-versions.txt`, selects the highest Mod SemVer whose `min_game_version` does not exceed that game version.
3. Deduplicates packages selected by more than one active game version.
4. Builds the root `RandomForeseer.Loader` dispatcher.
5. Stages the selected DLL/PCK pairs under `artifacts/workshop/content/lib/<mod-version>/` and writes their version and dependency metadata to a `.manifest` file.
6. Creates a Workshop-only root Mod manifest with `has_pck: false` and the dependencies of the lowest bundled Mod version; the dispatcher mounts the selected PCK and enforces its own dependency minimums.

At runtime, the dispatcher uses the same compatibility test against the packages present in the upload and loads the highest compatible Mod SemVer. Unknown or older host versions fall back to the newest bundled Mod version with a warning, matching the best-effort delivery policy.

Variant directories must not contain their original `RandomForeseer.json`: the game recursively treats every `.json` below a Workshop item as a Mod manifest.

All historical package manifests must use `min_version` for dependency minimums. The legacy `version` field is rejected. Bundled variants are expected to keep the same dependency ID set and vary only their minimum versions.

The dispatcher is always compiled against the oldest supported game API configured by `Sts2ApiSignatureRoot`, rather than whichever game branch is currently installed.

## Release order

1. Update and commit the normal release files.
2. On every compatibility source branch needed by an active game version, run `scripts/release.ps1 -Version X.Y.Z -PackageOnly` once to retain its package without creating a tag or GitHub Release. Run the normal command without `-PackageOnly` for the current GitHub release.
3. Update `active-game-versions.txt` to the game versions currently live on public/public-beta.
4. Run `scripts/prepare-workshop.ps1 -Version X.Y.Z`.
5. Inspect `artifacts/workshop/workshop.json` and `artifacts/workshop/content/`.
6. Run `scripts/upload-workshop.ps1 -Version X.Y.Z`.
7. Confirm localized descriptions and that Steam no longer reports a stale branch restriction.

The uploader path and Workshop item ID can be stored in `config.local.json` or passed directly to the upload script.
