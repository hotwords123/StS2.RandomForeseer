# Card play count hooks

Mirror files: `InCombat/Mirrors/HookMirrors.cs`,
`InCombat/Mirrors/Hooks/Card/ModifyCardPlayCountMirrors.cs`, and
`InCombat/Simulation/CombatPredictedCardExtensions.cs`.

## Hook specs

- `AbstractModel.ModifyCardPlayCount(CardModel, Creature?, int)`
- `AbstractModel.AfterModifyingCardPlayCount(CardModel)`

## Vanilla order

`CardModel.GeneratePlayCount` starts with one play plus enchantment Replay, chains every `ModifyCardPlayCount`
listener, and records only listeners that changed the count they received. It then starts a fresh listener pass and
calls `AfterModifyingCardPlayCount` only for those exact modifiers. The resulting count controls how many complete
before-play, card-effect, and after-play lifecycles execute.

The simulator mirrors these as separate value and after facades. The after facade starts the same fresh listener pass
and checks membership in the modifier list before dispatch. This path is also used when `DrumOfBattle` generates a
play count after being exhausted, matching vanilla state consumption even though that card is not entering
`OnPlayWrapper`.

## Vanilla listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `BurstPower` | 爆发 | The next configured owner Skills play one additional time. | Implemented with shared shadow amount, consuming one stack only when selected as a modifier. |
| `DuplicationPower` | 复制 | The next configured owner cards play one additional time. | Implemented with shared shadow amount. |
| `EchoFormPower` | 回响形态 | The first configured owner card series each turn play one additional time. | Implemented by combining live and shadow first-in-series started history; selected after hook is visual-only. |
| `KiVersePower` | 叽 诗句 | The next owner Wah/Cho/Ki cards play once more. | Implemented with shadow amount; only a selected modifier consumes one stack. Owner side-end consumes the remaining amount. |
| `OneTwoPunchPower` | 连环拳 | The next configured owner Attacks play one additional time. | Implemented with shared shadow amount. |
| `SignalBoostPower` | 信号增强 | The next configured owner Powers play one additional time. | Implemented with shared shadow amount. |
| `TagTeamPower` | 多人组队 | Another player's next qualifying Attack against the owner plays additional times equal to amount, then removes this power. | Implemented by reading the shadow amount through the original predicate logic and consuming all shadow stacks when selected. |
| `ThrowingAxe` | 投斧 | The owner's first card each combat plays one additional time. | Implemented with a prediction-local used-this-combat flag. |

## Parity notes

- Power stack consumers remain present in the live hook collection after their shadow amount reaches zero, so their
  selective value adapters return the unchanged count and cannot be reused by later chained cards.
- The shadow decrement/removal does not run the full vanilla power amount/removal lifecycle.
- An unregistered mod listener may use its original value method, but if it changes the play count its unsupported
  after hook records risk instead of silently skipping a potentially prediction-relevant state commit.

## Mock model list

- None.
