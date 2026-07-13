# Damage modifier hooks

Mirror files: `InCombat/Mirrors/HookMirrors.cs`,
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

The simulator calls the three value-producing modifier hooks directly, but does not call the `AfterModifying*` hooks. Those after hooks are mostly visual, except where noted below.

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

## ModifyDamage listeners

Current mirror status: implemented by directly calling original `Hook.ModifyDamage`. These hooks are expected to be read-only value modifiers during prediction.

### ModifyDamageAdditive listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `AccuracyPower` | 精准 | Owner's powered Shiv attacks gain flat damage. | Implemented by original hook. |
| `CalcifyPower` | 钙化 | Owner's Osty powered attacks gain flat damage. | Implemented by original hook. |
| `FakeStrikeDummy` | 打击木偶？？？ | Owner Strike-tag attacks gain flat damage. | Implemented by original hook. |
| `LeadershipPower` | 领袖气质 | Owner buffs allied powered attacks by flat damage. | Implemented by original hook. |
| `MiniatureCannon` | 微型大炮 | Owner upgraded-card powered attacks gain flat damage. | Implemented by original hook. |
| `MysticLighter` | 神秘打火机 | Owner enchanted-card powered attacks gain flat damage. | Implemented by original hook. |
| `OneForAllPower` | 一心化万 | Owner's powered 0-cost attacks gain flat damage; real card execution checks `CardPlay.Resources.EnergySpent`, while preview calls with `cardPlay == null` check current modified cost. | Implemented by original hook. StS2 v0.108.0 added the `CardPlay?` branch; simulated `AttackCommand` damage forwards `CardPlay`, while generic/direct damage forecasts still follow vanilla preview semantics. |
| `PhantomBladesPower` | 幻影之刃 | Owner's first Shiv attack this turn gains flat damage. | Implemented by original hook, but reads live `CombatManager.Instance.History.CardPlaysFinished`. |
| `StrikeDummy` | 打击木偶 | Owner Strike-tag attacks gain flat damage. | Implemented by original hook. |
| `StrengthPower` | 力量 | Owner powered attacks gain flat damage; negative amounts reduce damage. | Implemented by original hook. |
| `TaintedPower` | 污染 | Powered attacks against owner gain flat damage. | Implemented by original hook. |
| `VigorPower` | 活力 | Owner's next powered attack gains flat damage, usually scoped by `BeforeAttack`/`AfterAttack`. | Implemented by original hook, but direct simulator damage does not mirror attack-command state changes. |

### ModifyDamageMultiplicative listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `ColossusPower` | 巨像 | Powered attacks from Vulnerable dealers against owner are reduced. | Implemented by original hook. |
| `ConquerorPower` | 征服者 | `SovereignBlade` powered attacks against owner are doubled. | Implemented by original hook. |
| `CoveredPower` | 掩护 | Powered attacks against owner are reduced to zero. | Implemented by original hook. |
| `DiamondDiademPower` | 钻石头冠 | Powered attacks against owner are halved. | Implemented by original hook. |
| `DoubleDamagePower` | 双倍伤害 | Owner or pet powered card attacks are doubled. | Implemented by original hook. |
| `FlankingPower` | 夹击 | Powered attacks against owner are multiplied unless dealt by applier. | Implemented by original hook. |
| `FlutterPower` | 振翅 | Powered attacks against owner are reduced by configured percentage. | Implemented by original hook; post-hit decrement/stun is covered in `damage-hooks.md`. |
| `GigantificationPower` | 超巨化 | Owner's powered attack card is tripled, usually scoped by `BeforeAttack`/`AfterAttack`. | Implemented by original hook, but direct simulator damage does not mirror attack-command state changes. |
| `GuardedPower` | 护卫 | Powered attacks against owner are halved. | Implemented by original hook. |
| `HangPower` | 吊杀 | `Hang` damage against owner is multiplied by amount. | Implemented by original hook. |
| `InterceptPower` | 拦截 | Powered attacks against owner are increased by covered-creature count. | Implemented by original hook. |
| `KnockdownPower` | 击倒 | Powered attacks against owner are multiplied unless dealt by applier. | Implemented by original hook. |
| `LethalityPower` | 致死性 | Owner's first Attack card this turn deals bonus powered damage. | Implemented by original hook, but reads live `CombatManager.Instance.History.CardPlaysStarted`. |
| `PenNib` | 钢笔尖 | Owner's every tenth Attack card is doubled. | Implemented by original hook, but relies on live relic counter/card-play state. |
| `ShrinkPower` | 缩小 | Owner's powered attacks are reduced. | Implemented by original hook. |
| `SlowPower` | 缓慢 | Powered attacks against owner scale up with cards played this turn. | Implemented by original hook. |
| `SoarPower` | 翱翔 | Powered attacks against owner are reduced by configured percentage. | Implemented by original hook. |
| `SurroundedPower` | 遭到包围 | Back attacks against owner are multiplied. | Implemented by original hook. |
| `TankPower` | 肉盾 | Powered attacks against owner are doubled. | Implemented by original hook. |
| `TrackingPower` | 跟踪 | Owner or pet powered card attacks against Weak targets are multiplied. | Implemented by original hook. |
| `UndyingSigil` | 不死符文 | Incoming powered attacks from doomed enemies are reduced. | Implemented by original hook. |
| `VitruvianMinion` | 维特鲁威仆从 | Owner Minion-tag card attacks are doubled. | Implemented by original hook. |
| `VulnerablePower` | 易伤 | Powered attacks against owner are multiplied, with Paper Phrog, Cruelty, and Debilitate adjustments. | Implemented by original hook. |
| `WeakPower` | 虚弱 | Owner's powered attacks are reduced, with Paper Krane and Debilitate adjustments. | Implemented by original hook. |

