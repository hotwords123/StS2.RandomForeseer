# AfterCardDiscarded hook

Mirror file: `InCombat/Hooks/AfterCardDiscardedHook.cs`.

## Hook spec

- `AbstractModel.AfterCardDiscarded(PlayerChoiceContext, CardModel)`

## Original listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `ToughBandages` | 结实绷带 | If owner discards during owner side turn, gains block. | Implemented. Uses simulator `GainBlock` and matches original relevant state change. |
| `Tingsha` | 铜钹 | If owner discards during owner side turn, rolls a random hittable enemy and deals damage. | Implemented with cloned `CombatTargets` and simulator `Damage`; inherits current damage post-hook gaps. |

## Parity notes

- `ToughBandages` is consistent with original logic, except VFX/flash are intentionally omitted.
- Random target selection uses `State.HittableEnemies` with cloned `CombatTargets`, matching vanilla `CombatState.HittableEnemies`.

## Mock model list

- None.
