# Steam Workshop Release Workflow

This directory contains the tracked source files used to create a Steam Workshop upload workspace. The generated workspace lives under `artifacts/workshop/` and is safe to delete.

## Tracked files

- `image.png`: compressed Steam Workshop preview image used by the uploader. Keep it under 1 MB.
- `description.txt`: Simplified Chinese Workshop page description. `scripts/prepare-workshop.ps1` writes it to `localized.schinese.description`.
- `description.en.txt`: English Workshop page description. `scripts/prepare-workshop.ps1` writes it to `localized.english.description`.
- `workshop.json`: Workshop metadata used as the source template. Root `title`, `description`, and `visibility` are omitted so upload updates do not overwrite the default Steam page fields. `localized` contains tracked titles, localized descriptions are filled from the description files, and `changeNote` is filled from `CHANGELOG.md`.
- `config.json`: tracked example upload configuration.
- `config.local.json`: local override for `config.json`.

## Uploader

Use the local uploader branch specified in `config.local.json`. The upload script accepts either the uploader directory or a direct `ModUploader.exe` path; when given the directory it uses `bin/Release/net8.0/ModUploader.exe`, then falls back to the debug build.

Before the first upload on a machine, copy `config.json` to `config.local.json` and adjust local paths.

## Release order

1. Prepare and commit the normal GitHub release files: `RandomForeseer.json`, `README.md`, `README.en.md`, and `CHANGELOG.md`.
2. Run `scripts/release.ps1 -Version X.Y.Z` to build `artifacts/package/RandomForeseer/`, tag the commit, and create the GitHub Release.
3. Run `scripts/prepare-workshop.ps1 -Version X.Y.Z` to stage `artifacts/workshop/`.
4. Inspect `artifacts/workshop/workshop.json`, `artifacts/workshop/image.png`, and `artifacts/workshop/content/`.
5. Set proxy environment variables in the same terminal if the local network needs them for Steam Workshop upload.
6. Run `scripts/upload-workshop.ps1 -Version X.Y.Z` to upload.
7. Confirm the localized Steam Workshop title and descriptions after upload.

For the first upload, keep the Steam page private. After Steam returns a Workshop item id, store it in `config.local.json` or pass it explicitly with `-WorkshopItemId`.
