# Card reward hooks

Simulation-facing hook facade: `OutOfCombat/Mirrors/HookMirrors.cs`.

Mirror file:

- `OutOfCombat/Mirrors/Hooks/CardReward/TryModifyCardRewardOptionsMirrors.cs`

## Hook specs

- `AbstractModel.TryModifyCardRewardOptions(Player, List<CardCreationResult>, CardCreationOptions)`
- `AbstractModel.TryModifyCardRewardOptionsLate(Player, List<CardCreationResult>, CardCreationOptions)`
- `AbstractModel.AfterModifyingCardRewardOptions()`
- `AbstractModel.ModifyCardRewardCreationOptions(Player, CardCreationOptions)`
- `AbstractModel.ModifyCardRewardCreationOptionsLate(Player, CardCreationOptions)`
- `AbstractModel.ModifyCardRewardUpgradeOdds(Player, CardModel, decimal odds)`
- `AbstractModel.TryModifyCardRewardAlternatives(Player, CardReward, List<CardRewardAlternative>)`
- `AbstractModel.ShouldAllowSelectingMoreCardRewards(Player, CardReward)`

The project intentionally keeps using original pure-ish creation-option and upgrade-odds hooks:

- `Hook.ModifyCardRewardCreationOptions`
- `Hook.ModifyCardRewardUpgradeOdds`

## TryModifyCardRewardOptions listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `LastingCandy` | 吃不完的糖 | On every second combat card reward seen, appends an extra Power card reward candidate if the reward was generated from combat. | Implemented. Mirrors StS2 v0.108.0 `CombatRewardsSeen`, `IsCardReward`, and `IsFromCombat` gates with cloned reward RNG and no-modify-hooks creation options. |

## TryModifyCardRewardOptionsLate listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `FrozenEgg` | 冻结之蛋 | Upgrades Power rewards. | Implemented. |
| `MoltenEgg` | 熔火之蛋 | Upgrades Attack rewards. | Implemented. |
| `ToxicEgg` | 毒素之蛋 | Upgrades Skill rewards. | Implemented. |
| `SilverCrucible` | 白银熔炉 | Upgrades card reward cards while charges remain. | Implemented. |
| `LavaLamp` | 熔岩灯 | Upgrades combat-room card rewards if owner took no damage this combat. | Implemented from live relic state. If damage simulation starts mutating prediction-local Lava Lamp state, this hook must read that state. |
| `Glitter` | 亮片 | Adds Glam to valid rewards. | Implemented. |
| `FresnelLens` | 菲涅耳透镜 | Adds Nimble to valid rewards. | Implemented. |
| `SilkenTress` | 华美发束 | Adds Glam to card rewards while unused. | Implemented. |
| `WingCharm` | 羽翼护符 | Chooses one valid reward with niche RNG and adds Swift. | Implemented. |

## ModifyCardRewardCreationOptions listeners

Current mirror status: delegated to original `Hook.ModifyCardRewardCreationOptions` during base card creation. These listeners mutate only the caller-owned `CardCreationOptions`, so prediction callers must pass a fresh options instance for each reward preview.

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `DingyRug` | 肮脏地毯 | For owner's card rewards, adds the colorless card pool unless card pool modifications are disabled. | Implemented by original hook. |
| `PrismaticGem` | 棱彩宝石 | For owner's non-colorless card rewards, adds all unlocked character card pools unless card pool modifications are disabled. | Implemented by original hook. |
| `CharacterCards` | 角色卡牌修饰器 | For card rewards, adds the configured character's card pool unless card pool modifications are disabled. | Implemented by original hook. |
| `BigGameHunter` | 精英猎手 | For elite encounter rewards, forces uniform rare rewards when rarity and pool modifications are allowed; falls back to the player's card pool if the rare filter would produce no cards. | Implemented by original hook. StS2 v0.108.0 clarified this modifier as elite rare card rewards rather than generic better rewards. |

## AfterModifyingCardRewardOptions listeners

Current mirror status: not mirrored by `CardRewardPrediction.ApplyRewardModifiers`. These hooks commit one-shot/usage state after vanilla finishes modifying a card reward; prediction needs a larger transaction context before it can shadow these without mutating live relics.

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `SilverCrucible` | 白银熔炉 | Increments `TimesUsed` after modifying a card reward, until its card-reward charge limit is reached. | Not mirrored. The result modification is implemented, but chained previews can reuse the live charge count because the post-modification commit is not shadowed. |
| `SilkenTress` | 华美发束 | Marks `IsUsed = true` after modifying its first card reward. | Not mirrored. The result modification is implemented, but chained previews can repeatedly see the live unused state until the real reward is generated. |

## Card reward alternative listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `PaelsWing` | 佩尔之翼 | Adds the `SACRIFICE` alternative to card rewards; selecting it increments sacrifice count and obtains a relic every configured number of sacrifices. | Out of scope for the card-reward result mirrors. Existing UI hover prediction handles the generated sacrifice button through `PaelsWingSacrificePrediction` when the next sacrifice would trigger a relic. |

## Known empty card reward hooks

No vanilla non-mock listeners were found in StS2 v0.108.0 for:

- `ModifyCardRewardCreationOptionsLate`
- `ModifyCardRewardUpgradeOdds`
- `ShouldAllowSelectingMoreCardRewards`

## Parity notes

- `ModifyCardRewardCreationOptions` listeners are delegated to the original hook because vanilla treats them as option transforms. They may mutate `CardCreationOptions`; prediction callers protect parity by passing a fresh options instance and not reusing it.
- StS2 v0.108.0 moved `LastingCandy` from `AfterCombatEnd` / `CombatsSeen` to `BeforeCombatRewardOffered` / `CombatRewardsSeen`. Combat reward option factories must include `CardCreationFlags.IsFromCombat` for this mirror to trigger.
- `AfterModifyingCardRewardOptions` is not called during prediction. This intentionally avoids mutating live relic state, but leaves `SilverCrucible`/`SilkenTress` usage state unshadowed across chained reward previews.
- `OutOfCombat.Mirrors.HookMirrors` owns context construction and rebuilds the modifier sequence for the Early and Late phases. `TryModifyCardRewardOptionsMirrors` owns both exact-method registries, their handlers, and shared helpers.
- Both registries mirror the original bool-returning model methods. The facade executes every modifier and ORs the Early/Late results without short-circuiting. Models that return true are appended to the returned modifier list in phase/invocation order, matching vanilla without deduplication. The current prediction caller explicitly discards both outputs because `AfterModifyingCardRewardOptions` is still omitted.
- `TryModifyCardRewardAlternatives` is separate from card option generation. `PaelsWing` sacrifice prediction works from the generated button and should be maintained with the alternative UI patches rather than the card-reward result mirrors.
- `TryModifyCardRewardOptionsMirrorContext` records whether an unsupported listener was encountered, but card-reward prediction still has no public risk projection path, so that state is not surfaced to callers.

## Mock model list

- None.
