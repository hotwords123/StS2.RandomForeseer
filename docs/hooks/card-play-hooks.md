# Card play hooks

Research baseline: StS2 v0.110.1 (`a421e19`).

Mirror files: `InCombat/Mirrors/HookMirrors.cs`,
`InCombat/Mirrors/Hooks/Card/BeforeCardPlayedMirrors.cs`,
`InCombat/Mirrors/Hooks/Card/AfterCardPlayedMirrors.cs`,
`InCombat/Mirrors/Hooks/Card/ShouldPlayMirrors.cs`,
`InCombat/Mirrors/Hooks/Card/ModifyEnergyCostInCombatMirrors.cs`,
`InCombat/Mirrors/Hooks/Card/ModifyStarCostMirrors.cs`,
`InCombat/Mirrors/Hooks/Card/CardPlayHookPredictionStates.cs`,
`InCombat/Simulation/CombatPredictionHistory.cs`, and
`InCombat/Simulation/CombatPredictionSimulator.Card.cs`.

The simulator dispatches the paired hook lifecycle in vanilla order and records both shadow `CardPlayStarted` and
`CardPlayFinished` entries. Every reviewed non-Mock vanilla override has an exact handled or ignored registration;
unsupported prediction-relevant portions record risk from their exact trigger instead of relying on the registry's
unconditional unsupported fallback.

Current implementation coverage (excluding the Mock listener):

- 54 override occurrences are fully mirrored with current simulator primitives (`Glam`, both
  `ImitationLearningPower` phases, and the current-turn portion of `CurlUpPower` proved feasible with detached
  enchantment/card/listener-consumption state);
- all 13 **Ignorable** occurrences are registered as explicit no-ops or exact no-risk handlers;
- prediction-local generation of `Stomp`, `BansheesCry`, `Pinpoint`, `MakeItSo`, or `RightHandHand` records
  incomplete risk because generated cards are not yet part of later listener enumeration;
- all 15 **Cross-hook feasible** occurrences are mirrored through shared shadow state and selective cost,
  `ShouldPlay`, damage, and block hook mirrors;
- the remaining 13 partial/blocked occurrences have exact handlers: safe portions run where available, and
  `MethodMirrorIncomplete` is recorded only when an unsupported prediction-relevant trigger actually occurs. A
  listener that only commits state for an already-unsupported power application does not add a duplicate warning.

## Hook specs

- `AbstractModel.BeforeCardPlayed(CardPlay)`
- `AbstractModel.AfterCardPlayed(PlayerChoiceContext, CardPlay)`
- `AbstractModel.AfterCardPlayedLate(PlayerChoiceContext, CardPlay)`
- `AbstractModel.ShouldPlay(CardModel, AutoPlayType)`
- `AbstractModel.TryModifyEnergyCostInCombat(CardModel, decimal, out decimal)`
- `AbstractModel.TryModifyEnergyCostInCombatLate(CardModel, decimal, out decimal)`
- `AbstractModel.TryModifyStarCost(CardModel, decimal, out decimal)`

`Hook.AfterCardPlayed` is the facade for both after phases, so this document treats
`AfterCardPlayedLate` as part of the same hook family.

## Vanilla order and dispatch semantics

Before the per-play-index lifecycle below, `CardModel.OnPlayWrapper` resolves and commits selected result-location and
play-count modifiers. Their exact ordering and shadow consumption are documented in
`card-play-result-location-hooks.md` and `card-play-count-hooks.md`.

For every generated play index, `CardModel.OnPlayWrapper` does the following:

1. Constructs a new `CardPlay` with the current `PlayIndex` and shared `PlayCount`.
2. Dispatches `Hook.BeforeCardPlayed` through the guarded combat-listener iterator.
3. Records `CardPlayStarted`.
4. Runs `CardModel.OnPlay`, then the played card's enchantment and affliction effects, with an owner-death early return
   after each stage.
5. If the owner is still alive, records `CardPlayFinished`.
6. If combat is still in progress, calls `Hook.AfterCardPlayed`.
7. The facade directly iterates all combat listeners once for `AfterCardPlayed`, then starts a fresh direct
   iteration for `AfterCardPlayedLate`.

