# Steam Workshop Release Workflow

This directory contains the tracked source files used to create a Steam Workshop upload workspace. The generated workspace lives under `artifacts/workshop/` and is safe to delete.

## Tracked files

- `image.original.png`: original Steam Workshop preview image source.
- `image.png`: compressed Steam Workshop preview image used by the uploader. Keep it under 1 MB.
- `description.txt`: Simplified Chinese Workshop page description source for manual editing on Steam.
- `description.en.txt`: English Workshop page description source for manual editing on Steam.
- `workshop.json`: Workshop metadata used as the source template. `title`, `description`, and `visibility` are `null` so upload updates do not overwrite Steam page edits. `changeNote` is filled from `CHANGELOG.md` by `scripts/prepare-workshop.ps1`.
- `config.json`: non-secret upload configuration. `itemId` can be committed after the first private upload creates a Workshop item.
- `config.local.json`: optional ignored local override for machine-specific paths or an uncommitted item id.

## Release order

1. Prepare and commit the normal GitHub release files: `RandomForeseer.json`, `README.md`, `README.en.md`, and `CHANGELOG.md`.
2. Run `scripts/release.ps1 -Version X.Y.Z` to build `artifacts/package/RandomForeseer/`, tag the commit, and create the GitHub Release.
3. Run `scripts/prepare-workshop.ps1 -Version X.Y.Z` to stage `artifacts/workshop/`.
4. Inspect `artifacts/workshop/workshop.json`, `artifacts/workshop/image.png`, and `artifacts/workshop/content/`.
5. Set proxy environment variables in the same terminal if the local network needs them for Steam Workshop upload.
6. Run `scripts/upload-workshop.ps1 -Version X.Y.Z` to upload.
7. Edit the Steam Workshop page title and descriptions manually using `description.txt` and `description.en.txt`.

For the first upload, keep the Steam page private. After Steam returns a Workshop item id, store it in `config.json` or pass it explicitly with `-WorkshopItemId`.
