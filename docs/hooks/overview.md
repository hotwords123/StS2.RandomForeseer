# Hook mirror overview

## Guidelines

- Prefer original read-only value hooks when vanilla already uses them for previews. Current examples: `Hook.ModifyBlock`, `Hook.ModifyDamage`, `Hook.ModifyHpLost`, card reward creation options, and card reward upgrade odds.
- Mirror only side effects that can change prediction output: draw order, hand/discard/exhaust piles, preview card cost/dynamic vars, block, damage, death/liveness, orb counts, current-turn energy, and RNG consumption.
- Combat predictions are scoped to outcomes that can still affect the current player turn. Do not mark risk only because vanilla would mutate state for an enemy turn, a later player turn, room-end rewards, or future reward screens, unless that state can feed back into a prediction surfaced before the current player turn finishes.
- Do not simulate VFX, SFX, waits, achievement unlocks, or effects that cannot occur during the current player-turn prediction surface.
- Track supported current-turn energy and star gain/loss in `SimPlayerCombatState`. Simulated manual card plays spend shadow energy/stars; autoplay computes `ResourceInfo` values without spending resources, matching vanilla autoplay. Turn-start energy/star reset remains outside full card-play simulation.
- Treat Apply Power, Remove Power, summon, revive, monster move/state changes, combat removal, player death, and max HP mutation as unsupported until the simulator owns those state domains.
- Use `PredictionStateStore` for model-local counters/flags instead of mutating live model fields.
- Use exact model-type registrations. A derived model with the same base hook should be reviewed separately.
- If a listener has any unmodeled prediction-relevant side effect, mark the current source risky instead of silently ignoring it.
- Keep Mock models out of implementation/ignore registries; list them only in docs.

## Mirror registry architecture

`Common/Mirrors/ModelMethodMirrorRegistry.cs` provides the shared single-receiver dispatch used by
prediction mirrors. Both Action and result-producing variants perform base-method override detection,
exact model-type registration, lookup-result caching, and unsupported risk handling. Only the Action
variant supports explicit ignored registrations and the non-gameplay-mod ignore policy; result methods
require a handler for every supported override because an ignored implementation cannot supply return
semantics. Generic fallback handlers are intentionally unsupported: every handled or ignored runtime
type must be reviewed and registered explicitly. `MirrorMethodSpec` identifies the actual
declaring base method, so the same infrastructure can support public `AbstractModel` hooks and later
protected or more-specific virtual methods such as `CardModel.OnPlay` and `OrbModel.Evoke`.

`Common/Hooks/HookRegistry.cs` remains the hook invocation adapter: it enumerates listeners in caller-
provided order, honors `ShouldContinue`, and delegates each receiver to the shared Action registry.
The shared registry uses its typed context to establish the receiver's prediction source scope before
dispatch or unsupported risk marking. Registrations prepopulate the same runtime-type lookup dictionary later used
for lazy `NotOverridden` / `Ignored` / `Unsupported` results. Registries are constructed with all
registrations before first use, as required by the registry contract, and registered types are
validated as real overrides. Unsupported warnings are emitted only
when a lazy entry is first populated, while an `Unsupported` invocation still marks prediction risk
every time.
Listener discovery, hook phase ordering, and only-modifier dispatch remain outside the model-method
registry. Exact derived types must still be reviewed and registered independently.

## Current implementation may differ from vanilla

