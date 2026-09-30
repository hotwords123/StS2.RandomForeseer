# AfterCardExhausted hook

Mirror files: `InCombat/Mirrors/HookMirrors.cs`,
`InCombat/Mirrors/Hooks/Card/AfterCardExhaustedMirrors.cs`,
`InCombat/Mirrors/Hooks/TurnEnd/AfterSideTurnEndMirrors.cs`, and
`InCombat/Mirrors/Shared/JossPaperMirrors.cs`.

## Hook spec

- `AbstractModel.AfterCardExhausted(PlayerChoiceContext, CardModel, bool causedByEthereal)`

## Original listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `BurningSticks` | 燃烧木棍 | Once per combat, when owner exhausts a Skill, creates a clone in hand. | Implemented. Uses preview clone and state-store once/combat flag. |
| `CharonsAshes` | 卡戎之灰 | When owner exhausts a card, damages all hittable enemies. | Implemented with simulator `Damage`; inherits current damage post-hook gaps. |
| `DarkEmbracePower` | 黑暗之拥 | When owner's card exhausts, draws cards; ethereal exhaust only records a delayed cleanup count. | Implemented. Non-ethereal exhaust draws immediately; ethereal exhaust increments a prediction-state count for the draw in `AfterSideTurnEnd`. |
| `DrumOfBattle` | 战鼓 | When this card exhausts, gains energy based on generated play count. | Implemented for energy gain through the prediction-aware play-count hook, including selected modifier state commits. |
| `FeelNoPainPower` | 无惧疼痛 | When owner exhausts a card, gains block. | Implemented via `GainBlock`; matches relevant state change. |
| `ForgottenSoul` | 遗忘之魂 | When owner exhausts a card, rolls a random target and deals damage. | Implemented with cloned `CombatTargets` and simulator `Damage`; inherits current damage post-hook gaps. |
| `JossPaper` | 金纸 | Counts owner exhausts; at threshold draws cards. Ethereal exhaust only records a delayed cleanup count. | Implemented with shared prediction-state counters. Non-ethereal exhaust checks the threshold immediately; ethereal counts are added and checked in `AfterSideTurnEnd`. |
| `Midnight` | 午夜 | Whenever any card is exhausted, reduces this card's this-combat cost by 1. | Implemented. Finds the corresponding predicted Midnight card and mutates only its preview this-combat cost. |
| `SkillIronclad1Achievement` | 成就模型 | Counts exhausts for achievement unlock. | Ignored. Achievement state does not affect prediction. |

## Parity notes

- `DrumOfBattle` applies energy gain once per predicted generated play and uses the same selected-modifier commit path
  as normal card play; details are recorded in `card-play-count-hooks.md`.
- Ethereal exhaust records prediction-local counts without drawing immediately. After the hand flush,
  `DarkEmbracePower.AfterSideTurnEnd` and `JossPaper.AfterSideTurnEnd` mirrors perform the deferred draws through
  simulator `Draw`, including supported draw hooks and their chained effects. Counter accumulation and reset follow
  the vanilla listeners; see [end-turn-hooks.md](end-turn-hooks.md) for phase-two coverage and tradeoffs.

## Mock model list

- None.
