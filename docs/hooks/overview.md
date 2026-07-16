# Hook mirror overview

## Guidelines

- Prefer original read-only value hooks when vanilla already uses them for previews. Current examples: `Hook.ModifyBlock`, `Hook.ModifyDamage`, `Hook.ModifyHpLost`, card reward creation options, and card reward upgrade odds.
- Mirror only side effects that can change prediction output: draw order, hand/discard/exhaust piles, preview card cost/dynamic vars, block, damage, death/liveness, orb counts, current-turn energy, and RNG consumption.
- Combat predictions are scoped to outcomes that can still affect the current player turn. Do not mark risk only because vanilla would mutate state for an enemy turn, a later player turn, room-end rewards, or future reward screens, unless that state can feed back into a prediction surfaced before the current player turn finishes.
- Do not simulate VFX, SFX, waits, achievement unlocks, or effects that cannot occur during the current player-turn prediction surface.
- Treat Apply Power, Remove Power, summon, revive, monster move/state changes, combat removal, player death, and max HP mutation as unsupported until the simulator owns those state domains.
- Use `PredictionStateStore` for model-local counters/flags instead of mutating live model fields.
- If a listener has any unmodeled prediction-relevant side effect, append an explicit `CombatPredictionRiskReason` to prediction history instead of silently ignoring it.
- Keep Mock models out of implementation/ignore registries; list them only in docs.

## Mirror registry architecture

- `Common/Mirrors/ModelMethodMirrorRegistry.cs` handles single-model virtual-method dispatch: exact model-type registration, override detection, lookup caching, linked trace scoping, and unsupported-risk recording. Each dispatch frame stores the mapped source model and mirrored base method. Action registries may explicitly ignore reviewed overrides; result registries require a handler that supplies the return value.
- `IPredictionMirrorContext<TBase>` is a dispatcher-only contract. Combat contexts explicitly map ordinary listeners to the listener, orb receivers to the shadow orb, and `CardModel.OnPlay` receivers to the original card rather than its detached mutable preview. Typed handlers use the context's `History` alias for explicit risk reasons.
- `HookMirrors` facades own hook-level control flow, including context construction, listener enumeration, phase refresh, short-circuiting, result chaining, and only-modifier dispatch. The registry only dispatches one listener at a time.
- Hook mirrors are grouped first by domain and then by hook name under `Mirrors/Hooks/`. Each hook-name file owns its method specification, registry, context, handlers, and hook-local state; state or behavior shared by multiple hooks may use a separate model-centric file.
- Combat and out-of-combat code have independent `HookMirrors` facades but share the registry infrastructure. Mirrored model behavior that is not a hook, such as orb virtual methods and `CardModel.OnPlay`, lives in its model domain under `Mirrors/` and follows the same facade/registry split.
- `CombatPredictionHistory` stores semantic events, resolved events, and explicit risk events in one ordered timeline. Entries recorded within a prediction source scope capture its current immutable trace frame; source-less operations may record entries with no trace. Deferred card draws and individual generated cards append separate original and resolved entries; consumers use original order, resolved snapshots, and the maximum resolved timeline position. A reference-identity completion index rejects unresolved, duplicate, and cross-history completion. History also maintains exact entry-type counts so simulator safety limits can be checked without repeatedly scanning the full history.

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
