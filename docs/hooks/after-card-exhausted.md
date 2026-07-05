# AfterCardExhausted hook

Mirror file: `InCombat/Hooks/AfterCardExhaustedHook.cs`.

## Hook spec

- `AbstractModel.AfterCardExhausted(PlayerChoiceContext, CardModel, bool causedByEthereal)`

## Original listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `BurningSticks` | 燃烧木棍 | Once per combat, when owner exhausts a Skill, creates a clone in hand. | Implemented. Uses preview clone and state-store once/combat flag. |
| `CharonsAshes` | 卡戎之灰 | When owner exhausts a card, damages all hittable enemies. | Implemented with simulator `Damage`; inherits current damage post-hook gaps. |
| `DarkEmbracePower` | 黑暗之拥 | When owner's card exhausts, draws cards; ethereal exhaust only records a delayed cleanup count. | Implemented for non-ethereal. Ethereal path is intentionally a no-op because the actual draw happens in end-turn cleanup outside this simulation path. |
| `DrumOfBattle` | 战鼓 | When this card exhausts, gains energy based on generated play count. | Implemented for energy gain. Uses the original play-count value hook; marks risk if play-count modifiers are present because `AfterModifyingCardPlayCount` state commits are not mirrored. |
| `FeelNoPainPower` | 无惧疼痛 | When owner exhausts a card, gains block. | Implemented via `GainBlock`; matches relevant state change. |
| `ForgottenSoul` | 遗忘之魂 | When owner exhausts a card, rolls a random target and deals damage. | Implemented with cloned `CombatTargets` and simulator `Damage`; inherits current damage post-hook gaps. |
| `JossPaper` | 金纸 | Counts owner exhausts; at threshold draws cards. Ethereal exhaust only records a delayed cleanup count. | Implemented for non-ethereal. Ethereal path is intentionally a no-op because the actual draw happens in end-turn cleanup outside this simulation path. |
| `Midnight` | 午夜 | Whenever any card is exhausted, reduces this card's this-combat cost by 1. | Implemented. Finds the corresponding predicted Midnight card and mutates only its preview this-combat cost. |
| `SkillIronclad1Achievement` | 成就模型 | Counts exhausts for achievement unlock. | Ignored. Achievement state does not affect prediction. |

## Parity notes

- Non-ethereal `DarkEmbracePower`, `JossPaper`, `FeelNoPainPower`, and `BurningSticks` match the original prediction-relevant state changes.
- `DrumOfBattle` applies energy gain once per predicted generated play. Play-count modifier state commits are still not mirrored and are surfaced as risk.
- `CharonsAshes` and `ForgottenSoul` now mirror damage and target RNG, but inherit the current damage post-hook gaps.
- Ethereal exhaust delayed draws are intentionally not simulated by this draw/exhaust path, because their actual draw timing is in end-turn cleanup.
- StS2 v0.108.0 added `Midnight`. Its `AfterCardEnteredCombat` initial cost adjustment is a separate combat-entry hook; this document covers only the per-exhaust `AfterCardExhausted` reduction.

## Mock model list

- None.
