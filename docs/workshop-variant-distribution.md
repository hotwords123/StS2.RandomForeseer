# Steam Workshop variant distribution

Random Foreseer publishes two package forms:

- GitHub Releases contain the normal manifest, one `RandomForeseer.dll`, and one `RandomForeseer.pck`.
- Steam Workshop uploads contain a small root dispatcher plus the minimum historical package set needed by the currently active game versions.

This keeps version dispatch out of `RandomForeseerCode` and preserves the normal local/GitHub loading path.

## Source of truth

Each standard package is identified by its Mod SemVer and retained at `artifacts/packages/<mod-version>/RandomForeseer/`. Its `RandomForeseer.json` supplies both the Mod version and `min_game_version`; no separate branch-to-package mapping is maintained.

`workshop/active-game-versions.txt` is the only additional compatibility input. It lists the exact game versions currently active on the public-facing Steam branches. The list controls package inclusion, not runtime rejection.

For every active game version, the prepare script selects the highest Mod SemVer satisfying:

```text
package.min_game_version <= active_game_version
```

The union of those selections is uploaded. This omits historical packages that no active game version needs. If a newer Mod version retains an older `min_game_version`, it naturally replaces older packages across multiple game versions.

## Runtime selection

The generated `random-foreseer-variants.manifest` records each bundled package's Mod version, minimum game version, and relative directory.

The dispatcher resolves the host version through `ReleaseInfoManager`, then selects the highest bundled Mod SemVer satisfying the same minimum-game-version test. If the host version is unknown or older than every bundled minimum, it logs a warning and loads the highest bundled Mod SemVer as a best-effort fallback.

The dispatcher loads the selected business DLL into its own `AssemblyLoadContext`, associates it with the Mod through `ModManager.AssociateAssemblyWithMod` when that API exists, mounts the selected PCK, and forwards all discovered `ModInitializer` methods. The business assembly keeps the `RandomForeseer` identity; the root file is named `RandomForeseer.dll` as required by the game but has the distinct assembly identity `RandomForeseer.Loader`.

The loader project is compiled against the oldest supported game API under `Sts2ApiSignatureRoot`, not the currently installed game branch. This keeps the root dispatcher loadable by every active branch while the selected business assembly remains version-specific.

The 0.107 `ReflectionHelper.ModTypes` bridge is intentionally not patched. Random Foreseer currently registers Harmony patches, RitsuLib discovery, Godot scripts, settings, and localization explicitly against the selected business assembly and does not add game-discovered `AbstractModel` implementations. Add a bridge only if a future feature requires global Mod type discovery on an older game API.

## PCK handling

The Workshop-only root manifest declares `has_pck: false`, so the game does not look for a root `RandomForeseer.pck`. The dispatcher mounts the PCK paired with the selected DLL before invoking the real initializer. This preserves version-specific scenes and localization while keeping the ordinary GitHub package unchanged.

## Recursive manifest constraint

The game recursively interprets every `.json` below a Mod directory as a Mod manifest. Consequently, historical `RandomForeseer.json` files remain only in `artifacts/packages/` and are never copied into Workshop `content/lib/`. The generated variant metadata deliberately uses a `.manifest` extension despite containing JSON.

## Provenance and third-party code

Every standard package contains `build-info.txt` with its source commit/ref and build metadata.

Use `scripts/release.ps1 -Version X.Y.Z -PackageOnly` on a compatibility branch to retain its standard package without creating or checking a Git tag and without uploading a GitHub Release. The normal release invocation uses the same versioned package as its single-version ZIP input.

The dispatcher adapts the multi-variant loader design and implementation patterns from [STS2-RitsuLib](https://github.com/BAKAOLC/STS2-RitsuLib). Source attribution and the upstream MIT license are stored in `workshop/loader/THIRD_PARTY_NOTICES.md` and copied into each Workshop upload.
