# Damage modifier hooks

Mirror files: `InCombat/Mirrors/HookMirrors.cs`,
`InCombat/Mirrors/Hooks/Damage/ModifyDamageMirrors.cs`,
`InCombat/Mirrors/Hooks/Damage/ModifyHpLostMirrors.cs`,
`InCombat/Mirrors/Hooks/Damage/ModifyUnblockedDamageTargetMirrors.cs`,
`InCombat/Mirrors/Hooks/Damage/ShouldAllowHittingMirrors.cs`,
`InCombat/Mirrors/Hooks/Damage/AfterModifyingHpLostAfterOstyMirrors.cs`, and
`InCombat/Simulation/CombatPredictionSimulator.Damage.cs`.

This document covers the read-only damage modifier path used by `CombatPredictionSimulator.DamageTarget`. Post-result hooks such as `AfterDamageReceived` are documented in `damage-hooks.md`.

## Vanilla order

`CreatureCmd.Damage` runs the modifier path once per original target:

1. `Hook.ModifyDamage(..., CardPlay? cardPlay, ModifyDamageHookType.All, CardPreviewMode.None, out modifiers)`
2. `Hook.AfterModifyingDamageAmount(..., modifiers)`
3. `Hook.BeforeDamageReceived(...)`
4. Block loss on `originalTarget.PetOwner?.Creature ?? originalTarget`
5. `Hook.ModifyHpLost(..., HpLossHookPhase.BeforeOsty, out modifiers)`
6. `Hook.AfterModifyingHpLostBeforeOsty(..., modifiers)`
7. `Hook.ModifyUnblockedDamageTarget(...)`
8. `Hook.ModifyHpLost(..., HpLossHookPhase.AfterOsty, out modifiers)` for the redirected target
9. `Hook.AfterModifyingHpLostAfterOsty(..., modifiers)`
10. If damage was redirected, `Hook.ModifyHpLost(..., HpLossHookPhase.AfterOsty, out modifiers)` for original-target overkill damage
11. `Hook.AfterModifyingHpLostAfterOsty(..., modifiers)`

The simulator mirrors the value-producing passes and dispatches `AfterModifyingHpLostAfterOsty` to the exact
modifiers returned by the corresponding value pass. The other `AfterModifying*` hooks are currently omitted because
their reviewed vanilla listeners are visual only.

## Hook specs

- `AbstractModel.ModifyDamageAdditive(Creature?, decimal, ValueProp, Creature?, CardModel?, CardPlay?)`
- `AbstractModel.ModifyDamageMultiplicative(Creature?, decimal, ValueProp, Creature?, CardModel?, CardPlay?)`
- `AbstractModel.ModifyDamageCap(Creature?, ValueProp, Creature?, CardModel?, CardPlay?)`
- `AbstractModel.AfterModifyingDamageAmount(CardModel?)`
- `AbstractModel.ModifyHpLostBeforeOsty(Creature, decimal, ValueProp, Creature?, CardModel?)`
- `AbstractModel.ModifyHpLostBeforeOstyLate(Creature, decimal, ValueProp, Creature?, CardModel?)`
- `AbstractModel.AfterModifyingHpLostBeforeOsty()`
- `AbstractModel.ModifyUnblockedDamageTarget(Creature, decimal, ValueProp, Creature?)`
- `AbstractModel.ModifyHpLostAfterOsty(Creature, decimal, ValueProp, Creature?, CardModel?)`
- `AbstractModel.ModifyHpLostAfterOstyLate(Creature, decimal, ValueProp, Creature?, CardModel?)`
- `AbstractModel.AfterModifyingHpLostAfterOsty()`
- `AbstractModel.ShouldAllowHitting(Creature)`

## ModifyDamage listeners

Current mirror status: the simulator preserves vanilla additive, multiplicative, and cap passes. It calls original
read-only listener methods except for the prediction-state consumers documented below.

### ModifyDamageAdditive listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `AccuracyPower` | 精准 | Owner's powered Shiv attacks gain flat damage. | Implemented by original hook. |
| `CalcifyPower` | 钙化 | Owner's Osty powered attacks gain flat damage. | Implemented by original hook. |
| `FakeStrikeDummy` | 打击木偶？？？ | Owner Strike-tag attacks gain flat damage. | Implemented by original hook. |
| `LeadershipPower` | 领袖气质 | Owner buffs allied powered attacks by flat damage. | Implemented by original hook. |
| `MiniatureCannon` | 微型大炮 | Owner upgraded-card powered attacks gain flat damage. | Implemented by original hook. |
| `MysticLighter` | 神秘打火机 | Owner enchanted-card powered attacks gain flat damage. | Implemented by original hook. |
| `OneForAllPower` | 一心化万 | Owner's powered non-X 0-cost attacks gain flat damage; real card execution checks `CardPlay.Resources.EnergySpent`, while preview calls with `cardPlay == null` check current modified cost. | Exact additive adapter preserves spent-energy and X-cost rules; the null-play branch uses `GetEnergyCostWithModifiers`, mirroring `CardEnergyCost.GetWithModifiers(All)`. |
| `PhantomBladesPower` | 幻影之刃 | Owner's first Shiv attack this turn gains flat damage. | Exact additive adapter combines live current-turn and predicted finished-card history, filtered by player and Shiv tag. Started-but-unfinished Shivs do not consume the bonus. |
| `StrikeDummy` | 打击木偶 | Owner Strike-tag attacks gain flat damage. | Implemented by original hook. |
| `StrengthPower` | 力量 | Owner powered attacks gain flat damage; negative amounts reduce damage. | Implemented by original hook. |
| `TaintedPower` | 污染 | Powered attacks against owner gain flat damage. | Implemented by original hook. |
| `VigorPower` | 活力 | Owner's next powered attack gains flat damage, usually scoped by `BeforeAttack`/`AfterAttack`. | Implemented by a prediction-aware additive adapter using the selected attack and shadow amount shared with the attack hooks. |

