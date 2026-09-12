# Hook mirror overview

## Guidelines

- Prefer original read-only value hooks when vanilla already uses them for previews. Replace an exact listener only
  when a supported simulation path changes state that its original method would read incorrectly. The existence of
  a shadow state store alone does not require adapters for every possible consumer.
- Mirror only side effects that can change prediction output: draw order, card piles, preview card costs/dynamic vars,
  block, damage, death/liveness, orb counts, current-turn resources, and RNG consumption.
- Combat predictions are scoped to outcomes that can affect the current player turn. Later-turn, room-end and reward
  changes need no mirror or risk marker unless they feed back into that scope. Enemy attacks are outside this scope.
- Do not simulate VFX, SFX, waits, achievement unlocks, or unreachable effects.
- Treat state changes as unsupported until the simulator owns the required domain and lifecycle. Partial support
  must retain risk for unmodeled collection changes, callbacks, history, or other prediction-relevant effects.
- Use `PredictionStateStore` for model-local counters and flags instead of mutating live model fields. Share state
  between the action that changes it and the value/predicate hooks that consume it. Use `PowerAmountPredictionState`
  for the existing power consumers that need simulated stack consumption.
- Use prediction-aware helpers for state derived from detached preview models. Combine existing live history with
  simulated events only where needed, preserving the original event timing, ownership and turn filters.
- Preserve vanilla listener order, phases, guards, short-circuiting and result chaining. Read-only does not imply
  prediction-safe when a method derives values from live state.
- If a listener has an unmodeled prediction-relevant side effect, append an explicit risk reason to prediction history
  instead of silently ignoring it. Keep Mock models out of implementation/ignore registries; list them only in docs.
- Keep listener coverage, hook-specific dispatch rules, exceptions and known differences in the corresponding hook
  document. This overview contains shared principles, architecture and navigation.

## Mirror registry architecture

- `Common/Mirrors/MethodMirrorRegistry.cs` centralizes exact-type registration and dispatch, override detection,
  lookup caching, trace scoping, and unsupported-risk recording. Action registries can infer unregistered gameplay
  overrides and record incomplete risk; result registries require handlers that supply a return value and do not infer.
- Selective read-only value and predicate mirrors use `TryInvokeRegistered` for exact prediction-state overrides.
  A miss neither caches an unsupported lookup nor records risk; the facade calls the original listener in the same
  pass. Side-effect hooks use the full action registry: reviewed visual/no-op overrides are ignored, while unknown
  gameplay overrides record risk instead of running against live models.
- `IMethodMirrorContext<TBase>` is a dispatcher-only contract. Contexts select the appropriate trace source identity
  for the receiver, keeping original card identity separate from detached previews and using shadow receivers where
  appropriate. Typed handlers use the context's `History` alias for explicit risk reasons.
- `HookMirrors` facades own context construction, listener enumeration, phase refresh, short-circuiting, result
  chaining, and selected-modifier dispatch. The registry dispatches one listener at a time. Whether a later phase
  starts a fresh pass or directly iterates a returned modifier list follows the original facade.
- `Common/CompatibilityUtils.FilterHookListeners` provides the shared compatibility filter. When enabled, it preserves
  ordering while excluding listeners outside the base-game assembly. Facades retain their own enumeration rules;
  deliberate filtering exceptions are documented on the relevant hook page.
- Guarded iteration checks the simulator's shadow combat boundary at dispatch entry. Unguarded iteration remains
  available to finish an action already resolving. `CheckWinCondition` separately commits `IsInProgress = false` at
  a safe point. Command mirrors preserve the original distinction between ending and fully ended combat.
- Hook mirrors are grouped by domain and hook name under `Mirrors/Hooks/`. Each hook file owns its specification,
  registry, context, handlers and local state; shared behavior/state may use a separate model-centric file.
- Combat and out-of-combat facades share registry infrastructure. Non-hook model behavior lives in its model domain
  under `Mirrors/`, with coverage documented in `docs/mirrors/` rather than the hook pages.
- `CombatPredictionHistory` stores semantic, resolved and risk events in one ordered timeline. Entries capture the
  current immutable trace frame when present. Deferred operations keep separate original and resolved entries;
  consumers use original order, resolved snapshots and the maximum resolved timeline position. A reference-identity
  completion index rejects unresolved, duplicate and cross-history completion. Exact entry-type counts support
  simulator safety limits without repeated full-history scans.
- `CombatPredictionProjector.Project` consumes completed history and the root action frame from the same simulator.
  The original source identity and parent frames identify nested actions. The projector owns feature gates, ordered
  HoverTip projection, damage/highlight payloads, causal explanations and the shared risk boundary.

## Related docs

- `power-amount-hooks.md`
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
- `merchant-card-creation-results.md`
- `card-reward-hooks.md`
- `card-play-count-hooks.md`
- `card-play-hooks.md`
- `card-play-result-location-hooks.md`
