# ShouldDraw hook

Mirror file: `InCombat/Hooks/ShouldDrawHook.cs`.

## Hook spec

- `AbstractModel.ShouldDraw(Player, bool fromHandDraw)`

## Original listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `NoDrawPower` | 不可抽牌 | Blocks non-hand draws for the owner. Original flashes as side effect. | Implemented manually. Predicate matches original and avoids flash. |
| `Fiddle` | 小提琴 | Blocks non-hand draws during the owner's turn. | Implemented manually to avoid future original-method side effects. |

## Parity notes

- Dispatcher stops once `IsBlocked` becomes true, matching the original `Hook.ShouldDraw` short-circuit behavior.
- Both listeners are mirrored manually instead of calling original methods, so future flash/status side effects in originals will not leak into prediction.

## Mock model list

- None.
