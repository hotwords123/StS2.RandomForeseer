# Random Foreseer

Languages: [中文](README.md) | English

A random-outcome prediction mod for Slay the Spire 2. It previews selected RNG results without advancing the real game RNG, without needing to Save & Load.

Changelog: [CHANGELOG.md](CHANGELOG.md)

## Features

- **Transform prediction**: shows the exact card that the current RNG state will produce in transform selection grid hover tips and confirmation previews.
- **Random-card-generation potion prediction**: adds the predicted generated cards to random-card-generation potion hover tips.
- **Potion generation prediction**: shows the potions that Entropic Brew and Alchemize will generate.
- **Combat card generation prediction**: shows predicted generated cards when hovering supported random-card generators in hand during combat.
- **Combat card selection prediction**: shows or highlights existing cards that supported in-hand combat effects will select; predictions that may be shifted by side effects show a warning that can be disabled.
- **Draw-pile autoplay prediction**: shows the cards that Havoc, Cascade, and Distilled Chaos will play from the draw pile.
- **Combat transform prediction**: shows the cards that Entropy will transform selected hand cards into during combat.
- **Driftwood reroll prediction**: shows the cards that a card reward reroll will offer when hovering the Reroll button.
- **Relic pickup effect prediction**: relic tooltips (including Ancient options) show random cards, relics, potions, curses, and transform results that happen immediately on pickup.
- **Rest-site result prediction**: shows random results from relics such as Dream Catcher, Tiny Mailbox, and Shovel when hovering rest-site options.
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

### Potion Generation

- Entropic Brew (in and out of combat, including merchant stock)
- Alchemize

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

### Draw-Pile Autoplay

- Havoc
- Cascade
- Distilled Chaos

### Combat Transform

- Entropy

### Card Rewards

- Driftwood reroll

### Non-Ancient Events

- Immediate random results for event options in Aroma of Chaos, Battleworn Dummy, Brain Leech, Colorful Philosophers, Doll Room, Doors of Light and Dark, Endless Conveyor, Infested Automaton, Luminous Choir, Morphic Grove, Potion Courier, Punch Off, Ranwid the Elder, Reflections snoitcelfeR, Room Full of Cheese, The Round Tea Party, Slippery Bridge, Symbiote, Tablet of Truth, The Future of Potions?, The Legends Were True, This or That?, Tinker Time, Trash Heap, The Trial, Unrest Site, War Historian, Repy, Welcome to Wongo's, Wellspring, Whispering Hollow, and similar events.

### Relic Pickup Effects

- Immediate random results from Neow and other Ancient relic options
- Upon-pickup results for Cauldron, Orrery, Fragrant Mushroom, War Paint, Whetstone, and similar relics
- Immediate random results from relic rewards
- Immediate random results from merchant relics
- Card rewards from Dream Catcher, potions from Tiny Mailbox, and relics that Shovel's rest-site Dig option will obtain

## Installation

1. Install and enable `STS2-RitsuLib`.
2. Put the released `RandomForeseer` folder into the game's `mods` directory.
3. Start the game and confirm that `RandomForeseer` is loaded in the mod list.

Current manifest targets:

| Item | Value |
|---|---|
| Current version | `0.4.0` |
| Minimum game version | `0.107.0` |
| RitsuLib dependency | `0.4.9` |

## Settings

Open the RitsuLib mod settings page and select **Random Foreseer**:

| Setting | Effect |
|---|---|
| Enable singleplayer prediction | Controls whether any prediction results are shown in singleplayer, default on |
| Enable multiplayer prediction | Controls whether any prediction results are shown in multiplayer, default on |
| Enable fair mode | Limits predictions to information obtainable through Save & Load, default on |
| Predict transform results | Controls whether transform selection grid hover tips and confirmation previews show predicted cards |
| Predict combat transform results | Controls whether combat transform selections show predicted cards |
| Predict potion card results | Controls whether random-card-generation potions show predicted cards |
| Predict potion generation | Controls whether Entropic Brew and Alchemize show predicted potions |
| Predict combat card generation | Controls whether supported in-hand combat cards show predicted generated cards |
| Predict combat card selection | Controls whether supported in-hand combat cards show selected existing cards and hand highlights |
| Show selection prediction warnings | Controls whether side-effect-sensitive selection predictions show a warning, default on |
| Predict draw-pile autoplay | Controls whether Havoc, Cascade, and Distilled Chaos show the cards that will be played from the draw pile |
| Predict Driftwood rerolls | Controls whether Driftwood card reward rerolls show predicted cards |
| Predict relic pickup effects | Controls whether relic tooltips (including Ancient options) show random cards, relics, potions, curses, and transform results that happen immediately on pickup |
| Predict rest-site results | Controls whether rest-site options show immediate random results from relics such as Dream Catcher, Tiny Mailbox, and Shovel |
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
Common/                             Shared prediction HoverTip, RNG, and localization utilities
InCombat/                           In-combat card, potion, and Frozen Eye predictions
OutOfCombat/                        Out-of-combat event, reward, merchant, rest-site, and transform predictions
OutOfCombat/Events/                 Non-Ancient event option predictions
Debug/                              Debug entry points and test reward screens
RandomForeseer/localization/        Mod settings and UI localization resources
Entry.cs                            Mod entry point and Harmony patch registration
RandomForeseerSettings.cs           Setting definitions, persistence, and feature gates
RandomForeseer.csproj               C# project and build configuration
RandomForeseer.json                 Mod manifest
project.godot                       Godot project used for PCK export
scripts/release.ps1                 Local build, packaging, and release script
```
