# Block hooks

Simulation-facing hook facade: `InCombat/Mirrors/HookMirrors.cs`.

Mirror files:

- `InCombat/Mirrors/Hooks/Block/BeforeBlockGainedMirrors.cs`
- `InCombat/Mirrors/Hooks/Block/AfterBlockGainedMirrors.cs`
- `InCombat/Simulation/CombatPredictionSimulator.Block.cs`

## Hook specs

- `AbstractModel.BeforeBlockGained(Creature, decimal, ValueProp, CardModel?)`
- `AbstractModel.AfterBlockGained(Creature, decimal, ValueProp, CardModel?)`

## BeforeBlockGained listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| None | - | No vanilla listener currently overrides this hook. | Empty registry is correct. |

## AfterBlockGained listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `BeaconOfHopePower` | 希望灯塔 | During owner's turn, after owner gains block, gives living player teammates half as much block once per trigger chain. | Implemented with reentry guard. Matches original relevant state change. |
| `JuggernautPower` | 势不可当 | After owner gains positive block, rolls a random hittable enemy and deals damage. | Implemented with cloned `CombatTargets` and simulator `Damage`. |

## Parity notes

- `CombatPredictionSimulator.GainBlock` correctly uses original `Hook.ModifyBlock` for read-only amount modifiers, then mirrors `AfterBlockGained` side effects.
- `JuggernautPower` parity depends on `Damage`; current damage post-hook omissions still apply.

## Mock model list

- None.