The before hook is suppressed when combat is already over or ending at dispatch start. The after facade deliberately
bypasses that guard so listeners can finish resolving the card that caused a kill. It pushes each listener into the
`PlayerChoiceContext`; the before hook does not use a choice context.

The simulator preserves one before/ordinary-after/late-after cycle per simulated play index, including replayed cards,
and runs the ordinary phase for every listener before beginning the fresh late pass. The guarded before phase
suppresses dispatch after the shadow combat has started ending; both after passes use direct listener iteration. The
simulator also mirrors `CardModel.OnPlayWrapper`'s `IsOverOrEnding` check at the head of every play index, so a replay
does not enter its next lifecycle after an earlier index ends combat.

## Feasibility labels

- **Local feasible**: implementable with current simulator state, cloned RNG, card helpers, and optionally
  `PredictionStateStore`.
- **Cross-hook feasible**: also requires a prediction-aware value or predicate hook; mutating only local card-play
  state would not change the original hook's live-state read.
- **Partial / blocked**: part of the effect is modelable, but exact behavior needs an unsupported state domain or an
  independently missing model mirror.
- **Ignorable**: no state relevant to the current-player-turn prediction surface; use an explicit no-op registration.

The labels remain useful for architecture, while each row now records its implemented status or trigger-risk policy.

## BeforeCardPlayed listeners

