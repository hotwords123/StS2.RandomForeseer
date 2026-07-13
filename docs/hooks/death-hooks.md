# Death hooks

Mirror files:

- `InCombat/Mirrors/HookMirrors.cs` for simulation-facing death lifecycle dispatch.
- `InCombat/Mirrors/Hooks/Death/BeforeDeathMirrors.cs` for `BeforeDeath`.
- `InCombat/Mirrors/Hooks/Death/AfterDeathMirrors.cs` for `AfterDeath`.
- `InCombat/Mirrors/Hooks/Death/ShouldDieMirrors.cs` for `ShouldDie` / `ShouldDieLate`.
- `InCombat/Mirrors/Hooks/Death/AfterPreventingDeathMirrors.cs` for `AfterPreventingDeath`.
- `InCombat/Mirrors/Hooks/Death/DeathPreventerMirrors.cs` for model behavior and shared cross-hook
  state.

## Hook specs

- `AbstractModel.BeforeDeath(Creature)`
- `AbstractModel.AfterDeath(PlayerChoiceContext, Creature, bool wasRemovalPrevented, float deathAnimLength)`
- `AbstractModel.ShouldDie(Creature)`
- `AbstractModel.ShouldDieLate(Creature)`
- `AbstractModel.AfterPreventingDeath(Creature)`

## BeforeDeath listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `Crusher` | 碾碎爪 | Plays boss death audio/visual setup. | Ignored. VFX/SFX only. |
| `Rocket` | 火箭 | Plays death audio/visual setup. | Ignored. VFX/SFX only. |
| `HeistPower` | 盗窃 | On thief death, returns stolen gold as an extra combat reward and updates loot history. | Ignored. Combat rewards are outside the current prediction scope. |
| `SwipePower` | 顺走 | On thief death, returns stolen deck card as an extra combat reward and updates loot history. | Ignored. Combat rewards/deck return are outside the current prediction scope. |

## AfterDeath listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `GremlinHorn` | 地精之角 | When an enemy dies, owner gains energy and draws cards. | Implemented for energy and draw. Energy is applied before draw, matching vanilla order. |
| `Aeonglass` | 永世沙漏 | Music/progression side effect on own death. | Ignored. |
| `DecimillipedeSegment` | 残杀千足虫 | Segment death state/VFX handling. | Ignored in registry, but full combat-structure parity is unsupported. |
| `KinPriest` | 同族神官 | Encounter-specific death behavior. | Ignored in registry; no current prediction-relevant player turn effect. |
| `LagavulinMatriarch` | 乐加维林族母 | Stops sleep visuals / encounter death behavior. | Ignored in registry. |
| `Queen` | 女王 | Encounter-specific death/progression behavior. | Ignored in registry. |
| `SoulFysh` | 灵魂异鱼 | Encounter-specific death behavior. | Ignored in registry. |
| `TestSubject` | 实验体 #C{Count} | Encounter-specific revive/progression behavior. | Ignored in registry; precise behavior needs combat-structure support. |
| `TheInsatiable` | 无厌沙虫 | Encounter-specific death behavior. | Ignored in registry. |
| `Vantom` | 墨影幻灵 | Encounter-specific death behavior. | Ignored in registry. |
| `WaterfallGiant` | 瀑布巨兽 | Encounter-specific death behavior. | Ignored in registry. |
| `AdaptablePower` | 适者生存 | Revives/transitions Test Subject. | Missing; not implementable without revive/combat-structure support. |
| `ConstrictPower` | 紧缠 | Removes this power when applier dies. | Missing; power removal unsupported. |
| `DampenPower` | 抑制 | Removes/decrements delayed card downgrade state. | Missing; power removal/card mutation flow unsupported. |
| `CrabRagePower` | 蟹之怒 | When ally dies, applies Strength and gains block. | Missing. Block portion is implementable, Apply Power is not; should remain risk or partial only. |
| `CoveredPower` | 掩护 | Removes covered/intercept state when applier dies. | Missing; power removal unsupported. |
| `InfestedPower` | 寄生物 | Death-triggered infestation behavior. | Missing; likely spawn/apply-power flow, unsupported. |
| `GuardedPower` | 护卫 | Removes guard relation when guard dies. | Missing; power removal unsupported. |
| `IllusionPower` | 幻象 | Revive/stun/next move behavior. | Missing; not implementable without combat-structure and move support. |
| `HexPower` | 恶咒 | Death-triggered power/card effect. | Missing; likely power/card mutation support needed. |
| `MagicBombPower` | 魔法炸弹 | Removes bomb if applier dies. | Missing; power removal unsupported. |
| `ReattachPower` | 接续 | Revives/reattaches segment state. | Missing; not implementable without revive/combat-structure support. |
| `RavenousPower` | 饥饿 | Ally death causes devour/stun/strength behavior. | Missing; Apply Power/stun unsupported. |
| `PossessStrengthPower` | 抢夺力量 | Restores stolen Strength on owner death. | Missing; Apply Power unsupported. |
| `PossessSpeedPower` | 抢夺速度 | Restores stolen Dexterity on owner death. | Missing; Apply Power unsupported. |
| `ShrinkPower` | 缩小 | Removes this power when applier dies. | Missing; power removal unsupported. |
| `Melancholy` | 忧郁 | On a non-prevented death, reduces this card's cost while it is in a combat pile. | Implemented for shadow hand/draw/discard/exhaust/play pile cards. |
| `SurroundedPower` | 遭到包围 | Encounter positioning/side behavior on death. | Missing; combat-structure/position support needed. |
| `SurprisePower` | 意外 | Death-triggered encounter behavior. | Missing; needs separate review. |
| `StockPower` | 库存 | Death-triggered encounter behavior. | Missing; needs separate review. |
| `SteamEruptionPower` | 蒸汽喷发 | Waterfall Giant transitions to about-to-blow state. | Missing; not implementable without monster state support. |

