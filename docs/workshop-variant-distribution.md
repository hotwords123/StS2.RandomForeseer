# Steam Workshop variant distribution

## Release framework

Random Foreseer distributes two package forms:

- GitHub Releases provide a single version's manifest, DLL, PDB, and PCK.
- Workshop provides a root dispatcher and the version packages needed by active game branches.

Release tooling and Workshop metadata are maintained on main. Each source branch maintains its own
manifest, CHANGELOG, and README version information. Commands and release steps are documented in
[the release workflow](../workshop/README.md).

## Version sources and package selection

`workshop/branches.json` maps `public` and `public-beta` to a `git_branch` and active `game_version`.
Source manifests define Mod versions, minimum supported game versions, and dependencies.
Standard packages are retained at `artifacts/packages/<mod-version>/RandomForeseer/`; the manifest
inside each package is authoritative for that package's compatibility.

For each active game version, Workshop selects the highest retained Mod version satisfying:

```text
package.min_game_version <= active_game_version
```

Selection is bounded by the highest configured source Mod version. The union of the selections is
included in the upload. One package may serve both game branches.

## Workshop package structure

The root contains the dispatcher, root Mod manifest, and `mod-variants.manifest`. Selected version
packages are stored under `content/lib/<mod-version>/`, with their DLL/PCK pair and available PDB
and build information. The variant manifest records each package's version, minimum game version,
directory, and dependencies.

The root manifest uses the lowest selected minimum game version and the dependencies of the lowest
selected Mod version. All maintained variants use the same dependency IDs; their minimum dependency
versions may differ. Dependency minimums use the `min_version` field.

The game recursively treats JSON files under a Mod directory as possible Mod manifests. The Workshop
content therefore contains only the root `RandomForeseer.json`; individual package manifests stay in
the retained standard packages. Variant metadata uses the `.manifest` extension.

## Runtime behavior

The dispatcher selects the highest bundled Mod version compatible with the host game version.
Unknown host versions, or hosts older than all bundled minimums, use the newest bundled package as
a best-effort fallback with a warning.

The selected package's dependency requirements are enforced before initialization. A missing or
outdated dependency prevents that package from loading and is reported through the game's Mod status.
The Mod list, settings, telemetry, and multiplayer metadata use the selected Mod version.

Each selected DLL is paired with its own PCK, preserving its version-specific resources and localization.
The root manifest declares `has_pck: false`; the dispatcher mounts the selected package's PCK.
The dispatcher targets the oldest supported game API so the Workshop entry point is compatible
with every active game branch.

## Provenance

`build-info.txt` identifies each standard package's source commit, Git branch, and build game version.
Release tags identify the source commit used for the package. Published version packages are immutable.

The dispatcher adapts the multi-variant loader design from
[STS2-RitsuLib](https://github.com/BAKAOLC/STS2-RitsuLib). Attribution and the upstream MIT license
are stored in `workshop/loader/THIRD_PARTY_NOTICES.md` and included in Workshop uploads.