### ModifyDamageMultiplicative listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `ColossusPower` | 巨像 | Powered attacks from Vulnerable dealers against owner are reduced. | Implemented by original hook. |
| `ConquerorPower` | 征服者 | `SovereignBlade` powered attacks against owner are doubled. | Implemented by original hook. |
| `CoveredPower` | 掩护 | Powered attacks against owner are reduced to zero. | Implemented by original hook. |
| `DoubleDamagePower` | 双倍伤害 | Owner or pet powered card attacks are doubled. | Implemented by original hook. |
| `FlankingPower` | 夹击 | Powered attacks against owner are multiplied unless dealt by applier. | Implemented by original hook. |
| `FlutterPower` | 振翅 | Powered attacks against owner are reduced by configured percentage. | Implemented by a prediction-aware multiplier that reads the shadow stack amount consumed by `AfterDamageReceived`; post-hit stun scope is covered in `damage-hooks.md`. |
| `GigantificationPower` | 超巨化 | Owner's powered attack card is tripled, usually scoped by `BeforeAttack`/`AfterAttack`. | Implemented by a prediction-aware multiplier using the selected attack and shadow amount shared with the attack hooks. |
| `GuardedPower` | 护卫 | Powered attacks against owner are halved. | Implemented by original hook. |
| `HangPower` | 吊杀 | `Hang` damage against owner is multiplied by amount. | Implemented by original hook. |
| `InterceptPower` | 拦截 | Powered attacks against owner are increased by covered-creature count. | Implemented by original hook. |
| `KnockdownPower` | 击倒 | Powered attacks against owner are multiplied unless dealt by applier. | Implemented by original hook. |
| `LethalityPower` | 致死性 | Owner's first Attack card this turn deals bonus powered damage. | Exact multiplier combines live current-turn and predicted started-card history, filtered by player and Attack type. Uses the shadow pile and preview play index to retain the first play's multi-hit bonus and reject repeated plays. |
| `PenNib` | 钢笔尖 | Owner's every tenth Attack card is doubled. | Prediction-aware multiplier uses the shadow counter and original card identity shared with the before/after card hooks, matching vanilla's card-identity selection. |
| `ShrinkPower` | 缩小 | Owner's powered attacks are reduced. | Implemented by original hook. |
| `SlowPower` | 缓慢 | Powered attacks against owner scale up with cards played this turn. | Implemented by a prediction-aware multiplier reading the shadow card-play count. |
| `SoarPower` | 翱翔 | Powered attacks against owner are reduced by configured percentage. | Implemented by original hook. |
| `SurroundedPower` | 遭到包围 | Back attacks against owner are multiplied. | Implemented by a prediction-aware multiplier reading the facing updated by the simulated targeted card. |
| `TankPower` | 肉盾 | Powered attacks against owner deal 50% more damage. | Implemented by original hook. |
| `TrackingPower` | 跟踪 | Owner or pet powered card attacks against Weak targets are multiplied. | Implemented by original hook. |
| `UndyingSigil` | 不死符文 | Incoming powered attacks from dealers whose Doom is at least their HP are reduced. | Original hook retained. Enemy attacks are outside the current prediction scope, so no dedicated shadow-HP adapter is needed. |
| `VitruvianMinion` | 维特鲁威仆从 | Owner Minion-tag card attacks are doubled. | Implemented by original hook. |
| `VulnerablePower` | 易伤 | Powered attacks against owner are multiplied, with Paper Phrog, dealer or pet owner's Cruelty, and Debilitate adjustments. | Implemented by original hook. StS2 v0.109.0 made attacking pets inherit their owner's Cruelty adjustment. |
| `WeakPower` | 虚弱 | Owner's powered attacks are reduced, with Paper Krane and Debilitate adjustments. | Implemented by original hook. |

### ModifyDamageCap listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `HardToKillPower` | 难以杀灭 | Damage against owner is capped by amount. | Implemented by original hook. |
| `IntangiblePower` | 无实体 | Damage against owner is capped at 1 for block loss and previews. | Implemented by original hook. |

## ModifyHpLost listeners

Current mirror status: the phase-aware `HookMirrors.ModifyHpLost` mirrors the original phase flag and all four
BeforeOsty/AfterOsty listener passes. Exact registered consumers read prediction state while all other listeners
continue through their original read-only methods.

### ModifyHpLostBeforeOstyLate listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
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
| `BufferPower` | 缓冲 | Sets owner's HP loss to 0. | Implemented by a prediction-aware late adapter that stops applying after the shadow amount reaches zero. |
| `TheBoot` | 发条靴 | Raises owner's powered unblocked attack damage below threshold to minimum damage. | Implemented by original hook. |

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

## AfterModifying listeners

Current mirror status: `AfterModifyingHpLostAfterOsty` is dispatched only to the modifier list returned by the
mirrored value hook. Reviewed vanilla flash-only listeners are ignored. `BufferPower` decrements the same shadow
amount read by its late value-hook adapter.

### AfterModifyingDamageAmount listeners

| Model | 中文名 | Original effect | Current impact |
| --- | --- | --- | --- |
| `HardToKillPower` | 难以杀灭 | Flash only. | Not dispatched; ignorable. |
| `IntangiblePower` | 无实体 | Flash only. | Not dispatched; ignorable. |
| `SlowPower` | 缓慢 | Flash only. | Not dispatched; ignorable. |

### AfterModifyingHpLostBeforeOsty listeners

| Model | 中文名 | Original effect | Current impact |
| --- | --- | --- | --- |
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