| Model | 中文名 | Original effect | Prediction status |
| --- | --- | --- | --- |
| `SkillSilent1Achievement` | 成就模型 | Remembers the first local card on the current play stack for achievement bookkeeping. | **Ignorable.** Achievement state has no prediction effect. |
| `Stomp` | 踩踏 | Whenever owner plays an Attack, reduces this card's this-turn cost by 1. | **Local feasible.** Find the predicted listener card and mutate only its preview cost. Live plus shadow card-play history is already an established pattern. |
| `AfterimagePower` | 余像 | Snapshots the power amount for each owner card so the matching after hook grants that much block. | **Local feasible.** Store the amount by `CardPlay`/predicted card identity in `StateStore`. |
| `CalamityPower` | 劫难 | Snapshots the amount when owner plays an Attack; the matching after hook generates that many random Attacks. | **Local feasible.** Pair state locally; existing combat card-generation helpers and cloned `CombatCardGeneration` cover the result. |
| `ChainsOfBindingPower` | 魂缚锁链 | Marks that owner has played a non-dupe Bound card; `ShouldPlay` then prevents another Bound card this turn. | **Implemented cross-hook.** Auto-play gating reads the shadow flag. |
| `DanseMacabrePower` | 死亡之舞 | If owner spends at least the configured energy, grants block before the card effect. | **Local feasible.** `CardPlay.Resources` and simulator `GainBlock` are sufficient. |
| `FreeAttackPower` | 免费攻击 | Consumes one stack when owner plays an Attack from hand/play. | **Implemented cross-hook.** Shadow amount feeds the late energy-cost pass. |
| `FreePowerPower` | 免费能力 | Consumes one stack when owner plays a Power from hand/play. | **Implemented cross-hook.** Same targeted shadow-cost path as `FreeAttackPower`. |
| `FreeSkillPower` | 免费技能 | Consumes one stack when owner plays a Skill from hand/play. | **Implemented cross-hook.** Same targeted shadow-cost path as `FreeAttackPower`. |
| `GravityPower` | 引力 | Snapshots the power amount for each owner card so the matching after hook damages all hittable enemies. | **Local feasible.** Pair state plus simulator `Damage`. |
| `ImitationLearningPower` | 模仿学习 | On the first play in a series of the selected ally's Power, creates an owner-swapped clone for later auto-play. | **Implemented.** Creates a detached owner-swapped clone and pairs it by exact predicted-card occurrence; the after phase consumes that pair. Vanilla exposes a newly applied instance to hook iteration before `ImitationLearning.OnPlay` assigns `PlayerTarget`, so the mirror reads the nullable backing field and skips that transient uninitialized listener. |
| `JugglingPower` | 杂耍 | Counts owner Attacks; on the third, adds `Amount` clones of that Attack to hand. | **Local feasible.** Use `StateStore`, `PredictedCard.CreateClone`, `Contextual` generation classification, and normal generation hooks. |
| `MonologuePower` | 独白 | Snapshots the configured Strength amount for each owner card; the after hook applies it to the power owner. | **Risk on trigger.** Exact occurrence pairing is mirrored; matching unsupported Strength application records incomplete risk. |
| `OblivionPower` | 湮灭 | Snapshots amount for each applier card; the after hook applies Doom to the owner. | **Risk on trigger.** Exact occurrence pairing is mirrored; matching unsupported Doom application records incomplete risk. |
| `RupturePower` | 撕裂 | Opens a per-card accumulator so HP loss caused during that card is converted to Strength after the play. | **Risk on trigger.** The damage hook records risk only when owner HP loss would apply Strength. |
| `SerpentFormPower` | 群蛇形态 | Snapshots amount for each owner card; the after hook damages one random hittable enemy. | **Local feasible.** Pair state, cloned `CombatTargets`, and simulator `Damage` cover it. |
| `SlothPower` | 懒惰 | Increments the owner's cards-played counter before the card effect; `ShouldPlay` enforces the cap. | **Implemented cross-hook.** Nested auto-play gating reads the shadow counter. |
| `SpiritOfAshPower` | 灰烬之灵 | Owner gains block before playing an Ethereal card. | **Local feasible.** Use predicted keywords and simulator `GainBlock`. |
| `StormPower` | 雷暴 | Snapshots amount when owner plays a Power; the after hook channels that many Lightning orbs. | **Local feasible.** Pair state plus simulator `OrbChannel`. |
| `StranglePower` | 紧勒 | Snapshots amount for each applier card; the after hook deals unblockable damage to the power owner. | **Local feasible.** Pair state plus simulator `Damage`. |
| `SubroutinePower` | 子程序 | Snapshots amount when owner plays a Power; the after hook grants that much energy. | **Local feasible.** Pair state plus simulator `GainEnergy`. |
| `SurroundedPower` | 遭到包围 | Before owner's targeted card resolves, turns the power owner and pets toward that target, changing subsequent back-attack damage against the owner. | **Implemented cross-hook.** Retaliation/reaction damage reads shadow facing; visual flipping/music are ignored. |
| `TheSealedThronePower` | 封印王座 | Grants owner stars before every owner card effect. | **Implemented.** Uses simulator `GainStars`; `AfterStarsGained` remains outside current mirror coverage. |
| `VeilpiercerPower` | 刺破帷幕 | Consumes one stack when owner plays an Ethereal card from hand/play. | **Implemented cross-hook.** Shadow amount feeds its zero-cost modifier. |
| `ChemicalX` | 化学物X | Flashes when owner plays an energy-X or star-X card. | **Ignorable.** The actual X increase is the separate read-only `ModifyXValue` hook. |
| `IntimidatingHelmet` | 骇人头盔 | If owner spends at least the configured energy, grants block before the card effect. | **Local feasible.** `CardPlay.Resources` and simulator `GainBlock` are sufficient. |
| `MusicBox` | 音乐盒 | Remembers the first owner Attack this turn so the matching after hook can clone it with Ethereal. | **Local feasible.** Pair state, preview cloning, keyword mutation, and `Contextual` generated-card flow are available. |
| `PaelsEye` | 佩尔之眼 | Changes relic display status when owner manually plays a card before the relic has triggered. | **Ignorable.** Gameplay uses combat history/extra-turn hooks, not this status mutation. |
| `PenNib` | 钢笔尖 | Advances the Attack counter and marks every tenth owner Attack as the card whose damage is doubled. | **Implemented cross-hook.** Shadow counter and exact play occurrence feed a prediction-aware multiplier. |

## AfterCardPlayed listeners: relics

