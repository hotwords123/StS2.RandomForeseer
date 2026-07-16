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

- StS2 v0.108.0 moved passive trigger-count handling into `OrbModel.TriggerPassive`. `CombatPredictionSimulator.TriggerOrbPassive` mirrors that helper by applying the count hook and dispatching one passive body per iteration. Turn-end orb overrides call this helper, while direct `OrbPassive` calls still mirror `OrbCmd.Passive(..., countAffectedByHooks: false)`. The original helper's `AfterModifyingOrbPassiveTriggerCount` dispatch is omitted because its only current listener, `GoldPlatedCables`, only flashes the relic.
- A single simulation records at most 1000 successfully channeled orbs. Further channel attempts return `false` before queue mutation or hook dispatch and append `OrbChannelLimitExceeded` risk, preventing recursive channel and evoke effects from growing without bound.

## Mock model list

- None.
