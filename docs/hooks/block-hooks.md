# Block hooks

Simulation-facing hook facade: `InCombat/Mirrors/HookMirrors.cs`.

Mirror files:

- `InCombat/Mirrors/Hooks/Block/BeforeBlockGainedMirrors.cs`
- `InCombat/Mirrors/Hooks/Block/AfterBlockGainedMirrors.cs`
- `InCombat/Mirrors/Hooks/Block/AfterBlockBrokenMirrors.cs`
- `InCombat/Mirrors/Hooks/Block/ModifyBlockMirrors.cs`
- `InCombat/Mirrors/Hooks/Block/AfterModifyingBlockAmountMirrors.cs`
- `InCombat/Simulation/CombatPredictionSimulator.Block.cs`
- `InCombat/Simulation/CombatPredictionSimulator.Damage.cs`

## Hook specs

- `AbstractModel.BeforeBlockGained(Creature, decimal, ValueProp, CardModel?)`
- `AbstractModel.ModifyBlockAdditive(Creature, decimal, ValueProp, CardModel?, CardPlay?)`
- `AbstractModel.ModifyBlockMultiplicative(Creature, decimal, ValueProp, CardModel?, CardPlay?)`
- `AbstractModel.AfterModifyingBlockAmount(decimal, CardModel?, CardPlay?)`
- `AbstractModel.AfterBlockGained(Creature, decimal, ValueProp, CardModel?)`
- `AbstractModel.AfterBlockBroken(PlayerChoiceContext, Creature target, Creature? breaker)`

## BeforeBlockGained listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| None | - | No vanilla listener currently overrides this hook. | Empty registry is correct. |

## ModifyBlockAdditive listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `StretchPower` | 拉伸 | Adds block throughout the selected owner CardPlay, then consumes its starting amount after that play. | Exact adapter checks powered block and ownership, marks a local bonus flag and shares the selected occurrence with before/after card hooks. Null-play previews read remaining amount; nested different plays receive no bonus. The original live gaveBonusBlock write is isolated. |

## ModifyBlockMultiplicative listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `PaelsLegion` | 佩尔的士兵 | Doubles block while its cooldown allows, then commits the selected card occurrence. | Exact adapter shares shadow cooldown and the exact CardPlay with AfterModifyingBlockAmount/AfterCardPlayed; nested plays cannot borrow pending doubling. |
| `Vambrace` | 臂甲 | Doubles block once per combat for the selected owner card. | Exact adapter shares shadow triggering-card identity and once-per-combat state with selected-modifier and after-play commits. |
| `UnmovablePower` | 不动 | Doubles qualifying block until enough other CardPlay block-gain entries have occurred this turn. | Combines current-turn live BlockGained entries and CombatPredictionBlockGainedEntry records. Counts entries, not distinct cards; gains from the current CardPlay do not spend its own eligibility. |

## AfterBlockGained listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `BeaconOfHopePower` | 希望灯塔 | During owner's turn, after owner gains block, gives living player teammates half as much block once per trigger chain. | Implemented with reentry guard. Matches original relevant state change. |
| `JuggernautPower` | 势不可当 | After owner gains positive block, rolls a random hittable enemy and deals damage. | Implemented with cloned `CombatTargets` and simulator `Damage`. |

## AfterBlockBroken listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `BurrowedPower` | 埋地 | When owner's block breaks, removes the power and stuns the burrowed monster. | Ignored. Power removal and monster state are outside the simulator, and the result affects later enemy behavior rather than the current player-turn prediction surface. |
| `HandDrill` | 手钻 | When owner or owner's pet breaks an enemy's block, applies Vulnerable to that enemy. | Risk only when the trigger condition matches; this listener's power application is not mirrored. |

## Parity notes

- Successful positive modified block gains are recorded after the shadow block write and before `AfterBlockGained`,
  matching vanilla history timing. These semantic records leave live history unchanged and add no new presentation.

- StS2 v0.109.0 added `PlayerChoiceContext` and the nullable block-breaking creature to
  `AfterBlockBroken`. The simulator forwards the damage dealer as `breaker`.
- Explicit block loss also dispatches `AfterBlockBroken` when positive shadow block reaches zero, forwarding the
  remover. `Expose` uses this command; its Hand Drill interaction still records risk under the listener coverage above.
- Vanilla `Hook.AfterBlockBroken` deliberately iterates `combatState.IterateHookListeners()`
  directly instead of using the normal combat-ending guard, so a block-breaking killing hit still
  dispatches the hook. The mirror uses the same unguarded listener path and runs before
  `AfterCurrentHpChanged` and post-damage hooks.
- `CombatPredictionSimulator.GainBlock` follows v0.109.0 by returning before block hooks when the
  target is already dead in shadow state.
- Card-sourced block gains forward the simulated source and `CardPlay` to the prediction-aware block modifier path,
  matching vanilla modifiers that inspect the current card play.
- The prediction-aware block hook mirrors preserve vanilla enchantment/additive/multiplicative order. Unregistered
  value listeners call their original read-only methods; exact registrations replace prediction-state-dependent
  live-state reads without unsupported-risk noise. Their selected-modifier commit shares the exact `CardPlay` with
  `AfterCardPlayed`, so cooldown and once-per-combat consumption remain occurrence-safe in nested chains.
  `PaelsLegion` doubles only while no play has been selected or the current CardPlay is the selected occurrence;
  nested plays do not borrow the pending outer play's doubling.
- `AfterModifyingBlockAmount` is a side-effect hook and therefore uses the full action registry for listeners selected
  by the value passes. `FastenPower` is explicitly ignored because its original effect is visual-only;
  `PaelsLegion` and `Vambrace` commit shadow state. An unregistered override is not executed against the live model and
  records `MethodNotMirrored`, because a Mod listener may change prediction-relevant state.

## Mock model list

- None.
