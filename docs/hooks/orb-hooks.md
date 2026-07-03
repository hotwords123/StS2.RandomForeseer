# Orb hooks

Mirror files: `InCombat/Hooks/OrbHooks.cs`, `InCombat/Hooks/OrbPassiveCountHooks.cs`, `InCombat/Simulation/OrbBehavior.cs`.

## Hook specs

- `AbstractModel.AfterOrbChanneled(PlayerChoiceContext, Player, OrbModel)`
- `AbstractModel.AfterOrbEvoked(PlayerChoiceContext, OrbModel, IEnumerable<Creature>)`
- `AbstractModel.ModifyOrbPassiveTriggerCounts(OrbModel, int)`

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

## Parity notes

- `ThunderPower` and any future orb damage inherit the current damage post-hook omissions.
- `Metronome` mirrors only prediction state; display/status activation is intentionally not mutated.
- Turn-end orb queue simulation mirrors `OrbQueue.BeforeTurnEnd` by dispatching
  `BeforeTurnEndOrbTrigger` semantics after passive-trigger-count hooks. Vanilla
  Lightning, Frost, Dark, and Glass orbs forward this trigger to `Passive`;
  `PlasmaOrb` has no turn-end trigger and is intentionally a no-op here.
- StS2 v0.108.0 moved passive trigger-count handling into `OrbModel.TriggerPassive`.
  The simulator keeps the count loop in `SimulateOrbQueueBeforeTurnEnd`, then runs
  one passive body per iteration. Direct `OrbPassive` calls still mirror
  `OrbCmd.Passive(..., countAffectedByHooks: false)`.
- StS2 v0.108.0 made Frost orbs grant passive/evoke block to all players while
  the owner has `HibernatePower`. `OrbBehavior` mirrors owner-first block gain
  order and returns all player creatures for Frost evoke targets, matching
  vanilla's `AfterOrbEvoked` target list.

## Mock model list

- None.
