# Orb hooks

Mirror files: `InCombat/Mirrors/HookMirrors.cs`, `InCombat/Mirrors/Orbs/` for `OrbModel`
behavior, and `InCombat/Mirrors/Hooks/Orb/` for orb-related `AbstractModel` hooks.

## Hook specs

- `AbstractModel.AfterOrbChanneled(PlayerChoiceContext, Player, OrbModel)`
- `AbstractModel.AfterOrbEvoked(PlayerChoiceContext, OrbModel, IEnumerable<Creature>)`
- `AbstractModel.ModifyOrbPassiveTriggerCounts(OrbModel, int)`
- `AbstractModel.AfterModifyingOrbPassiveTriggerCount(OrbModel)`

## AfterOrbChanneled listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `Metronome` | 节拍器 | Counts owner orb channels; at threshold damages all hittable enemies. | Implemented with `StateStore` counter and simulator `Damage`; inherits current damage post-hook gaps. |

## AfterOrbEvoked listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `ThunderPower` | 雷霆 | When owner evokes Lightning, damages evoke targets. | Implemented. Filters living targets and calls `Damage`; VFX/SFX omitted. |

## ModifyOrbPassiveTriggerCounts listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `GoldPlatedCables` | 镀金缆线 | Adds one passive trigger for owner's first orb. | Implemented. Matches original relevant count change. |

## AfterModifyingOrbPassiveTriggerCount listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `GoldPlatedCables` | 镀金缆线 | Flashes after modifying the passive trigger count. | Intentionally omitted as cosmetic-only; `TriggerOrbPassive` does not dispatch this after hook. |

## Parity notes

- `ThunderPower` and any future orb damage inherit the current damage post-hook omissions.
- `Metronome` mirrors only prediction state; display/status activation is intentionally not mutated.
- `OrbMirrors` owns independent exact-type registries for `OrbModel.Passive`,
  `OrbModel.Evoke`, and `OrbModel.BeforeTurnEndOrbTrigger`. Implementations are grouped by orb type
  so each orb's passive, evoke, turn trigger, and shared helpers stay together. Unknown overrides
  use the shared model-method mirror risk handling; the result-producing Evoke registry does not
  permit ignored registrations.
- `HookMirrors` owns listener enumeration for the orb hook family. Each hook-name mirror file under
  `InCombat/Mirrors/Hooks/Orb/` owns its method spec, registry, context, handlers, and hook-local
  prediction state; simulator callers pass ordinary hook arguments.
- Turn-end orb queue simulation mirrors `OrbQueue.BeforeTurnEnd` by dispatching the
  `BeforeTurnEndOrbTrigger` registry. Vanilla Lightning, Frost, Dark, and Glass orbs forward this
  trigger through `OrbModel.TriggerPassive`; `PlasmaOrb` does not override the method, so the
  registry treats it as `NotOverridden`.
- StS2 v0.108.0 moved passive trigger-count handling into `OrbModel.TriggerPassive`.
  `CombatPredictionSimulator.TriggerOrbPassive` mirrors that helper by applying the count hook and
  dispatching one passive body per iteration. Turn-end orb overrides call this helper, while direct
  `OrbPassive` calls still mirror `OrbCmd.Passive(..., countAffectedByHooks: false)`. The original
  helper's `AfterModifyingOrbPassiveTriggerCount` dispatch is omitted because its only current
  listener, `GoldPlatedCables`, only flashes the relic.
- StS2 v0.108.0 made Frost orbs grant passive/evoke block to all players while
  the owner has `HibernatePower`. `FrostOrbMirrors` preserves owner-first block gain
  order and returns all player creatures for Frost evoke targets, matching
  vanilla's `AfterOrbEvoked` target list.

## Mock model list

- None.
