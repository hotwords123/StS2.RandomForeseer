# Card OnPlay mirror

## Vanilla entry

StS2 v0.113.0 runs each concrete `CardModel.OnPlay(PlayerChoiceContext, CardPlay)` from the card-play lifecycle. The lifecycle owns resource spending, play/result piles, wrapper hooks, enchantments, afflictions and history; the model-specific `OnPlay` body issues the card's commands.

Combat prediction never invokes the real virtual method. `CombatPredictionSimulator.ManualPlay` creates a shadow `CardPlay`, then `CardOnPlayMirrors` dispatches the exact or inferred mirror against the mutable predicted card without mutating the real card, piles, creatures or RNG.

After that dispatch, the simulator invokes the mutable preview's `EnchantmentModel.OnPlay` and `AfflictionModel.OnPlay` mirrors in vanilla order, including the owner-death boundary after each method. Their registrations and limitations are documented in [enchantment-on-play.md](enchantment-on-play.md) and [affliction-on-play.md](affliction-on-play.md).

## Dispatch priority

`CardOnPlayMirrors` uses one exact-runtime-type `MethodMirrorRegistry<CardModel, CardOnPlayMirrorContext>` with this fixed priority:

1. An explicitly registered exact handler is `Handled`.
2. A reviewed non-gameplay mod override is `Ignored`.
3. `CardOnPlayInferrer` may classify an unregistered gameplay override as `Inferred`; when
   `PredictBaseGameCardsOnly` is enabled, it rejects non-base-game runtime types before inspecting their IL.
4. Every other gameplay override is `Unsupported`.

Exact handlers always win and are never combined with inferred behavior. `InferCardOnPlayEffectsEnabled` is a regular,
default-enabled setting under card resolution scope; it controls the registry's `AllowInference` policy. The
combat-card prediction entry does not require an exact registration. `CanMirror` only queries exact registrations
and is not used to gate that entry.

Every dispatch kind may enter the shadow card-play lifecycle, subject to the usual prediction, compatibility,
targeting and playability checks. `Inferred` executes its inferred handler and records `MethodMirrorIncomplete`;
`Unsupported` skips the unknown `OnPlay` body and records `MethodNotMirrored`; `NotOverridden` has no override to
simulate, and `Ignored` is intentionally skipped. Disabling inference leaves unregistered gameplay overrides
unsupported; it does not prevent their surrounding lifecycle from running. Resource spending, result-pile movement,
exhaust hooks and other supported lifecycle effects still run around those bodies.

The settings are synchronized at runtime. Changing the general inference switch updates `AllowInference`, which clears
resolved Type lookups only when its value changes. Changing `PredictBaseGameCardsOnly` explicitly invalidates the
inference lookup cache because the inferrer reads that external policy. Root Mod cards are rejected at the combat-card
prediction entry before a session or simulator is created. Nested Mod card plays still execute their surrounding
shadow lifecycle, but their unregistered `OnPlay` overrides resolve as `Unsupported` instead of using best-effort
inference. Clearing the cache in both policy paths prevents previously cached inferred or unsupported results from
bypassing the current setting.

## General inference

The inferrer inspects original IL through RitsuLib's `GetOriginalIl()`. Async `OnPlay` methods resolve to their generated `MoveNext` body, and the returned ordered direct call targets form a Type-level classification that the registry caches with its handler. Instance values such as upgrade state, dynamic vars and selected target are resolved only when the cached handler runs.

The general inferrer currently recognizes five direct templates:

