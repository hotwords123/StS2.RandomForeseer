# Random Foreseer

Languages: [中文](README.md) | English

A random-outcome prediction mod for Slay the Spire 2. It previews selected RNG results without advancing the real game RNG, without needing to Save & Load.

Changelog: [CHANGELOG.md](CHANGELOG.md)

## Features

- **Transform prediction**: shows the exact card that the current RNG state will produce in transform confirmation previews.
- **Random-card-generation potion prediction**: adds the predicted generated cards to random-card-generation potion hover tips.
- **Combat card generation prediction**: shows predicted generated cards when hovering supported random-card generators in hand during combat.
- **Combat card selection prediction**: shows or highlights existing cards that supported in-hand combat effects will select; predictions that may be shifted by side effects show a warning that can be disabled.
- **Driftwood reroll prediction**: shows the cards that a card reward reroll will offer when hovering the Reroll button.
- **Out-of-combat relic result prediction**: shows immediate random results when hovering Neow and other Ancient relic options, relic rewards, and merchant relics.
- **Shovel dig relic prediction**: shows the relic that Shovel's rest-site Dig option will obtain when hovered.
- **Event option prediction**: shows immediate random rewards, random upgrades/downgrades, and random follow-up options when hovering non-Ancient event options.
- **Frozen Eye**: shows the combat draw pile in actual draw order when opened.

Each feature can be toggled independently from the mod settings page, and predictions can also be disabled globally for singleplayer or multiplayer. Fair mode is enabled by default and limits predictions to information obtainable through Save & Load.

## Currently Supported Predictions

### Transform Sources

- Astrolabe
- New Leaf
- Aroma of Chaos
- Endless Conveyor
- Morphic Grove
- Symbiote
- The Trial
- Whispering Hollow

### Random-Card-Generation Potions

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
- Largesse
- Manifest Authority
- Metamorphosis
- Quasar
- Splash
- Stoke
- White Noise
- Mad Science (Chaos rider only)

### Combat Card Selection

- True Grit (unupgraded)
- Cinder
- Thrash
- Hidden Gem
- Drain Power
- Anointed
- Seeker Strike (random candidates)
- Uproar

### Card Rewards

- Driftwood reroll

### Non-Ancient Events

- Immediate random results for event options in Aroma of Chaos, Battleworn Dummy, Brain Leech, Colorful Philosophers, Doll Room, Doors of Light and Dark, Endless Conveyor, Infested Automaton, Luminous Choir, Morphic Grove, Potion Courier, Punch Off, Ranwid the Elder, Reflections snoitcelfeR, Room Full of Cheese, The Round Tea Party, Slippery Bridge, Symbiote, Tablet of Truth, The Future of Potions?, The Legends Were True, This or That?, Tinker Time, Trash Heap, The Trial, Unrest Site, War Historian, Repy, Welcome to Wongo's, Wellspring, Whispering Hollow, and similar events.

### Out-of-Combat Relics

- Immediate random results from Neow and other Ancient relic options
- Upon-pickup results for Cauldron, Orrery, Fragrant Mushroom, War Paint, Whetstone, and similar relics
- Immediate random results from relic rewards
- Immediate random results from merchant relics
- Relics that Shovel's rest-site Dig option will obtain

## Installation

1. Install and enable `STS2-RitsuLib`.
2. Put the released `RandomForeseer` folder into the game's `mods` directory.
3. Start the game and confirm that `RandomForeseer` is loaded in the mod list.

Current manifest targets:

| Item | Value |
|---|---|
| Current version | `0.2.0` |
| Minimum game version | `0.106.0` |
| RitsuLib dependency | `0.4.4` |

## Settings

Open the RitsuLib mod settings page and select **Random Foreseer**:

| Setting | Effect |
|---|---|
| Enable singleplayer prediction | Controls whether any prediction results are shown in singleplayer, default on |
| Enable multiplayer prediction | Controls whether any prediction results are shown in multiplayer, default on |
| Enable fair mode | Limits predictions to information obtainable through Save & Load, default on |
| Predict transform results | Controls whether transform confirmation previews show predicted cards |
| Predict potion card results | Controls whether random-card-generation potions show predicted cards |
| Predict combat card generation | Controls whether supported in-hand combat cards show predicted generated cards |
| Predict combat card selection | Controls whether supported in-hand combat cards show selected existing cards and hand highlights |
| Show selection prediction warnings | Controls whether side-effect-sensitive selection predictions show a warning, default on |
| Predict Driftwood rerolls | Controls whether Driftwood card reward rerolls show predicted cards |
| Predict out-of-combat relic results | Controls whether Ancient relic options, relic rewards, and merchant relics show immediate random results |
| Predict Shovel dig relics | Controls whether Shovel's rest-site Dig option shows the relic it will obtain |
| Predict event option results | Controls whether non-Ancient event options show immediate random results |
| Slippery Bridge reroll previews | Controls how many future Hold On rerolls are previewed for Slippery Bridge, default 5 |
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