| Model | 中文名 | Original effect | Prediction status |
| --- | --- | --- | --- |
| `ArtOfWar` | 孙子兵法 | Records that owner played an Attack, affecting next-turn energy. | **Ignorable.** Only a later turn is affected. |
| `BrilliantScarf` | 艳丽围巾 | Counts manual owner cards so the fifth card's energy and star costs are zero. | **Implemented cross-hook.** Energy and star cost helpers read the shadow counter. |
| `DaughterOfTheWind` | 风的女儿 | Grants block after every owner Attack. | **Local feasible.** Use simulator `GainBlock`. |
| `GamePiece` | 棋子 | Draws cards after owner plays a Power. | **Local feasible.** Use simulator `Draw`. |
| `HelicalDart` | 螺线飞镖 | Playing an owner Shiv applies Dexterity. | **Risk on trigger.** Owner Shiv plays record incomplete risk for unsupported Dexterity application. |
| `IronClub` | 铁棒 | Counts owner cards and draws one every configured interval. | **Local feasible.** Initialize a `StateStore` counter from live state and use simulator `Draw`. |
| `IvoryTile` | 象牙麻将牌 | If the card spent enough energy, grants energy. | **Local feasible.** `CardPlay.Resources` and simulator `GainEnergy` are sufficient. |
| `Kunai` | 苦无 | Every configured number of owner Attacks applies Dexterity. | **Risk on trigger.** Shadow counter is maintained; only threshold hits record risk. |
| `Kusarigama` | 锁镰 | Every configured number of owner Attacks damages one random hittable enemy. | **Local feasible.** Use shadow counter, cloned `CombatTargets`, and simulator `Damage`. |
| `LetterOpener` | 开信刀 | Every configured number of owner Skills damages all hittable enemies. | **Local feasible.** Use shadow counter and simulator `Damage`. |
| `LostWisp` | 迷失鬼火 | Owner Power cards damage all hittable enemies. | **Local feasible.** Use simulator `Damage`. |
| `MummifiedHand` | 干瘪之手 | After owner plays a Power, selects a random card in hand through four cost-based fallback pools and makes it free this turn. | **Local feasible.** Use shadow hand, cloned `CombatCardSelection`, and prediction cost helpers; never call live card cost/state APIs. |
| `MusicBox` | 音乐盒 | Clones the remembered Attack into hand, adds Ethereal, and consumes the once-per-turn trigger. | **Local feasible.** Complete the paired `StateStore` transaction and use normal generated-card flow. |
| `Nunchaku` | 双截棍 | Every configured number of owner Attacks grants energy. | **Local feasible.** Shadow counter plus simulator `GainEnergy`. |
| `OrnamentalFan` | 精致折扇 | Every configured number of owner Attacks grants block. | **Local feasible.** Shadow counter plus simulator `GainBlock`. |
| `PaelsLegion` | 佩尔的士兵 | After the card whose block was doubled finishes, starts the relic cooldown and consumes the doubling trigger. | **Implemented cross-hook.** Block selection and cooldown commit share the exact `CardPlay`; pet animation is ignored. |
| `PenNib` | 钢笔尖 | Clears the current doubled-Attack marker after that card resolves. | **Implemented cross-hook.** Clears the exact occurrence used by the damage multiplier. |
| `Permafrost` | 永冻冰晶 | The first owner Power each combat grants block and consumes the trigger. | **Local feasible.** State-store once/combat flag plus simulator `GainBlock`. |
| `Pocketwatch` | 怀表 | Counts owner cards for next-turn hand draw. | **Ignorable.** Only a later turn is affected. |
| `RainbowRing` | 彩虹戒指 | After owner has played Attack, Skill, and Power this turn, applies Strength and Dexterity once. | **Risk on trigger.** Shadow counters and once-per-turn consumption are maintained; completion records risk. |
| `RazorTooth` | 剃刀牙 | Upgrades an upgradable owner Attack or Skill after it is played. | **Implemented.** Upgrades only the detached predicted card through the simulator command; the live/deck card is never mutated. |
| `RippleBasin` | 波纹水盆 | Changes display status after an owner Attack. | **Ignorable.** End-turn gameplay checks card-play history directly; the status is cosmetic. |
| `Shuriken` | 手里剑 | Every configured number of owner Attacks applies Strength. | **Risk on trigger.** Shadow counter is maintained; only threshold hits record risk. |
| `TuningFork` | 音叉 | Counts owner Skills persistently and grants block at each threshold. | **Local feasible.** Initialize the shadow counter from `SkillsPlayed`; use simulator `GainBlock`. |
| `UnsettlingLamp` | 不安油灯 | Marks its one-combat debuff-doubling trigger finished after the triggering card. | **Deferred without additional risk.** This state commit only matters after an unsupported power application; that source already records risk, so the after hook uses an exact no-op handler to avoid a duplicate warning. |
| `Vambrace` | 臂甲 | After the card whose block was doubled finishes, consumes the once-per-combat doubling. | **Implemented cross-hook.** Shadow block multiplier and exact play commit cover chained block gains. |
| `VelvetChoker` | 天鹅绒颈圈 | Increments the owner's cards-played counter used by `ShouldPlay`. | **Implemented cross-hook.** Nested auto-play gating reads the shadow counter. |