| Candidate | Recognized IL shape | Mirrored behavior |
| --- | --- | --- |
| Attack | Direct `AttackCommand.Execute` call | Builds an attack from `CalculatedDamage`, `Damage` or `OstyDamage`, reads optional `HitCount`, `Repeat` or `CalculatedHits` in that priority order (otherwise energy/star X), and targets a single, all or random enemy according to the card. |
| Block | Direct `CreatureCmd.GainBlock` call | Uses `CalculatedBlock` or `Block`. Self-target cards and enemy-targeting attack cards gain block on the owner; `AnyAlly` uses the selected ally; `AllAllies` uses all living player teammates. |
| Owner draw | A supported `CardPileCmd.Draw` call-site recipe | Draws a fixed one card or the standard `Cards` value for the owner from shadow piles, including shuffle and draw hooks. |
| Vulnerable application | A direct, unguarded `PowerCmd.Apply<VulnerablePower>` call | Resolves the standard `VulnerablePower`/`Power` amount and target shape, then enters the shared shadow `PowerCmd.Apply` boundary. Artifact can consume the application before `AfterPowerAmountChanged`; an active Vicious then draws through the existing shadow draw pipeline. |
| Weak application | A direct, unguarded `PowerCmd.Apply<WeakPower>` call | Resolves the standard `WeakPower`/`Power` amount and target shape through the same power boundary, including Artifact consumption. |

Candidates are deduplicated by effect kind and executed in their first direct-call order, so multiple direct calls of
the same recognized kind produce one general effect. Weak and Vulnerable are distinct kinds, preserving their direct
call order. Unsupported targets and an unavailable Osty skip the affected general action and retain incomplete risk.
Missing attack/block/draw vars also skip that action; power inference retains its one-stack fallback when neither the
power-specific var nor `Power` exists.

This covers `Comet`, `FallingStar`, `GammaBlast`, `Uppercut`, `KnowThyPlace`, `Putrefy` and `MeteorShower` without exact
registrations. Meteor Shower's two group applications retain Weak on all enemies before Vulnerable on all enemies.
Inference does not reconstruct per-target loops; Shockwave requires an exact mirror to preserve that different order.

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

## Exact Vulnerable mirrors

These v0.111.0 cards need command ordering, conditions or dependent amounts beyond general inference:

| Cards / 中文名 | Exact coverage |
| --- | --- |
| `Shockwave` / 震荡波 | Weak then Vulnerable for each enemy. |
| `Expose` / 暴露 | Lose shadow block, clear existing Artifact's shadow amount, then apply Vulnerable. |
| `Dominate` / 主宰 | Apply Vulnerable, then grant Strength using the resulting shadow amount, including a newly returned instance. |
| `HighFive` / 击掌 | Skip the entire effect without living Osty; otherwise attack from Osty and apply Vulnerable to all enemies. |
| `MoltenFist` / 熔融之拳 | Attack, then double existing shadow Vulnerable only if the target survives and has positive stacks. |
| `MadScience` / 疯狂科学 | Attack + Sapping variant uses its custom Weak/Vulnerable variables after attacking. The existing Skill + Chaos variant remains supported; other variants retain incomplete risk. |
| `Nail` / 钉住 | Attacks, then applies Weak and Vulnerable only when the selected target is currently impaled. Reads live arrow/power state without changing it. |
| `Uproot` / 连根拔起 | Applies Vulnerable only when the selected target currently holds the owner's arrow. Dislodging arrows and its hooks are intentionally omitted with incomplete risk. |

Exact card coverage does not eliminate the [power command limitations](power-apply.md). Newly applied powers remain
absent from later collection lookups: for example, Molten Fist cannot find Vulnerable first created by an earlier
predicted card, and repeated Dominate cannot accumulate such a new instance. Existing instances share updated shadow
amounts. Expose omits Artifact's removal lifecycle, and its Hand Drill follow-up remains risk-only under the
[block hook coverage](../hooks/block-hooks.md). Original value hooks that read live power amounts still miss these
shadow changes. Successful power changes retain incomplete risk.

## Exact draw mirrors

Draw shapes outside the owner-only rule remain exact registrations:

| Shape | Exact cards | Prediction behavior |
| --- | --- | --- |
| Ally draw | `Constellation`, `HuddleUp` | Resolves the selected player or all living player teammates from shadow state. |
| Context-derived count | `CompileDriver`, `Scrawl` | Counts distinct shadow orbs or current shadow hand capacity. |
| Conditional draw | `Fetch`, `Ftl`, `Impatience`, `Restlessness` | Evaluates the card-specific completed-play history, Osty or shadow-hand predicate before drawing. Live entries are combined with `CombatPredictionCardPlayFinishedEntry` records written at vanilla's `CardPlayFinished` point. |
| Draw-result follow-up | `EscapePlan`, `Pillage`, `Scrape` | Uses the actual shadow cards returned by draw to apply conditional block, repeated draw or discard. |
| Live arrow condition | `KiHop`, `Wrest` | Ki Hop snapshots the target's current impaled condition before attacking, then conditionally draws. Wrest draws only when the target currently holds the owner's arrow, without dislodging it; omitted dislodge effects retain risk. |
| Melody-derived count | `SteadyCadence` / 平稳韵律 | Attacks, then computes draw count from CalculationBase/CalculationExtra and the distinct Wah/Cho/Ki tags in current-turn live plus shadow finished plays. |
| Discard then draw | `CalculatedGamble`, `WheelOfFortune` / 命运之轮 | Snapshots the current shadow hand before discard hooks and their nested effects. Wheel of Fortune uses its fixed draw count, including when the hand is empty. |

These handlers take priority over inference, including when their original IL also happens to match an owner-draw
recipe. `CombatPredictionSimulator.Draw` returns the drawn `PredictedCard` objects for these follow-up mirrors while
preserving the existing history and hook order.

`Expertise` / 独门技术 and `Joy` / 喜悦 have no exact handler. They follow the general inference/unsupported policy:
an inferred owner draw does not include RetainHandPower or energy gain. Expertise's hand-retention effect needs a
complete power/listener domain; it is not approximated with per-card Retain flags.

## Exact card generation and random selection

Generation handlers retain the shared combat card-pool filtering, detached previews, cloned RNG and generation/pile
hook order. The following cards use those existing domains:

| Cards / 中文名 | Exact coverage |
| --- | --- |
| `TheHierophant` / 教皇 | Distinct colorless cards from `CombatCardGeneration`; applies each card's temporary -1 energy cost immediately before adding that card to hand. |
| `TheLovers` / 恋人 | Generates enough cards to fill the shadow hand from the other unlocked character pools, with the original own-pool fallback and upgrade handling. |
| `TheFool` / 愚者 | Repeatedly shuffles the current playable hand with `CombatCardSelection`, falls back to all hand cards, then auto-plays the selected card through the existing lifecycle. |
| `TheWorld` / 世界 | Randomly returns discard cards to hand with `CombatCardSelection`, capped by the card value and current hand capacity. |
| `KiChant` / 叽 唱诵 | Performs necessary shadow shuffling, preserves the original remaining draw-pile top order, and records the top 3/4 cards as selection candidates. Stops at unresolved choice; does not fabricate the chosen card or subsequent discards. |
| `BlotOutTheSky` / 遮天蔽日 | Generates one distinct Shoot-tag card from the owner's unlocked character pool using CombatCardGeneration, makes it free this turn, then adds it through the existing generation pipeline. Does not grant or change arrow slots; the omitted non-native first-slot grant retains risk. |
| `ChoChorus` / 啾 副歌 | Gains block, snapshots Wah/Ki cards in the current shadow hand, then auto-plays them in order. Each child's mirror determines its own coverage. |
| `TheDevil` / 恶魔 | Exhausts a hand snapshot, then batch-selects that many cards from draw-pile top before auto-playing them. The upgraded version upgrades each selected card before its movement into Play. |
| `TheMagician` / 魔术师 | Calls simulator OrbAddSlots, which owns the combat-ending guard and ten-slot limit, then selects random orbs with cloned CombatOrbGeneration and channels them. Ending combat skips slot growth/channeling but does not skip the random selections. |

`TheMoon` / 月亮, `TheSun` / 太阳, `Rummage` / 翻找 and `TheHermit` / 隐者 have no exact registrations. Fixed-card
generation, energy and discard/exhaust choices are not covered by a recognized owner-draw recipe. These cards retain
the general inference/unsupported boundary rather than claiming complete or choice-aware exact support.

