# Random Foreseer

Languages: [中文](README.md) | English

A random-outcome prediction mod for Slay the Spire 2. It previews selected RNG results without advancing the real game RNG, so you can see what will happen before confirming an action.

## Features

- **Transform prediction**: shows the exact card that the current RNG state will produce in transform confirmation previews.
- **Random-card potion prediction**: adds the predicted generated cards to random-card potion hover tips.
- **Combat card generation prediction**: shows predicted generated cards when hovering supported random-card generators in hand during combat.
- **Driftwood reroll prediction**: shows the cards that a card reward reroll will offer when hovering the Reroll button.
- **Out-of-combat relic result prediction**: shows immediate random results when hovering Neow and other Ancient relic options, relic rewards, and merchant relics.
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

### Combat Card Generation

- Bundle of Joy
- Discovery
- Distraction
- Infernal Blade
- Jack of All Trades
- Jackpot
- Manifest Authority
- Metamorphosis
- Quasar
- Splash
- Stoke
- White Noise
- Mad Science (Chaos rider only)

### Card Rewards

- Driftwood reroll

### Out-of-Combat Relics

- Immediate random results from Neow and other Ancient relic options
- Upon-pickup results for Astrolabe, Alchemical Coffer, Calling Bell, Glass Eye, Pandora's Box, Sand Castle, Scroll Boxes, Sea Glass, Sere Talon, Toy Box, Cauldron, Orrery, Fragrant Mushroom, War Paint, Whetstone, and similar relics
- Immediate random results from relic rewards
- Immediate random results from merchant relics

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
| Predict combat card generation | Controls whether supported in-hand combat cards show predicted generated cards |
| Predict Driftwood rerolls | Controls whether Driftwood card reward rerolls show predicted cards |
| Predict out-of-combat relic results | Controls whether Ancient relic options, relic rewards, and merchant relics show immediate random results |
| Enable Ancient event debug reroll | Controls whether Ancient event pages show a debug Reroll button; off by default |
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