## Death-prevention listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `FairyInABottle` | 瓶中精灵 | `ShouldDie` prevents owner death once; `AfterPreventingDeath` uses the potion wrapper, then heals owner for 30% max HP, minimum 1. | Implemented for shadow used state and healing. `BeforePotionUsed`/`AfterPotionUsed` from `OnUseWrapper` are intentionally not mirrored. |
| `LizardTail` | 蜥蜴尾巴 | `ShouldDieLate` prevents owner death once; `AfterPreventingDeath` marks the relic used and heals owner for 50% max HP, minimum 1. | Implemented for shadow used state and healing. Live relic state is not mutated. |

## Parity notes

- `CombatPredictionSimulator` updates shadow liveness, runs before/after death registries, records shadow creature removal for supported enemy death paths, and mirrors selected player death cleanup in shadow combat state.
- Prediction omits death animations, so `HookMirrors.AfterDeath` receives a zero
  `deathAnimLength`; no currently handled listener consumes this presentation-only value.
- The simulator does not model power cleanup/removal, full creature revive, monster move/state transitions, hook deactivation, or combat loss. Most missing death listeners need those capabilities.
- `GremlinHorn` mirrors energy and draw. `Melancholy` mutates only matching shadow `PredictedCard` previews and does not mutate the live card.
- `FairyInABottle` and `LizardTail` use `PredictionStateStore` to avoid mutating live potion/relic used state while still preventing repeated use in the same simulation.
- `HookMirrors.ShouldDie` preserves first-preventer short-circuiting and rebuilds the listener
  sequence between the normal and late phases. The two predicate registries return their mirrored
  bool results, while `AfterPreventingDeath` is dispatched only to the selected preventer if it is
  still an active listener.
- Unsupported `ShouldDie` / `ShouldDieLate` listeners are risk-marked rather than fully mirrored; adding one requires both predicate behavior and matching `AfterPreventingDeath` side effects.
- `HeistPower` and `SwipePower` are intentionally ignored because they only affect combat rewards/deck return, which is outside the current prediction scope.

## Mock model list

- `MockInvincibleOnDeathPower`
- `MockPreventDeathPower`
- `MockRevivePower`