`BeatDown`, `Catastrophe` and `Uproar` also select cards with `CombatCardSelection`. Their shuffles are local random
selection operations; actual pile shuffling continues to use `Shuffle`. Each auto-play iteration observes pile
changes and nested effects from the preceding play.

## Miscellaneous exact mirrors

`MiscCardMirrors` groups the `Alchemize`, `MadScience` and `SecondWind` entry points. Mad Science dispatches its
supported variants by rider: Attack + Sapping to `VulnerableCardMirrors`, and Skill + Chaos to
`CardGenerationCardMirrors`. Other variants retain incomplete risk.

`SecondWind` snapshots every non-Attack card in the shadow hand, then exhausts and grants Block for each card in
vanilla order. Each exhaust dispatches the existing `AfterCardExhausted` mirror family, so `DarkEmbracePower` draws
from the shadow pile and the resulting cards are included in the play prediction without joining the current
Second Wind snapshot. A repeated Second Wind play takes a fresh snapshot and can therefore exhaust cards drawn by an
earlier play.

## Exact multi-hit attack mirrors

`AstralPulse`, `DaggerSpray`, `TwinStrike`, `Ricochet`, `RipAndTear` and `SwordBoomerang` retain exact registrations
but share `GeneralAttackOnPlay`, which recognizes `HitCount` before the older count shapes. These reviewed cards
remain supported with inference disabled and do not gain inference-only risk. The handler reads the local count
rather than `PreviewValue`; the shared attack pipeline applies hit-count hooks once.

Multi-hit attacks whose count or follow-up effects cannot be reconstructed by the general attack template use the
central `MultiHitAttackCardMirrors` registration group:

| Shape | Exact cards | Prediction behavior |
| --- | --- | --- |
| Conditional count | `Dismantle`, `HeavenlyDrill`, `Spite` | Reconstructs target Vulnerable, the doubled energy-X threshold, and the current-turn HP-loss condition. |
| Shadow card/orb count | `Barrage`, `Flechettes` | Counts the owner's shadow orb queue or shadow hand rather than querying the detached preview's live collections. |
| Live plus shadow history count | `Finisher`, `HelixDrill`, `LunarBlast`, `PullFromBelow`, `Radiate`, `Rattle`, `TearAsunder` | Combines matching vanilla history with prediction-local card-play, energy, star, attack, or damage entries. Current-turn queries use the simulator combat state instead of the detached card's null `CombatState`. |
| Multi-step lifecycle | `FiendFire`, `GunkUp`, `Maul`, `SovereignBlade` | Exhausts a snapshot of the shadow hand before attacking, generates the fixed `Slimed`, grows all shadow `Maul` copies, or resolves Seeking Edge targeting and Parry block. |

`Dismantle`, `GunkUp`, `Maul`, `SovereignBlade` and `Spite` read `DynamicVars.HitCount` in their dedicated logic;
the selection handlers for `Thrash` and `Uproar`, and the orb handler for `Refract`, do the same. Refract's separate
orb-channel count still uses `Repeat`. `CalculatedHitCountVar`
inherits `CalculatedVar`, but its multiplier can still depend on live piles or combat history, so the relevant
handlers retain prediction-aware hit-count calculations.

Helix Drill subtracts the current unfinished play's actual `Resources.EnergySpent` from the combined energy history.
It does not subtract the energy value used for X or free-play resolution. Fiend Fire still snapshots and exhausts
the actual shadow hand before attacking; its new calculated hit-count var does not replace that command sequence.

These handlers never call `CalculatedVar.Calculate` on the detached card. `Dismantle` and `SovereignBlade` still read
the target/owner's live power collection because power application is not a simulator-owned state domain; an earlier
unsupported power application already records prediction risk. VFX, SFX and waits are intentionally omitted.

Beat Down retains its own per-selected-card ending check before resolving each random target. Relying only on the
shared auto-play command would incorrectly consume an extra `CombatTargets` RNG value after an earlier selected card
ends combat.

## Special exact handlers

