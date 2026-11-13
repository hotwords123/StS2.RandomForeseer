# Potion OnUse mirror

## Vanilla entry

StS2 v0.109.0 executes `PotionModel.OnUse(PlayerChoiceContext, Creature?)` inside `PotionModel.OnUseWrapper` after removing the potion and dispatching `Hook.BeforePotionUsed`. The wrapper also owns VFX, waits, combat effect bookkeeping, potion-use history, `Hook.AfterPotionUsed`, run history and the empty-hand check; those wrapper responsibilities are separate from the model-specific `OnUse` body.

`PotionModel.EnqueueManualUse` supplies the owner creature when the caller omits a target and that owner is valid, while `PotionModel.IsValidTarget` defines potion-specific target semantics. In particular, potion `TargetType.Self` receives the owner creature rather than the null target used by self-targeting cards.

## Prediction entry and dispatch

`PotionOnUseMirrors` uses one `MethodMirrorRegistry<PotionModel, PotionOnUseMirrorContext>` for exact-runtime-type dispatch. `CanMirror` is a read-only query; `Invoke` opens a method frame sourced by the original potion, executes a registered handler, or records `MethodNotMirrored` risk for an unsupported gameplay override.

`CombatPredictionSimulator.ManualUse` mirrors only target completion, target validation, the root `PredictionActionKind.PotionUse` frame and dispatch of the `OnUse` body. It does not call the real virtual method, `OnUseWrapper`, commands or an async choice context, so it cannot remove or queue the real potion, advance real RNG, mutate real combat state or run wrapper hooks. The returned root frame is a stable identity that must be paired only with the same simulator history.

## Implemented handlers

| Domain | Runtime types | Mirrored behavior |
| --- | --- | --- |
| Card generation options | `AttackPotion`, `SkillPotion`, `PowerPotion`, `ColorlessPotion` | Generates the three exact options with cloned `CombatCardGeneration`, records one options entry and then records unresolved player-choice risk. The unknown selected card and its hand insertion are not fabricated. |
| Card generation | `CosmicConcoction` | Generates the configured number of distinct colorless cards, upgrades them and adds them to the target shadow hand through generated-card history and hooks. |
| Card generation | `OrobicAcid` | Generates one Attack, Skill and Power in vanilla RNG order, makes each free this turn and adds them to the target shadow hand through generated-card history and hooks. |
| Card generation | `BottledSong` | Uses all unlocked character pools to generate one Wah, Cho and Ki card in that order with cloned `CombatCardGeneration`. Each independent selection follows the combat/run filters and full distinct shuffle, skips an empty category, and makes the result free this turn before adding the batch. This does not execute Melody or arrow effects. |
| Potion generation | `EntropicBrew` | Uses `Rng.ForPlayer(target).NonCombatGeneration` and the shared reward helper. The simulator caches a `CombatPredictionPlayerRngSet` per player, retaining the cloned stream across repeated calls. It records enough results to fill the entire target potion belt, preserving the existing presentation policy that the player may discard potions before use; potion slots and procurement hooks are not mutated. |
| Draw-pile autoplay | `DistilledChaos` | Selects cards from the top of the shadow draw pile, records each direct autoplay entry and executes each supported child card through the shared nested card-play path. |
| Draw | `BottledPotential` | Moves the shadow hand to draw, shuffles with cloned RNG and then draws the configured count. |
| Draw | `Clarity` | Draws the configured count. Its later power application cannot affect the displayed draw result and remains outside the unsupported power state domain. |
| Draw | `CureAll` | Applies the prediction-owned energy gain before drawing the configured count. |
| Draw | `GlowwaterPotion` | Exhausts the complete shadow hand through exhaust hooks and then draws the configured count. |
| Draw and cost randomization | `SneckoOil` | Draws first, then iterates the complete shadow hand in order and consumes cloned `CombatEnergyCosts` for each eligible non-X card. A final batch history entry snapshots all randomized hand cards for potion-draw presentation. |
| Draw | `SwiftPotion` | Draws the configured count. |
| Orb channel | `EssenceOfDarkness` | Reads the target shadow orb queue's initial capacity and channels that many Dark Orbs through the shared channel, evoke, hook and damage simulation. |

`CardGenerationPotionMirrors.Generate` is shared by combat OnUse simulation and out-of-combat unfair previews so card pools, RNG order and card mutations have one implementation; `AddsToHand` tells the combat adapter whether to record choice options or add every result through generated-card history and hooks.

`EntropicBrewMirrors.Generate` similarly shares the full-belt potion reward policy between combat history and out-of-combat HoverTips without fabricating combat state or mutating potion slots.

## Unsupported and intentionally omitted behavior

`CrystallineEuphoria` has no exact registration. Its fixed Joy/Sadness generation and resulting hooks are not
simulated; it follows the same unsupported boundary as other unregistered potions.

All unregistered exact potion runtime types remain unsupported and are rejected by `CanMirror`; direct invocation records `MethodNotMirrored` risk. `Hook.BeforePotionUsed` and `Hook.AfterPotionUsed` are not mirrored and must not be assumed to run around a predicted potion use. Potion removal, VFX, waits, combat effect bookkeeping, vanilla potion-use history, run history, empty-hand checks, potion-slot mutation and UI cost animations are intentionally outside this entry.

## Maintenance

Keep every concrete registration in the single `PotionOnUseMirrors` index and group implementations by result domain. When adding a handler, reproduce RNG consumption and prediction-relevant shadow state in vanilla order, use semantic history entries rather than presentation DTOs, and record explicit risk for any omitted behavior that can change a surfaced result. Every exact runtime type must be reviewed independently; inherited or modded overrides must not silently reuse another type's handler. Wrapper hooks or additional wrapper state must be reviewed and documented separately rather than added implicitly to `ManualUse`.
