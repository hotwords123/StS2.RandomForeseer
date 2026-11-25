# Damage modifier hooks

Mirror files: `InCombat/Mirrors/HookMirrors.cs`,
`InCombat/Mirrors/Hooks/Damage/ModifyDamageMirrors.cs`,
`InCombat/Mirrors/Hooks/Damage/ModifyHpLostMirrors.cs`,
`InCombat/Mirrors/Hooks/Damage/ModifyPreBlockDamageTargetMirrors.cs`,
`InCombat/Mirrors/Hooks/Damage/ModifyUnblockedDamageTargetMirrors.cs`,
`InCombat/Mirrors/Hooks/Damage/ShouldAllowHittingMirrors.cs`,
`InCombat/Mirrors/Hooks/Damage/AfterModifyingHpLostAfterOstyMirrors.cs`, and
`InCombat/Simulation/CombatPredictionSimulator.Damage.cs`.

This document covers the read-only damage modifier path used by `CombatPredictionSimulator.Damage`. Post-result hooks such as `AfterDamageReceived` are documented in `damage-hooks.md`.

## Vanilla order

In v0.114.0, `CreatureCmd.Damage` calculates and applies a result for each original target. It dispatches post-hit hooks after the target loop.

### Calculate the result

1. Run `Hook.ModifyDamage` for the original target, then `Hook.AfterModifyingDamageAmount` for the returned modifiers.
2. `CreatureCmd.GetDamageResults` calls `Hook.ModifyPreBlockDamageTarget` with the **modified** damage. If the hit
   redirects before block, run `Hook.ModifyHpLost` in the `BeforeWuppy` phase for the original target and calculate
   the redirect recipient's HP loss. Only its overkill reaches block.
3. Calculate block on `originalTarget.PetOwner?.Creature ?? originalTarget`. Run `ModifyHpLost` in the `BeforeOsty`
   phase for the original target, then choose the HP recipient with `Hook.ModifyUnblockedDamageTarget`.
4. Run `ModifyHpLost` in the `AfterOsty` phase for the HP recipient and calculate its HP loss. If it differs from
   the original target, run `AfterOsty` again on the original target for the recipient's overkill.

The HP and block calculations use `dryRun: true`. `DamageResults` holds the amounts, optional redirect results,
and the modifier lists for the application callbacks.

### Apply the result

`Hook.BeforeDamageReceived` runs for the original target with the modified damage after calculation and before any
HP or block loss from this hit. A nested hit triggered here can change current state. `CreatureCmd.ApplyDamageResults`
then applies the previously calculated amounts to that state in this order.

1. Apply HP loss to the pre-block redirect recipient, if present.
2. Apply block loss to the block redirect recipient, if present, then to the original target.
3. Call `Hook.AfterModifyingHpLostBeforeOsty` for the recorded `BeforeOsty` modifiers.
4. Apply HP loss to the unblocked redirect recipient, if present, then call `Hook.AfterModifyingHpLostAfterOsty` for
   the first `AfterOsty` pass.
5. Apply HP loss to the original target, then call `Hook.AfterModifyingHpLostAfterOsty` for the overkill pass, if present.

The simulator writes HP and block loss to prediction state. It dispatches `AfterModifyingHpLostAfterOsty` to the
modifiers recorded for each pass. The reviewed vanilla listeners for `AfterModifyingDamageAmount` and
`AfterModifyingHpLostBeforeOsty` only affect presentation, so the simulator skips those callbacks.

### Record results and dispatch post-hit hooks

After each target's result is applied, `CreatureCmd.Damage` uses `DamageResults.AllResults()` for history, in
order of original target, block redirect, pre-block redirect, and unblocked redirect. Original combat history
is written while combat is in progress and has yet to start ending.

After resolving all original targets, `CreatureCmd.Damage` iterates the accumulated results in the same order.
For each receiver it calls `AfterBlockBroken` if block broke, `AfterCurrentHpChanged` if HP was lost, and
`AfterDamageGiven` when a combat state exists. It then calls `AfterDamageReceived` or queues the receiver for
death processing if the result was lethal and the receiver is still dead. The queued deaths are processed
after the result loop. See [Damage hooks](damage-hooks.md) for details of the prediction dispatch.

