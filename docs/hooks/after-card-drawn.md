# AfterCardDrawn hooks

Mirror files: `InCombat/Mirrors/HookMirrors.cs` and
`InCombat/Mirrors/Hooks/Card/AfterCardDrawnMirrors.cs`.

## Hook specs

- `AbstractModel.AfterCardDrawnEarly(PlayerChoiceContext, CardModel, bool)`
- `AbstractModel.AfterCardDrawn(PlayerChoiceContext, CardModel, bool)`

## AfterCardDrawnEarly listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `HellraiserPower` | 地狱狂徒 | When owner draws a Strike, auto-plays it; when all hittable enemies have infinite HP, limits these auto-plays per turn. | Implemented for trigger predicates, prediction-local infinite-target counter/reset, cap handling, and simulator autoplay. Generic card `OnPlay` bodies remain risk-marked by `AutoPlay`; Hellraiser's autoplay-only attack presentation changes are omitted. |

## AfterCardDrawn listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `ConfusedPower` | 混乱 | Randomizes drawn owner card cost to 0-3 if canonical cost is non-negative. | Implemented. Uses cloned `CombatEnergyCosts`; matches original relevant state change. |
| `Slither` | 蛇行 | Randomizes this enchanted card's cost to 0-3 after it enters hand. | Implemented. Uses cloned `CombatEnergyCosts`; matches original relevant state change. |
| `IterationPower` | 迭代 | First Status drawn each turn draws more cards. | Implemented. Counts live `CardDrawnEntry` history plus simulator `CombatPredictionHistory` card-draw entries, then uses simulator draw. |
| `PagestormPower` | 书页风暴 | Draws cards when owner draws an Ethereal card. | Implemented. Uses simulator draw. |
| `ChainsOfBindingPower` | 魂缚锁链 | During owner's turn, afflicts eligible drawn cards with Bound up to a per-turn limit. | Implemented. Uses live `CardAfflictedEntry` history plus simulator `CombatPredictionHistory` card-affliction entries; applies Bound through simulator `Afflict`. End-turn cleanup is mirrored by the turn-end hook family. |
| `CorrosiveWavePower` | 腐蚀波 | Applies Poison to all hittable enemies when owner draws a card. | Risk only. Apply Power is outside current simulator architecture. |
| `SpeedsterPower` | 速行者 | On non-hand draw during owner's turn, damages all hittable enemies. | Implemented with simulator `Damage`; inherits current damage post-hook gaps. |
| `CacophonyPower` | 杂音 | Decrements its 33-card counter on every drawn card; at zero waits, rolls a random hittable enemy with `RunState.Rng.CombatTargets`, deals unpowered damage, then resets the counter to 33. | Implemented. StS2 v0.108.0 added this listener; mirror uses prediction-local counter state, cloned `CombatTargets` RNG, and simulator `Damage`. |
| `KinglyKick` | 王者之踢 | When this card is drawn, reduces this-combat cost by 1. | Implemented. Matches original relevant state change. |
| `KinglyPunch` | 王者之拳 | When this card is drawn, increases its damage by `Increase`. | Implemented. Matches current prediction-relevant state. |
| `AutomationPower` | 自动化 | Counts drawn cards and grants energy at threshold. | Implemented. Uses prediction-local cards-left state and simulator `GainEnergy`; live display/internal data is not mutated. |
| `Void` | 虚空 | Waits, then loses energy when this card is drawn. | Implemented for energy loss. Wait is omitted. |

## Parity notes

- Simulator draw appends `CombatPredictionCardDrawnEntry` before dispatching mirrored `AfterCardDrawn`
  hooks, matching vanilla history timing for listeners that inspect cards drawn this turn. After the
  hooks it appends `CombatPredictionCardDrawResolvedEntry` with the final card snapshot. Projection
  keeps started-entry order but uses the resolved snapshot and checkpoint.
- A single simulation records at most 100 card draws. Further draw attempts stop the current draw loop and add unknown prediction risk, preventing recursive draw listeners and auto-play effects from running without bound.

## Mock model list

- `MockRemoveDrawnCardsFromCombatPower`
