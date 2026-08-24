# Card OnPlay mirror

## Vanilla entry

StS2 v0.109.0 runs each concrete `CardModel.OnPlay(PlayerChoiceContext, CardPlay)` from the card-play lifecycle. The lifecycle owns resource spending, play/result piles, wrapper hooks, enchantments, afflictions and history; the model-specific `OnPlay` body issues the card's commands.

Combat prediction never invokes the real virtual method. `CombatPredictionSimulator.ManualPlay` creates a shadow `CardPlay`, then `CardOnPlayMirrors` dispatches the exact or inferred mirror against the mutable predicted card without mutating the real card, piles, creatures or RNG.

After that dispatch, the simulator invokes the mutable preview's `EnchantmentModel.OnPlay` and `AfflictionModel.OnPlay` mirrors in vanilla order, including the owner-death boundary after each method. Their registrations and limitations are documented in [enchantment-on-play.md](enchantment-on-play.md) and [affliction-on-play.md](affliction-on-play.md).

## Dispatch priority

`CardOnPlayMirrors` uses one exact-runtime-type `MethodMirrorRegistry<CardModel, CardOnPlayMirrorContext>` with this fixed priority:

1. An explicitly registered exact handler is `Handled`.
2. A reviewed non-gameplay mod override is `Ignored`.
3. `CardOnPlayInferrer` may classify an unregistered gameplay override as `Inferred`; when
   `PredictBaseGameCardsOnly` is enabled, it rejects non-base-game runtime types before inspecting their IL.
4. Every other gameplay override is `Unsupported`.

Exact handlers always win and are never combined with inferred behavior. `CanMirror` accepts only `Handled`, so the
default combat-card prediction entry opens sessions only for exact registrations. The experimental best-effort card
play setting bypasses that entry gate and enables the registry's `AllowInference` policy. With it enabled, every
dispatch kind may enter the shadow card-play lifecycle: `Inferred` executes its inferred handler and records
`MethodMirrorIncomplete`; `Unsupported` skips the unknown `OnPlay` body and records `MethodNotMirrored`;
`NotOverridden` has no override to simulate, and `Ignored` is intentionally skipped. Resource spending, result-pile
movement, exhaust hooks and other supported lifecycle effects still run around those bodies. `CanMirror` checks the
explicit registration table directly, so the default root gate does not analyze or cache unregistered types.

The settings are synchronized at runtime. Changing the general inference switch updates `AllowInference`, which clears
resolved Type lookups only when its value changes. Changing `PredictBaseGameCardsOnly` explicitly invalidates the
inference lookup cache because the inferrer reads that external policy. Root Mod cards are rejected at the combat-card
prediction entry before a session or simulator is created. Nested Mod card plays still execute their surrounding
shadow lifecycle, but their unregistered `OnPlay` overrides resolve as `Unsupported` instead of using best-effort
inference. Clearing the cache in both policy paths prevents previously cached inferred or unsupported results from
bypassing the current setting.

## General inference

The inferrer inspects original IL through RitsuLib's `GetOriginalIl()`. Async `OnPlay` methods resolve to their generated `MoveNext` body, and the returned ordered direct call targets form a Type-level classification that the registry caches with its handler. Instance values such as upgrade state, dynamic vars and selected target are resolved only when the cached handler runs.

The general inferrer currently recognizes three direct templates:

| Candidate | Recognized IL shape | Mirrored behavior |
| --- | --- | --- |
| Attack | Direct `AttackCommand.Execute` call | Builds an attack from `CalculatedDamage`, `Damage` or `OstyDamage`, applies optional `Repeat`, and targets a single, all or random enemy according to the card. |
| Block | Direct `CreatureCmd.GainBlock` call | Uses `CalculatedBlock` or `Block`. Self-target cards and enemy-targeting attack cards gain block on the owner; `AnyAlly` uses the selected ally; `AllAllies` uses all living player teammates. |
| Owner draw | A supported `CardPileCmd.Draw` call-site recipe | Draws a fixed one card or the standard `Cards` value for the owner from shadow piles, including shuffle and draw hooks. |

Candidates are deduplicated by effect kind and executed in their first direct-call order, so multiple direct calls of
the same recognized kind produce one general effect. Missing standard vars, unsupported targets and an unavailable
Osty skip the affected general action and retain incomplete risk.

