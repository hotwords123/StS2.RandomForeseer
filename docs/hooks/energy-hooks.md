# Energy hooks

Mirror files: `InCombat/Simulation/SimPlayerCombatState.cs`, `InCombat/Simulation/CombatPredictionSimulator.Energy.cs`.

## Hook specs

- `AbstractModel.ModifyEnergyGain(Player, decimal)`
- `AbstractModel.AfterModifyingEnergyGain()`

## Current mirror behavior

`SimPlayerCombatState` seeds `Energy` and `Stars` from the live `PlayerCombatState`. Energy gain/loss mutates only this shadow state; stars are currently state-only and do not have gain/loss helpers.

`GainEnergy` mirrors the prediction-relevant part of `PlayerCmd.GainEnergy`: it ignores non-positive gain, runs original `Hook.ModifyEnergyGain`, and adds the modified positive amount to shadow energy with vanilla's current energy clamp. It intentionally does not call `Hook.AfterModifyingEnergyGain`; reviewed vanilla after listeners only flash UI and do not mutate prediction-relevant state.

`LoseEnergy` mirrors `PlayerCmd.LoseEnergy`'s state change directly. Vanilla energy loss does not run an energy-gain modifier hook.

## ModifyEnergyGain listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `NoEnergyGainPower` | 无法获得能量 | Sets owner energy gain to 0. | Implemented by original `Hook.ModifyEnergyGain`. The after hook is intentionally omitted because it only flashes UI. |

## Energy state callers

| Source | 中文名 | Mirror status |
| --- | --- | --- |
| `AutomationPower` | 自动化 | Gains energy when the prediction-local draw counter reaches its threshold. |
| `Void` | 虚空 | Loses energy when the drawn card is this Void. |
| `DrumOfBattle` | 战鼓 | Gains energy once per predicted generated play. |
| `GremlinHorn` | 地精之角 | Gains energy before drawing, matching vanilla order. |
| `PlasmaOrb` | 等离子 | Gains energy on direct passive/evoke simulation. |
| `PaelsTears` | 佩尔之泪 | Records the leftover-energy predicate prediction-locally; the later turn-start gain is outside the current end-turn prediction boundary. |

## Parity notes

- Energy/star spending, card-play `ResourceInfo`, and turn-start energy reset are still outside full card-play simulation.
- X-value previews that are owned by the simulator should read `SimPlayerCombatState.Energy` rather than live `PlayerCombatState.Energy`.
- If vanilla adds an `AfterModifyingEnergyGain` listener with prediction-relevant state, add an exact mirror instead of continuing to rely on the current no-op assumption.