## Hook specs

- `AbstractModel.ModifyDamageAdditive(Creature?, decimal, ValueProp, Creature?, CardModel?, CardPlay?)`
- `AbstractModel.ModifyDamageMultiplicative(Creature?, decimal, ValueProp, Creature?, CardModel?, CardPlay?)`
- `AbstractModel.ModifyDamageMultiplicativeLate(Creature?, decimal, ValueProp, Creature?, CardModel?, CardPlay?)`
- `AbstractModel.ModifyDamageCap(Creature?, ValueProp, Creature?, CardModel?, CardPlay?)`
- `AbstractModel.AfterModifyingDamageAmount(CardModel?)`
- `AbstractModel.ModifyPreBlockDamageTarget(Creature originalTarget, Creature? currentTarget, decimal, ValueProp, Creature?)`
- `AbstractModel.ModifyHpLostBeforeOsty(Creature, decimal, ValueProp, Creature?, CardModel?)`
- `AbstractModel.ModifyHpLostBeforeOstyLate(Creature, decimal, ValueProp, Creature?, CardModel?)`
- `AbstractModel.AfterModifyingHpLostBeforeOsty()`
- `AbstractModel.ModifyUnblockedDamageTarget(Creature, decimal, ValueProp, Creature?)`
- `AbstractModel.ModifyHpLostAfterOsty(Creature, decimal, ValueProp, Creature?, CardModel?)`
- `AbstractModel.ModifyHpLostAfterOstyLate(Creature, decimal, ValueProp, Creature?, CardModel?)`
- `AbstractModel.AfterModifyingHpLostAfterOsty()`
- `AbstractModel.ShouldAllowHitting(Creature)`

## ModifyDamage listeners

Current mirror status: the simulator preserves vanilla additive, ordinary multiplicative, late multiplicative, and
cap passes. `ModifyDamageHookType` controls the enchantment and listener phases; cap-only damage preserves caps
without applying bonuses again. It calls original read-only listener methods except for the prediction-state
consumers documented below.

### ModifyDamageAdditive listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `AccuracyPower` | 精准 | Owner's powered Shiv attacks gain flat damage. | Implemented by original hook. |
| `CalcifyPower` | 钙化 | Owner's Osty powered attacks gain flat damage. | Implemented by original hook. |
| `FakeStrikeDummy` | 打击木偶？？？ | Owner Strike-tag attacks gain flat damage. | Implemented by original hook. |
| `LeadershipPower` | 领袖气质 | Owner buffs allied powered attacks by flat damage. | Implemented by original hook. |
| `MiniatureCannon` | 微型大炮 | Owner upgraded-card powered attacks gain flat damage. | Implemented by original hook. |
| `MysticLighter` | 神秘打火机 | Owner enchanted-card powered attacks gain flat damage. | Implemented by original hook. |
| `OneForAllPower` | 一心化万 | Owner's powered non-X attacks gain flat damage when manual play spends zero energy or autoplay has zero EnergyValue; null-play previews check current modified cost. | Exact additive adapter distinguishes manual EnergySpent from autoplay EnergyValue; the null-play branch uses prediction-aware modified cost. |
| `PhantomBladesPower` | 幻影之刃 | Owner's first Shiv attack this turn gains flat damage. | Exact additive adapter combines live current-turn and predicted finished-card history, filtered by player and Shiv tag. Started-but-unfinished Shivs do not consume the bonus. |
| `StrikeDummy` | 打击木偶 | Owner Strike-tag attacks gain flat damage. | Implemented by original hook. |
| `StrengthPower` | 力量 | Owner powered attacks gain flat damage; negative amounts reduce damage. | Implemented by original hook. |
| `TaintedPower` | 污染 | Powered attacks against owner gain flat damage. | Implemented by original hook. |
| `VigorPower` | 活力 | Owner's next powered attack gains flat damage, usually scoped by `BeforeAttack`/`AfterAttack`. | Implemented by a prediction-aware additive adapter using the selected attack and shadow amount shared with the attack hooks. |
| `AimPower` | 瞄准 | Adds Aim to the selected powered arrow attack, including HeavyShot scaling. | Deferred with the arrow domain. Predictions still read live command/amount state for Aim, Impaled and arrow slots. |

