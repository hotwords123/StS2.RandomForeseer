# Damage and HP hooks

Mirror files: `InCombat/Mirrors/HookMirrors.cs`,
`InCombat/Mirrors/Hooks/Block/AfterBlockBrokenMirrors.cs`,
`InCombat/Mirrors/Hooks/Damage/AfterCurrentHpChangedMirrors.cs`,
`InCombat/Mirrors/Hooks/Damage/AfterDamageGivenMirrors.cs`,
`InCombat/Mirrors/Hooks/Damage/BeforeDamageReceivedMirrors.cs`,
`InCombat/Mirrors/Hooks/Damage/AfterDamageReceivedMirrors.cs`,
`InCombat/Simulation/CombatPredictionSimulator.Damage.cs`, and
`InCombat/Simulation/CombatPredictionSimulator.Heal.cs`.

This document covers the implemented `BeforeDamageReceived`, `AfterBlockBroken`, `AfterCurrentHpChanged`, `AfterDamageGiven`, and `AfterDamageReceived` mirrors plus the remaining damage/hp/block gaps.

Damage hooks use the current player-turn prediction scope from `overview.md`: only effects that can feed back into predictions before the current player turn finishes need to be mirrored or marked risky. Enemy intent/stun changes, next-turn counters, later orb-passive triggers, and later reward-screen state can be ignored here unless another current-turn prediction consumes them.

## Hook specs

- `AbstractModel.BeforeDamageReceived(PlayerChoiceContext, Creature, decimal, ValueProp, Creature?, CardModel?)`
- `AbstractModel.AfterBlockBroken(PlayerChoiceContext, Creature target, Creature? breaker)`
- `AbstractModel.AfterCurrentHpChanged(Creature, decimal delta)`
- `AbstractModel.AfterDamageGiven(PlayerChoiceContext, Creature?, DamageResult, ValueProp, Creature, CardModel?)`
- `AbstractModel.AfterDamageReceived(PlayerChoiceContext, Creature, DamageResult, ValueProp, Creature?, CardModel?)`
- `AbstractModel.AfterDamageReceivedLate(...)`

## BeforeDamageReceived listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `ThornsPower` | 荆棘 | If owner is hit by a powered attack, damages the dealer. `Omnislice` also triggers it. | Implemented with simulator `Damage`; hook dispatch records the source. |

## AfterBlockBroken listeners

| Model | 中文名 | Original effect | Current status and feasibility |
| --- | --- | --- | --- |
| `BurrowedPower` | 埋地 | When owner's block breaks, removes/stuns burrowed monster state. | Ignored. Not implementable without power removal/monster state support, and only affects later enemy behavior. |
| `HandDrill` | 手钻 | Owner or pet breaking enemy block applies Vulnerable. | Risk only when trigger condition matches. Apply Power unsupported. StS2 v0.109.0 moved this listener from `AfterDamageGiven` and added the `breaker` argument. |

## AfterCurrentHpChanged listeners

| Model | 中文名 | Original effect | Current status and feasibility |
| --- | --- | --- | --- |
| `Crusher` | 碾碎爪 | Plays hurt animation on own HP loss. | Ignorable. VFX only. |
| `Rocket` | 火箭 | Plays hurt animation on own HP loss. | Ignorable. VFX only. |
| `RedSkull` | 红头骨 | Applies/removes Strength when owner's HP crosses threshold. | Marked risky when the shadow HP threshold would require applying/removing Strength. Apply/remove Power unsupported. |
| `NecroMasteryPower` | 亡灵精通 | When owner's Osty loses HP, damages enemies based on HP lost. | Implemented with simulator `Damage` against current shadow hittable enemies. |
| `MeatOnTheBone` | 带骨肉 | Updates active status based on owner's HP threshold. | Ignorable for current predictions; heal is room-end/start behavior, not damage-chain effect. |

## AfterDamageGiven listeners

| Model | 中文名 | Original effect | Current status and feasibility |
| --- | --- | --- | --- |
| `SkillIronclad2Achievement` | 成就模型 | Unlocks achievement for very large damage. | Ignored. Achievement state does not affect prediction. |
| `EnvenomPower` | 涂毒 | Owner's powered unblocked attack applies Poison. | Risk only when trigger condition matches. Apply Power unsupported. |
| `MonarchsGazePower` | 王之凝视 | Owner's powered attack applies Strength Down. | Risk only when trigger condition matches. Apply Power unsupported. |
| `ConcoctPower` | 调制 | Owner's powered unblocked attack applies Poison to the damaged target. | Risk only when trigger condition matches. Apply Power unsupported. StS2 v0.108.0 added this listener. |
| `ImbalancedPower` | 失衡 | On fully blocked owner attack, triggers monster-specific state. | Ignored by current-turn scope: stun/off-balance state affects later monster behavior, not current player-turn predictions. |
| `PaperCutsPower` | 纸伤难愈 | Owner's powered unblocked attack makes player lose max HP. | Ignored by current prediction scope: max HP mutation is not consumed by current combat hover predictions. |
| `ReaperFormPower` | 死神形态 | Applies Doom on attack. | Risk only when trigger condition matches. Apply Power unsupported. |
| `SicEmPower` | 紧追不放 | Osty hitting marked target summons/acts. | Risk only when trigger condition matches. Summon unsupported. |
| `UnderworldPower` | 幽冥之界 | Powered attacks from allied creatures other than owner or owner's own pet apply Doom equal to total damage times amount. | Risk only when trigger condition matches. Apply Power unsupported. StS2 v0.108.0 added this listener; v0.109.0 added the same-side gate. |