Inferred execution also has a runtime exception boundary for Mod-defined or otherwise unexpected dynamic-var shapes.
It logs and captures the exception, stops the remaining inferred `OnPlay` actions, and allows the surrounding shadow
card-play lifecycle to continue. The registry records `MethodMirrorIncomplete` before entering the inferred handler,
so a contained failure remains visible as prediction risk.

The owner-draw matcher accepts the two-argument one-card overload and the four-argument overload when the count is a
decimal constant of one, direct `DynamicVars.Cards.BaseValue`/`IntValue`, or the compiler-generated async state field
used by cards such as `Prepared`. The player slot must end in a `CardModel.Owner` getter and the four-argument
`fromHandDraw` flag must be false. `DrawWithoutBlockingOnOtherPlayers`, target-player draws, calculated counts and
nonstandard vars are rejected.

This is a narrow local call-site recipe rather than general stack or control-flow interpretation. In particular, it
does not trace the receiver of the `Owner` getter back to the current card. Its conditional-draw rejection only
recognizes a conditional branch immediately adjacent to argument preparation (while excluding the compiler's initial
async state dispatch); outer conditions and loops can be missed.

## Exact draw mirrors

Draw shapes outside the owner-only rule remain exact registrations:

| Shape | Exact cards | Prediction behavior |
| --- | --- | --- |
| Ally draw | `Constellation`, `HuddleUp` | Resolves the selected player or all living player teammates from shadow state. |
| Context-derived count | `CompileDriver`, `Scrawl` | Counts distinct shadow orbs or current shadow hand capacity. |
| Conditional draw | `Fetch`, `Ftl`, `Impatience`, `Restlessness` | Evaluates the card-specific completed-play history, Osty or shadow-hand predicate before drawing. Live entries are combined with `CombatPredictionCardPlayFinishedEntry` records written at vanilla's `CardPlayFinished` point. |
| Draw-result follow-up | `EscapePlan`, `Expertise`, `Pillage`, `Scrape` | Uses the actual shadow cards returned by draw to apply conditional block, retain, repeated draw or discard. |

These handlers take priority over inference, including when their original IL also happens to match an owner-draw
recipe. `CombatPredictionSimulator.Draw` returns the drawn `PredictedCard` objects for these follow-up mirrors while
preserving the existing history and hook order.

Beat Down retains its own per-selected-card ending check before resolving each random target. Relying only on the
shared auto-play command would incorrectly consume an extra `CombatTargets` RNG value after an earlier selected card
ends combat.

## Deliberate limits and risks

- Only calls directly present in the original `OnPlay`/`MoveNext` body are considered. Same-card helpers, virtual/interface dispatch, delegates, reflection and arbitrary transitive calls are not followed.
- Original IL does not include behavior introduced by Harmony prefixes, postfixes or transpilers.
- Direct attack and block calls may still be conditional. General inference does not reconstruct arbitrary control flow, so a structurally inferred candidate may execute in a state where vanilla would skip it. The draw check rejects only a narrow adjacent-branch shape; reviewed conditional draw cards use exact mirrors.
- General attack, block and owner-draw parameter resolution is intentionally limited to standard dynamic-var, count and target templates. Calculated special values, dependent command results and nonstandard targeting require exact mirrors.
- Direct `Cards.IntValue` recipes reuse `Cards.BaseValue` when the cached action executes. Vanilla card-count vars are integral, but a Mod card with a fractional value could differ because the simulator applies draw-count ceiling instead of first truncating to `IntValue`.
- Inference does not imply the complete `OnPlay` was mirrored. Power application, HP loss, energy, card movement/generation and other commands remain omitted unless an exact handler covers the card.
- A recognized attack or block whose runtime card lacks a supported damage/block var or target shape skips that general action and records incomplete risk; its Type-level classification remains cached.

These limits are intentional. Expanding inference should add narrowly named, offline-verifiable templates rather than evolve into a general IL interpreter.

## Maintenance

Keep exact registrations in `CardOnPlayMirrors.CreateRegistry` and register the single general inferrer after them. The
best-effort setting controls whether the registry may infer unregistered types; the compatibility setting is checked
inside the inferrer and additionally blocks root Mod-card prediction. Any new setting read by the inferrer must
explicitly invalidate its lookup cache when changed. When adding an inferred template, match an unambiguous original
command, define conservative instance-time parameter and target resolution, preserve call order where RitsuLib exposes
it, add positive and negative offline samples, and document omitted control flow. New templates must continue to clone
RNG and mutate only prediction-owned state.