### ModifyDamageMultiplicative listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `ColossusPower` | 巨像 | Powered attacks from Vulnerable dealers against owner are reduced. | Implemented by original hook. |
| `ConquerorPower` | 征服者 | `SovereignBlade` powered attacks against owner are doubled. | Implemented by original hook. |
| `CoveredPower` | 掩护 | Powered attacks against owner are reduced to zero. | Implemented by original hook. |
| `DoubleDamagePower` | 双倍伤害 | Owner or pet powered card attacks are doubled. | Exact multiplier reads the shadow amount decremented at owner side-end; active semantics delegate to the original hook. Expired listeners return 1; collection membership and generic removal callbacks remain deferred. |
| `FlankingPower` | 夹击 | Powered attacks against owner are multiplied unless dealt by applier. | Implemented by original hook. |
| `FlutterPower` | 振翅 | Powered attacks against owner are reduced by configured percentage. | Implemented by a prediction-aware multiplier that reads the shadow stack amount consumed by `AfterDamageReceived`; post-hit stun scope is covered in `damage-hooks.md`. |
| `GigantificationPower` | 超巨化 | Owner's powered attack card is tripled, usually scoped by `BeforeAttack`/`AfterAttack`. | Implemented by a prediction-aware multiplier using the selected attack and shadow amount shared with the attack hooks. |
| `GuardedPower` | 护卫 | Powered attacks against owner are halved. | Exact multiplier shares the shadow amount consumed on applier death; active semantics delegate to the original hook. Expired listeners return 1; collection membership and generic removal callbacks remain deferred. |
| `HangPower` | 吊杀 | `Hang` damage against owner is multiplied by amount. | Implemented by original hook. |
| `InterceptPower` | 拦截 | Powered attacks against owner are increased by covered-creature count. | Implemented by original hook. |
| `KnockdownPower` | 击倒 | Powered attacks against owner are multiplied unless dealt by applier. | Implemented by original hook. |
| `LethalityPower` | 致死性 | Owner's first Attack card this turn deals bonus powered damage. | Exact multiplier combines live current-turn and predicted started-card history, filtered by player and Attack type. Excludes the current Play-pile card from that count only for a non-null CardPlay; replay exclusion still uses its shadow pile and preview play index. |
| `PenNib` | 钢笔尖 | Owner's every tenth Attack card is doubled. | Prediction-aware multiplier uses the shadow counter and original card identity shared with the before/after card hooks, matching vanilla's card-identity selection. |
| `ShrinkPower` | 缩小 | Owner's powered attacks are reduced. | Exact multiplier reads shadow expiration, including negative infinite duration, shared with death and side-end hooks. Expired listeners return 1; collection membership and generic removal callbacks remain deferred. |
| `SlowPower` | 缓慢 | Powered attacks against owner scale up with cards played this turn. | Implemented by a prediction-aware multiplier reading the shadow card-play count. |
| `SoarPower` | 翱翔 | Powered attacks against owner are reduced by configured percentage. | Implemented by original hook. |
| `SurroundedPower` | 遭到包围 | Back attacks against owner are multiplied. | Implemented by a prediction-aware multiplier reading the facing updated by the simulated targeted card. |
| `WahHammer` | 哇 战锤 | Doubles its own damage against a target with ImpaledPower. | Exact adapter matches either original or preview source identity through PredictedCard.References, then reads the target's current live ImpaledPower membership. Arrow/power state is intentionally frozen during prediction; simulated hits do not update impalement. |
| `TankPower` | 肉盾 | Powered attacks against owner deal 50% more damage. | Implemented by original hook. |
| `TrackingPower` | 跟踪 | Owner or pet powered card attacks against Weak targets are multiplied. | Implemented by original hook. |
| `UndyingSigil` | 不死符文 | Incoming powered attacks from dealers whose Doom is at least their HP are reduced. | Original hook retained. Enemy attacks are outside the current prediction scope, so no dedicated shadow-HP adapter is needed. |
| `VitruvianMinion` | 维特鲁威仆从 | Owner Minion-tag card attacks are doubled. | Implemented by original hook. |
| `VulnerablePower` | 易伤 | Powered attacks against owner are multiplied, with Paper Phrog, dealer or pet owner's Cruelty, and Debilitate adjustments. | Implemented by original hook. StS2 v0.109.0 made attacking pets inherit their owner's Cruelty adjustment. |
| `WeakPower` | 虚弱 | Owner's powered attacks are reduced, with Paper Krane and Debilitate adjustments. | Implemented by original hook. |

