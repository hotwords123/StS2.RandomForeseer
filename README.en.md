# Random Foreseer

Languages: [中文](README.md) | English

A random-outcome prediction mod for Slay the Spire 2. It previews selected RNG results without advancing the real game RNG, without needing to Save & Load.

Changelog: [CHANGELOG.md](CHANGELOG.md)

Steam Workshop: [Random Foreseer](https://steamcommunity.com/sharedfiles/filedetails/?id=3747531952)

The Workshop package automatically loads the newest compatible Mod version for the running game version; GitHub Releases provide a standard single-version package.

## Features

### Out-of-combat prediction

- **Transform prediction**: shows the exact card that the current RNG state will produce in transform selection grid hover tips and confirmation previews.
- **Next Act Ancient and boss prediction**: the top bar on boss reward screens in the first two Acts shows the next Act's starting Ancient and ending boss.
- **Event option prediction**: shows immediate random rewards, random upgrades/downgrades, and random follow-up options when hovering non-Ancient event options.
- **Crystal Sphere clairvoyance**: shows item locations and types through unrevealed fog in the Crystal Sphere minigame.
- **Relic pickup effect prediction**: relic tooltips (including Ancient options) show random cards, relics, potions, curses, and transform results that happen immediately on pickup.
- **Courier restock prediction**: while you have The Courier, hovering merchant cards, potions, or relics you can afford shows the item that will restock that slot after purchase.
- **Driftwood reroll prediction**: shows the cards that a card reward reroll will offer when hovering the Reroll button.
- **Pael's Wing sacrifice prediction**: shows the relic awarded by an activating Sacrifice button.
- **Rest-site result prediction**: shows random results from relics such as Dream Catcher, Tiny Mailbox, and Shovel when hovering rest-site options.

### In-combat prediction

- **Combat transform prediction**: shows the cards that Entropy will transform selected hand cards into during combat.
- **End-turn effect prediction**: shows aggregated supported end-turn damage for all players ending their turn; overlay indicators and health bar forecasts can be configured separately for End Turn button hover or always during the player turn; hovering a target creature or prediction indicator shows per-hit damage source details.
- **Frozen Eye**: shows the combat draw pile in actual draw order when opened, and previews the discard pile order after shuffle during the player's turn.
- **Combat card generation prediction**: shows predicted generated cards when hovering supported random-card generators in hand during combat.
- **Card draw prediction**: shows cards drawn by supported card-play effects, including cards after shuffle.
- **Combat card selection prediction**: shows or highlights existing cards that supported combat card-selection effects will select when hovering cards in hand or card-play targets; predictions that may be shifted by side effects show a warning that can be disabled.
- **Orb effect prediction**: shows targets that supported orb-triggering cards and potions will hit when hovering those sources or their targets during combat, with health bar forecasts.
- **Random-card-generation potion prediction**: adds the predicted generated cards to random-card-generation potion hover tips.
- **Potion draw prediction**: shows the cards that supported draw potions will draw, including cards after shuffle.
- **Potion generation prediction**: shows the potions that Entropic Brew and Alchemize will generate.
- **Draw-pile autoplay prediction**: shows the cards that Havoc, Cascade, and Distilled Chaos will play from the draw pile.
- **Random-target attack prediction**: shows targets that supported random-enemy attack cards will hit during combat, with health bar forecasts.

Each feature can be toggled independently from the mod settings page, and predictions can also be disabled globally for singleplayer or multiplayer. Fair mode is enabled by default and limits predictions to information obtainable through Save & Load.

## Integrations

### lemonSpire2

When lemonSpire2 is installed, its teammate panel reuses Random Foreseer's existing predictions:

- Combat card generation, combat card selection, draw-pile autoplay, card draw, and potion generation predictions for teammate hand cards
- Relic pickup effect predictions for teammate Ancient relic choices
- Immediate random result predictions for teammate merchant relics and merchant potions

## Installation

1. Install and enable `STS2-RitsuLib`.
2. Put the released `RandomForeseer` folder into the game's `mods` directory.
3. Start the game and confirm that `RandomForeseer` is loaded in the mod list.

Current manifest targets:

| Item | Value |
|---|---|
| Current version | `0.13.6` |
| Minimum game version | `0.111.0` |
| RitsuLib dependency | `0.5.12` |

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
RandomForeseer.csproj - C# project and build configuration
RandomForeseer.json - Mod manifest
RandomForeseer/localization/ - Mod settings and UI localization resources
RandomForeseerCode/ - C# source code
RandomForeseerCode/Common/ - Shared prediction HoverTip, RNG, and localization utilities
RandomForeseerCode/Data/ - Settings data, persistence, and migrations
RandomForeseerCode/Debug/ - Debug entry points and test reward screens
RandomForeseerCode/Entry.cs - Mod entry point and Harmony patch registration
RandomForeseerCode/InCombat/ - In-combat card, potion, and Frozen Eye predictions
RandomForeseerCode/Integrations/ - Optional integration patches for other mods
RandomForeseerCode/Localization/ - Mod localization registration and text access
RandomForeseerCode/OutOfCombat/ - Out-of-combat event, reward, merchant, rest-site, and transform predictions
RandomForeseerCode/OutOfCombat/Events/ - Non-Ancient event option predictions
RandomForeseerCode/Settings/ - Settings page registration and UI bindings
project.godot - Godot project used for PCK export
scripts/ - Local development, maintenance, and release scripts
workshop/loader/ - Steam Workshop multi-version package loader
```