## AfterCardPlayed listeners: powers

| Model | 中文名 | Original effect | Prediction status |
| --- | --- | --- | --- |
| `AfterimagePower` | 余像 | Consumes the paired snapshot and grants owner block. | **Local feasible.** Complete the before/after pair with simulator `GainBlock`. |
| `BlackHolePower` | 黑洞 | On the last play in a series, a card that spent stars damages all enemies. | **Local feasible.** `CardPlay.Resources`, series metadata, and simulator `Damage` are available. |
| `CalamityPower` | 劫难 | Consumes the paired snapshot and generates random Attack cards into hand. | **Local feasible.** Existing safe `GetForCombat` prediction helpers reproduce the pool and cloned RNG without adding cards to live state. |
| `CurlUpPower` | 蜷身 | After the damaging card finishes, grants block, marks the louse curled, and removes this power. | **Implemented for current scope.** Tracks the exact damaging card, grants block in after-play order, and consumes the shadow listener; curled state is read only by later monster moves. |
| `DevourLifePower` | 吞噬生命 | Playing an owner Soul summons Osty. | **Risk on trigger.** Matching Soul plays record incomplete summon risk. |
| `EchoFormPower` | 回响形态 | Updates VFX after card-play-started history reaches the replay limit. | **Ignorable.** Replay count is handled by separate play-count hooks; this body is visual only. |
| `EnragePower` | 激怒 | Whenever a Skill is played, applies Strength to the power owner. | **Ignorable for current scope.** Vanilla applies it to Test Subject, so it only changes later enemy-turn attacks. |
| `GalvanicPower` | 流电 | A Galvanized card deals move damage to its owner after play. | **Local feasible.** Read predicted affliction and use simulator `Damage`. |
| `GravityPower` | 引力 | Consumes the paired snapshot and damages all hittable enemies. | **Local feasible.** Use simulator `Damage`. |
| `HauntPower` | 纠缠 | An owner Soul damages one random hittable enemy. | **Local feasible.** Cloned `CombatTargets` plus simulator `Damage`. |
| `ImitationLearningPower` | 模仿学习 | Consumes the paired clone, decrements the power, and auto-plays the cloned ally Power. | **Implemented.** Decrements the shadow amount and auto-plays the detached clone through the normal simulator path; the cloned card's `OnPlay` mirror independently reports its own support or risk. |
| `MasterPlannerPower` | 谋划专家 | Adds Sly to every owner Skill after it resolves. | **Local feasible.** Mutate only predicted keywords so later shadow plays see it. |
| `MonologuePower` | 独白 | Applies the paired Strength amount to the power owner and updates its accumulator. | **Risk on trigger.** Only a matching paired occurrence records risk. |
| `OblivionPower` | 湮灭 | Applies the paired Doom amount to the owner. | **Risk on trigger.** Only a matching paired occurrence records risk. |
| `PaleBlueDotPower` | 暗淡蓝点 | On the fifth owner card this turn, applies next-turn draw once. | **Ignorable for current scope.** The result cannot affect the current player turn. |
| `PanachePower` | 神气制胜 | Counts owner cards and damages all enemies every five cards. | **Local feasible.** State-store counter plus simulator `Damage`. |
| `RagePower` | 狂怒 | Grants block after every owner Attack. | **Local feasible.** Use simulator `GainBlock`. |
| `RupturePower` | 撕裂 | Converts the paired during-card HP-loss accumulator into Strength. | **Risk on trigger.** Relies on the damage mirror's HP-loss-conditional risk without adding an unconditional warning. |
| `SerpentFormPower` | 群蛇形态 | Consumes the paired snapshot and damages one random hittable enemy. | **Local feasible.** Cloned `CombatTargets` plus simulator `Damage`. |
| `SlowPower` | 缓慢 | Increments the damage multiplier by 10 percentage points after every card. | **Implemented cross-hook.** The multiplicative damage pass reads the shadow amount. |
| `SmoggyPower` | 烟雾弥漫 | After owner plays a Skill, afflicts every unafflicted owner Skill with Smog. | **Local feasible.** Iterate shadow piles and use simulator `Afflict`; do not call live card APIs. |
| `SneakyPower` | 鬼祟 | Whenever another creature's Attack is played, grants block to the power owner. | **Local feasible.** Use simulator `GainBlock`. |
| `StormPower` | 雷暴 | Consumes the paired snapshot and channels Lightning orbs. | **Local feasible.** Use simulator `OrbChannel`. |
| `StranglePower` | 紧勒 | Consumes the paired snapshot and deals unblockable damage to the power owner. | **Local feasible.** Use simulator `Damage`. |
| `SubroutinePower` | 子程序 | Consumes the paired snapshot and grants owner energy. | **Local feasible.** Use simulator `GainEnergy`. |
| `TenderPower` | 柔嫩 | After every owner card, applies negative Strength and Dexterity to owner. | **Risk on trigger.** Owner card plays record incomplete risk. |
| `VitalSparkPower` | 活力火花 | A Tainted card applies TaintedPower to its owner after play. | **Ignorable for current scope.** `TaintedPower` only changes later powered attack damage received and does not affect the current-player-turn prediction surface. |
| `VoidFormPower` | 虚空形态 | Counts non-auto owner cards on the last play in a series; later cards stop being free at the power amount. | **Implemented cross-hook.** Shadow count feeds both energy- and star-cost helpers. |
| `WitheringPresencePower` | 凋萎存在 | Counts target-player cards and adds a Wither to hand every six. | **Local feasible.** State-store counter plus fixed generated-card flow. |

