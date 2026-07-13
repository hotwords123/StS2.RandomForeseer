# AfterCardGeneratedForCombat hook

Mirror files: `InCombat/Mirrors/HookMirrors.cs` and
`InCombat/Mirrors/Hooks/Card/AfterCardGeneratedForCombatMirrors.cs`.

## Hook spec

- `AbstractModel.AfterCardGeneratedForCombat(CardModel, Player? creator)`

## Original listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `Aeonglass` | 永世沙漏 | Generated `Wither` cards are fake-upgraded to match Aeonglass's current `WitherUpgradeCount`. | Implemented. Mutates only the generated preview `Wither`. |
| `ArsenalPower` | 武器库 | When the owner creates any card, applies Strength to owner. | Risk only. Apply Power is outside current simulator architecture. |
| `Regalite` | 君王矿石 | When owner creates any card, owner gains block. | Implemented via `GainBlock`; inherits current block hook gaps. |
| `SoulboundPower` | 未本地化 | When the applier creates a `Soul`, adds this power's amount of `Soul` cards to the target owner's draw pile, guarded by a recursion flag. | Implemented. Uses prediction-local recursion state and recursive `AddGeneratedCardsToCombat`. |
| `PillarOfCreationPower` | 创世之柱 | When owner creates any card, owner gains block. | Implemented via `GainBlock`; inherits current block hook gaps. |
| `SmokestackPower` | 烟囱 | When owner creates a Status, deals unpowered damage to all hittable enemies. | Implemented via simulator `Damage`; inherits current damage hook gaps. |
| `TrashToTreasurePower` | 化废为宝 | When owner creates a Status, channels random orbs using `RunState.Rng.CombatOrbGeneration`. | Implemented. Uses cloned `CombatOrbGeneration` and simulator `OrbChannel`; inherits current orb hook gaps. |
| `RocketPunch` | 火箭飞拳 | When owner creates a Status for themself, this card's cost is set to 0 until played. | Implemented. Finds the predicted `RocketPunch` and mutates only preview cost. |

## Parity notes

- Vanilla `AddGeneratedCardsToCombat` processes cards one at a time: `History.CardGenerated`, `Add`, `AfterCardGeneratedForCombat`.
- Because `Add` itself dispatches `AfterCardEnteredCombat` and `AfterCardChangedPiles`, the generated-card order is: generated history, combat entry, pile changed, generated-for-combat.
- The simulator currently omits generated-card history because no prediction logic consumes it.
- Hook dispatch currently iterates only the live `CombatState` listeners; cards generated only inside the simulator are not included as later hook listeners.
- `SoulboundPower` recursively generates cards through the simulator and uses prediction-local state rather than mutating the live `IsAddingSoul` flag.
- `ArsenalPower` remains unsupported because Strength application requires power-state simulation.

## Mock model list

- None.
