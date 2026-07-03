# Shuffle hooks

Mirror file: `InCombat/Hooks/ShuffleHooks.cs`.

## Hook specs

- `AbstractModel.ModifyShuffleOrder(Player, List<CardModel>, bool isInitialShuffle)`
- `AbstractModel.AfterShuffle(PlayerChoiceContext, Player)`

## ModifyShuffleOrder listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `PerfectFit` | 完美契合 | On non-initial shuffle, moves this enchanted card to the top of the shuffled cards. | Implemented. Matches original relevant order change. |

## AfterShuffle listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `BiiigHug` | 大～抱抱 | Adds a generated `Soot` card into the draw pile at a random position. | Implemented with cloned shuffle RNG and predicted card creation. |
| `StratagemPower` | 计策 | Opens a combat pile selection and moves selected cards to hand. | Risk only. Requires modeling card selection UI/result; not implementable with current simulator alone. |
| `TheAbacus` | 算盘 | Owner gains block after shuffle. | Implemented via `GainBlock`; matches relevant state change. |

## Parity notes

- The simulator's `Shuffle` combines discard and draw pile state, stable-shuffles with cloned RNG, then runs these mirrors before replacing shadow piles.
- `BiiigHug` intentionally creates a preview card instead of calling original generated-card commands.

## Mock model list

- None.
