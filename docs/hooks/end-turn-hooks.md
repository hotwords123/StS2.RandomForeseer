# End turn hooks

Simulation-facing hook facade: `InCombat/Mirrors/HookMirrors.cs`.

Mirror files:

- `InCombat/Mirrors/CombatMirrorContext.cs`
- `InCombat/Mirrors/Hooks/TurnEnd/AfterAutoPostPlayPhaseEnteredMirrors.cs`
- `InCombat/Mirrors/Hooks/TurnEnd/AfterFlushMirrors.cs`
- `InCombat/Mirrors/Hooks/TurnEnd/AfterSideTurnEndMirrors.cs`
- `InCombat/Mirrors/Hooks/TurnEnd/BeforeFlushMirrors.cs`
- `InCombat/Mirrors/Hooks/TurnEnd/BeforeSideTurnEndMirrors.cs`
- `InCombat/Mirrors/Hooks/TurnEnd/DoomPowerMirrors.cs`
- `InCombat/Mirrors/Hooks/TurnEnd/OrichalcumMirrors.cs`
- `InCombat/Mirrors/Hooks/TurnEnd/SideTurnEndMirrorContext.cs`
- `InCombat/Mirrors/Shared/JossPaperMirrors.cs`

## Hook specs

- `AbstractModel.AfterAutoPostPlayPhaseEntered(PlayerChoiceContext, Player)`
- `AbstractModel.BeforeSideTurnEndVeryEarly(PlayerChoiceContext, CombatSide, IEnumerable<Creature>)`
- `AbstractModel.BeforeSideTurnEndEarly(PlayerChoiceContext, CombatSide, IEnumerable<Creature>)`
- `AbstractModel.BeforeSideTurnEnd(PlayerChoiceContext, CombatSide, IEnumerable<Creature>)`
- `AbstractModel.BeforeFlush(PlayerChoiceContext, Player)`
- `AbstractModel.BeforeFlushLate(PlayerChoiceContext, Player)`
- `AbstractModel.ShouldFlush(Player)`
- `AbstractModel.AfterFlush(PlayerChoiceContext, Player, IReadOnlyCollection<CardModel>, IReadOnlyCollection<CardModel>)`
- `AbstractModel.AfterSideTurnEnd(PlayerChoiceContext, CombatSide, IEnumerable<Creature>)`
- `AbstractModel.AfterSideTurnEndLate(PlayerChoiceContext, CombatSide, IEnumerable<Creature>)`
- `AbstractModel.ShouldEtherealTrigger(CardModel)`

## AfterAutoPostPlayPhaseEntered listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `HowlFromBeyond` | 彼岸咆哮 | If in exhaust pile, auto-plays for owner. | Implemented for immediate auto-play damage through simulator `AutoPlay` and the shared `CardOnPlayMirrors` handler. |
| `IAmInvincible` | 所向无敌 | If top of owner draw pile, auto-plays from draw pile. | Implemented for draw-pile selection, play-pile movement, and immediate block gain through simulator `AutoPlayFromDrawPile` and the shared `CardOnPlayMirrors` handler. |
| `StampedePower` | 惊逃 | Auto-plays playable Attacks in hand. | Selects candidates with cloned `Shuffle` RNG and calls generic simulator `AutoPlay`; unsupported generic `OnPlay` bodies are risk-marked, so this remains a partial mirror until individual attack effects or full card-play simulation are supported. |

## BeforeSideTurnEndVeryEarly listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `Orichalcum` | 奥利哈钢 | Records whether owner had no block before early end-turn block effects. | Implemented with state-store flag. |
| `FakeOrichalcum` | 奥利哈钢？？？ | Same as Orichalcum. | Implemented with state-store flag. |
| `AsleepPower` | 沉睡 | Removes Plating near wake-up timing. | Ignored. Power removal is unsupported and does not directly affect currently modeled predictions. |

## BeforeSideTurnEndEarly listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `PlatingPower` | 覆甲 | Owner gains block before damage effects. | Implemented via `GainBlock`. |
| `RegenPower` | 再生 | If the owner is a living participant, heals owner by amount and decrements Regen. | Healing implemented via simulator `Heal` before normal side-turn-end effects such as Doom. Regen amount decrement is not persisted because no later hook in this simulation consumes it. |
| `PaelsEye` | 佩尔之眼 | If owner played no cards, exhausts all cards in hand before granting an extra turn. | Implemented for immediate hand exhaust and exhaust hooks; still marks risk because extra-turn scheduling is not modeled. |

