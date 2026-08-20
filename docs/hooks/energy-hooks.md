# Energy hooks

Mirror files: `InCombat/Simulation/SimPlayerCombatState.cs`, `InCombat/Simulation/CombatPredictionSimulator.Energy.cs`.

## Hook specs

- `AbstractModel.ModifyEnergyGain(Player, decimal)`
- `AbstractModel.AfterModifyingEnergyGain()`
- `AbstractModel.ShouldPayExcessEnergyCostWithStars(Player)`

## Current mirror behavior

`SimPlayerCombatState` seeds `Energy` and `Stars` from the live `PlayerCombatState`. Energy gain/loss and supported star gain mutate only this shadow state through simulator command helpers.

`GainEnergy` mirrors the prediction-relevant part of `PlayerCmd.GainEnergy`: it ignores non-positive gain, runs original `Hook.ModifyEnergyGain`, and adds the modified positive amount to shadow energy with vanilla's current energy clamp. It intentionally does not call `Hook.AfterModifyingEnergyGain`; reviewed vanilla after listeners only flash UI and do not mutate prediction-relevant state.

`LoseEnergy` mirrors `PlayerCmd.LoseEnergy`'s state change directly. Vanilla energy loss does not run an energy-gain modifier hook.

`GainStars` runs the original read-only `ShouldGainStars` predicate and mutates shadow state. The `AfterStarsGained`
hook family is not yet mirrored; in particular, Black Hole's damage after gaining stars remains a separate coverage
gap.

## ModifyEnergyGain listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `NoEnergyGainPower` | 无法获得能量 | Sets owner energy gain to 0. | Implemented by original `Hook.ModifyEnergyGain`. The after hook is intentionally omitted because it only flashes UI. |

There are currently no vanilla overrides of `ShouldPayExcessEnergyCostWithStars`. Calling the original read-only hook
preserves listener order and supports Mod overrides whose decision depends only on live listener/player state; a future
listener that consumes prediction-local model state will require a selective mirror.

## Energy state callers

| Source | 中文名 | Mirror status |
| --- | --- | --- |
| `AutomationPower` | 自动化 | Gains energy when the prediction-local draw counter reaches its threshold. |
| `Void` | 虚空 | Loses energy when the drawn card is this Void. |
| `DrumOfBattle` | 战鼓 | Gains energy once per predicted generated play. |
| `GremlinHorn` | 地精之角 | Gains energy before drawing, matching vanilla order. |
| `PlasmaOrb` | 等离子 | Gains energy on direct passive/evoke simulation. |
| `TheSealedThronePower` | 封印王座 | Gains stars before each owner card through simulator `GainStars`. |
| `PaelsTears` | 佩尔之泪 | Records the leftover-energy predicate prediction-locally; the later turn-start gain is outside the current end-turn prediction boundary. |

## Parity notes

- None.
