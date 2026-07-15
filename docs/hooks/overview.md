# Hook mirror overview

## Guidelines

- Prefer original read-only value hooks when vanilla already uses them for previews. Current examples: `Hook.ModifyBlock`, `Hook.ModifyDamage`, `Hook.ModifyHpLost`, card reward creation options, and card reward upgrade odds.
- Mirror only side effects that can change prediction output: draw order, hand/discard/exhaust piles, preview card cost/dynamic vars, block, damage, death/liveness, orb counts, current-turn energy, and RNG consumption.
- Combat predictions are scoped to outcomes that can still affect the current player turn. Do not mark risk only because vanilla would mutate state for an enemy turn, a later player turn, room-end rewards, or future reward screens, unless that state can feed back into a prediction surfaced before the current player turn finishes.
- Do not simulate VFX, SFX, waits, achievement unlocks, or effects that cannot occur during the current player-turn prediction surface.
- Treat Apply Power, Remove Power, summon, revive, monster move/state changes, combat removal, player death, and max HP mutation as unsupported until the simulator owns those state domains.
- Use `PredictionStateStore` for model-local counters/flags instead of mutating live model fields.
- If a listener has any unmodeled prediction-relevant side effect, mark the current source risky instead of silently ignoring it.
- Keep Mock models out of implementation/ignore registries; list them only in docs.

## Mirror registry architecture

- `Common/Mirrors/ModelMethodMirrorRegistry.cs` handles single-model virtual-method dispatch: exact
  model-type registration, override detection, lookup caching, source scoping, and unsupported-risk
  marking. Action registries may explicitly ignore reviewed overrides; result registries require a
  handler that supplies the return value.
- `HookMirrors` facades own hook-level control flow, including context construction, listener
  enumeration, phase refresh, short-circuiting, result chaining, and only-modifier dispatch. The
  registry only dispatches one listener at a time.
- Hook mirrors are grouped first by domain and then by hook name under `Mirrors/Hooks/`. Each
  hook-name file owns its method specification, registry, context, handlers, and hook-local state;
  state or behavior shared by multiple hooks may use a separate model-centric file.
- Combat and out-of-combat code have independent `HookMirrors` facades but share the registry
  infrastructure. Mirrored model behavior that is not a hook, such as orb virtual methods and
  `CardModel.OnPlay`, lives in its model domain under `Mirrors/` and follows the same
  facade/registry split.
- `CombatPredictionHistory` stores simulator events in one ordered timeline and records the current
  risk checkpoint at the same list position. Entry handles may move that checkpoint past deferred
  processing; consumers use the maximum checkpoint among relevant entries instead of end-of-simulation risk.

## Current implementation may differ from vanilla