## BeforeSideTurnEnd listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `Orichalcum` | 奥利哈钢 | If very-early flag is set, owner gains block. | Implemented. |
| `FakeOrichalcum` | 奥利哈钢？？？ | Same as Orichalcum. | Implemented. |
| `CloakClasp` | 斗篷扣 | Owner gains block per card in hand. | Implemented. |
| `RippleBasin` | 波纹水盆 | Owner gains block if no Attack was played this turn. | Implemented. Uses live combat history like original. |
| `HailstormPower` | 冰雹风暴 | If owner has enough Frost orbs, damages all hittable enemies. | Implemented via `Damage`. |
| `ScreamingFlagon` | 尖叫酒壶 | If owner hand is empty, damages all hittable enemies. | Implemented via `Damage`. |
| `StoneCalendar` | 历石 | On configured turn, damages all hittable enemies. | Implemented via `Damage`. |
| `TheBombPower` | 炸弹 | At final countdown, damages all hittable enemies. | Implemented via `Damage`. |
| `DoomPower` | 灾厄 | On a non-player side end, a participating, living owner triggers only when it is first in the side's doomed-creature list; Doom then kills that list. | Simulates the batch kill and records `MethodMirrorIncomplete` before killing because `AfterDiedToDoom` and the full death lifecycle remain unsupported. |
| `Regret` | 悔恨 | Stores hand size so its turn-end-in-hand effect later deals unblockable self damage. | Implemented by storing the shadow hand size on the card's mutable preview for the `CardModel.OnTurnEndInHand` mirror. |
| `PaelsTears` | 佩尔之泪 | Gains energy. | Ignored. Energy does not affect current predictions. |
| `ChainsOfBindingPower` | 魂缚锁链 | Clears Bound from all cards and resets internal flag. | Implemented for mirror pile state by clearing Bound on `PredictedCard` previews. Live internal flag reset is not mutated. |
| `SandpitPower` | 沙坑 | Updates creature positions on enemy side turn end. | Ignored. Enemy-side positioning does not affect current player-turn predictions. |

## ShouldFlush, BeforeFlush, and AfterFlush

`ShouldFlush` directly forwards to `Hook.ShouldFlush`, preserving vanilla listener order and its short-circuit result.
Vanilla then dispatches `BeforeFlush` and `BeforeFlushLate` for each ending player before phase two. The simulator
preserves those as separate listener passes. `SlumberingEssence` reduces its attached card's predicted energy cost by
1 when that card is still in its owner's simulated hand. The modifier is added to the mutable preview and lasts until
the card is played, matching the vanilla `EnergyCost.AddUntilPlayed(-1)` call without changing canonical card state.
`BeforeFlushLate` currently has a placeholder registry.

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `SlumberingEssence` | 沉眠精华 | Reduces the enchanted card's cost by 1 if it remains in its owner's hand. | Implemented on the mutable preview with an until-played cost modifier. |

The simulator then moves non-retained shadow hand cards to the discard pile and invokes `AfterFlush`.
Vanilla also runs `EndOfTurnCleanup`; prediction intentionally skips it to avoid cloning every combat card on each
refresh. This performance tradeoff can leave turn-local costs or flags active in uncommon deferred-draw/autoplay chains.

`AfterFlush` currently uses a placeholder registry. Each listener override without a registered handler records
`MethodNotMirrored` risk; the vanilla override is `Bookmark`, whose retained-card cost selection is not yet simulated.
The registry receives predicted flushed and retained cards and never invokes the live override.

## AfterSideTurnEnd listeners

