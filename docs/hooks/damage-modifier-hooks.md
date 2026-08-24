# Damage modifier hooks

Mirror files: `InCombat/Mirrors/HookMirrors.cs`,
`InCombat/Mirrors/Hooks/Damage/ModifyDamageMirrors.cs`,
`InCombat/Mirrors/Hooks/Damage/ModifyHpLostMirrors.cs`,
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
| `OneForAllPower` | 一心化万 | Owner's powered non-X 0-cost attacks gain flat damage; real card execution checks `CardPlay.Resources.EnergySpent`, while preview calls with `cardPlay == null` check current modified cost. | Implemented by original hook. StS2 v0.108.0 added the `CardPlay?` branch and v0.109.0 excluded X-cost cards; simulated `AttackCommand` damage forwards `CardPlay`, while generic/direct damage forecasts still follow vanilla preview semantics. |
| `PhantomBladesPower` | 幻影之刃 | Owner's first Shiv attack this turn gains flat damage. | Implemented by original hook, but reads live `CombatManager.Instance.History.CardPlaysFinished`. |
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
| `LethalityPower` | 致死性 | Owner's first Attack card this turn deals bonus powered damage. | Implemented by original hook, but reads live `CombatManager.Instance.History.CardPlaysStarted`. |
| `PenNib` | 钢笔尖 | Owner's every tenth Attack card is doubled. | Implemented by a prediction-aware multiplier using the shadow counter and exact `CardPlay` occurrence shared with the before/after card hooks. |
| `ShrinkPower` | 缩小 | Owner's powered attacks are reduced. | Implemented by original hook. |
| `SlowPower` | 缓慢 | Powered attacks against owner scale up with cards played this turn. | Implemented by a prediction-aware multiplier reading the shadow card-play count. |
| `SoarPower` | 翱翔 | Powered attacks against owner are reduced by configured percentage. | Implemented by original hook. |
| `SurroundedPower` | 遭到包围 | Back attacks against owner are multiplied. | Implemented by a prediction-aware multiplier reading the facing updated by the simulated targeted card. |
| `TankPower` | 肉盾 | Powered attacks against owner are doubled. | Implemented by original hook. |
| `TrackingPower` | 跟踪 | Owner or pet powered card attacks against Weak targets are multiplied. | Implemented by original hook. |
| `UndyingSigil` | 不死符文 | Incoming powered attacks from doomed enemies are reduced. | Implemented by original hook. |
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
| `BeatingRemnant` | 律动残余 | Caps owner's per-turn HP loss while combat is in progress. | Implemented by a prediction-aware adapter using the per-turn damage counter shared with `AfterDamageReceived`. |
| `IntangiblePower` | 无实体 | Caps owner's HP loss to 1 while combat is in progress. | Implemented by original hook. |
| `SlipperyPower` | 滑溜 | Caps owner's HP loss to 1. | Implemented by a prediction-aware adapter that stops applying the cap after the shadow amount reaches zero. |
| `TungstenRod` | 钨合金棍 | Reduces owner's HP loss by configured amount. | Implemented by original hook. |

### ModifyHpLostAfterOstyLate listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `BufferPower` | 缓冲 | Sets owner's HP loss to 0. | Implemented by a prediction-aware late adapter that stops applying after the shadow amount reaches zero. |
| `TheBoot` | 发条靴 | Raises owner's powered unblocked attack damage below threshold to minimum damage. | Implemented by original hook. |

## ModifyUnblockedDamageTarget listeners

Current mirror status: implemented by manually reproducing the original unguarded listener pass in
`HookMirrors.ModifyUnblockedDamageTarget`.

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `DieForYouPower` | 为你而死 | Living Osty absorbs powered unblocked attack damage that would hit its owner. | Implemented by original hook. The simulator then creates one `DamageResult` for Osty and, if Osty takes overkill, a second result for the original target. |

`ShouldAllowHitting` uses a manually reproduced live guarded pass when building hittable shadow targets. Both it and
`ModifyUnblockedDamageTarget` apply compatibility filtering. `ModifyDamage` is the deliberate exception: its
prediction-state-aware mirror retains Mod listeners so Mod damage semantics remain visible.

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
| `IntangiblePower` | 无实体 | Flash only. | Ignored by `DamageModifiersHook`. |
| `TheBoot` | 发条靴 | Flash only. | Ignored by `DamageModifiersHook`. |
| `TungstenRod` | 钨合金棍 | Flash only. | Ignored by `DamageModifiersHook`. |

## Parity notes

- The simulator intentionally uses the original `Hook.Modify*` value path because vanilla previews also use these hooks without mutating RNG.
- StS2 v0.108.0 added `CardPlay?` to damage modifiers. Real card execution passes the active `CardPlay`; hover forecasts and other vanilla previews pass `null`. The simulator forwards `AttackCommand.CardPlay` through `ExecuteAttack(AttackCommand)` for simulated card-play/autoplay attacks, but direct `Damage` calls and helper-created attacks without a `CardPlay` still pass `null`.
- StS2 v0.109.0 deleted `DiamondDiademPower`; `DiamondDiadem` no longer contributes a
  multiplicative damage listener.
- Damage modifiers normally call the original read-only listener methods. The multiplicative hook mirror has exact
  registrations only for `FlutterPower`, `GigantificationPower`, `PenNib`, `SlowPower`, and `SurroundedPower`, while
  the additive mirror registers only `VigorPower`. Chained simulation therefore reads their shadow state without
  treating every other listener as an unsupported mirror; history-dependent
  `LethalityPower` and `PhantomBladesPower` still read live history.
- `SlipperyPower`, `BufferPower`, `FlutterPower`, `VigorPower`, and `GigantificationPower` use the same shared
  shadow-amount state from their side-effect and value-hook mirrors. This models gameplay-relevant power removal
  without mutating the live power collection; `FlutterPower`'s resulting monster stun remains outside the current
  player-turn prediction scope.
- `HardenedShellPower` and `BeatingRemnant` use shared shadow per-turn damage counters initialized from their live
  state. Their value adapters therefore apply the remaining HP-loss cap across chained damage without mutating model
  fields; Hardened Shell's HP display update is visual-only.
- The shadow decrement does not yet mirror the full vanilla `PowerCmd.ModifyAmount` lifecycle, including power-amount
  hooks and removal callbacks.

## Mock model list

- `MockRevivePower` overrides `ModifyDamageMultiplicative` in test support only.
