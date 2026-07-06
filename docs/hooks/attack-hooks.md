# Attack command hooks

Mirror files: `InCombat/Hooks/AttackHooks.cs`, `InCombat/Simulation/CombatPredictionSimulator.Attack.cs`, `InCombat/Simulation/CombatPredictionSimulator.History.cs`.

`CombatPredictionSimulator.ExecuteAttack(AttackCommand)` mirrors the prediction-relevant `AttackCommand.Execute` target loop and dispatches targeted attack hook mirrors. Per-hit damage is still delegated to `CombatPredictionSimulator.Damage`. Callers must push the attack's card/monster source before invoking `ExecuteAttack`; the method itself only pushes hook listeners through `AttackHooks`.

Per-hit `CreatureCmd.Damage` modifier and result hooks are documented in `damage-modifier-hooks.md` and `damage-hooks.md`.

## Vanilla order

`AttackCommand.Execute(PlayerChoiceContext?)` performs the following gameplay-relevant work:

1. Rejects missing attacker/targets, ending combat, or dead attacker.
2. `Hook.BeforeAttack(combatState, command)`.
3. `Hook.ModifyAttackHitCount(combatState, command, _hitCount)`.
4. For each hit:
   - Stops if the attacker dies.
   - Refreshes possible targets and filters to living targets. Multi-target attacks re-read opponents each hit.
   - Stops when there are no valid targets in live combat.
   - For random targeting, consumes `RunState.Rng.CombatTargets` once per hit and optionally removes previous receivers when duplicates are disallowed.
   - Runs visuals, waits, `_afterAttackerAnim`, and `_beforeDamage`.
   - Calls `CreatureCmd.Damage(...)` with either the selected random/single target or the full valid-target list, `DamageProps`, `Attacker`, `ModelSource as CardModel`, and `CardPlay`.
   - Appends the returned `DamageResult` list to `AttackCommand.Results`.
5. Records `CombatManager.Instance.History.CreatureAttacked(...)`.
6. `Hook.AfterAttack(combatState, choiceContext ?? new BlockingPlayerChoiceContext(), command)`.

`AttackContext` is a separate vanilla path used by `EchoingSlash` and `Omnislice`:

1. `AttackContext.CreateAsync(...)` creates an `AttackCommand(0).FromCard(cardPlay.Card, cardPlay).TargetingAllOpponents(combatState)` and calls `Hook.BeforeAttack`.
2. The card manually calls `CreatureCmd.Damage` one or more times and passes each returned hit list to `AttackContext.AddHit`.
3. `DisposeAsync` calls `Hook.AfterAttack`.

`AttackContext` does not run `ModifyAttackHitCount`, target selection, random targeting, visuals, waits, or `_beforeDamage`; those are owned by the custom card implementation.

## Hook specs

- `AbstractModel.BeforeAttack(AttackCommand)`
- `AbstractModel.ModifyAttackHitCount(AttackCommand, int)`
- `AbstractModel.AfterAttack(PlayerChoiceContext, AttackCommand)`

## BeforeAttack listeners

| Model | 中文名 | Original effect | Current status and feasibility |
| --- | --- | --- | --- |
| `GigantificationPower` | 超巨化 | Records the first matching powered Attack-card command so its damage multiplier applies to every hit of that attack, then consumes one stack in `AfterAttack`. | Partially mirrored. Trigger state is tracked prediction-locally so `AfterAttack` can mark consumption risk. Damage still comes from original `ModifyDamageMultiplicative`; shadow power decrement is unsupported. |
| `HellraiserPower` | 地狱狂徒 | For cards being auto-played by Hellraiser, changes attack VFX/animation settings. | Ignorable for prediction. It does not affect target, RNG, damage, block, card piles, or power state. |
| `VigorPower` | 活力 | Records the first matching powered attack command and starting Vigor amount so all hits of that attack receive the bonus, then removes that starting amount in `AfterAttack`. | Partially mirrored. Trigger state is tracked prediction-locally so `AfterAttack` can mark consumption risk. Damage still comes from original `ModifyDamageAdditive`; shadow power decrement is unsupported. |

## ModifyAttackHitCount listeners

No vanilla gameplay model currently overrides `ModifyAttackHitCount`.

The attack mirror dispatches a registry for this hook and marks unknown modded overrides risky, because the original hook sits between `BeforeAttack` and the per-hit target loop.

## AfterAttack listeners