Vanilla calls `AfterSideTurnEnd` in player turn phase two, after each ending player's hand is flushed and
`PlayerCombatState.EndOfTurnCleanup` runs. It then calls `AfterSideTurnEndLate` as a separate pass. The simulator
dispatches both passes through exact-type registries. Only listeners with explicit handlers run; an unregistered
listener is silently skipped, its vanilla body is not called, and no risk is recorded. This keeps the registry focused
on supported behavior while full power removal/duration mirrors are unavailable, accepting uncommon chained-effect
differences as a temporary implementation tradeoff. The rows below identify
effects that can contribute to the current damage/health projection or to damage reached through an end-of-turn draw.

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `ConsumingShadowPower` | 吞噬暗影 | For a participating owner with orbs, evokes the current last orb once per stack, removing it each time. | Implemented with one simulator `OrbEvokeLast` call per iteration. Each call re-reads the shadow queue and dequeues before evoke/hooks; an empty queue does nothing. |
| `ConstrictPower` | 紧缠 | Deals `Amount` unpowered damage to its participating owner. | Implemented through simulator `Damage` with the owner as dealer. |
| `DarkEmbracePower` | 黑暗之拥 | Draws `Amount ×` the number of Ethereal cards exhausted this turn after the hand flush. | Implemented with a prediction-state Ethereal count collected by `AfterCardExhausted` and simulator `Draw` after flush. |
| `DemisePower` | 消亡 | Deals `Amount` unblockable, unpowered damage to its participating owner. | Implemented through simulator `Damage` with `Unblockable \| Unpowered`. |
| `DoomPower` | 灾厄 | On a non-enemy side end, a participating, living owner triggers only when it is first in the side's doomed-creature list; Doom then kills that list. | Batch kill is simulated when vanilla's trigger conditions are met. Records `MethodMirrorIncomplete` before killing, only on that trigger, because the subsequent `AfterDiedToDoom` pass and some recursive death behavior are not mirrored. |
| `JossPaper` | 金纸 | Adds the Ethereal exhaust count to its counter and draws for each completed threshold after the hand flush. | Implemented with prediction-state counters and simulator `Draw`; threshold and remainder use the vanilla dynamic-var values. |
| `MagicBombPower` | 魔法炸弹 | If the owner is a participant and its applier is alive, deals `Amount` unpowered damage to the owner, then removes itself. | Implemented with the living-applier guard and simulator `Damage`. The trailing `PowerCmd.Remove` is omitted because it is cleanup after damage and does not affect this turn's projection. |
| `ParryingShield` | 招架盾 | If its player owner is participating and has at least the relic's block threshold, chooses a random hittable enemy and deals its damage value. | Implemented using shadow block, cloned `CombatTargets` RNG, and simulator `Damage`. |

## AfterSideTurnEndLate listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `DisintegrationPower` | 瓦解 | Deals `Amount` unpowered damage to its participating owner after all regular `AfterSideTurnEnd` listeners. | Implemented through simulator `Damage` in the late pass, preserving its order after regular listeners. |

The two deferred draw listeners matter to the damage projection indirectly. Their draws enter the existing
`AfterCardDrawnEarly`/`AfterCardDrawn` mirror pipeline: for example, `HellraiserPower` can autoplay a drawn Strike,
`SpeedsterPower` can damage enemies on a non-hand draw during its owner's turn, and `CacophonyPower` can select a
random target and deal damage. The simulator's draw pipeline preserves nested draws, cloned RNG, autoplay, and damage
history order; `DarkEmbracePower` and `JossPaper` are not harmless bookkeeping.

### AfterSideTurnEnd effects intentionally not simulated

These vanilla overrides are not registered while full power lifecycle mirrors remain unavailable. Their direct effects
are mostly cleanup or later-turn state, but some can affect subsequent callbacks in this turn. The dispatch policy
above deliberately skips them without risk, accepting the uncommon accuracy limitations described below.

- Removal-only cleanup: `BorrowedTimePower`, `BurstPower`, `ConcoctPower`, `CorrosiveWavePower`, `CoveredPower`,
  `DuplicationPower`, `FlameBarrierPower`, `FlankingPower`, `GravityPower`, `InterceptPower`, `KnockdownPower`,
  `NoDrawPower`, `NoEnergyGainPower`, `OblivionPower`, `OneTwoPunchPower`, `RagePower`, `ReboundPower`,
  `RingingPower`, `ShadowmeldPower`, `SicEmPower`, `StranglePower`, `TaintedPower`, `TangledPower`, and
  `UnderworldPower`.
- Turn counters, durations, reset flags, and presentation: `AsleepPower`, `ColossusPower`, `ConquerorPower`,
  `DebilitatePower`, `DoubleDamagePower`, `EscapeArtistPower`, `FrailPower`, `HatchPower`, `HellraiserPower`,
  `IntangiblePower`, `JugglingPower`, `NoBlockPower`, `PaleBlueDotPower`, `PanachePower`, `RetainHandPower`,
  `ShrinkPower`, `SkittishPower`, `SlumberPower`, `WeakPower`, `VulnerablePower`, `ArtOfWar`, and `Kusarigama`.
