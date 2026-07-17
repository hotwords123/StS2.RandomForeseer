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
| `SoulboundPower` | 灵魂绑定 | When the applier creates a `Soul`, adds this power's amount of `Soul` cards at random positions in the target owner's draw pile, guarded by a recursion flag. | Implemented. Uses prediction-local recursion state, recursive `AddGeneratedCardsToCombat`, and cloned Shuffle RNG for random insertion. StS2 v0.109.0 changed the insertion position from bottom to random. |
| `PillarOfCreationPower` | 创世之柱 | The first time owner creates a card each turn, owner gains block. | Implemented via `GainBlock`; initializes a prediction-local triggered flag from live `CardGeneratedEntry` history, then triggers at most once during the simulation. StS2 v0.109.0 added the once-per-turn gate. |
| `SmokestackPower` | 烟囱 | When owner creates a Status, deals unpowered damage to all hittable enemies. | Implemented via simulator `Damage`; inherits current damage hook gaps. |
| `TrashToTreasurePower` | 化废为宝 | When owner creates a Status, channels random orbs using `RunState.Rng.CombatOrbGeneration`. | Implemented. Uses cloned `CombatOrbGeneration` and simulator `OrbChannel`; inherits current orb hook gaps. |
| `RocketPunch` | 火箭飞拳 | When owner creates a Status for themself, this card's cost is set to 0 until played. | Implemented. Finds the predicted `RocketPunch` and mutates only preview cost. |

## Parity notes

- Vanilla `AddGeneratedCardsToCombat` processes cards one at a time: `History.CardGenerated`, `Add`, `AfterCardGeneratedForCombat`.
- Because `Add` itself dispatches `AfterCardEnteredCombat` and `AfterCardChangedPiles`, the generated-card order is: generated history, combat entry, pile changed, generated-for-combat.
- For each card in `AddGeneratedCardsToCombat`, the simulator appends a
  `CombatPredictionCardGeneratedEntry`, adds the card, runs its generation hooks, and then
  appends the matching `CombatPredictionCardGenerationResolvedEntry`.
- Hook dispatch currently iterates only the live `CombatState` listeners; cards generated only inside the simulator are not included as later hook listeners.
- The `PillarOfCreationPower` state combines live current-turn generation history with a
  prediction-local flag, so a multi-card generation batch gains block only for its first eligible
  card without mutating live power state.

## Mock model list

- None.