| Model | 中文名 | Original effect | Current status and feasibility |
| --- | --- | --- | --- |
| `BoneFlute` | 骨笛 | When Osty attacks for the relic owner, owner gains block. | Implemented with simulator `GainBlock`. |
| `Flatten` | 重压 | When Osty attacks, this card's cost becomes 0 for the turn. | Implemented for cards present in simulator piles/previews. Live listener cards are not mutated. |
| `GigantificationPower` | 超巨化 | Clears the recorded command and decrements one stack if this was the selected attack. | Triggered cases are marked risky. Shadow power decrement/removal is unsupported. |
| `PainfulStabsPower` | 疼痛戳刺 | If owner dealt powered unblocked attack damage to player creatures, adds `Wound` cards to each affected player's discard pile based on unblocked hit count. | Implemented by generating prediction-local `Wound` cards and inserting them into simulated discard piles. |
| `SkittishPower` | 胆小 | Once per turn, when owner receives unblocked `Move` damage from a card attack, owner gains block. | Implemented with prediction-local per-turn trigger state initialized from live `HasGainedBlockThisTurn`, then simulator `GainBlock`. |
| `SuckPower` | 吮吸 | For each hit where owner dealt powered unblocked attack damage, applies Strength to owner. Pet/Osty trample duplicate results are collapsed before counting. | Triggered cases are marked risky. Apply Power unsupported until shadow power application exists. |
| `VigorPower` | 活力 | Removes the amount of Vigor that existed when the attack started. | Triggered cases are marked risky. Shadow power decrement/removal is unsupported. |

## Current attack simulation gaps

- `CombatPredictionSimulator.ExecuteAttack(AttackCommand)` now resolves targets, consumes cloned `CombatTargets` RNG, calls mirrored attack hooks, calls `Damage`, appends each hit through `AttackCommand.AddResultsInternal`, and records shadow attack history from `AttackCommand.Results`.
- Existing direct `Damage` calls still bypass `BeforeAttack`, `ModifyAttackHitCount`, `AfterAttack`, attack result grouping, and attack history. They are suitable for non-attack damage and partial hit previews, not full attack behavior.
- The simulator has shadow `AttackHistory`, but original value hooks and card model methods still read live `CombatManager.Instance.History`, not this shadow history.
- Original attack hooks must not be called directly during prediction. `VigorPower`, `GigantificationPower`, `Flatten`, `BoneFlute`, `SkittishPower`, and others mutate live power/card/creature state.
- Target parity covers cloned random targeting, per-hit refresh of shadow living opponents, single-target vs multi-target result shape, and duplicate-disallowed random targeting behavior. If a command has no attacker or no targets configured, the simulator marks risk, logs a warning, and returns no hits instead of throwing, preserving existing hover-risk callers.
- `CalculatedDamageVar.Calculate(target)` may read live combat state. The simulator calculates the value for parity, but marks the attack source risky.
- Reviewed vanilla `_beforeDamage` and `_afterAttackerAnim` callbacks are command-local cosmetic effects only: VFX/SFX, waits, screen shake, radial blur, hit stop, and audio-only strength fields. The simulator does not execute them and does not mark risk solely because a vanilla attack command contains these callbacks.
- Risk attribution assumes the caller has already pushed the attack source. The invalid-command entry checks record unknown risk when no source is active rather than inferring a source from `AttackCommand`; other risk paths still rely on the caller-owned source scope.
- Attack-scoped power consumption requires shadow power amount/removal support or trigger-scoped risk. Until then, chained attack predictions can overuse live `VigorPower`, `GigantificationPower`, `PenNib`, and similar state.
- `AttackContext` cards need a separate mirror shape. They share `BeforeAttack`/`AfterAttack`, but their hit generation is card-specific and does not pass through `AttackCommand.Execute`.

## Remaining implementation sequence

1. Add shadow power amount/removal support for `VigorPower`, `GigantificationPower`, and `SuckPower`.
2. Teach history-dependent value hooks and card logic to read simulator shadow attack/card-play history where prediction chains need it.
3. Re-review any new vanilla or modded `_beforeDamage` / `_afterAttackerAnim` callbacks if card-play prediction starts building real attack commands for those cards; current reviewed vanilla callbacks are cosmetic-only.
4. Add separate `AttackContext` mirrors for `EchoingSlash` and `Omnislice` if card-play prediction starts simulating their full attack bodies.

## Mock model list

- `MockGainBlockOnAttackPower` overrides `AfterAttack` in test support only.