- Next-turn resources, stats, or card state: `HighVoltagePower`, `MonologuePower`, `NemesisPower`, `RitualPower`,
  `SmoggyPower`, `TenderPower`, `TemporaryDexterityPower`, `TemporaryFocusPower`, `TemporaryStrengthPower`,
  `TerritorialPower`, and `LunarPastry`.
- `BattlewornDummyTimeLimitPower` sets the event encounter's `RanOutOfTime` flag and escapes its owner at timeout.
  Its owner is an enemy, so it runs at that enemy side's end rather than the current player-side end. It can change
  that special encounter's result, but is outside the current damage-only EndTurnPrediction payload; supporting it
  requires enemy-turn encounter/escape state simulation.

When extending phase two, preserve listener order and carry state that later callbacks consume. For example, skipped
`NoDrawPower` removal can block `JossPaper`'s deferred draws, and an expired replay or damage modifier can still affect
autoplay. These uncommon interactions are accepted for now without risk until the required power lifecycle is modeled.

`MockPhaseObserverPower` is a test observer, not a vanilla gameplay listener.

## Parity notes

- StS2 v0.108.0 renamed the side-wide turn-end hooks from `BeforeTurnEnd` / `AfterTurnEnd` to `BeforeSideTurnEnd` / `AfterSideTurnEnd`. The simulator mirrors the player phase-one `BeforeSideTurnEnd` path and dispatches phase-two `AfterFlush`, `AfterSideTurnEnd`, and `AfterSideTurnEndLate` through registries.
- StS2 v0.109.0 removed `DiamondDiadem` from `BeforeSideTurnEnd` and deleted
  `DiamondDiademPower`. The relic now grants block and Blur at the first side-turn start, outside
  the current end-turn prediction surface, so it is no longer registered here.
- `Regret` records the shadow hand size on its mutable preview before ethereal cards are exhausted and before
  turn-end card resolution moves cards to the play pile, matching vanilla timing without mutating the real card's
  private counter.
- StS2 v0.111.0 moved play-pile and result-pile transitions for cards with turn-end-in-hand effects out of
  `CardModel.OnTurnEndInHandWrapper` and into `CombatManager.DoTurnEndCards` / `ResolveTurnEndCardEffects`.
  Effects remain serialized in hand order; only their presentation tweens overlap. The simulator already follows the
  same shadow-pile order and now documents the new ownership explicitly.
- The end-turn simulator currently follows the prediction-relevant ordering of StS2 v0.111.0
  `CombatManager.EndPlayerTurnPhaseOneInternal`: auto-post-play hooks, `BeforeSideTurnEnd`, per-player orb triggers,
  ethereal exhaust, then turn-end card resolution. It invokes the simulator's `CheckWinCondition` safe point after
  `BeforeSideTurnEnd`, after all per-player turn-end tasks, and after `BeforeFlush`. Each player's `DoTurnEnd`
  independently stops after its orb triggers when the shadow combat is already over or ending. This preserves
  vanilla's task boundary without running ethereal exhaust or turn-end card effects after combat-ending damage.
- After all per-player phase-one turn-end tasks, vanilla calls `BeforeFlush` and `BeforeFlushLate` for each ending
  player, then checks for combat end before phase two. The simulator dispatches both passes; `BeforeFlush` mirrors
  `SlumberingEssence` (沉眠精华), while unknown and late overrides record `MethodNotMirrored` risk.
- Phase two mirrors `FlushPlayerHand` for each ending player, directly forwards `ShouldFlush`, moves shadow cards,
  and dispatches `AfterFlush`; it then dispatches regular and late side-end passes. `EndOfTurnCleanup` is intentionally
  skipped to avoid cloning all combat cards, as described above. Keep
  participant selection aligned with `PlayersTakingExtraTurn` and the actual ending players. `AfterFlush` remains a
  placeholder registry whose unregistered overrides record risk; regular and late side-end passes invoke only exact
  registered handlers and silently skip all other listeners without risk.
- Remaining side-end work includes modeling `AfterDiedToDoom` consequences and other death recursion that can follow a
  Doom batch kill. Deferred Ethereal draws for `DarkEmbracePower` and `JossPaper` now flow through
  `CombatPredictionSimulator.Draw`, including prediction-aware draw hooks that can cause more draws, autoplay, RNG use,
  and damage.
- Ethereal resolution uses `HookMirrors.ShouldEtherealTrigger`, preserving the original guarded all-must-allow
  predicate while applying compatibility filtering to Mod listeners.

## Mock model list

- `MockPhaseObserverPower`