### ModifyDamageMultiplicativeLate listeners

A fresh listener pass runs after ordinary multiplication and before caps, with the same unfiltered run-listener
enumeration as the other damage value passes. Vanilla currently has no concrete override; Mod value hooks follow
the normal read-only fallback policy.

### ModifyDamageCap listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `IntangiblePower` | 无实体 | Damage against owner is capped at 1 for block loss and previews. | Implemented by original hook. |

## ModifyHpLost listeners

Current mirror status: the phase-aware `HookMirrors.ModifyHpLost` mirrors the original phase flag and all four
BeforeOsty/AfterOsty listener passes. Exact registered consumers read prediction state while all other listeners
continue through their original read-only methods.

`HpLossHookPhase.BeforeWuppy` currently executes no HP-loss modifier in vanilla. The mirror references phases by
enum names rather than their numeric bit values.

### ModifyHpLostBeforeOstyLate listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `HardToKillPower` | 难以杀灭 | Caps the owner's HP loss by amount before Osty redirection. | Uses the original read-only HP-loss hook. |
| `HardenedShellPower` | 硬化外壳 | Caps owner's HP loss by remaining per-turn shell amount before Osty redirection. | Implemented by a prediction-aware late adapter using the per-turn damage counter shared with `AfterDamageReceived`. |

### ModifyHpLostAfterOsty listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `BeatingRemnant` | 律动残余 | Caps owner's per-turn HP loss while combat is in progress. | Prediction-aware adapter uses shadow `IsInProgress` and the per-turn damage counter shared with `AfterDamageReceived`. |
| `IntangiblePower` | 无实体 | Caps owner's HP loss to 1 while combat is in progress. | Exact AfterOsty adapter uses shadow `IsInProgress`; its unconditional damage-cap hook still uses the original method. |
| `SlipperyPower` | 滑溜 | Caps owner's HP loss to 1. | Implemented by a prediction-aware adapter that stops applying the cap after the shadow amount reaches zero. |
| `TungstenRod` | 钨合金棍 | Reduces owner's HP loss by configured amount. | Implemented by original hook. |

### ModifyHpLostAfterOstyLate listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `BufferPower` | 缓冲 | Sets owner's HP loss to 0, except damage sourced by BrightestFlame. | Implemented by a prediction-aware late adapter that stops applying after the shadow amount reaches zero; the original method supplies the source-card exception. |
| `TheBoot` | 发条靴 | Raises owner's powered unblocked attack damage below threshold to minimum damage. | Implemented by original hook. |

## ModifyPreBlockDamageTarget listeners

The original hook directly enumerates combat listeners without an ending guard, chaining a nullable pre-block
target from an initial null value. The facade accepts the original target, modified damage amount, props and
dealer, and returns the context's nullable pre-block target.

`WuppysLoyaltyPower` / 瓦皮的忠诚 has an exact adapter: powered attacks against its owner's player creature
redirect to Wuppy while its shadow HP is positive. A listener that does not redirect preserves the previous
nullable target; unregistered overrides retain unsupported risk.

The simulator calculates the pre-block target's HP loss using the BeforeWuppy phase for the original target. Only
that result's overkill reaches the block, BeforeOsty, Osty redirect and AfterOsty passes. The prediction runs
`BeforeDamageReceived` for the original target before applying the calculated results. For example, 20 damage
against a player with 8-HP Wuppy and 5 Block consumes Wuppy's 8 HP, then the player's 5 Block, leaving 7 damage
for Osty or the player. The pre-block result enters shadow history and post-damage/death dispatch after the
original target's result.