`Timber` / 大树将倾 resolves its selected-target attack first and performs a separate all-enemy attack with
OnKillDamage only when the first attack reports a kill. `WahBlast` / 哇 爆破 attacks all opponents once or twice
according to whether Cho or Ki appears in live plus shadow finished-play history. `WahHammer` / 哇 战锤 shares the
general attack handler but has an exact damage multiplier adapter that matches original/preview source identity
and reads the target's current live ImpaledPower.

These arrow-related handlers intentionally freeze arrow and impaled-power state at the real combat state. They do
not shoot, dislodge, return or destroy arrows, update hit counters, or introduce arrow lifecycle callbacks. A chain
that would change those states in vanilla may therefore diverge; missing side-effect hooks retain existing risk.

`EchoingSlash` and `Omnislice` are explicitly registered because their `OnPlay` bodies group multiple damage calls
inside vanilla `AttackContext`, which the general attack inference cannot represent. Their handlers use
`SimAttackContext` to dispatch the shared attack hooks and mirror each card's conditional or chained hit logic.

Omnislice's spread uses `ValueProp.Move` with only the cap damage hooks. It carries the first hit's resolved damage
without applying ordinary additive or multiplicative modifiers again, while retaining powered damage and Thorns
semantics. The spread excludes the original selected target, matching vanilla even when the first hit is redirected.

## Deliberate limits and risks

- Only calls directly present in the original `OnPlay`/`MoveNext` body are considered. Same-card helpers, virtual/interface dispatch, delegates, reflection and arbitrary transitive calls are not followed.
- Original IL does not include behavior introduced by Harmony prefixes, postfixes or transpilers.
- Direct attack and block calls may still be conditional. General inference does not reconstruct arbitrary control flow, so a structurally inferred candidate may execute in a state where vanilla would skip it. The draw check rejects only a narrow adjacent-branch shape; reviewed conditional draw cards use exact mirrors.
- General attack, block and owner-draw parameter resolution is intentionally limited to standard dynamic-var, count and target templates. Calculated special values, dependent command results and nonstandard targeting require exact mirrors.
- Direct `Cards.IntValue` recipes reuse `Cards.BaseValue` when the cached action executes. Vanilla card-count vars are integral, but a Mod card with a fractional value could differ because the simulator applies draw-count ceiling instead of first truncating to `IntValue`.
- Inference does not imply the complete `OnPlay` was mirrored. Outside the narrow Weak/Vulnerable templates, power application, HP loss, energy, card movement/generation and other commands remain omitted unless an exact handler covers the card.
- The power-application boundary mirrors hook order and prediction-owned listener effects but does not yet maintain a complete shadow power collection. It therefore supports existing Artifact/Vicious state while recording mirror risk for unregistered power lifecycle or action-hook overrides.
- Arrow/ArrowSlot state and lifecycle are not advanced. The explicitly supported arrow conditions read current live state, while Melody queries combine live and shadow card-play history without separate mutable counters. The complete transformations used by `MourningBlow` / 悲痛一击 and `PunchOfJoy` / 喜悦之拳 remain outside exact coverage; a recognized attack does not imply their transformation lifecycle is supported.
- A recognized attack or block whose runtime card lacks a supported damage/block var or target shape skips that general action and records incomplete risk; its Type-level classification remains cached.

These limits are intentional. Expanding inference should add narrowly named, offline-verifiable templates rather than evolve into a general IL interpreter.

## Maintenance

Keep exact registrations in `CardOnPlayMirrors.CreateRegistry` and register the single general inferrer after them.
`InferCardOnPlayEffectsEnabled` controls whether the registry may infer unregistered types; the compatibility setting
is checked inside the inferrer and additionally blocks root Mod-card prediction. Any new setting read by the inferrer must
explicitly invalidate its lookup cache when changed. When adding an inferred template, match an unambiguous original
command, define conservative instance-time parameter and target resolution, preserve call order where RitsuLib exposes
it, add positive and negative offline samples, and document omitted control flow. New templates must continue to clone
RNG and mutate only prediction-owned state.
