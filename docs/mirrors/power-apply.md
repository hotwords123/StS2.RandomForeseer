# Power application mirror

## Scope

`CombatPredictionSimulator.ApplyPower<T>` mirrors the prediction-relevant boundary of `PowerCmd.Apply<T>` without
mutating a live creature or its power collection. The first caller is the direct Vulnerable template inferred from
card `OnPlay` IL.

The command reuses `HookMirrors` for the original ordering:

1. `BeforePowerAmountChanged`.
2. Additive and multiplicative `ModifyPowerAmountGiven` passes when the applier is in combat.
3. `TryModifyPowerAmountReceived` and its selected modifier list.
4. Multiplayer scaling for powers that opt into it.
5. New-power `BeforeApplied`.
6. `AfterModifyingPowerAmountGiven` and `AfterModifyingPowerAmountReceived` for selected modifiers.
7. New-power `AfterApplied`, followed by `AfterPowerAmountChanged`, only for a nonzero modified amount.

All Hook listener passes use the existing combat listener enumeration, ending guard and compatibility filter.
Read-only amount modifiers fall back to their original implementations. Action hooks and Power lifecycle methods use
placeholder registries so unsupported overrides record prediction risk instead of mutating live state.

## Supported listener state

- `ArtifactPower.TryModifyPowerAmountReceived` reuses the original predicate while consulting the existing
  `PowerAmountPredictionState`. Its mirrored `AfterModifyingPowerAmountReceived` decrements that shadow amount. A
  blocked Vulnerable application therefore does not reach `AfterPowerAmountChanged`.
- `ViciousPower.AfterPowerAmountChanged` checks the existing shadow power amount and, for a positive owner-applied
  Vulnerable change, calls the simulator's existing `Draw` method. Draw pile, shuffle RNG, draw hooks and projected
  card results stay centralized in the existing pipeline.

## Deliberate limit

The command does not yet add, stack, remove or enumerate predicted power instances. Existing live power instances are
located through `PowerCmd.FindExistingInstanceForStacking`, and prediction-owned mutable amounts are used for the
supported Artifact and Vicious listeners. A future complete power domain should extend the creature shadow state and
the existing Hook registries rather than introduce a separate listener loop or per-card side-effect mirror.