Wuppy's death retains its creature and loyalty power under the original removal predicates. Subsequent hits
observe zero shadow HP and stop redirecting. Combat-start summoning, relic counter persistence and rest-site
recovery remain outside this current-combat damage path; prediction starts from the existing pet's HP.

## ModifyUnblockedDamageTarget listeners

Current mirror status: `HookMirrors.ModifyUnblockedDamageTarget` preserves the original unguarded listener pass
and chains each returned target into the next listener. The exact registered adapter replaces the live liveness
read; unregistered listeners still call their original method in order.

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `DieForYouPower` | 为你而死 | Living Osty absorbs powered unblocked attack damage that would hit its owner. | Exact adapter reads shadow Osty liveness. Subsequent hits stop redirecting after simulated death. A living Osty's overkill still passes to the original target through the second AfterOsty pass. |

`ShouldAllowHitting` uses the shadow guarded pass when building hittable shadow targets. Both it and
`ModifyUnblockedDamageTarget` apply compatibility filtering. `ModifyDamage` is the deliberate exception: its
prediction-state-aware mirror retains Mod listeners so Mod damage semantics remain visible.

### ShouldAllowHitting listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `DieForYouPower` | 为你而死 | Rejects any dead creature, including Osty, for hitting/power receipt. | Exact adapter reads the queried creature's shadow liveness, preserving vanilla's global predicate. |
| `AdaptablePower` | 适者生存 | Rejects the owner while reviving. | Original predicate; simulated revive/transition remains unsupported and is reported by death hooks. |
| `IllusionPower` | 幻象 | Rejects the owner while reviving. | Original predicate; simulated revive/transition remains unsupported and is reported by death hooks. |
| `ReattachPower` | 接续 | Rejects the owner while reviving. | Original predicate; simulated revive/transition remains unsupported and is reported by death hooks. |
| `WuppysLoyaltyPower` | 瓦皮的忠诚 | Rejects any dead queried creature for hitting/power receipt. | Exact adapter reads queried-creature shadow liveness; pre-block redirection also stops after shadow death. |

## AfterModifying listeners

`AfterModifyingHpLostAfterOsty` dispatches to the modifiers captured during calculation. The first pass runs after
applying redirected unblocked damage, when present, and before applying HP loss to the original target. A second
pass for redirected overkill runs after applying the original target's HP loss. `BufferPower` decrements the
shadow amount read by its late value hook. The BeforeOsty callbacks only flash in vanilla and are skipped.

### AfterModifyingDamageAmount listeners

| Model | 中文名 | Original effect | Current impact |
| --- | --- | --- | --- |
| `IntangiblePower` | 无实体 | Flash only. | Not dispatched; ignorable. |
| `SlowPower` | 缓慢 | Flash only. | Not dispatched; ignorable. |

### AfterModifyingHpLostBeforeOsty listeners

| Model | 中文名 | Original effect | Current impact |
| --- | --- | --- | --- |
| `HardToKillPower` | 难以杀灭 | Flash only. | Ignored. |
| `HardenedShellPower` | 硬化外壳 | Flash only. | Not dispatched; ignorable. |

### AfterModifyingHpLostAfterOsty listeners

| Model | 中文名 | Original effect | Current impact |
| --- | --- | --- | --- |
| `BeatingRemnant` | 律动残余 | Flash only. | Ignored; the prediction-relevant per-turn counter is mirrored in `AfterDamageReceivedMirrors`. |
| `BufferPower` | 缓冲 | Decrements Buffer after it prevents HP loss. | Implemented with prediction-local amount decrement; later simulated hits stop consuming Buffer after the live number of stacks. |
| `IntangiblePower` | 无实体 | Flash only. | Ignored by `AfterModifyingHpLostAfterOstyMirrors`. |
| `TheBoot` | 发条靴 | Flash only. | Ignored by `AfterModifyingHpLostAfterOstyMirrors`. |
| `TungstenRod` | 钨合金棍 | Flash only. | Ignored by `AfterModifyingHpLostAfterOstyMirrors`. |

