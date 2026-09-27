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
| `Regalite` | 君王矿石 | The first time its owner creates a card each turn, owner gains block. | Implemented via `GainBlock`; initializes a prediction-local used flag from the live relic and triggers at most once during the simulation. Inherits current block hook gaps. |
| `SoulboundPower` | 灵魂绑定 | When the applier creates a `Soul`, adds this power's amount of `Soul` cards at random positions in the target owner's draw pile, guarded by a recursion flag. | Implemented. Uses prediction-local recursion state, recursive `AddGeneratedCardsToCombat`, and cloned Shuffle RNG for random insertion. StS2 v0.109.0 changed the insertion position from bottom to random. |
| `PillarOfCreationPower` | 创世之柱 | Whenever its owner creates a card, owner gains block. | Implemented via `GainBlock`; inherits current block hook gaps. StS2 v0.110.0 removed the once-per-turn gate. |
| `SmokestackPower` | 烟囱 | When owner creates a Status, deals unpowered damage to all hittable enemies. | Implemented via simulator `Damage`; inherits current damage hook gaps. |
| `TrashToTreasurePower` | 化废为宝 | When owner creates a Status, channels random orbs using `RunState.Rng.CombatOrbGeneration`. | Implemented. Uses cloned `CombatOrbGeneration` and simulator `OrbChannel`; inherits current orb hook gaps. |
| `RocketPunch` | 火箭飞拳 | When owner creates a Status for themself, this card's cost is reduced by 1 until played. | Implemented. Finds the predicted `RocketPunch` and mutates only preview cost. |

## Parity notes

- Vanilla `AddGeneratedCardsToCombat` processes cards one at a time: `History.CardGenerated`, `Add`, `AfterCardGeneratedForCombat`.
- When combat is ending but still in progress, vanilla keeps that sequence: `Add` returns a failed pile result, and
  the guarded `AfterCardGeneratedForCombat` listener iteration runs no listeners. The simulator keeps both history
  entries, but the resolved entry has no preview snapshot; projection therefore omits the card tip and causal effect.
- Because `Add` itself dispatches `AfterCardEnteredCombat` and `AfterCardChangedPiles`, the generated-card order is: generated history, combat entry, pile changed, generated-for-combat.
- For each card in `AddGeneratedCardsToCombat`, the simulator appends a
  `CombatPredictionCardGeneratedEntry`, attempts to add the card, runs its generation hooks, and then
  appends the matching `CombatPredictionCardGenerationResolvedEntry`. Its `PreviewCard` is a post-hook snapshot when
  pile insertion succeeded and `null` when insertion failed.
- Hook dispatch currently iterates only the live `CombatState` listeners; cards generated only inside the simulator are not included as later hook listeners.
- StS2 v0.110.0 moved the once-per-turn behavior from `PillarOfCreationPower` to `Regalite`.
  The mirror seeds `Regalite`'s prediction-local flag from its live `_usedThisTurn` state, while
  `PillarOfCreationPower` now gains block for every eligible card in a multi-card generation batch.

## Mock model list

- None.
