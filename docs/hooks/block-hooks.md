# Block hooks

Simulation-facing hook facade: `InCombat/Mirrors/HookMirrors.cs`.

Mirror files:

- `InCombat/Mirrors/Hooks/Block/BeforeBlockGainedMirrors.cs`
- `InCombat/Mirrors/Hooks/Block/AfterBlockGainedMirrors.cs`
- `InCombat/Mirrors/Hooks/Block/AfterBlockBrokenMirrors.cs`
- `InCombat/Simulation/CombatPredictionSimulator.Block.cs`
- `InCombat/Simulation/CombatPredictionSimulator.Damage.cs`

## Hook specs

- `AbstractModel.BeforeBlockGained(Creature, decimal, ValueProp, CardModel?)`
- `AbstractModel.AfterBlockGained(Creature, decimal, ValueProp, CardModel?)`
- `AbstractModel.AfterBlockBroken(PlayerChoiceContext, Creature target, Creature? breaker)`

## BeforeBlockGained listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| None | - | No vanilla listener currently overrides this hook. | Empty registry is correct. |

## AfterBlockGained listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `BeaconOfHopePower` | 希望灯塔 | During owner's turn, after owner gains block, gives living player teammates half as much block once per trigger chain. | Implemented with reentry guard. Matches original relevant state change. |
| `JuggernautPower` | 势不可当 | After owner gains positive block, rolls a random hittable enemy and deals damage. | Implemented with cloned `CombatTargets` and simulator `Damage`. |

## AfterBlockBroken listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `BurrowedPower` | 埋地 | When owner's block breaks, removes the power and stuns the burrowed monster. | Ignored. Power removal and monster state are outside the simulator, and the result affects later enemy behavior rather than the current player-turn prediction surface. |
| `HandDrill` | 手钻 | When owner or owner's pet breaks an enemy's block, applies Vulnerable to that enemy. | Risk only when the trigger condition matches. Apply Power is unsupported. |

## Parity notes

- StS2 v0.109.0 added `PlayerChoiceContext` and the nullable block-breaking creature to
  `AfterBlockBroken`. The simulator forwards the damage dealer as `breaker`.
- Vanilla `Hook.AfterBlockBroken` deliberately iterates `combatState.IterateHookListeners()`
  directly instead of using the normal combat-ending guard, so a block-breaking killing hit still
  dispatches the hook. The mirror uses the same unguarded listener path and runs before
  `AfterCurrentHpChanged` and post-damage hooks.
- `CombatPredictionSimulator.GainBlock` follows v0.109.0 by returning before block hooks when the
  target is already dead in shadow state.

## Mock model list

- None.