## Parity notes

- The simulator preserves the original `Hook.Modify*` passes, replacing exact listeners where a read-only original method would nevertheless read stale live state.
- StS2 v0.108.0 added `CardPlay?` to damage modifiers. Real card execution passes the active `CardPlay`; hover forecasts and other vanilla previews pass `null`. The simulator forwards `AttackCommand.CardPlay` through `ExecuteAttack(AttackCommand)` for simulated card-play/autoplay attacks, but direct `Damage` calls and helper-created attacks without a `CardPlay` still pass `null`.
- StS2 v0.109.0 deleted `DiamondDiademPower`; `DiamondDiadem` no longer contributes a
  multiplicative damage listener.
- Exact adapters cover the history, cost, HP, facing, attack-selection and amount consumers identified in the tables.
  Registry misses retain the original listener without recording unsupported risk. All predicted histories remain
  local; live history supplies only the pre-existing current-turn plays. Lethality uses started entries; Phantom Blades
  uses finished entries. Simulated history is already scoped to the current turn; only live entries need turn
  filtering. Their bonus amounts retain the original live reads.
- `SlipperyPower`, `BufferPower`, `FlutterPower`, `VigorPower`, and `GigantificationPower` use the same shared
  shadow-amount state from their side-effect and value-hook mirrors. This models gameplay-relevant power removal
  without mutating the live power collection; `FlutterPower`'s resulting monster stun remains outside the current
  player-turn prediction scope.
- `HardenedShellPower` and `BeatingRemnant` use shared shadow per-turn damage counters initialized from their live
  state. Their value adapters therefore apply the remaining HP-loss cap across chained damage without mutating model
  fields; Hardened Shell's HP display update is visual-only.
- The shadow decrement does not yet mirror the full vanilla `PowerCmd.ModifyAmount` lifecycle, including power-amount
  hooks and removal callbacks.

## Scope and remaining boundaries (v0.111.0)

- Coverage follows the vanilla v0.111.0 additive, multiplicative, cap, HP-loss, redirection and hitting-predicate
  overrides, including Weak/Vulnerable's delegated relic/power multipliers. `Damage` checks shadow dealer death
  before entering the target loop; a dealer killed by an earlier simulated action cannot deal damage in a new call.
- Shadow-amount adapters remain limited to the existing consumers whose amounts are changed by supported
  simulation paths. Other value hooks retain their
  original amount reads; partial power commands continue to record incomplete risk.
- Remaining original value methods read card type/tags/upgrades/enchantment from the preview, fixed dynamic vars,
  ownership/side relationships, or existing powers/relics. These reads do not require an extra adapter while those
  domains stay unchanged. In particular, `InterceptPower` counts its stored covered-creature list, not living allies;
  filtering that list by shadow liveness would differ from vanilla.
- Power creation/removal and full listener membership are still outside the simulator's state ownership.
  `CoveredPower`, `GuardedPower`, and `ShrinkPower` can therefore retain stale effects after their applier dies;
  their unmirrored `AfterDeath` callbacks already record risk. `SurroundedPower` death-time facing/back-attack changes
  have the same upstream limitation. These cannot be fixed accurately by checking the applier's HP alone, because
  death prevention and callback timing determine actual removal.
- Likewise, newly applying powers and changing their collection membership still needs full power/listener support.
  Weak/Vulnerable/Colossus/Tracking and delegated relic/power lookups can still see removed powers. Original value
  hooks also retain live amount reads outside the targeted adapters. The corresponding unsupported commands/hooks
  remain risk boundaries; parity beyond them is not guaranteed. Monster revive predicates retain the explicit
  limits above.
- Combat-active HP-loss caps use `IsInProgress`, not `IsOverOrEnding`: a killing action may finish its damage/hooks
  until the simulator commits the combat-end safe point.

## Mock model list

- `MockRevivePower` overrides `ModifyDamageMultiplicative` in test support only.
- `MockRevivePower` and `MockUnhittablePower` override `ShouldAllowHitting` in test support only.
