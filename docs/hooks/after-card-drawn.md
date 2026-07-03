# AfterCardDrawn hooks

Mirror file: `InCombat/Hooks/AfterCardDrawnHook.cs`.

## Hook specs

- `AbstractModel.AfterCardDrawnEarly(PlayerChoiceContext, CardModel, bool)`
- `AbstractModel.AfterCardDrawn(PlayerChoiceContext, CardModel, bool)`

## AfterCardDrawnEarly listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `HellraiserPower` | 地狱狂徒 | When owner draws a Strike, auto-plays it. | Risk only. `AutoPlay` is not mirrored, so current handler marks risk. Not implementable precisely without completing card autoplay simulation. |

## AfterCardDrawn listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `ConfusedPower` | 混乱 | Randomizes drawn owner card cost to 0-3 if canonical cost is non-negative. | Implemented. Uses cloned `CombatEnergyCosts`; matches original relevant state change. |
| `Slither` | 蛇行 | Randomizes this enchanted card's cost to 0-3 after it enters hand. | Implemented. Uses cloned `CombatEnergyCosts`; matches original relevant state change. |
| `IterationPower` | 迭代 | First Status drawn each turn draws more cards. | Implemented. Uses shadow draw state counter and simulator draw. |
| `PagestormPower` | 书页风暴 | Draws cards when owner draws an Ethereal card. | Implemented. Uses simulator draw. |
| `ChainsOfBindingPower` | 魂缚锁链 | During owner's turn, afflicts eligible drawn cards with Bound up to a per-turn limit. | Implemented. Mirrors affliction on preview card and tracks per-turn count. End-turn cleanup portion is in `EndTurnHooks`. |
| `CorrosiveWavePower` | 腐蚀波 | Applies Poison to all hittable enemies when owner draws a card. | Risk only. Apply Power is outside current simulator architecture. |
| `SpeedsterPower` | 速行者 | On non-hand draw during owner's turn, damages all hittable enemies. | Implemented with simulator `Damage`; inherits current damage post-hook gaps. |
| `CacophonyPower` | 杂音 | Decrements its 33-card counter on every drawn card; at zero waits, rolls a random hittable enemy with `RunState.Rng.CombatTargets`, deals unpowered damage, then resets the counter to 33. | Implemented. StS2 v0.108.0 added this listener; mirror uses prediction-local counter state, cloned `CombatTargets` RNG, and simulator `Damage`. |
| `KinglyKick` | 王者之踢 | When this card is drawn, reduces this-combat cost by 1. | Implemented. Matches original relevant state change. |
| `KinglyPunch` | 王者之拳 | When this card is drawn, increases its damage by `Increase`. | Implemented. Matches current prediction-relevant state. |
| `AutomationPower` | 自动化 | Counts drawn cards and grants energy at threshold. | Ignored. Energy gain does not affect current predictions. |
| `Void` | 虚空 | Waits, then loses energy when this card is drawn. | Ignored. Energy/wait do not affect current predictions. |

## Parity notes

- Implemented cost/draw/affliction/card-stat handlers match the original gameplay predicates and use shadow state.
- `CorrosiveWavePower` is not implementable under the current rule because it applies powers.
- `CacophonyPower` uses the live counter as the initial shadow value and resets to the canonical card threshold after a simulated trigger, avoiding a hard-coded 33 if vanilla changes that value later.

## Mock model list

- `MockRemoveDrawnCardsFromCombatPower`
