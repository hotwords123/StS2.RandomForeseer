# Random Foreseer

Languages: [中文](README.md) | English

A random-outcome prediction mod for Slay the Spire 2. It previews selected RNG results without advancing the real game RNG, so you can see what will happen before confirming an action.

## Features

- **Transform prediction**: shows the exact card that the current RNG state will produce in transform confirmation previews.
- **Random-card potion prediction**: adds the predicted generated cards to random-card potion hover tips.
- **Frozen Eye**: shows the combat draw pile in actual draw order when opened.

Each feature can be toggled independently from the mod settings page.

## Currently Supported Predictions

### Transform Sources

- Astrolabe
- New Leaf
- Aroma of Chaos
- Endless Conveyor
- Morphic Grove
- Symbiote
- Trial
- Whispering Hollow

### Random-Card Potions

- Attack Potion
- Skill Potion
- Power Potion
- Colorless Potion
- Cosmic Concoction
- Orobic Acid

## Installation

1. Install and enable `STS2-RitsuLib`.
2. Put the released `RandomForeseer` folder into the game's `mods` directory.
3. Start the game and confirm that `RandomForeseer` is loaded in the mod list.

Current manifest targets:

| Item | Value |
|---|---|
| Minimum game version | `0.106.0` |
| RitsuLib dependency | `0.3.8` |

## Settings

Open the RitsuLib mod settings page and select **Random Foreseer**:

| Setting | Effect |
|---|---|
| Predict transform results | Controls whether transform confirmation previews show predicted cards |
| Predict potion card results | Controls whether random-card potions show predicted cards |
| Enable Frozen Eye | Controls whether the draw pile screen shows cards in actual draw order |

## Build From Source

Before the first build, copy the local path configuration:

```powershell
Copy-Item .\local.props.template .\local.props
```

Configure these values in `local.props`:

| Field | Description |
|---|---|
| `Sts2Dir` | Slay the Spire 2 install directory |
| `Sts2DataDir` | Game DLL directory, usually `$(Sts2Dir)/data_sts2_windows_x86_64` |
| `GodotExe` | MegaDot/Godot executable used to export the PCK |
| `RitsuLibDeployDir` | Local RitsuLib deployment directory |

Common build command:

```powershell
dotnet build .\RandomForeseer.csproj
```

Validate C# compilation only, without copying to the game directory or exporting a PCK:

```powershell
dotnet build .\RandomForeseer.csproj /p:RunPckExport=false /p:CopyModOnBuild=false
```

A full build deploys the DLL, manifest, and PCK to `$(Sts2Dir)/mods/RandomForeseer`.

## Project Layout

```text
RandomForeseerCode/                 C# source
RandomForeseer/localization/        Settings localization
RandomForeseer.csproj               C# project and build configuration
RandomForeseer.json                 Mod manifest
project.godot                       Godot project used for PCK export
```