| Area | Model | 中文名 | Difference |
| --- | --- | --- | --- |
| Damage modifier after hooks | `BufferPower` | 缓冲 | `ModifyHpLostAfterOstyLate` is applied, but `AfterModifyingHpLostAfterOsty` cannot shadow-decrement the live Buffer stack; affected predictions are marked risky. |
| Damage post hooks | All damage callers | - | `AfterCurrentHpChanged`, `AfterDamageGiven`, and `AfterDamageReceived` have targeted mirrors, trigger-scoped risk marking, and current-scope ignored registrations. `AfterBlockBroken` is still mostly not mirrored. |
| Damage modifier live-state reads | `LethalityPower`, `PhantomBladesPower`, `PenNib`, `VigorPower`, `GigantificationPower` | 致死性 / 幻影之刃 / 钢笔尖 / 活力 / 超巨化 | The simulator calls original value hooks, so these listeners read live combat history or live attack/card-play state instead of simulator shadow history/state. StS2 v0.108.0 added `CardPlay?` to these hooks; simulated `AttackCommand` damage forwards the command's `CardPlay`, while generic/direct damage forecasts still pass `null` like vanilla previews. |
| Damage modifier `CardPlay?` preview semantics | `OneForAllPower` | 一心化万 | Original hook is used. Generic/direct damage forecasts still pass `null` like vanilla previews and check current modified cost. Simulated card-play/autoplay attack commands can pass a constructed `CardPlay` with simulator `ResourceInfo`, so those paths use energy/stars spent/value semantics closer to real card execution. |
| Attack command lifecycle | `VigorPower`, `GigantificationPower`, `BoneFlute`, `Flatten`, `PainfulStabsPower`, `SkittishPower`, `SuckPower` | 活力 / 超巨化 / 骨笛 / 重压 / 疼痛戳刺 / 胆小 / 吮吸 | `CombatPredictionSimulator.ExecuteAttack(AttackCommand)` mirrors the target loop, cloned random targeting, attack hook dispatch, `AttackCommand.Results` population, shadow attack history, and supported `AfterAttack` side effects. Power application/removal remains risk-marked; reviewed vanilla command-local callbacks are ignored as cosmetic-only. Direct `Damage` calls still bypass the attack lifecycle. See `attack-hooks.md`. |
| Frost orb Hibernate block | `FrostOrb`, `HibernatePower` | 冰霜 / 休眠 | StS2 v0.108.0 gives Frost passive/evoke block to all players while Hibernate is active. The simulator mirrors block recipients and evoke targets, but any new all-player block hook side effects still need exact registrations. |
| Autoplay | `HowlFromBeyond`, `IAmInvincible`, `StampedePower`, `HellraiserPower` | 彼岸咆哮 / 所向无敌 / 惊逃 / 地狱狂徒 | Simulator `AutoPlay`/`AutoPlayFromDrawPile` mirrors card selection, target resolution, X/star capture, play-pile movement, `ResourceInfo` construction, `CardPlay` construction, result-pile value hooks, supported result-pile movement, and targeted `OnPlay` delegates for `HowlFromBeyond`/`IAmInvincible`. Full card-play lifecycle is still incomplete: `BeforeCardAutoPlayed`, `BeforeCardPlayed`/`AfterCardPlayed`, card-play history, `AfterModifyingCardPlayResultPileOrPosition`, enchantment/affliction play effects, and unsupported generic `OnPlay` bodies remain omitted or risk-marked. Cost/result-pile value hooks can still read live `CardModel.Pile`, combat history, or model-local counters instead of simulator shadow state. |
| Ethereal exhaust | `DarkEmbracePower`, `JossPaper` | 黑暗之拥 / 金纸 | Ethereal hook calls only record delayed counts in vanilla; actual draw timing is end-turn cleanup, outside this simulation path, so the mirror intentionally does not mark risk here. |
| Death processing | Many death listeners | - | Simulator tracks shadow liveness/removal, selected player death cleanup, secondary-enemy death chains, and Fairy in a Bottle/Lizard Tail prevented-death predicates/healing via `DeathPreventHooks`. Power cleanup/removal, full revive flows, hook deactivation, combat loss, and many encounter-specific death listeners remain unsupported or risk-marked. |
| Extra-turn scheduling | `PaelsEye` | 佩尔之眼 | Immediate hand exhaust is mirrored, but the later extra-turn flow is still outside the simulator. |
| Death reward return | `HeistPower`, `SwipePower` | 盗窃 / 顺走 | Ignored by scope: they only return stolen combat rewards/deck cards. |
| End-turn healing | `RegenPower` | 再生 | StS2 v0.108.0 resolves Regen in `BeforeSideTurnEndEarly`, before normal `DoomPower` checks. The mirror applies shadow healing before Doom, but does not persist the Regen power decrement because no later hook in this simulation consumes it. |
| End-turn ignored listeners | `Regret`, `DiamondDiadem` | 悔恨 / 钻石头冠 | Acceptable for current scope: `Regret` only records hand size in this hook, and `DiamondDiademPower` matters on later enemy attacks. |
| Card reward state commit | `SilverCrucible`, `SilkenTress` | 白银熔炉 / 华美发束 | Result modifiers are mirrored, but `Hook.AfterModifyingCardRewardOptions` is not. Chained previews can reuse live usage state until prediction owns a transaction-local state commit. |
| Card reward risk surfacing | Unsupported card reward listeners | - | `CardRewardHookContext` records risk internally but callers do not consume a risk snapshot. |
| Card pile hook dispatch | Generated combat pile additions | - | Simulator generated-card helpers mirror supported `AfterCardGeneratedForCombat` listeners, but still skip `AfterCardEnteredCombat` and `AfterCardChangedPiles` by current scope. See the dedicated card-pile hook docs. |

## Related docs

- `after-card-changed-piles.md`
- `after-card-discarded.md`
- `after-card-drawn.md`
- `after-card-entered-combat.md`
- `after-card-exhausted.md`
- `after-card-generated-for-combat.md`
- `attack-hooks.md`
- `should-draw.md`
- `block-hooks.md`
- `damage-modifier-hooks.md`
- `damage-hooks.md`
- `death-hooks.md`
- `end-turn-hooks.md`
- `energy-hooks.md`
- `orb-hooks.md`
- `shuffle-hooks.md`
- `card-reward-hooks.md`