### ModifyDamageCap listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `HardToKillPower` | 难以杀灭 | Damage against owner is capped by amount. | Implemented by original hook. |
| `IntangiblePower` | 无实体 | Damage against owner is capped at 1 for block loss and previews. | Implemented by original hook. |

## ModifyHpLost listeners

Current mirror status: implemented by directly calling original `Hook.ModifyHpLost`. These hooks change unblocked HP loss after block has been removed.

### ModifyHpLostBeforeOstyLate listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `HardenedShellPower` | 硬化外壳 | Caps owner's HP loss by remaining per-turn shell amount before Osty redirection. | Implemented by original hook; post-hit per-turn counter update is covered in `damage-hooks.md`. |

### ModifyHpLostAfterOsty listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `BeatingRemnant` | 律动残余 | Caps owner's per-turn HP loss while combat is in progress. | Implemented by original hook; post-hit per-turn counter update is covered in `damage-hooks.md`. |
| `IntangiblePower` | 无实体 | Caps owner's HP loss to 1 while combat is in progress. | Implemented by original hook. |
| `SlipperyPower` | 滑溜 | Caps owner's HP loss to 1. | Implemented by original hook; post-hit decrement is covered in `damage-hooks.md`. |
| `TungstenRod` | 钨合金棍 | Reduces owner's HP loss by configured amount. | Implemented by original hook. |

### ModifyHpLostAfterOstyLate listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `BufferPower` | 缓冲 | Sets owner's HP loss to 0. | Implemented by original hook, but missing after hook means repeated simulated hits can reuse the same live Buffer stack. |
| `TheBoot` | 发条靴 | Raises owner's powered unblocked attack damage below threshold to minimum damage. | Implemented by original hook. |

## ModifyUnblockedDamageTarget listeners

Current mirror status: implemented by directly calling original `Hook.ModifyUnblockedDamageTarget`.

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `DieForYouPower` | 为你而死 | Living Osty absorbs powered unblocked attack damage that would hit its owner. | Implemented by original hook. The simulator then creates one `DamageResult` for Osty and, if Osty takes overkill, a second result for the original target. |

## AfterModifying listeners

Current mirror status: `AfterModifyingHpLostAfterOsty` is dispatched only to the modifier list returned by the original value hook. Reviewed vanilla flash-only listeners are ignored. `BufferPower` is marked risky because the simulator cannot shadow-decrement the live power stack without changing what the next original `ModifyHpLostAfterOstyLate` call reads.

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
| `BeatingRemnant` | 律动残余 | Flash only. | Ignored by `DamageModifiersHook`; damage-received state is marked risky in `DamageReceivedHooks`. |
| `BufferPower` | 缓冲 | Decrements Buffer after it prevents HP loss. | Marked risky when returned as a modifier. Prediction can diverge on multiple simulated hits because the live Buffer amount is not shadow-decremented. |
| `IntangiblePower` | 无实体 | Flash only. | Ignored by `DamageModifiersHook`. |
| `TheBoot` | 发条靴 | Flash only. | Ignored by `DamageModifiersHook`. |
| `TungstenRod` | 钨合金棍 | Flash only. | Ignored by `DamageModifiersHook`. |

## Parity notes

- The simulator intentionally uses the original `Hook.Modify*` value path because vanilla previews also use these hooks without mutating RNG.
- StS2 v0.108.0 added `CardPlay?` to damage modifiers. Real card execution passes the active `CardPlay`; hover forecasts and other vanilla previews pass `null`. The simulator forwards `AttackCommand.CardPlay` through `ExecuteAttack(AttackCommand)` for simulated card-play/autoplay attacks, but direct `Damage` calls and helper-created attacks without a `CardPlay` still pass `null`.
- Direct original hook calls read live model state. History-dependent listeners such as `LethalityPower` and `PhantomBladesPower` do not read simulator shadow history, so chained simulated card plays or auto-plays can drift until those powers get targeted mirrors.
- Attack-command scoped listeners such as `VigorPower`, `GigantificationPower`, and `PenNib` rely on live `BeforeAttack`/`BeforeCardPlayed`/`AfterAttack` state. `CombatPredictionSimulator.ExecuteAttack(AttackCommand)` now mirrors the attack lifecycle around per-hit `Damage`, but direct simulator `Damage` calls still bypass that lifecycle, and original value hooks still read live power/history state until targeted shadow-state mirrors exist. See `attack-hooks.md`.
- `AfterModifyingHpLostAfterOstyMirrors` explicitly registers the reviewed vanilla listeners. Add
  new exact registrations if vanilla gains more after-modifying listeners.
- Missing shadow state for `BufferPower` is the only currently known `AfterModifying*` hook with immediate prediction-relevant state; it is surfaced as risk instead of silently diverging.
- `HardenedShellPower`, `BeatingRemnant`, and `SlipperyPower` rely on damage post hooks to update counters or decrement powers after HP loss; those are tracked in `damage-hooks.md`, not in this modifier document.

## Mock model list

- `MockRevivePower` overrides `ModifyDamageMultiplicative` in test support only.