## AfterDamageReceived listeners

| Model | 中文名 | Original effect | Current status and feasibility |
| --- | --- | --- | --- |
| `LagavulinMatriarch` | 乐加维林族母 | Wakes/stops sleep visuals on damage. | Ignorable for current predictions except monster state; not currently modeled. |
| `AsleepPower` | 沉睡 | On HP loss, removes Plating, stuns/wakes monster, removes self. | Ignored by current-turn scope: wake/stun changes affect enemy behavior later; Plating removal affects later block generation, not current player-turn damage predictions. |
| `CurlUpPower` | 蜷身 | Records the powered attack card, then grants block/removes self later in `AfterCardPlayed`. | Marked risky when its trigger condition occurs. Directly granting block in `AfterDamageReceived` would be too early. |
| `SelfFormingClay` | 自成型黏土 | On owner HP loss, applies next-turn block power. | Ignored by current-turn scope: the block is gained next turn. |
| `FlameBarrierPower` | 火焰屏障 | When owner is attacked, damages dealer. | Implemented with simulator `Damage`. |
| `FlutterPower` | 振翅 | On powered HP loss, decrements/removes mitigation power. | Marked risky when powered owner HP loss occurs. Power mutation/removal and stun unsupported. |
| `InfernoPower` | 狱火 | During owner's turn, HP loss triggers all-enemy damage. | Implemented with simulator `Damage` against current shadow hittable enemies. |
| `HardenedShellPower` | 硬化外壳 | Tracks non-fully-blocked hits for later HP-loss cap/status. | Marked risky when owner is not fully blocked. Original value hook reads live internal state, so a state-store-only mirror would not affect later cap checks. |
| `PersonalHivePower` | 人体蜂房 | On powered damage, adds Dazed cards to dealer draw pile. | Implemented by generating Dazed previews and inserting them into the simulated draw pile with cloned Shuffle RNG. |
| `PlowPower` | 横冲直撞 | Damage-received movement/stun behavior. | Ignored by current-turn scope: the stun/monster state change affects later enemy behavior. |
| `LavaLamp` | 熔岩灯 | Marks owner took damage this combat, affecting card reward upgrades. | Ignored by current-turn scope: card reward modification happens after combat, outside combat hover prediction. |
| `ReflectPower` | 倒映 | Reflects blocked powered attack damage to dealer. | Implemented with simulator `Damage`. |
| `RupturePower` | 撕裂 | Owner losing HP during own turn applies or delays Strength. | Marked risky when its HP-loss condition occurs. Apply Power and delayed `AfterCardPlayed` application unsupported. |
| `DemonTongue` | 恶魔之舌 | Once per turn, owner HP loss heals owner. | Implemented with simulator `Heal` and a prediction-local triggered-this-turn flag. |
| `BeatingRemnant` | 律动残余 | Tracks per-turn HP loss cap/status. | Marked risky when owner is targeted. Original value hook reads live per-turn state, so chained simulated damage may drift. |
| `EmotionChip` | 情感芯片 | Tracks owner HP loss for next turn orb passive behavior/status. | Ignored by current-turn scope: the orb passive trigger occurs on the next player turn. |
| `SlipperyPower` | 滑溜 | Decrements on HP loss. | Marked risky when owner loses HP. Power mutation/removal unsupported, and later caps read live amount. |
| `ShriekPower` | 尖叫 | Stuns owner when HP first decreases below threshold. | Ignored by current-turn scope: enemy stun/intent changes affect later enemy behavior. |
| `CentennialPuzzle` | 百年积木 | First owner HP loss each combat draws cards. | Implemented with prediction-local used flag and simulator `Draw`. |
| `SlumberPower` | 熟睡 | Decrements/wakes on HP loss. | Ignored by current-turn scope: wake/stun changes affect later enemy behavior. |
| `TheGambitPower` | 孤注一掷 | Powered HP loss removes self and kills owner. | Marked risky when powered owner HP loss occurs. Kill/power removal unsupported. |

## AfterDamageReceivedLate listeners

| Model | 中文名 | Original effect | Current status and feasibility |
| --- | --- | --- | --- |
| None | - | No vanilla listener currently overrides late phase. | Registry still runs to catch modded late overrides as unsupported risk. |

## Parity notes

- `CombatPredictionSimulator.Heal` mirrors `CreatureCmd.Heal`'s shadow HP change and positive-delta `AfterCurrentHpChanged` dispatch. It intentionally omits heal VFX/SFX, map-point healing history, waits, and player hook activation on revive.
- Damage processing dispatches `AfterBlockBroken` before `AfterCurrentHpChanged` and
  `AfterDamageGiven`, matching `CreatureCmd.Damage`. Its listener details and unguarded iteration
  rule are also recorded in `block-hooks.md`.
- `AfterDamageGiven` listeners that only affect achievements, later monster behavior, or max HP state not consumed by current hover predictions are registered ignored instead of surfaced as risk.
- The remaining post-result mirrors are surfaced as risk only when their trigger conditions can affect the current player-turn prediction surface.
- Not implementable without architecture changes: Apply/Remove Power, summon, revive, monster move/state transitions, max HP loss, and combat removal. This includes StS2 v0.108.0 `ConcoctPower` and `UnderworldPower` until prediction owns power application.

## Mock model list

- None found for these hook names.