| Area | Model | 中文名 | Difference |
| --- | --- | --- | --- |
| Damage modifier after hooks | `BufferPower` | 缓冲 | `ModifyHpLostAfterOstyLate` is applied, but `AfterModifyingHpLostAfterOsty` cannot shadow-decrement the live Buffer stack; affected predictions are marked risky. |
| Damage post hooks | All damage callers | - | `AfterCurrentHpChanged`, `AfterDamageGiven`, and `AfterDamageReceived` have targeted mirrors, trigger-scoped risk marking, and current-scope ignored registrations. `AfterBlockBroken` is still mostly not mirrored. |
| Damage modifier live-state reads | `LethalityPower`, `PhantomBladesPower`, `PenNib`, `VigorPower`, `GigantificationPower` | 致死性 / 幻影之刃 / 钢笔尖 / 活力 / 超巨化 | The simulator calls original value hooks, so these listeners read live combat history or live attack/card-play state instead of simulator shadow history/state. StS2 v0.108.0 added `CardPlay?` to these hooks; simulated `AttackCommand` damage forwards the command's `CardPlay`, while generic/direct damage forecasts still pass `null` like vanilla previews. |
| Damage modifier `CardPlay?` preview semantics | `OneForAllPower` | 一心化万 | Original hook is used. Generic/direct damage forecasts still pass `null` like vanilla previews and check current modified cost. Simulated card-play/autoplay attack commands can pass a constructed `CardPlay` with simulator `ResourceInfo`, so those paths use energy/stars spent/value semantics closer to real card execution. |
| Attack command lifecycle | `VigorPower`, `GigantificationPower`, `BoneFlute`, `Flatten`, `PainfulStabsPower`, `SkittishPower`, `SuckPower` | 活力 / 超巨化 / 骨笛 / 重压 / 疼痛戳刺 / 胆小 / 吮吸 | `CombatPredictionSimulator.ExecuteAttack(AttackCommand)` mirrors the target loop, cloned random targeting, attack hook dispatch, `AttackCommand.Results` population, shadow attack history, and supported `AfterAttack` side effects. Power application/removal remains risk-marked; reviewed vanilla command-local callbacks are ignored as cosmetic-only. Direct `Damage` calls still bypass the attack lifecycle. See `attack-hooks.md`. |
| Frost orb Hibernate block | `FrostOrb`, `HibernatePower` | 冰霜 / 休眠 | StS2 v0.108.0 gives Frost passive/evoke block to all players while Hibernate is active. The simulator mirrors block recipients and evoke targets, but any new all-player block hook side effects still need exact registrations. |
| Autoplay | `HowlFromBeyond`, `IAmInvincible`, `StampedePower`, `HellraiserPower` | 彼岸咆哮 / 所向无敌 / 惊逃 / 地狱狂徒 | Simulator `AutoPlay`/`AutoPlayFromDrawPile` mirrors card selection, target resolution, X/star capture, play-pile movement, `ResourceInfo` and `CardPlay` construction, result-pile value hooks, and supported result-pile movement. `HowlFromBeyond`/`IAmInvincible` and registered random-target attack, orb, card-selection, and card-generation cards reuse their shared `CardOnPlayMirrors` handlers during generic autoplay, with Hellraiser using the supported attack subset. Card-generation choice screens are recorded as unresolved options, and only Mad Science's Chaos rider is mirrored; its other forms are risk-marked. Full card-play lifecycle is still incomplete: unregistered `OnPlay` bodies, `BeforeCardAutoPlayed`, `BeforeCardPlayed`/`AfterCardPlayed`, card-play history, `AfterModifyingCardPlayResultPileOrPosition`, and enchantment/affliction play effects remain omitted or risk-marked. Cost/result-pile value hooks can still read live `CardModel.Pile`, combat history, or model-local counters instead of simulator shadow state. |
| Ethereal exhaust | `DarkEmbracePower`, `JossPaper` | 黑暗之拥 / 金纸 | Ethereal hook calls only record delayed counts in vanilla; actual draw timing is end-turn cleanup, outside this simulation path, so the mirror intentionally does not mark risk here. |
| Death processing | Many death listeners | - | Simulator tracks shadow liveness/removal, selected player death cleanup, secondary-enemy death chains, and Fairy in a Bottle/Lizard Tail prevented-death predicates/healing via `HookMirrors`. Power cleanup/removal, full revive flows, hook deactivation, combat loss, and many encounter-specific death listeners remain unsupported or risk-marked. |
| Extra-turn scheduling | `PaelsEye` | 佩尔之眼 | Immediate hand exhaust is mirrored, but the later extra-turn flow is still outside the simulator. |
| Death reward return | `HeistPower`, `SwipePower` | 盗窃 / 顺走 | Ignored by scope: they only return stolen combat rewards/deck cards. |
| End-turn healing | `RegenPower` | 再生 | StS2 v0.108.0 resolves Regen in `BeforeSideTurnEndEarly`, before normal `DoomPower` checks. The mirror applies shadow healing before Doom, but does not persist the Regen power decrement because no later hook in this simulation consumes it. |
| End-turn ignored listeners | `Regret`, `DiamondDiadem` | 悔恨 / 钻石头冠 | Acceptable for current scope: `Regret` only records hand size in this hook, and `DiamondDiademPower` matters on later enemy attacks. |
| Card reward state commit | `SilverCrucible`, `SilkenTress` | 白银熔炉 / 华美发束 | Result modifiers are mirrored, but `Hook.AfterModifyingCardRewardOptions` is not. Chained previews can reuse live usage state until prediction owns a transaction-local state commit. |
| Card reward risk surfacing | Unsupported card reward listeners | - | `TryModifyCardRewardOptionsMirrorContext` records risk internally but callers do not consume a risk snapshot. |
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
