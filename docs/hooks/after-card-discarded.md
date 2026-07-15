# AfterCardDiscarded hook

Mirror files: `InCombat/Mirrors/HookMirrors.cs` and
`InCombat/Mirrors/Hooks/Card/AfterCardDiscardedMirrors.cs`.

## Hook spec

- `AbstractModel.AfterCardDiscarded(PlayerChoiceContext, CardModel)`

## Original listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `ToughBandages` | 结实绷带 | If owner discards during owner side turn, gains block. | Implemented. Uses simulator `GainBlock` and matches original relevant state change. |
| `Tingsha` | 铜钹 | If owner discards during owner side turn, rolls a random hittable enemy and deals damage. | Implemented with cloned `CombatTargets` and simulator `Damage`; inherits current damage post-hook gaps. |

## Parity notes

- None.

## Mock model list

- None.