## AfterCardPlayed listeners: cards, enchantments, and bookkeeping

| Model | 中文名 | Original effect | Prediction status |
| --- | --- | --- | --- |
| `BansheesCry` | 女妖之嚎 | Whenever owner plays an Ethereal card, reduces this listener card's this-combat cost. | **Local feasible.** Find the predicted listener card and mutate only its preview cost. |
| `Pinpoint` | 精密瞄准 | Whenever owner plays a Skill, reduces this listener card's this-turn cost. | **Local feasible.** Find the predicted listener card and mutate only its preview cost. |
| `Glam` | 华彩 | Disables Replay on its card after the first play this combat. | **Implemented.** Mutates only detached enchantment used/status; the shadow play-count helper consumes it on later plays. |
| `Goopy` | 黏糊 | Increments its enchantment amount, permanently increasing the card's block bonus. | **Local feasible for prediction.** Mutate only the detached enchantment/card preview; never touch `DeckVersion`. |
| `Vigorous` | 活力 | Disables its damage bonus after its card is played. | **Local feasible.** Store disabled status on the detached predicted enchantment for later shadow plays. |
| `Play20CardsSingleTurnAchievement` | 成就模型 | Counts local cards and unlocks an achievement at 20. | **Ignorable.** |
| `SkillSilent1Achievement` | 成就模型 | Clears local stack/achievement counters after the remembered root card. | **Ignorable.** |
| `CccComboModel` | 徽章模型 | Counts local cards and unlocks the 20-card badge. | **Ignorable.** |

## AfterCardPlayedLate listeners

| Model | 中文名 | Original effect | Research disposition |
| --- | --- | --- | --- |
| `MakeItSo` | 如此甚好 | If outside hand, returns itself to hand after every configured owner Skill, using finished-card history that already includes the current play. | **Local feasible.** Combine live and shadow finished history, then move the predicted listener card from its shadow pile. |
| `RightHandHand` | 得力助手 | If in discard, returns itself to hand after owner plays a card that spent at least the configured energy. | **Local feasible.** Use `CardPlay.Resources` and shadow pile movement. |

