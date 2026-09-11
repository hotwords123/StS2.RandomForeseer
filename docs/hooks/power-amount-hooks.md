# Power amount hooks

Coverage is based on StS2 v0.111.0. Command-level differences and limitations are maintained in
[Power command mirrors](../mirrors/power-apply.md).

## Listener coverage

| Hook | Listener / 中文名 | Original effect | Prediction status and risk |
| --- | --- | --- | --- |
| `BeforePowerAmountChanged` | Unregistered overrides | Model-specific effects before amount modifiers. | Unsupported overrides record risk. |
| `ModifyPowerAmountGivenAdditive` / `ModifyPowerAmountGivenMultiplicative` | Original listeners | Modify the amount given. | Original-method fallback; live-state dependencies remain. |
| `TryModifyPowerAmountReceived` | `ArtifactPower` / 人工制品 | Blocks a visible debuff applied to its owner. | Supported using the shadow amount. |
| `TryModifyPowerAmountReceived` | Other listeners | Modify the amount received. | Original-method fallback; live-state dependencies remain. |
| `AfterModifyingPowerAmountGiven` | Unregistered overrides | Side effects of given modifiers. | Unsupported overrides record risk. |
| `AfterModifyingPowerAmountReceived` | `ArtifactPower` / 人工制品 | Decrements itself after blocking a debuff. | Partial: shadow decrement only; nested amount hooks and removal are missing. |
| `AfterModifyingPowerAmountReceived` | Other overrides | Side effects of received modifiers. | Unsupported overrides record risk. |
| `AfterPowerAmountChanged` | `ViciousPower` / 凶恶 | Draws cards when its owner applies a positive Vulnerable change. | Supported using the shadow amount and simulated draw. |
| `AfterPowerAmountChanged` | Other overrides | Model-specific responses to amount changes. | Unsupported overrides record risk. |

## Known limitations

Shadow amount writes are visible only to prediction-aware listeners. Original value-hook fallbacks still read live
state, and newly applied or removed powers are not reflected in listener enumeration. Artifact consumption also
bypasses the full `PowerCmd.Decrement` lifecycle. Additional listener mirrors and power collection support are
required to close these gaps.
