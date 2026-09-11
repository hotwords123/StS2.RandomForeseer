# Power command mirrors

`CombatPredictionSimulator.ApplyPower`, `ModifyPowerAmount` and `RemovePower` partially mirror StS2 v0.111.0 `PowerCmd`.
Power amounts use shared prediction state; the full power domain is not yet simulated.
Hook coverage is documented in [Power amount hooks](../hooks/power-amount-hooks.md).

## Differences and known limitations

- **New powers:** application saves the shadow amount on the returned instance, but does not add it to a predicted
  collection or assign its `Owner`. The immediate caller can read that amount; later collection lookups cannot
  discover the instance, so repeated applications do not stack on a power created earlier in the prediction.
- **Existing powers:** `ModifyPowerAmount` updates the shared shadow amount, but stacking lookups and listener
  enumeration still use live collections. Removal is not evaluated, and hooks that read live `power.Amount`
  do not observe predicted changes.
- **History and duration:** `PowerReceived` history and `SkipNextDurationTick` are not simulated. Consumers of
  these values need prediction-aware implementations.
- **Lifecycle:** `BeforeApplied` and `AfterApplied` have placeholder registries; unsupported overrides record risk.
  `RemovePower` is a placeholder that clears the shadow amount and records `MethodMirrorIncomplete` for a non-null
  instance. It does not change collections or dispatch removal events and `AfterRemoved`; generic lookup still uses
  the live collection. Automatic removal after amount changes remains unmodeled.
- **Combat membership:** eligibility uses prediction-side membership because shadow removal leaves the live
  `Creature.CombatState` intact. This represents vanilla's default removal behavior, not removal with
  `unattach: false`, which retains the combat reference.
- **Presentation:** visual events, waits and intent presentation are intentionally omitted.

Successful power applications and nonzero integer amount changes retain `MethodMirrorIncomplete` risk because
collection changes, removal and live-state consumers remain unsupported. Updating a shadow amount alone does not
make subsequent predictions fully reliable.