## Implemented slices

1. Add hook contexts/registries and exact vanilla dispatch order. Add shadow `CardPlayStarted` before `OnPlay`; keep
   `CardPlayFinished` before both after phases. Preserve the before guarded / after unguarded distinction against
   simulated combat-ending state.
2. First cover local effects that can change the current card or immediate projection: `PenNib`, pre-play block/stars,
   post-play block/damage/draw/energy/orbs, and paired RNG effects.
3. Add selective hook mirrors for the four cross-hook families: damage modifiers (`PenNib`, `SlowPower`,
   `SurroundedPower`), block modifiers (`PaelsLegion`, `Vambrace`), cost modifiers (free-card powers,
   `BrilliantScarf`, `VeilpiercerPower`, `VoidFormPower`), and `ShouldPlay` (`ChainsOfBindingPower`, `SlothPower`,
   `VelvetChoker`). Calling the original hooks after only updating `StateStore` would still read stale live fields.
4. Add shadow counters/history consumers and card-listener mutation (`Stomp`, `BansheesCry`, `Pinpoint`,
   `MakeItSo`, `RightHandHand`). Centralize live-plus-shadow card-play counts instead of duplicating the helper already
   present in `CardDrawCardMirrors`.
5. Register explicit no-ops for ignorable listeners and explicit risk handlers for Apply/Remove Power, summon, and
   monster-state effects. Do not silently no-op a trigger that can alter a later nested action.

## Parity and risk notes

- The former simulator TODOs were lifecycle gaps rather than merely missing visible bonus effects. The completed
  lifecycle and hook mirrors now advance shadow cost, `ShouldPlay`, block, and damage state through chained plays.
- Selective hook mirrors preserve original listener order while replacing only exact listeners that consume card-play
  shadow state. Other cost, predicate, damage, and block value listeners continue to call their original read-only
  methods; those selective registry misses bypass unsupported lookup and do not record mirror risk. Side-effect hooks
  such as `AfterModifyingBlockAmount` retain normal action-registry unsupported-risk handling.
- Hook listeners are currently enumerated from the live `CombatState`. Prediction-generated cards are not added as
  listeners, so generated copies of `Stomp`, `BansheesCry`, `Pinpoint`, `MakeItSo`, or `RightHandHand` remain a parity
  gap until hook iteration can include shadow generated cards in vanilla order. Their generation now records
  `MethodMirrorIncomplete` rather than silently omitting that future listener behavior.
- Multi-play cards run both hook phases once per play index. Pair state should use `CardPlay` occurrence identity or
  an equivalent stack-safe key, not only card identity; nested play can interleave another card before the outer after
  phase.
- `AfterCardPlayedLate` must remain a second full listener pass. Running each listener's ordinary and late methods
  back-to-back would change `MakeItSo`/`RightHandHand` pile observations and other listener interactions.
- `RazorTooth` and `Goopy` have persistent live/deck side effects in vanilla. Prediction may mirror their detached
  current-combat card state when relevant, but must never mutate the live card or its `DeckVersion`.

## Coverage summary

StS2 v0.110.1 has 29 `BeforeCardPlayed`, 65 `AfterCardPlayed`, and 2 `AfterCardPlayedLate` overrides. Excluding the
single Mock listener gives 95 override occurrences:

- 54 are fully mirrored with current simulator primitives (including both `ImitationLearningPower` phases, `Glam`,
  and current-scope `CurlUpPower`);
- 15 are mirrored through targeted value/predicate hooks that also read shadow state;
- 13 are ignorable under the current-player-turn scope;
- 13 retain trigger-conditional partial/blocked coverage for Apply/Remove Power, summon, or monster state gaps; when
  a hook only commits state for an already-unsupported source, its exact handler avoids adding a duplicate warning.

These counts classify override occurrences, so a paired model such as `AfterimagePower` appears once in each relevant
phase.

## Mock model list

- `MockCloneCardsOnPlayPower`: after any owner card, adds a clone to hand. Once shadow generated-card listeners are
  supported, it is useful as a recursion/ordering test but should not be registered as vanilla gameplay coverage.
